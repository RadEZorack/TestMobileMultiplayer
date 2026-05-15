using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace BasicMultiplayer
{
    public sealed class WebRtcPeerMediaClient : MonoBehaviour
    {
        private const int RequestedCameraWidth = 320;
        private const int RequestedCameraHeight = 240;
        private const int RequestedCameraFps = 15;
        private const int MicrophoneSampleRate = 48000;
        private const float AvButtonSize = 72f;
        private const float CameraStartTimeoutSeconds = 5f;
        private const float MicrophoneStartTimeoutSeconds = 3f;

        [SerializeField] private UdpGameClient client;
        [SerializeField] private bool showAvButton = true;
        [SerializeField] private bool useSecureWebSocket = true;
        [SerializeField] private int directWebSocketPort = 8080;
        [SerializeField] private string webSocketUrlOverride;

        private readonly ConcurrentQueue<string> _incomingSignalingMessages = new();
        private readonly Dictionary<int, PeerState> _peersByPlayerId = new();
        private readonly Dictionary<int, bool> _remoteMediaEnabledByPlayerId = new();
        private readonly Dictionary<int, RemoteVideoLayout> _remoteVideoLayoutsByPlayerId = new();
        private readonly SemaphoreSlim _webSocketSendLock = new(1, 1);
        private CancellationTokenSource _lifetimeCancellation;
        private ClientWebSocket _webSocket;
        private RTCIceServer[] _iceServers = Array.Empty<RTCIceServer>();
        private WebCamTexture _webCamTexture;
        private Texture2D _localVideoTexture;
        private VideoStreamTrack _localVideoTrack;
        private AudioStreamTrack _localAudioTrack;
        private AudioSource _microphoneSource;
        private AudioClip _microphoneClip;
        private string _microphoneDeviceName;
        private string _status = "AV off";
        private int _boundPlayerId;
        private bool _signalingConnecting;
        private bool _hasRtcConfig;
        private bool _wantsPublishing;
        private bool _isPublishing;
        private bool _startingPublishing;
        private bool _localVideoCopyFailedLogged;
        private int _localVideoRotationDegrees;
        private bool _localVideoVerticallyMirrored;

        public event Action<int, Texture> RemoteVideoReceived;
        public event Action<int, int, bool> RemoteVideoLayoutReceived;
        public event Action<int, AudioStreamTrack> RemoteAudioReceived;
        public event Action<int> RemotePeerClosed;
        public event Action<NameResultMessage> NameResultReceived;
        public event Action<PlayerNameMessage[]> PlayerNamesReceived;
        public event Action<ChatMessage[]> ChatHistoryReceived;
        public event Action<ChatMessage> ChatReceived;
        public bool IsSignalingReady => _webSocket != null && _webSocket.State == WebSocketState.Open;

        private void Awake()
        {
            if (client == null)
            {
                client = GetComponent<UdpGameClient>();
            }

            _lifetimeCancellation = new CancellationTokenSource();
        }

        private void Start()
        {
            StartCoroutine(WebRTC.Update());
        }

        private void Update()
        {
            DrainSignalingMessages();
            EnsureSignalingConnection();

            if (_wantsPublishing && !_isPublishing && !_startingPublishing)
            {
                StartCoroutine(StartPublishingCoroutine());
            }

            if (_isPublishing)
            {
                UpdateLocalVideoTexture();
                RefreshLocalVideoLayout();
            }
        }

        private void OnGUI()
        {
            if (!showAvButton)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            var uiScale = GetUiScale();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            GUI.color = _isPublishing ? new Color(0.3f, 1f, 0.55f, 0.92f) : new Color(1f, 1f, 1f, 0.82f);

            if (GUI.Button(GetAvButtonRect(uiScale), _isPublishing ? "AV\nON" : "AV"))
            {
                TogglePublishing();
            }

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void OnDestroy()
        {
            _ = SendMediaEnabledAsync(false);
            _lifetimeCancellation.Cancel();
            CloseAllPeers();
            StopLocalMedia();
            _webSocket?.Abort();
            _webSocket?.Dispose();
            _webSocketSendLock.Dispose();
            _lifetimeCancellation.Dispose();
        }

        private void TogglePublishing()
        {
            if (_isPublishing || _wantsPublishing)
            {
                _wantsPublishing = false;
                _startingPublishing = false;
                _isPublishing = false;
                _status = "AV off";
                _ = SendMediaEnabledAsync(false);
                CloseAllPeers();
                StopLocalMedia();
                return;
            }

            if (client == null || client.LocalPlayerId == 0)
            {
                _status = "AV waiting for server";
                return;
            }

            _wantsPublishing = true;
            _status = "AV starting";
        }

        private void EnsureSignalingConnection()
        {
            if (client == null || client.LocalPlayerId == 0)
            {
                return;
            }

            if (_boundPlayerId != 0 && _boundPlayerId != client.LocalPlayerId)
            {
                ResetSignalingState();
            }

            if (_webSocket != null
                && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting))
            {
                return;
            }

            if (_signalingConnecting)
            {
                return;
            }

            _signalingConnecting = true;
            _ = ConnectSignalingAsync(client.LocalPlayerId, _lifetimeCancellation.Token);
        }

        private async Task ConnectSignalingAsync(int playerId, CancellationToken cancellationToken)
        {
            try
            {
                var url = BuildWebSocketUrl();

                if (string.IsNullOrEmpty(url))
                {
                    _status = "AV missing server";
                    return;
                }

                var socket = new ClientWebSocket();
                await socket.ConnectAsync(new Uri(url), cancellationToken);
                _webSocket = socket;
                _boundPlayerId = playerId;

                await SendClientMessageAsync(new ClientMessage
                {
                    type = "hello",
                    sessionId = client.SessionId,
                    playerId = playerId
                }, cancellationToken);

                _status = "AV signaling ready";
                _ = ReceiveSignalingLoopAsync(socket, cancellationToken);
            }
            catch (Exception exception) when (exception is WebSocketException
                || exception is UriFormatException
                || exception is InvalidOperationException
                || exception is OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _status = $"AV signaling failed: {exception.Message}";
                }
            }
            finally
            {
                _signalingConnecting = false;
            }
        }

        private async Task ReceiveSignalingLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    using var message = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    _incomingSignalingMessages.Enqueue(Encoding.UTF8.GetString(message.ToArray()));
                }
            }
            catch (Exception exception) when (exception is WebSocketException
                || exception is ObjectDisposedException
                || exception is OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _incomingSignalingMessages.Enqueue("{\"type\":\"closed\"}");
                }
            }
        }

        private void DrainSignalingMessages()
        {
            while (_incomingSignalingMessages.TryDequeue(out var message))
            {
                HandleSignalingMessage(message);
            }
        }

        private void HandleSignalingMessage(string message)
        {
            var envelope = JsonUtility.FromJson<ServerMessageEnvelope>(message);

            switch (envelope.type)
            {
                case "config":
                    var configMessage = JsonUtility.FromJson<ConfigServerMessage>(message);
                    ApplyRtcConfig(configMessage.iceServers);
                    break;

                case "media-state":
                    var mediaStateMessage = JsonUtility.FromJson<MediaStateServerMessage>(message);
                    ApplyMediaState(mediaStateMessage.players);
                    break;

                case "signal":
                    var signalMessage = JsonUtility.FromJson<SignalServerMessage>(message);
                    HandlePeerSignal(signalMessage.from, signalMessage.payload);
                    break;

                case "name-result":
                    NameResultReceived?.Invoke(JsonUtility.FromJson<NameResultMessage>(message));
                    break;

                case "player-names":
                    var playerNamesMessage = JsonUtility.FromJson<PlayerNamesServerMessage>(message);
                    PlayerNamesReceived?.Invoke(playerNamesMessage.players ?? Array.Empty<PlayerNameMessage>());
                    break;

                case "chat-history":
                    var chatHistoryMessage = JsonUtility.FromJson<ChatHistoryServerMessage>(message);
                    ChatHistoryReceived?.Invoke(chatHistoryMessage.messages ?? Array.Empty<ChatMessage>());
                    break;

                case "chat":
                    var chatMessage = JsonUtility.FromJson<ChatServerMessage>(message);

                    if (chatMessage.message != null)
                    {
                        ChatReceived?.Invoke(chatMessage.message);
                    }

                    break;

                case "closed":
                    ResetSignalingState();
                    break;
            }
        }

        public Task SendDisplayNameChangeAsync(string displayName)
        {
            return SendClientMessageAsync(new ClientMessage
            {
                type = "name-change",
                displayName = DeviceDisplayNameStore.Sanitize(displayName)
            }, _lifetimeCancellation.Token);
        }

        public Task SendChatAsync(string text)
        {
            return SendClientMessageAsync(new ClientMessage
            {
                type = "chat",
                text = text ?? string.Empty
            }, _lifetimeCancellation.Token);
        }

        private void ApplyRtcConfig(IceServerMessage[] iceServers)
        {
            if (iceServers == null || iceServers.Length == 0)
            {
                _iceServers = Array.Empty<RTCIceServer>();
                _hasRtcConfig = true;

                if (_isPublishing)
                {
                    _ = SendMediaEnabledAsync(true);
                }

                return;
            }

            _iceServers = new RTCIceServer[iceServers.Length];

            for (var index = 0; index < iceServers.Length; index++)
            {
                _iceServers[index] = new RTCIceServer
                {
                    urls = iceServers[index].urls,
                    username = iceServers[index].username,
                    credential = iceServers[index].credential,
                    credentialType = RTCIceCredentialType.Password
                };
            }

            _hasRtcConfig = true;

            if (_isPublishing)
            {
                _ = SendMediaEnabledAsync(true);
            }

            RefreshPeerConnections();
        }

        private void ApplyMediaState(MediaStatePlayerMessage[] players)
        {
            _remoteMediaEnabledByPlayerId.Clear();
            _remoteVideoLayoutsByPlayerId.Clear();

            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player.playerId != client.LocalPlayerId && player.enabled)
                    {
                        _remoteMediaEnabledByPlayerId[player.playerId] = true;
                        var layout = new RemoteVideoLayout(
                            NormalizeVideoRotation(player.videoRotation),
                            player.videoMirrored);
                        _remoteVideoLayoutsByPlayerId[player.playerId] = layout;
                        RemoteVideoLayoutReceived?.Invoke(player.playerId, layout.RotationDegrees, layout.Mirrored);
                    }
                }
            }

            RefreshPeerConnections();
        }

        private void RefreshPeerConnections()
        {
            if (!_isPublishing || !_hasRtcConfig || client == null || client.LocalPlayerId == 0)
            {
                return;
            }

            var stalePeerIds = new List<int>();

            foreach (var peerId in _peersByPlayerId.Keys)
            {
                if (!_remoteMediaEnabledByPlayerId.ContainsKey(peerId))
                {
                    stalePeerIds.Add(peerId);
                }
            }

            foreach (var peerId in stalePeerIds)
            {
                ClosePeer(peerId);
            }

            foreach (var pair in _remoteMediaEnabledByPlayerId)
            {
                var peer = GetOrCreatePeer(pair.Key);

                if (client.LocalPlayerId < pair.Key && !peer.OfferStarted)
                {
                    peer.OfferStarted = true;
                    StartCoroutine(CreateOfferCoroutine(peer));
                }
            }
        }

        private IEnumerator StartPublishingCoroutine()
        {
            _startingPublishing = true;
            IosWebRtcAudioSession.Configure();

            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam | UserAuthorization.Microphone);

            if (!_wantsPublishing)
            {
                _startingPublishing = false;
                yield break;
            }

            var hasCameraPermission = Application.HasUserAuthorization(UserAuthorization.WebCam);
            var hasMicrophonePermission = Application.HasUserAuthorization(UserAuthorization.Microphone);

            if (!hasCameraPermission && !hasMicrophonePermission)
            {
                _status = "AV permission denied";
                _wantsPublishing = false;
                _startingPublishing = false;
                yield break;
            }

            if (hasCameraPermission)
            {
                yield return StartCameraCoroutine();
            }

            if (hasMicrophonePermission)
            {
                yield return StartMicrophoneCoroutine();
            }

            _isPublishing = _localVideoTrack != null || _localAudioTrack != null;
            _startingPublishing = false;

            if (!_isPublishing)
            {
                _wantsPublishing = false;
                _status = "AV no camera or mic";
                yield break;
            }

            _status = "AV on";
            if (_hasRtcConfig)
            {
                _ = SendMediaEnabledAsync(true);
            }

            RefreshPeerConnections();
        }

        private IEnumerator StartCameraCoroutine()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogWarning("AV camera start skipped: no camera devices were reported by Unity.");
                yield break;
            }

            var deviceName = GetPreferredCameraDeviceName();
            _webCamTexture = string.IsNullOrEmpty(deviceName)
                ? new WebCamTexture(RequestedCameraWidth, RequestedCameraHeight, RequestedCameraFps)
                : new WebCamTexture(deviceName, RequestedCameraWidth, RequestedCameraHeight, RequestedCameraFps);
            _webCamTexture.Play();

            var timeoutAt = Time.realtimeSinceStartup + CameraStartTimeoutSeconds;

            while (_wantsPublishing
                && Time.realtimeSinceStartup < timeoutAt
                && (!_webCamTexture.didUpdateThisFrame || _webCamTexture.width <= 16 || _webCamTexture.height <= 16))
            {
                yield return null;
            }

            if (!_wantsPublishing)
            {
                yield break;
            }

            if (!_webCamTexture.isPlaying || _webCamTexture.width <= 16 || _webCamTexture.height <= 16)
            {
                Debug.LogWarning(
                    $"AV camera start failed: '{deviceName}' did not produce frames before timeout. " +
                    "If multiple local clients are running on the same Mac, only one may be able to use the physical camera.");
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
                yield break;
            }

            if (!TryCreateLocalVideoTrack(deviceName))
            {
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
                yield break;
            }

            RefreshLocalVideoLayout(forceBroadcast: false);
        }

        private IEnumerator StartMicrophoneCoroutine()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("AV microphone start skipped: no microphone devices were reported by Unity.");
                yield break;
            }

            _microphoneDeviceName = Microphone.devices[0];
            var sampleRate = GetMicrophoneSampleRate(_microphoneDeviceName);
            _microphoneClip = Microphone.Start(_microphoneDeviceName, loop: true, lengthSec: 1, frequency: sampleRate);

            if (_microphoneClip == null)
            {
                Debug.LogWarning($"AV microphone start failed: '{_microphoneDeviceName}' did not return an AudioClip.");
                _microphoneDeviceName = null;
                yield break;
            }

            var timeoutAt = Time.realtimeSinceStartup + MicrophoneStartTimeoutSeconds;

            while (Microphone.GetPosition(_microphoneDeviceName) <= 0 && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (Microphone.GetPosition(_microphoneDeviceName) <= 0)
            {
                Debug.LogWarning($"AV microphone start failed: '{_microphoneDeviceName}' did not produce samples before timeout.");
                Microphone.End(_microphoneDeviceName);
                _microphoneDeviceName = null;
                _microphoneClip = null;
                yield break;
            }

            if (_microphoneSource == null)
            {
                _microphoneSource = gameObject.AddComponent<AudioSource>();
                _microphoneSource.playOnAwake = false;
                _microphoneSource.loop = true;
                _microphoneSource.spatialBlend = 0f;
            }

            _microphoneSource.volume = 1f;
            _microphoneSource.mute = false;
            _microphoneSource.clip = _microphoneClip;
            _localAudioTrack = new AudioStreamTrack(_microphoneSource);
            _localAudioTrack.Loopback = false;
            _microphoneSource.Play();

            Debug.Log($"AV microphone started: '{_microphoneDeviceName}' at {sampleRate} Hz.");
        }

        private PeerState GetOrCreatePeer(int remotePlayerId)
        {
            if (_peersByPlayerId.TryGetValue(remotePlayerId, out var peer))
            {
                return peer;
            }

            var configuration = new RTCConfiguration
            {
                iceServers = _iceServers
            };
            var connection = new RTCPeerConnection(ref configuration);
            var receiveStream = new MediaStream();

            peer = new PeerState(remotePlayerId, connection, receiveStream);
            _peersByPlayerId[remotePlayerId] = peer;

            receiveStream.OnAddTrack = e =>
            {
                if (e.Track is VideoStreamTrack videoTrack)
                {
                    peer.RemoteVideoTrack = videoTrack;
                    videoTrack.OnVideoReceived += texture => RemoteVideoReceived?.Invoke(remotePlayerId, texture);

                    if (videoTrack.Texture != null)
                    {
                        RemoteVideoReceived?.Invoke(remotePlayerId, videoTrack.Texture);
                    }
                }
                else if (e.Track is AudioStreamTrack audioTrack)
                {
                    peer.RemoteAudioTrack = audioTrack;
                    RemoteAudioReceived?.Invoke(remotePlayerId, audioTrack);
                }
            };

            connection.OnTrack = e => receiveStream.AddTrack(e.Track);
            connection.OnIceCandidate = candidate =>
            {
                if (candidate == null)
                {
                    return;
                }

                _ = SendSignalAsync(remotePlayerId, new SignalPayload
                {
                    kind = "ice",
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex ?? -1
                });
            };

            if (_localVideoTrack != null)
            {
                connection.AddTrack(_localVideoTrack);
            }

            if (_localAudioTrack != null)
            {
                connection.AddTrack(_localAudioTrack);
            }

            return peer;
        }

        private void HandlePeerSignal(int remotePlayerId, SignalPayload payload)
        {
            if (!_isPublishing || payload == null || remotePlayerId == 0)
            {
                return;
            }

            var peer = GetOrCreatePeer(remotePlayerId);

            switch (payload.kind)
            {
                case "offer":
                    StartCoroutine(ApplyOfferCoroutine(peer, payload.sdp));
                    break;

                case "answer":
                    StartCoroutine(ApplyAnswerCoroutine(peer, payload.sdp));
                    break;

                case "ice":
                    if (peer.HasRemoteDescription)
                    {
                        AddIceCandidate(peer, payload);
                    }
                    else
                    {
                        peer.PendingIceCandidates.Enqueue(payload);
                    }

                    break;
            }
        }

        private IEnumerator CreateOfferCoroutine(PeerState peer)
        {
            var offerOperation = peer.Connection.CreateOffer();
            yield return offerOperation;

            if (offerOperation.IsError)
            {
                yield break;
            }

            var description = offerOperation.Desc;
            var setOperation = peer.Connection.SetLocalDescription(ref description);
            yield return setOperation;

            if (!setOperation.IsError)
            {
                _ = SendSignalAsync(peer.RemotePlayerId, new SignalPayload
                {
                    kind = "offer",
                    sdp = description.sdp
                });
            }
        }

        private IEnumerator ApplyOfferCoroutine(PeerState peer, string sdp)
        {
            var description = new RTCSessionDescription
            {
                type = RTCSdpType.Offer,
                sdp = sdp
            };
            var remoteOperation = peer.Connection.SetRemoteDescription(ref description);
            yield return remoteOperation;

            if (remoteOperation.IsError)
            {
                yield break;
            }

            peer.HasRemoteDescription = true;
            FlushPendingIce(peer);

            var answerOperation = peer.Connection.CreateAnswer();
            yield return answerOperation;

            if (answerOperation.IsError)
            {
                yield break;
            }

            var answer = answerOperation.Desc;
            var localOperation = peer.Connection.SetLocalDescription(ref answer);
            yield return localOperation;

            if (!localOperation.IsError)
            {
                _ = SendSignalAsync(peer.RemotePlayerId, new SignalPayload
                {
                    kind = "answer",
                    sdp = answer.sdp
                });
            }
        }

        private IEnumerator ApplyAnswerCoroutine(PeerState peer, string sdp)
        {
            var description = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = sdp
            };
            var operation = peer.Connection.SetRemoteDescription(ref description);
            yield return operation;

            if (!operation.IsError)
            {
                peer.HasRemoteDescription = true;
                FlushPendingIce(peer);
            }
        }

        private void AddIceCandidate(PeerState peer, SignalPayload payload)
        {
            var candidate = new RTCIceCandidate(new RTCIceCandidateInit
            {
                candidate = payload.candidate,
                sdpMid = payload.sdpMid,
                sdpMLineIndex = payload.sdpMLineIndex >= 0 ? payload.sdpMLineIndex : (int?)null
            });
            peer.Connection.AddIceCandidate(candidate);
        }

        private void FlushPendingIce(PeerState peer)
        {
            while (peer.PendingIceCandidates.Count > 0)
            {
                AddIceCandidate(peer, peer.PendingIceCandidates.Dequeue());
            }
        }

        private Task SendMediaEnabledAsync(bool enabled)
        {
            return SendClientMessageAsync(new ClientMessage
            {
                type = "media",
                enabled = enabled,
                videoRotation = enabled ? _localVideoRotationDegrees : 0,
                videoMirrored = enabled && _localVideoVerticallyMirrored
            }, _lifetimeCancellation.Token);
        }

        private Task SendSignalAsync(int targetPlayerId, SignalPayload payload)
        {
            return SendClientMessageAsync(new ClientMessage
            {
                type = "signal",
                to = targetPlayerId,
                payload = payload
            }, _lifetimeCancellation.Token);
        }

        private async Task SendClientMessageAsync(ClientMessage message, CancellationToken cancellationToken)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonUtility.ToJson(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocketSendLock.WaitAsync(cancellationToken);

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken);
                }
            }
            catch (Exception exception) when (exception is WebSocketException
                || exception is ObjectDisposedException
                || exception is OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _status = $"AV send failed: {exception.Message}";
                }
            }
            finally
            {
                _webSocketSendLock.Release();
            }
        }

        private void CloseAllPeers()
        {
            var peerIds = new List<int>(_peersByPlayerId.Keys);

            foreach (var peerId in peerIds)
            {
                ClosePeer(peerId);
            }
        }

        private void ClosePeer(int remotePlayerId)
        {
            if (!_peersByPlayerId.TryGetValue(remotePlayerId, out var peer))
            {
                return;
            }

            _peersByPlayerId.Remove(remotePlayerId);
            _remoteVideoLayoutsByPlayerId.Remove(remotePlayerId);
            peer.Dispose();
            RemotePeerClosed?.Invoke(remotePlayerId);
        }

        private void StopLocalMedia()
        {
            _localVideoTrack?.Dispose();
            _localVideoTrack = null;

            if (_localVideoTexture != null)
            {
                Destroy(_localVideoTexture);
                _localVideoTexture = null;
            }

            _localVideoCopyFailedLogged = false;

            _localAudioTrack?.Dispose();
            _localAudioTrack = null;

            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }

            if (!string.IsNullOrEmpty(_microphoneDeviceName))
            {
                Microphone.End(_microphoneDeviceName);
                _microphoneDeviceName = null;
            }

            if (_microphoneSource != null)
            {
                _microphoneSource.Stop();
                _microphoneSource.clip = null;
            }

            _microphoneClip = null;
        }

        private bool TryCreateLocalVideoTrack(string deviceName)
        {
            try
            {
                _localVideoTrack = new VideoStreamTrack(_webCamTexture);
                Debug.Log($"AV camera started: '{deviceName}' direct texture {_webCamTexture.width}x{_webCamTexture.height}.");
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"AV camera direct texture unsupported, trying compatible texture: {exception.Message}");
            }

            try
            {
                var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
                _localVideoTexture = new Texture2D(
                    _webCamTexture.width,
                    _webCamTexture.height,
                    supportedFormat,
                    TextureCreationFlags.None)
                {
                    name = "Augmego WebRTC Camera Texture"
                };
                _localVideoCopyFailedLogged = false;

                if (!UpdateLocalVideoTexture(force: true))
                {
                    throw new InvalidOperationException("Graphics.ConvertTexture could not copy the camera frame.");
                }

                _localVideoTrack = new VideoStreamTrack(_localVideoTexture);
                Debug.Log($"AV camera started: '{deviceName}' via compatible {supportedFormat} texture {_webCamTexture.width}x{_webCamTexture.height}.");
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                Debug.LogWarning($"AV camera start failed: compatible texture could not be created. {exception.Message}");
                _localVideoTrack?.Dispose();
                _localVideoTrack = null;

                if (_localVideoTexture != null)
                {
                    Destroy(_localVideoTexture);
                    _localVideoTexture = null;
                }

                _localVideoCopyFailedLogged = false;

                return false;
            }
        }

        private bool UpdateLocalVideoTexture(bool force = false)
        {
            if (_webCamTexture == null || _localVideoTexture == null)
            {
                return false;
            }

            if (!force && !_webCamTexture.didUpdateThisFrame)
            {
                return true;
            }

            if (Graphics.ConvertTexture(_webCamTexture, _localVideoTexture))
            {
                return true;
            }

            if (!_localVideoCopyFailedLogged)
            {
                _localVideoCopyFailedLogged = true;
                Debug.LogWarning("AV camera compatible texture copy failed; local video may not publish on this graphics backend.");
            }

            return false;
        }

        private void RefreshLocalVideoLayout(bool forceBroadcast = true)
        {
            if (_webCamTexture == null)
            {
                return;
            }

            var rotation = NormalizeVideoRotation(_webCamTexture.videoRotationAngle);
            var mirrored = _webCamTexture.videoVerticallyMirrored;

            if (!forceBroadcast
                || (rotation == _localVideoRotationDegrees && mirrored == _localVideoVerticallyMirrored))
            {
                _localVideoRotationDegrees = rotation;
                _localVideoVerticallyMirrored = mirrored;
                return;
            }

            _localVideoRotationDegrees = rotation;
            _localVideoVerticallyMirrored = mirrored;

            if (_hasRtcConfig)
            {
                _ = SendMediaEnabledAsync(true);
            }
        }

        private void ResetSignalingState()
        {
            _hasRtcConfig = false;
            _boundPlayerId = 0;
            _remoteMediaEnabledByPlayerId.Clear();
            _remoteVideoLayoutsByPlayerId.Clear();
            CloseAllPeers();

            if (_isPublishing)
            {
                _status = "AV reconnecting";
            }
        }

        private string BuildWebSocketUrl()
        {
            if (!string.IsNullOrWhiteSpace(webSocketUrlOverride))
            {
                return webSocketUrlOverride.Trim();
            }

            if (client == null || string.IsNullOrWhiteSpace(client.ServerHost))
            {
                return string.Empty;
            }

            var host = client.ServerHost;

            if (useSecureWebSocket)
            {
                return $"wss://{host}/rtc";
            }

            return $"ws://{host}:{directWebSocketPort}/rtc";
        }

        private static string GetPreferredCameraDeviceName()
        {
            foreach (var device in WebCamTexture.devices)
            {
                if (device.isFrontFacing)
                {
                    return device.name;
                }
            }

            return WebCamTexture.devices.Length > 0 ? WebCamTexture.devices[0].name : string.Empty;
        }

        private static int GetMicrophoneSampleRate(string deviceName)
        {
            Microphone.GetDeviceCaps(deviceName, out var minFrequency, out var maxFrequency);

            if (minFrequency == 0 && maxFrequency == 0)
            {
                return MicrophoneSampleRate;
            }

            if (maxFrequency > 0)
            {
                return Mathf.Clamp(MicrophoneSampleRate, Mathf.Max(1, minFrequency), maxFrequency);
            }

            return Mathf.Max(1, minFrequency);
        }

        private static int NormalizeVideoRotation(int degrees)
        {
            var normalized = degrees % 360;

            if (normalized < 0)
            {
                normalized += 360;
            }

            return ((normalized + 45) / 90 * 90) % 360;
        }

        public bool TryGetRemoteVideoLayout(int playerId, out int rotationDegrees, out bool mirrored)
        {
            if (_remoteVideoLayoutsByPlayerId.TryGetValue(playerId, out var layout))
            {
                rotationDegrees = layout.RotationDegrees;
                mirrored = layout.Mirrored;
                return true;
            }

            rotationDegrees = 0;
            mirrored = false;
            return false;
        }

        private static float GetUiScale()
        {
#if UNITY_IOS || UNITY_ANDROID
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 430f, 1.4f, 2.2f);
#else
            return 1f;
#endif
        }

        private static Rect GetAvButtonRect(float uiScale)
        {
            var safeArea = Screen.safeArea;
            var leftInset = safeArea.xMin / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            return new Rect(Mathf.Max(12f, leftInset + 12f), Mathf.Max(12f, topInset + 12f), AvButtonSize, AvButtonSize);
        }

        [Serializable]
        private sealed class ClientMessage
        {
            public string type;
            public string sessionId;
            public int playerId;
            public bool enabled;
            public int videoRotation;
            public bool videoMirrored;
            public int to;
            public string displayName;
            public string text;
            public SignalPayload payload;
        }

        [Serializable]
        private sealed class ServerMessageEnvelope
        {
            public string type;
        }

        [Serializable]
        private sealed class ConfigServerMessage
        {
            public string type;
            public int playerId;
            public IceServerMessage[] iceServers;
        }

        [Serializable]
        private sealed class MediaStateServerMessage
        {
            public string type;
            public MediaStatePlayerMessage[] players;
        }

        [Serializable]
        private sealed class SignalServerMessage
        {
            public string type;
            public int from;
            public SignalPayload payload;
        }

        [Serializable]
        private sealed class PlayerNamesServerMessage
        {
            public string type;
            public PlayerNameMessage[] players;
        }

        [Serializable]
        private sealed class ChatHistoryServerMessage
        {
            public string type;
            public ChatMessage[] messages;
        }

        [Serializable]
        private sealed class ChatServerMessage
        {
            public string type;
            public ChatMessage message;
        }

        [Serializable]
        public sealed class NameResultMessage
        {
            public string type;
            public bool ok;
            public string displayName;
            public int retryAfterSeconds;
        }

        [Serializable]
        public sealed class PlayerNameMessage
        {
            public int playerId;
            public string displayName;
        }

        [Serializable]
        public sealed class ChatMessage
        {
            public long sequence;
            public int playerId;
            public string displayName;
            public string text;
            public long sentAtUnixMs;
        }

        [Serializable]
        private sealed class IceServerMessage
        {
            public string[] urls;
            public string username;
            public string credential;
        }

        [Serializable]
        private sealed class MediaStatePlayerMessage
        {
            public int playerId;
            public bool enabled;
            public int videoRotation;
            public bool videoMirrored;
        }

        [Serializable]
        public sealed class SignalPayload
        {
            public string kind;
            public string sdp;
            public string candidate;
            public string sdpMid;
            public int sdpMLineIndex = -1;
        }

        private sealed class PeerState : IDisposable
        {
            public PeerState(int remotePlayerId, RTCPeerConnection connection, MediaStream receiveStream)
            {
                RemotePlayerId = remotePlayerId;
                Connection = connection;
                ReceiveStream = receiveStream;
            }

            public int RemotePlayerId { get; }
            public RTCPeerConnection Connection { get; }
            public MediaStream ReceiveStream { get; }
            public Queue<SignalPayload> PendingIceCandidates { get; } = new();
            public VideoStreamTrack RemoteVideoTrack { get; set; }
            public AudioStreamTrack RemoteAudioTrack { get; set; }
            public bool HasRemoteDescription { get; set; }
            public bool OfferStarted { get; set; }

            public void Dispose()
            {
                RemoteVideoTrack?.Dispose();
                RemoteAudioTrack?.Dispose();
                ReceiveStream?.Dispose();
                Connection?.Close();
                Connection?.Dispose();
            }
        }

        private readonly struct RemoteVideoLayout
        {
            public RemoteVideoLayout(int rotationDegrees, bool mirrored)
            {
                RotationDegrees = rotationDegrees;
                Mirrored = mirrored;
            }

            public int RotationDegrees { get; }
            public bool Mirrored { get; }
        }
    }
}

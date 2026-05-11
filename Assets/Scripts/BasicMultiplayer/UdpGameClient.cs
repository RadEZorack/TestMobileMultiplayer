using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace BasicMultiplayer
{
    public sealed class UdpGameClient : MonoBehaviour
    {
        private const float InputSendRate = 30f;
        private const float ReconnectHelloInterval = 1f;
        private const float JoystickRadiusPixels = 90f;

        [SerializeField] private string serverHost = "dev.augmego.ca";
        [SerializeField] private int serverPort = 7777;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private bool autoConnectOnStart = false;

        private readonly ConcurrentQueue<string> _incomingMessages = new();
        private readonly Dictionary<int, PlayerSnapshot> _players = new();
        private readonly object _socketLock = new();
        private readonly string _clientSessionId = Guid.NewGuid().ToString("N");

        private UdpClient _udp;
        private Thread _receiveThread;
        private IPEndPoint _serverEndpoint;
        private float _inputSendTimer;
        private float _helloTimer;
        private int _inputSequence;
        private int _localPlayerId;
        private string _status = "Disconnected";
        private Vector2 _moveInput;
        private Vector2 _joystickOrigin;
        private Vector2 _joystickCurrent;
        private int _joystickTouchId = -1;
        private bool _isRunning;

        public IReadOnlyDictionary<int, PlayerSnapshot> Players => _players;
        public int LocalPlayerId => _localPlayerId;
        public bool IsConnected => _udp != null;
        public Vector2 MoveInput => _moveInput;

        private void Start()
        {
            Application.runInBackground = true;

            if (autoConnectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            DrainIncomingMessages();
            _moveInput = ReadMoveInput();

            if (_udp == null)
            {
                return;
            }

            _inputSendTimer += Time.deltaTime;
            _helloTimer += Time.deltaTime;

            if (_localPlayerId == 0 && _helloTimer >= ReconnectHelloInterval)
            {
                _helloTimer = 0f;
                SendHello();
            }

            if (_inputSendTimer >= 1f / InputSendRate)
            {
                _inputSendTimer = 0f;
                SendInput(_moveInput);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                SendInput(Vector2.zero);
            }
        }

        public void Connect()
        {
            Disconnect();

            try
            {
                var host = NormalizeServerHost(serverHost);

                if (string.IsNullOrEmpty(host))
                {
                    _status = "Enter a server host";
                    return;
                }

                if (!TryCreateConnectedUdpClient(host, serverPort, out var udp, out var endpoint, out var error))
                {
                    _status = error;
                    return;
                }

                _serverEndpoint = endpoint;
                _udp = udp;
                _isRunning = true;
                _localPlayerId = 0;
                _players.Clear();
                ClearIncomingMessages();

                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Basic UDP Game Client"
                };
                _receiveThread.Start();

                _status = $"Connecting to {host} ({_serverEndpoint.Address})";
                _helloTimer = ReconnectHelloInterval;
                SendHello();
            }
            catch (Exception exception) when (exception is SocketException
                || exception is ArgumentException
                || exception is FormatException)
            {
                CloseSocket();
                _status = $"Connect failed: {exception.Message}";
            }
        }

        public void Disconnect()
        {
            CloseSocket();
            _localPlayerId = 0;
            _players.Clear();
            _status = "Disconnected";
        }

        private void CloseSocket()
        {
            _isRunning = false;

            lock (_socketLock)
            {
                _udp?.Close();
                _udp?.Dispose();
                _udp = null;
            }

            _receiveThread = null;
        }

        private void ReceiveLoop()
        {
            var remoteEndpoint = new IPEndPoint(GetAnyAddressForCurrentSocket(), 0);

            while (_isRunning)
            {
                try
                {
                    var udp = _udp;

                    if (udp == null)
                    {
                        return;
                    }

                    var bytes = udp.Receive(ref remoteEndpoint);
                    var message = Encoding.UTF8.GetString(bytes);
                    _incomingMessages.Enqueue(message);
                }
                catch (SocketException)
                {
                    if (_isRunning)
                    {
                        _incomingMessages.Enqueue("ERROR Socket receive failed");
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void DrainIncomingMessages()
        {
            while (_incomingMessages.TryDequeue(out var message))
            {
                ParseServerMessage(message);
            }
        }

        private void ParseServerMessage(string message)
        {
            var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return;
            }

            switch (parts[0])
            {
                case "WELCOME":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var playerId))
                    {
                        _localPlayerId = playerId;
                        _status = $"Connected as player {playerId}";
                    }

                    break;

                case "STATE":
                    ParseState(parts);
                    break;

                case "ERROR":
                    _status = message;
                    break;
            }
        }

        private void ParseState(string[] parts)
        {
            if (parts.Length < 6)
            {
                return;
            }

            _players.Clear();

            for (var index = 3; index + 2 < parts.Length; index += 3)
            {
                if (!int.TryParse(parts[index], out var id)
                    || !float.TryParse(parts[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    || !float.TryParse(parts[index + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    continue;
                }

                _players[id] = new PlayerSnapshot(id, new Vector2(x, y));
            }
        }

        private void SendInput(Vector2 input)
        {
            Send(string.Format(
                CultureInfo.InvariantCulture,
                "INPUT2 {0} {1} {2:0.###} {3:0.###}",
                _clientSessionId,
                _inputSequence++,
                input.x,
                input.y));
        }

        private void SendHello()
        {
            Send($"HELLO2 {_clientSessionId} {SanitizeToken(playerName)}");
        }

        private void Send(string message)
        {
            lock (_socketLock)
            {
                if (_udp == null)
                {
                    return;
                }

                var bytes = Encoding.UTF8.GetBytes(message);

                try
                {
                    _udp.Send(bytes, bytes.Length);
                }
                catch (SocketException exception)
                {
                    _incomingMessages.Enqueue($"ERROR Send failed: {exception.Message}");
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private Vector2 ReadMoveInput()
        {
            var move = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    move.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    move.x += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    move.y -= 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    move.y += 1f;
                }
            }

            move += ReadTouchJoystickInput();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                move.x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                move.x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                move.y -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                move.y += 1f;
            }

            move += ReadLegacyTouchJoystickInput();
#endif

            return Vector2.ClampMagnitude(move, 1f);
        }

#if ENABLE_INPUT_SYSTEM
        private Vector2 ReadTouchJoystickInput()
        {
            var touchscreen = Touchscreen.current;

            if (touchscreen == null)
            {
                _joystickTouchId = -1;
                return Vector2.zero;
            }

            if (_joystickTouchId >= 0)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!IsTouchPressed(touch) || touch.touchId.ReadValue() != _joystickTouchId)
                    {
                        continue;
                    }

                    _joystickCurrent = touch.position.ReadValue();
                    return Vector2.ClampMagnitude((_joystickCurrent - _joystickOrigin) / JoystickRadiusPixels, 1f);
                }
            }

            foreach (var touch in touchscreen.touches)
            {
                if (!IsTouchPressed(touch))
                {
                    continue;
                }

                var position = touch.position.ReadValue();

                if (position.x > Screen.width * 0.62f)
                {
                    continue;
                }

                _joystickTouchId = touch.touchId.ReadValue();
                _joystickOrigin = position;
                _joystickCurrent = position;
                return Vector2.zero;
            }

            _joystickTouchId = -1;
            return Vector2.zero;
        }

        private static bool IsTouchPressed(TouchControl touch)
        {
            return touch.press.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private Vector2 ReadLegacyTouchJoystickInput()
        {
            if (_joystickTouchId >= 0)
            {
                for (var index = 0; index < Input.touchCount; index++)
                {
                    var touch = Input.GetTouch(index);

                    if (touch.fingerId != _joystickTouchId)
                    {
                        continue;
                    }

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        _joystickTouchId = -1;
                        return Vector2.zero;
                    }

                    _joystickCurrent = touch.position;
                    return Vector2.ClampMagnitude((_joystickCurrent - _joystickOrigin) / JoystickRadiusPixels, 1f);
                }
            }

            for (var index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);

                if (touch.position.x > Screen.width * 0.62f)
                {
                    continue;
                }

                _joystickTouchId = touch.fingerId;
                _joystickOrigin = touch.position;
                _joystickCurrent = touch.position;
                return Vector2.zero;
            }

            _joystickTouchId = -1;
            return Vector2.zero;
        }
#endif

        private void OnGUI()
        {
            var previousMatrix = GUI.matrix;
            var uiScale = GetUiScale();
            var safeArea = Screen.safeArea;
            var leftInset = safeArea.xMin / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var left = Mathf.Max(12f, leftInset + 12f);
            var top = Mathf.Max(12f, topInset + 12f);
            var availableWidth = Mathf.Max(280f, (Screen.width / uiScale) - left - 12f);
            var panelWidth = Mathf.Min(680f, availableWidth);

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            GUILayout.BeginArea(new Rect(left, top, panelWidth, 256f), GUI.skin.box);
            GUILayout.Label("UDP Multiplayer Prototype");
            GUILayout.Label(_status, GUILayout.Height(24f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Server IP", GUILayout.Width(76f), GUILayout.Height(42f));
            serverHost = GUILayout.TextField(serverHost, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(48f), GUILayout.Height(42f));
            var portText = GUILayout.TextField(
                serverPort.ToString(CultureInfo.InvariantCulture),
                GUILayout.Width(110f),
                GUILayout.Height(42f));

            if (int.TryParse(portText, out var parsedPort))
            {
                serverPort = Mathf.Clamp(parsedPort, 1, 65535);
            }

            GUILayout.Label("Name", GUILayout.Width(52f), GUILayout.Height(42f));
            playerName = GUILayout.TextField(playerName, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (_udp == null)
            {
                if (GUILayout.Button("Connect", GUILayout.Height(54f)))
                {
                    Connect();
                }
            }
            else if (GUILayout.Button("Disconnect", GUILayout.Height(54f)))
            {
                Disconnect();
            }

            GUILayout.Label($"Players: {_players.Count}", GUILayout.Width(110f), GUILayout.Height(54f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUI.matrix = previousMatrix;

            DrawJoystickDebug();
        }

        private void DrawJoystickDebug()
        {
            if (_joystickTouchId < 0)
            {
                return;
            }

            var origin = new Vector2(_joystickOrigin.x, Screen.height - _joystickOrigin.y);
            var current = new Vector2(_joystickCurrent.x, Screen.height - _joystickCurrent.y);

            GUI.Box(new Rect(origin.x - 48f, origin.y - 48f, 96f, 96f), string.Empty);
            GUI.Box(new Rect(current.x - 24f, current.y - 24f, 48f, 48f), string.Empty);
        }

        private void ClearIncomingMessages()
        {
            while (_incomingMessages.TryDequeue(out _))
            {
            }
        }

        private IPAddress GetAnyAddressForCurrentSocket()
        {
            return _serverEndpoint != null && _serverEndpoint.AddressFamily == AddressFamily.InterNetworkV6
                ? IPAddress.IPv6Any
                : IPAddress.Any;
        }

        private static bool TryCreateConnectedUdpClient(
            string host,
            int port,
            out UdpClient udp,
            out IPEndPoint endpoint,
            out string error)
        {
            udp = null;
            endpoint = null;
            error = string.Empty;

            if (!TryResolveAddresses(host, out var addresses, out error))
            {
                return false;
            }

            Exception lastException = null;

            for (var index = 0; index < addresses.Count; index++)
            {
                var address = addresses[index];

                try
                {
                    endpoint = new IPEndPoint(address, port);
                    udp = new UdpClient(address.AddressFamily);
                    udp.Connect(endpoint);
                    return true;
                }
                catch (Exception exception) when (exception is SocketException
                    || exception is ArgumentException
                    || exception is ObjectDisposedException)
                {
                    lastException = exception;
                    udp?.Close();
                    udp?.Dispose();
                    udp = null;
                    endpoint = null;
                }
            }

            error = lastException == null
                ? $"Could not open UDP socket for {host}:{port}"
                : $"Could not open UDP socket for {host}:{port}: {lastException.Message}";
            return false;
        }

        private static bool TryResolveAddresses(string host, out List<IPAddress> addresses, out string error)
        {
            addresses = new List<IPAddress>();
            error = string.Empty;

            if (IPAddress.TryParse(host, out var literalAddress))
            {
                addresses.Add(literalAddress);
                return true;
            }

            try
            {
                addresses.AddRange(Dns.GetHostAddresses(host));
            }
            catch (SocketException exception)
            {
                error = $"DNS failed for {host}: {exception.Message}";
                return false;
            }
            catch (ArgumentException exception)
            {
                error = $"Invalid server host {host}: {exception.Message}";
                return false;
            }

            addresses.RemoveAll(address => address.AddressFamily != AddressFamily.InterNetwork
                && address.AddressFamily != AddressFamily.InterNetworkV6);
            addresses.Sort(CompareAddressPreference);

            if (addresses.Count == 0)
            {
                error = $"Could not resolve {host}";
                return false;
            }

            return true;
        }

        private static int CompareAddressPreference(IPAddress left, IPAddress right)
        {
            return GetAddressPreference(left).CompareTo(GetAddressPreference(right));
        }

        private static int GetAddressPreference(IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return 0;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return 1;
            }

            return 2;
        }

        private static float GetUiScale()
        {
#if UNITY_IOS || UNITY_ANDROID
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 430f, 1.4f, 2.2f);
#else
            return 1f;
#endif
        }

        private static string NormalizeServerHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var host = value.Trim();

            if (Uri.TryCreate(host, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }

            return host;
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Player";
            }

            return value.Trim().Replace(' ', '_');
        }
    }
}

using System;
using System.Collections;
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
        private const float PlayerSpeed = 4.5f;
        private const float MaxTrustedPlayerCoordinate = 4096f;
        private const float JoystickRadiusPixels = 105f;
        private const float LeftJoystickScreenMax = 0.48f;
        private const float RightJoystickScreenMin = 0.52f;
        private const float JoystickScreenMaxY = 0.58f;
        private const int NoPointerId = -1;
        private const int MousePointerId = -2;

        [SerializeField] private string serverHost = "dev.augmego.ca";
        [SerializeField] private int serverPort = 7777;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private bool showConnectionOverlay = false;
        [SerializeField] private bool showVirtualJoysticks = true;
        [SerializeField] private GameAuthClient authClient;
        [SerializeField] private bool waitForAuthBeforeConnect = true;

        private readonly ConcurrentQueue<string> _incomingMessages = new();
        private readonly Dictionary<int, PlayerSnapshot> _players = new();
        private readonly SortedDictionary<long, VoxelEditMessage> _pendingVoxelEditsBySequence = new();
        private readonly object _socketLock = new();
        private readonly string _clientSessionId = Guid.NewGuid().ToString("N");

        private UdpClient _udp;
        private Thread _receiveThread;
        private IPEndPoint _serverEndpoint;
        private float _inputSendTimer;
        private float _helloTimer;
        private int _inputSequence;
        private int _voxelEditSequence;
        private int _localPlayerId;
        private long _lastAppliedVoxelEditSequence;
        private string _status = "Disconnected";
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private Vector2 _localPosition;
        private Vector2 _moveJoystickOrigin;
        private Vector2 _moveJoystickCurrent;
        private Vector2 _lookJoystickOrigin;
        private Vector2 _lookJoystickCurrent;
        private int _moveJoystickPointerId = NoPointerId;
        private int _lookJoystickPointerId = NoPointerId;
        private bool _isRunning;
        private bool _hasLocalPosition;

        public IReadOnlyDictionary<int, PlayerSnapshot> Players => _players;
        public int LocalPlayerId => _localPlayerId;
        public string SessionId => authClient != null && authClient.HasGameSession ? authClient.GameSessionId : _clientSessionId;
        public string ServerHost => NormalizeServerHost(serverHost);
        public int ServerPort => serverPort;
        public bool IsConnected => _udp != null;
        public bool HasLocalPosition => _hasLocalPosition;
        public Vector2 LocalPosition => _localPosition;
        public Vector2 MoveInput => _moveInput;
        public Vector2 LookInput => _lookInput;
        public float MovementYawDegrees { get; set; }
        public Func<Vector2, Vector2> MoveInputFilter { get; set; }
        public event Action<VoxelEditMessage> VoxelEditReceived;

        private void Start()
        {
            Application.runInBackground = true;

            if (authClient == null)
            {
                authClient = GetComponent<GameAuthClient>();
            }

            if (autoConnectOnStart || !showConnectionOverlay)
            {
                if (waitForAuthBeforeConnect && authClient != null)
                {
                    StartCoroutine(ConnectWhenAuthReadyCoroutine());
                }
                else
                {
                    Connect();
                }
            }
        }

        private void Update()
        {
            DrainIncomingMessages();
            ReadLocalInput();
            SimulateLocalPlayerMovement();

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
            if (waitForAuthBeforeConnect
                && authClient != null
                && !authClient.HasGameSession)
            {
                _status = $"Waiting for auth: {authClient.Status}";
                return;
            }

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
                _hasLocalPosition = false;
                _localPosition = Vector2.zero;
                _players.Clear();
                ResetVoxelEditSync();
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
            _hasLocalPosition = false;
            _localPosition = Vector2.zero;
            _players.Clear();
            _status = "Disconnected";
        }

        private IEnumerator ConnectWhenAuthReadyCoroutine()
        {
            while (authClient != null && !authClient.HasGameSession)
            {
                _status = $"Waiting for auth: {authClient.Status}";
                yield return null;
            }

            Connect();
        }

        public bool TryGetLocalPosition(out Vector2 position)
        {
            position = _localPosition;
            return _hasLocalPosition;
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
                        var playerChanged = playerId != _localPlayerId;
                        _localPlayerId = playerId;
                        _status = $"Connected as player {playerId}";

                        if (parts.Length >= 5
                            && TryParseFloat(parts[3], out var spawnX)
                            && TryParseFloat(parts[4], out var spawnY)
                            && (!_hasLocalPosition || playerChanged))
                        {
                            SetLocalPosition(new Vector2(spawnX, spawnY));
                        }
                    }

                    break;

                case "STATE":
                    ParseState(parts);
                    break;

                case "EDIT":
                    ParseVoxelEdit(parts);
                    break;

                case "ERROR":
                    _status = message;
                    break;
            }
        }

        private void ParseState(string[] parts)
        {
            if (parts.Length < 3)
            {
                return;
            }

            _players.Clear();

            for (var index = 3; index + 2 < parts.Length; index += 3)
            {
                if (!int.TryParse(parts[index], out var id)
                    || !TryParseFloat(parts[index + 1], out var x)
                    || !TryParseFloat(parts[index + 2], out var y))
                {
                    continue;
                }

                if (id == _localPlayerId)
                {
                    if (!_hasLocalPosition)
                    {
                        SetLocalPosition(new Vector2(x, y));
                    }

                    continue;
                }

                _players[id] = new PlayerSnapshot(id, new Vector2(x, y));
            }
        }

        private void ParseVoxelEdit(string[] parts)
        {
            if (parts.Length < 7
                || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerId)
                || !TryParseVoxelEditAction(parts[3], out var action)
                || !int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                || !int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                || !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
            {
                return;
            }

            var voxelType = parts.Length > 7 ? parts[7] : "marker";
            QueueVoxelEdit(new VoxelEditMessage(sequence, playerId, action, new Vector3Int(x, y, z), voxelType));
        }

        private void QueueVoxelEdit(VoxelEditMessage edit)
        {
            if (edit.Sequence <= _lastAppliedVoxelEditSequence || _pendingVoxelEditsBySequence.ContainsKey(edit.Sequence))
            {
                return;
            }

            _pendingVoxelEditsBySequence[edit.Sequence] = edit;

            while (_pendingVoxelEditsBySequence.TryGetValue(_lastAppliedVoxelEditSequence + 1, out var nextEdit))
            {
                _pendingVoxelEditsBySequence.Remove(nextEdit.Sequence);
                _lastAppliedVoxelEditSequence = nextEdit.Sequence;
                VoxelEditReceived?.Invoke(nextEdit);
            }
        }

        private void SendInput(Vector2 input)
        {
            if (_hasLocalPosition)
            {
                Send(string.Format(
                    CultureInfo.InvariantCulture,
                    "INPUT2 {0} {1} {2:0.###} {3:0.###} {4} {5:0.###} {6:0.###}",
                    SessionId,
                    _inputSequence++,
                    input.x,
                    input.y,
                    _lastAppliedVoxelEditSequence,
                    _localPosition.x,
                    _localPosition.y));
                return;
            }

            Send(string.Format(
                CultureInfo.InvariantCulture,
                "INPUT2 {0} {1} {2:0.###} {3:0.###} {4}",
                SessionId,
                _inputSequence++,
                input.x,
                input.y,
                _lastAppliedVoxelEditSequence));
        }

        private void SendHello()
        {
            Send($"HELLO2 {SessionId} {SanitizeToken(playerName, fallback: "Player")} {_lastAppliedVoxelEditSequence}");
        }

        public void SendVoxelEdit(VoxelEditAction action, Vector3Int cell, string voxelType)
        {
            var actionToken = action == VoxelEditAction.Remove ? "REMOVE" : "PLACE";

            Send(string.Format(
                CultureInfo.InvariantCulture,
                "EDIT2 {0} {1} {2} {3} {4} {5} {6} {7}",
                SessionId,
                _voxelEditSequence++,
                actionToken,
                cell.x,
                cell.y,
                cell.z,
                SanitizeToken(voxelType, fallback: "marker"),
                _lastAppliedVoxelEditSequence));
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

        private void ReadLocalInput()
        {
            var move = Vector2.zero;
            var look = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            move += ReadKeyboardMoveInput();
            ReadTouchJoystickInput(ref move, ref look);
            ReadMouseJoystickInput(ref move, ref look);
#elif ENABLE_LEGACY_INPUT_MANAGER
            move += ReadLegacyKeyboardMoveInput();
            ReadLegacyTouchJoystickInput(ref move, ref look);
            ReadLegacyMouseJoystickInput(ref move, ref look);
#endif

            var worldMoveInput = TransformMoveInput(Vector2.ClampMagnitude(move, 1f));

            if (MoveInputFilter != null)
            {
                worldMoveInput = Vector2.ClampMagnitude(MoveInputFilter(worldMoveInput), 1f);
            }

            _moveInput = worldMoveInput;
            _lookInput = Vector2.ClampMagnitude(look, 1f);
        }

        private void SimulateLocalPlayerMovement()
        {
            if (_localPlayerId == 0 || !_hasLocalPosition)
            {
                return;
            }

            _localPosition += _moveInput * PlayerSpeed * Time.deltaTime;
            _localPosition.x = Mathf.Clamp(_localPosition.x, -MaxTrustedPlayerCoordinate, MaxTrustedPlayerCoordinate);
            _localPosition.y = Mathf.Clamp(_localPosition.y, -MaxTrustedPlayerCoordinate, MaxTrustedPlayerCoordinate);
        }

        private void SetLocalPosition(Vector2 position)
        {
            _localPosition = position;
            _hasLocalPosition = true;
        }

        private Vector2 TransformMoveInput(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f)
            {
                return Vector2.zero;
            }

            var yaw = MovementYawDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(yaw);
            var cos = Mathf.Cos(yaw);

            return new Vector2(
                input.x * cos + input.y * sin,
                input.y * cos - input.x * sin);
        }

#if ENABLE_INPUT_SYSTEM
        private static Vector2 ReadKeyboardMoveInput()
        {
            var move = Vector2.zero;
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return move;
            }

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

            return move;
        }

        private void ReadTouchJoystickInput(ref Vector2 move, ref Vector2 look)
        {
            var touchscreen = Touchscreen.current;

            if (touchscreen == null)
            {
                ResetTouchPointer(ref _moveJoystickPointerId);
                ResetTouchPointer(ref _lookJoystickPointerId);
                return;
            }

            UpdateTouchJoystick(
                touchscreen,
                rightSide: false,
                ref _moveJoystickPointerId,
                ref _moveJoystickOrigin,
                ref _moveJoystickCurrent,
                ref move);
            UpdateTouchJoystick(
                touchscreen,
                rightSide: true,
                ref _lookJoystickPointerId,
                ref _lookJoystickOrigin,
                ref _lookJoystickCurrent,
                ref look);
        }

        private void UpdateTouchJoystick(
            Touchscreen touchscreen,
            bool rightSide,
            ref int pointerId,
            ref Vector2 origin,
            ref Vector2 current,
            ref Vector2 value)
        {
            if (pointerId >= 0)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!IsTouchPressed(touch) || touch.touchId.ReadValue() != pointerId)
                    {
                        continue;
                    }

                    current = touch.position.ReadValue();
                    value += GetJoystickValue(origin, current);
                    return;
                }

                pointerId = NoPointerId;
            }

            foreach (var touch in touchscreen.touches)
            {
                if (!IsTouchPressed(touch))
                {
                    continue;
                }

                var position = touch.position.ReadValue();

                if (!IsJoystickSide(position, rightSide))
                {
                    continue;
                }

                pointerId = touch.touchId.ReadValue();
                origin = position;
                current = position;
                return;
            }
        }

        private void ReadMouseJoystickInput(ref Vector2 move, ref Vector2 look)
        {
            var mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            var isPressed = mouse.leftButton.isPressed;

            if (!isPressed)
            {
                ResetMousePointer(ref _moveJoystickPointerId);
                ResetMousePointer(ref _lookJoystickPointerId);
                return;
            }

            var position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsJoystickSide(position, rightSide: true))
                {
                    BeginMouseJoystick(ref _lookJoystickPointerId, ref _lookJoystickOrigin, ref _lookJoystickCurrent, position);
                }
                else
                {
                    BeginMouseJoystick(ref _moveJoystickPointerId, ref _moveJoystickOrigin, ref _moveJoystickCurrent, position);
                }
            }

            if (_moveJoystickPointerId == MousePointerId)
            {
                _moveJoystickCurrent = position;
                move += GetJoystickValue(_moveJoystickOrigin, _moveJoystickCurrent);
            }

            if (_lookJoystickPointerId == MousePointerId)
            {
                _lookJoystickCurrent = position;
                look += GetJoystickValue(_lookJoystickOrigin, _lookJoystickCurrent);
            }
        }

        private static bool IsTouchPressed(TouchControl touch)
        {
            return touch.press.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private static Vector2 ReadLegacyKeyboardMoveInput()
        {
            var move = Vector2.zero;

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

            return move;
        }

        private void ReadLegacyTouchJoystickInput(ref Vector2 move, ref Vector2 look)
        {
            UpdateLegacyTouchJoystick(
                rightSide: false,
                ref _moveJoystickPointerId,
                ref _moveJoystickOrigin,
                ref _moveJoystickCurrent,
                ref move);
            UpdateLegacyTouchJoystick(
                rightSide: true,
                ref _lookJoystickPointerId,
                ref _lookJoystickOrigin,
                ref _lookJoystickCurrent,
                ref look);
        }

        private void UpdateLegacyTouchJoystick(
            bool rightSide,
            ref int pointerId,
            ref Vector2 origin,
            ref Vector2 current,
            ref Vector2 value)
        {
            if (pointerId >= 0)
            {
                for (var index = 0; index < Input.touchCount; index++)
                {
                    var touch = Input.GetTouch(index);

                    if (touch.fingerId != pointerId)
                    {
                        continue;
                    }

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        pointerId = NoPointerId;
                        return;
                    }

                    current = touch.position;
                    value += GetJoystickValue(origin, current);
                    return;
                }

                pointerId = NoPointerId;
            }

            for (var index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);

                if (touch.phase == TouchPhase.Ended
                    || touch.phase == TouchPhase.Canceled
                    || !IsJoystickSide(touch.position, rightSide))
                {
                    continue;
                }

                pointerId = touch.fingerId;
                origin = touch.position;
                current = touch.position;
                return;
            }
        }

        private void ReadLegacyMouseJoystickInput(ref Vector2 move, ref Vector2 look)
        {
            if (!Input.GetMouseButton(0))
            {
                ResetMousePointer(ref _moveJoystickPointerId);
                ResetMousePointer(ref _lookJoystickPointerId);
                return;
            }

            var position = (Vector2)Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                if (IsJoystickSide(position, rightSide: true))
                {
                    BeginMouseJoystick(ref _lookJoystickPointerId, ref _lookJoystickOrigin, ref _lookJoystickCurrent, position);
                }
                else
                {
                    BeginMouseJoystick(ref _moveJoystickPointerId, ref _moveJoystickOrigin, ref _moveJoystickCurrent, position);
                }
            }

            if (_moveJoystickPointerId == MousePointerId)
            {
                _moveJoystickCurrent = position;
                move += GetJoystickValue(_moveJoystickOrigin, _moveJoystickCurrent);
            }

            if (_lookJoystickPointerId == MousePointerId)
            {
                _lookJoystickCurrent = position;
                look += GetJoystickValue(_lookJoystickOrigin, _lookJoystickCurrent);
            }
        }
#endif

        private static Vector2 GetJoystickValue(Vector2 origin, Vector2 current)
        {
            return Vector2.ClampMagnitude((current - origin) / JoystickRadiusPixels, 1f);
        }

        private static bool IsJoystickSide(Vector2 position, bool rightSide)
        {
            if (position.y > Screen.height * JoystickScreenMaxY)
            {
                return false;
            }

            if (rightSide)
            {
                return position.x >= Screen.width * RightJoystickScreenMin;
            }

            return position.x <= Screen.width * LeftJoystickScreenMax;
        }

        private static void BeginMouseJoystick(
            ref int pointerId,
            ref Vector2 origin,
            ref Vector2 current,
            Vector2 position)
        {
            pointerId = MousePointerId;
            origin = position;
            current = position;
        }

        private static void ResetMousePointer(ref int pointerId)
        {
            if (pointerId == MousePointerId)
            {
                pointerId = NoPointerId;
            }
        }

        private static void ResetTouchPointer(ref int pointerId)
        {
            if (pointerId >= 0)
            {
                pointerId = NoPointerId;
            }
        }

        private void OnGUI()
        {
            if (!showConnectionOverlay)
            {
                DrawVirtualJoysticks();
                return;
            }

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

            DrawVirtualJoysticks();
        }

        private void DrawVirtualJoysticks()
        {
            if (!showVirtualJoysticks)
            {
                return;
            }

            var previousColor = GUI.color;

            DrawVirtualJoystick(
                _moveJoystickPointerId,
                _moveJoystickOrigin,
                _moveJoystickCurrent,
                new Vector2(Screen.width * 0.18f, Screen.height * 0.22f),
                new Color(0.1f, 0.78f, 1f, 0.38f));
            DrawVirtualJoystick(
                _lookJoystickPointerId,
                _lookJoystickOrigin,
                _lookJoystickCurrent,
                new Vector2(Screen.width * 0.82f, Screen.height * 0.22f),
                new Color(1f, 0.78f, 0.18f, 0.38f));

            GUI.color = previousColor;
        }

        private static void DrawVirtualJoystick(
            int pointerId,
            Vector2 activeOrigin,
            Vector2 activeCurrent,
            Vector2 idleOrigin,
            Color color)
        {
            var isActive = pointerId != NoPointerId;
            var origin = isActive ? activeOrigin : idleOrigin;
            var current = isActive ? activeCurrent : idleOrigin;
            var guiOrigin = ToGuiPosition(origin);
            var guiCurrent = ToGuiPosition(current);
            var baseSize = isActive ? 112f : 96f;
            var knobSize = isActive ? 48f : 34f;

            GUI.color = isActive ? color : new Color(color.r, color.g, color.b, 0.18f);
            GUI.Box(new Rect(guiOrigin.x - baseSize * 0.5f, guiOrigin.y - baseSize * 0.5f, baseSize, baseSize), string.Empty);

            GUI.color = isActive ? new Color(color.r, color.g, color.b, 0.72f) : new Color(color.r, color.g, color.b, 0.28f);
            GUI.Box(new Rect(guiCurrent.x - knobSize * 0.5f, guiCurrent.y - knobSize * 0.5f, knobSize, knobSize), string.Empty);
        }

        private static Vector2 ToGuiPosition(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private static bool TryParseFloat(string value, out float parsed)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }

        private void ClearIncomingMessages()
        {
            while (_incomingMessages.TryDequeue(out _))
            {
            }
        }

        private void ResetVoxelEditSync()
        {
            _lastAppliedVoxelEditSequence = 0;
            _pendingVoxelEditsBySequence.Clear();
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

        private static string SanitizeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var sanitized = value.Trim().Replace(' ', '_');
            return sanitized.Length == 0 ? fallback : sanitized;
        }

        private static bool TryParseVoxelEditAction(string value, out VoxelEditAction action)
        {
            switch (value.ToUpperInvariant())
            {
                case "PLACE":
                    action = VoxelEditAction.Place;
                    return true;

                case "REMOVE":
                    action = VoxelEditAction.Remove;
                    return true;

                default:
                    action = default;
                    return false;
            }
        }
    }
}

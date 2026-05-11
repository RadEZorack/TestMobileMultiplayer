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

        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private bool autoConnectOnStart = false;

        private readonly ConcurrentQueue<string> _incomingMessages = new();
        private readonly Dictionary<int, PlayerSnapshot> _players = new();
        private readonly object _socketLock = new();

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
                Send($"HELLO {SanitizeToken(playerName)}");
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
                if (!IPAddress.TryParse(serverHost, out var address))
                {
                    var addresses = Dns.GetHostAddresses(serverHost);

                    if (addresses.Length == 0)
                    {
                        _status = $"Could not resolve {serverHost}";
                        return;
                    }

                    address = addresses[0];
                }

                _serverEndpoint = new IPEndPoint(address, serverPort);
                _udp = new UdpClient();
                _udp.Connect(_serverEndpoint);
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

                _status = $"Connecting to {_serverEndpoint}";
                _helloTimer = ReconnectHelloInterval;
                Send($"HELLO {SanitizeToken(playerName)}");
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
            var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

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
                "INPUT {0} {1:0.###} {2:0.###}",
                _inputSequence++,
                input.x,
                input.y));
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
                _udp.Send(bytes, bytes.Length);
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
            var panelWidth = Mathf.Min(440f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(12f, 12f, panelWidth, 178f), GUI.skin.box);
            GUILayout.Label("UDP Multiplayer Prototype");
            GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Host", GUILayout.Width(48f));
            serverHost = GUILayout.TextField(serverHost);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(48f));
            var portText = GUILayout.TextField(serverPort.ToString(CultureInfo.InvariantCulture), GUILayout.Width(80f));

            if (int.TryParse(portText, out var parsedPort))
            {
                serverPort = Mathf.Clamp(parsedPort, 1, 65535);
            }

            GUILayout.Label("Name", GUILayout.Width(48f));
            playerName = GUILayout.TextField(playerName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (_udp == null)
            {
                if (GUILayout.Button("Connect", GUILayout.Height(42f)))
                {
                    Connect();
                }
            }
            else if (GUILayout.Button("Disconnect", GUILayout.Height(42f)))
            {
                Disconnect();
            }

            GUILayout.Label($"Players: {_players.Count}");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

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

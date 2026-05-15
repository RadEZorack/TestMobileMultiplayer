using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BasicMultiplayer
{
    public sealed class WorldChatView : MonoBehaviour
    {
        private const int MaxDisplayedMessages = 50;
        private const float ChatButtonWidth = 92f;
        private const float ChatButtonHeight = 48f;
        private const float PanelWidth = 420f;
        private const float PanelHeight = 312f;
        private const float PanelMargin = 12f;
        private static readonly List<WorldChatView> Instances = new();

        [SerializeField] private WebRtcPeerMediaClient realtimeClient;
        [SerializeField] private UdpGameClient udpClient;
        [SerializeField] private bool showChat = true;

        private readonly List<WebRtcPeerMediaClient.ChatMessage> _messages = new();
        private readonly Dictionary<int, string> _playerNames = new();
        private bool _expanded;
        private bool _sentNameForCurrentConnection;
        private string _nameInput = "Player";
        private string _chatInput = string.Empty;
        private string _status = string.Empty;
        private float _nameCooldownUntil;

        private void Awake()
        {
            if (realtimeClient == null)
            {
                realtimeClient = GetComponent<WebRtcPeerMediaClient>();
            }

            if (udpClient == null)
            {
                udpClient = GetComponent<UdpGameClient>();
            }

            _nameInput = DeviceDisplayNameStore.Get();
            udpClient?.SetLocalDisplayName(_nameInput);
        }

        private void OnEnable()
        {
            if (!Instances.Contains(this))
            {
                Instances.Add(this);
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            Unsubscribe();
        }

        private void Update()
        {
            if (realtimeClient == null)
            {
                return;
            }

            if (!realtimeClient.IsSignalingReady)
            {
                _sentNameForCurrentConnection = false;
                return;
            }

            if (!_sentNameForCurrentConnection)
            {
                _sentNameForCurrentConnection = true;
                _ = realtimeClient.SendDisplayNameChangeAsync(_nameInput);
            }
        }

        private void OnGUI()
        {
            if (!showChat)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            var uiScale = GetUiScale();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            if (!_expanded)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.84f);

                if (GUI.Button(GetCollapsedButtonRect(uiScale), "Chat"))
                {
                    _expanded = true;
                }

                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
                return;
            }

            GUI.color = new Color(1f, 1f, 1f, 0.94f);
            DrawChatPanel(uiScale);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        public static bool IsScreenPositionOverAnyChat(Vector2 screenPosition)
        {
            foreach (var instance in Instances)
            {
                if (instance != null && instance.showChat && instance.IsScreenPositionOverChat(screenPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsScreenPositionOverChat(Vector2 screenPosition)
        {
            var uiScale = GetUiScale();
            var guiPosition = new Vector2(screenPosition.x / uiScale, (Screen.height - screenPosition.y) / uiScale);
            var rect = _expanded ? GetPanelRect(uiScale) : GetCollapsedButtonRect(uiScale);
            return rect.Contains(guiPosition);
        }

        private void DrawChatPanel(float uiScale)
        {
            var rect = GetPanelRect(uiScale);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("World Chat", GUILayout.Height(30f));

            if (GUILayout.Button("X", GUILayout.Width(42f), GUILayout.Height(30f)))
            {
                _expanded = false;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(138f));
            var recent = _messages.Skip(Mathf.Max(0, _messages.Count - 6)).ToArray();

            if (recent.Length == 0)
            {
                GUILayout.Label("No messages yet.", GUILayout.Height(22f));
            }
            else
            {
                foreach (var message in recent)
                {
                    GUILayout.Label($"{GetDisplayName(message.playerId, message.displayName)}: {message.text}", GUILayout.Height(22f));
                }
            }

            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(52f), GUILayout.Height(36f));
            _nameInput = GUILayout.TextField(_nameInput, GUILayout.ExpandWidth(true), GUILayout.Height(36f));
            var cooldown = GetNameCooldownSeconds();
            GUI.enabled = cooldown <= 0 && realtimeClient != null && realtimeClient.IsSignalingReady;

            if (GUILayout.Button(cooldown > 0 ? cooldown.ToString() : "Apply", GUILayout.Width(74f), GUILayout.Height(36f)))
            {
                SendNameChange();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("WorldChatInput");
            _chatInput = GUILayout.TextField(_chatInput, GUILayout.ExpandWidth(true), GUILayout.Height(40f));

            if (GUILayout.Button("Send", GUILayout.Width(74f), GUILayout.Height(40f)))
            {
                SendChatMessage();
            }

            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "WorldChatInput")
            {
                SendChatMessage();
                Event.current.Use();
            }

            if (!string.IsNullOrWhiteSpace(_status))
            {
                GUILayout.Label(_status, GUILayout.Height(24f));
            }

            GUILayout.EndArea();
        }

        private void SendNameChange()
        {
            if (realtimeClient == null || !realtimeClient.IsSignalingReady)
            {
                _status = "Chat connecting";
                return;
            }

            _nameInput = DeviceDisplayNameStore.Sanitize(_nameInput);
            _ = realtimeClient.SendDisplayNameChangeAsync(_nameInput);
        }

        private void SendChatMessage()
        {
            var text = SanitizeChatInput(_chatInput);

            if (text.Length == 0 || realtimeClient == null || !realtimeClient.IsSignalingReady)
            {
                return;
            }

            _chatInput = string.Empty;
            _ = realtimeClient.SendChatAsync(text);
        }

        private void Subscribe()
        {
            if (realtimeClient == null)
            {
                return;
            }

            realtimeClient.NameResultReceived += HandleNameResult;
            realtimeClient.PlayerNamesReceived += HandlePlayerNames;
            realtimeClient.ChatHistoryReceived += HandleChatHistory;
            realtimeClient.ChatReceived += HandleChat;
        }

        private void Unsubscribe()
        {
            if (realtimeClient == null)
            {
                return;
            }

            realtimeClient.NameResultReceived -= HandleNameResult;
            realtimeClient.PlayerNamesReceived -= HandlePlayerNames;
            realtimeClient.ChatHistoryReceived -= HandleChatHistory;
            realtimeClient.ChatReceived -= HandleChat;
        }

        private void HandleNameResult(WebRtcPeerMediaClient.NameResultMessage result)
        {
            if (result == null)
            {
                return;
            }

            if (result.ok)
            {
                _nameInput = DeviceDisplayNameStore.Sanitize(result.displayName);
                DeviceDisplayNameStore.Set(_nameInput);
                udpClient?.SetLocalDisplayName(_nameInput);
                _status = $"Name: {_nameInput}";
                _nameCooldownUntil = 0f;
                return;
            }

            _status = result.retryAfterSeconds > 0
                ? $"Name cooldown: {result.retryAfterSeconds}s"
                : "Name change failed";
            _nameCooldownUntil = Time.realtimeSinceStartup + Mathf.Max(0, result.retryAfterSeconds);
        }

        private void HandlePlayerNames(WebRtcPeerMediaClient.PlayerNameMessage[] players)
        {
            _playerNames.Clear();

            if (players == null)
            {
                return;
            }

            foreach (var player in players)
            {
                _playerNames[player.playerId] = DeviceDisplayNameStore.Sanitize(player.displayName);
            }
        }

        private void HandleChatHistory(WebRtcPeerMediaClient.ChatMessage[] messages)
        {
            _messages.Clear();

            if (messages == null)
            {
                return;
            }

            foreach (var message in messages.OrderBy(message => message.sequence))
            {
                AddMessage(message);
            }
        }

        private void HandleChat(WebRtcPeerMediaClient.ChatMessage message)
        {
            AddMessage(message);
        }

        private void AddMessage(WebRtcPeerMediaClient.ChatMessage message)
        {
            if (message == null || _messages.Any(existing => existing.sequence == message.sequence))
            {
                return;
            }

            _messages.Add(message);
            _messages.Sort((left, right) => left.sequence.CompareTo(right.sequence));

            while (_messages.Count > MaxDisplayedMessages)
            {
                _messages.RemoveAt(0);
            }
        }

        private string GetDisplayName(int playerId, string fallback)
        {
            return _playerNames.TryGetValue(playerId, out var displayName)
                ? displayName
                : DeviceDisplayNameStore.Sanitize(fallback);
        }

        private int GetNameCooldownSeconds()
        {
            return Mathf.Max(0, Mathf.CeilToInt(_nameCooldownUntil - Time.realtimeSinceStartup));
        }

        private static string SanitizeChatInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value.Trim().Replace('\n', ' ').Replace('\r', ' ');
            return sanitized.Length > 160 ? sanitized.Substring(0, 160) : sanitized;
        }

        private static Rect GetCollapsedButtonRect(float uiScale)
        {
            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            return new Rect(
                (screenWidth - ChatButtonWidth) * 0.5f,
                Mathf.Max(PanelMargin, topInset + PanelMargin),
                ChatButtonWidth,
                ChatButtonHeight);
        }

        private static Rect GetPanelRect(float uiScale)
        {
            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var width = Mathf.Min(PanelWidth, screenWidth - PanelMargin * 2f);
            return new Rect(
                (screenWidth - width) * 0.5f,
                Mathf.Max(PanelMargin, topInset + PanelMargin),
                width,
                PanelHeight);
        }

        private static float GetUiScale()
        {
#if UNITY_IOS || UNITY_ANDROID
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 430f, 1.35f, 2.1f);
#else
            return 1f;
#endif
        }
    }
}

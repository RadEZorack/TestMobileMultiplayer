using System.Collections.Generic;
using UnityEngine;
using VoxelPlay;

namespace BasicMultiplayer
{
    public sealed class PlayerWorldView : MonoBehaviour
    {
        private const float AvatarCenterHeight = VoxelPlayMultiplayerDemo.PlayerAvatarCenterHeight;

        [SerializeField] private UdpGameClient client;
        [SerializeField] private VoxelPlayMultiplayerDemo voxelPlayDemo;
        [SerializeField] private WebRtcPeerMediaClient realtimeClient;
        [SerializeField] private float climbSpeedBlocksPerSecond = 3.25f;
        [SerializeField] private float fallSpeedBlocksPerSecond = 12f;

        private readonly Dictionary<int, Transform> _avatars = new();
        private readonly Dictionary<int, float> _displayedFootYByPlayer = new();
        private readonly Dictionary<int, string> _displayNamesByPlayerId = new();
        private Material _remoteMaterial;

        private void Awake()
        {
            if (client == null)
            {
                client = GetComponent<UdpGameClient>();
            }

            if (voxelPlayDemo == null)
            {
                voxelPlayDemo = GetComponent<VoxelPlayMultiplayerDemo>();
            }

            if (realtimeClient == null)
            {
                realtimeClient = GetComponent<WebRtcPeerMediaClient>();
            }

            _remoteMaterial = BasicMultiplayerMaterials.Create(new Color(1f, 0.64f, 0.15f));
        }

        private void OnEnable()
        {
            if (realtimeClient != null)
            {
                realtimeClient.PlayerNamesReceived += HandlePlayerNames;
            }
        }

        private void OnDisable()
        {
            if (realtimeClient != null)
            {
                realtimeClient.PlayerNamesReceived -= HandlePlayerNames;
            }
        }

        public bool TryGetAvatarTransform(int playerId, out Transform avatar)
        {
            return _avatars.TryGetValue(playerId, out avatar);
        }

        private void Update()
        {
            if (client == null)
            {
                return;
            }

            var seenIds = new HashSet<int>();

            foreach (var pair in client.Players)
            {
                var id = pair.Key;
                var snapshot = pair.Value;

                if (id == client.LocalPlayerId)
                {
                    continue;
                }

                seenIds.Add(id);

                if (!_avatars.TryGetValue(id, out var avatar))
                {
                    avatar = CreateAvatar(id);
                    _avatars[id] = avatar;
                }

                var targetPosition = GetTargetPosition(id, snapshot.Position);
                avatar.position = Vector3.Lerp(avatar.position, targetPosition, 18f * Time.deltaTime);
                FaceLabelToCamera(avatar);
                SetAvatarVisible(avatar, isVisible: true);

                var renderer = avatar.GetComponentInChildren<Renderer>();

                if (renderer != null && renderer.enabled)
                {
                    renderer.sharedMaterial = _remoteMaterial;
                }
            }

            var staleIds = new List<int>();

            foreach (var id in _avatars.Keys)
            {
                if (!seenIds.Contains(id))
                {
                    staleIds.Add(id);
                }
            }

            foreach (var id in staleIds)
            {
                Destroy(_avatars[id].gameObject);
                _avatars.Remove(id);
                _displayedFootYByPlayer.Remove(id);
            }
        }

        private Transform CreateAvatar(int id)
        {
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.name = $"Player {id}";
            avatar.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(avatar.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = id.ToString();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.22f;
            textMesh.color = Color.white;
            SetAvatarLabel(avatar.transform, id);

            return avatar.transform;
        }

        private void HandlePlayerNames(WebRtcPeerMediaClient.PlayerNameMessage[] players)
        {
            _displayNamesByPlayerId.Clear();

            if (players != null)
            {
                foreach (var player in players)
                {
                    _displayNamesByPlayerId[player.playerId] = DeviceDisplayNameStore.Sanitize(player.displayName);
                }
            }

            foreach (var pair in _avatars)
            {
                SetAvatarLabel(pair.Value, pair.Key);
            }
        }

        private void SetAvatarLabel(Transform avatar, int playerId)
        {
            var label = avatar.Find("Label");

            if (label == null || !label.TryGetComponent<TextMesh>(out var textMesh))
            {
                return;
            }

            textMesh.text = _displayNamesByPlayerId.TryGetValue(playerId, out var displayName)
                ? displayName
                : playerId.ToString();
        }

        private static void SetAvatarVisible(Transform avatar, bool isVisible)
        {
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }
        }

        private Vector3 GetTargetPosition(int playerId, Vector2 serverPosition)
        {
            var worldPosition = voxelPlayDemo != null
                ? voxelPlayDemo.GetPlayerWorldPosition(serverPosition)
                : serverPosition;

            var terrainHeight = 0f;

            if (voxelPlayDemo != null && voxelPlayDemo.TryGetPlayerTargetFootY(playerId, serverPosition, out var targetFootY))
            {
                terrainHeight = targetFootY;
            }
            else
            {
                var environment = VoxelPlayEnvironment.instance;

                if (environment != null && environment.initialized)
                {
                    terrainHeight = environment.GetTerrainHeight(worldPosition.x, worldPosition.y, includeWater: false);
                }
            }

            var displayedFootY = GetDisplayedFootY(playerId, terrainHeight);
            return new Vector3(worldPosition.x, displayedFootY + AvatarCenterHeight, worldPosition.y);
        }

        private float GetDisplayedFootY(int playerId, float targetFootY)
        {
            if (!_displayedFootYByPlayer.TryGetValue(playerId, out var displayedFootY))
            {
                _displayedFootYByPlayer[playerId] = targetFootY;
                return targetFootY;
            }

            var speed = targetFootY > displayedFootY
                ? climbSpeedBlocksPerSecond
                : fallSpeedBlocksPerSecond;
            displayedFootY = Mathf.MoveTowards(displayedFootY, targetFootY, speed * Time.deltaTime);
            _displayedFootYByPlayer[playerId] = displayedFootY;
            return displayedFootY;
        }

        private static void FaceLabelToCamera(Transform avatar)
        {
            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            var label = avatar.Find("Label");

            if (label != null)
            {
                label.rotation = Quaternion.LookRotation(label.position - camera.transform.position, Vector3.up);
            }
        }
    }
}

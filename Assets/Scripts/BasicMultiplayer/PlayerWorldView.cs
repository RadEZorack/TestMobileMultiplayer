using System.Collections.Generic;
using UnityEngine;

namespace BasicMultiplayer
{
    public sealed class PlayerWorldView : MonoBehaviour
    {
        [SerializeField] private UdpGameClient client;

        private readonly Dictionary<int, Transform> _avatars = new();
        private Material _localMaterial;
        private Material _remoteMaterial;

        private void Awake()
        {
            if (client == null)
            {
                client = GetComponent<UdpGameClient>();
            }

            _localMaterial = BasicMultiplayerMaterials.Create(new Color(0.1f, 0.75f, 1f));
            _remoteMaterial = BasicMultiplayerMaterials.Create(new Color(1f, 0.64f, 0.15f));
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
                seenIds.Add(id);

                if (!_avatars.TryGetValue(id, out var avatar))
                {
                    avatar = CreateAvatar(id);
                    _avatars[id] = avatar;
                }

                var targetPosition = new Vector3(snapshot.Position.x, 0.8f, snapshot.Position.y);
                avatar.position = Vector3.Lerp(avatar.position, targetPosition, 18f * Time.deltaTime);

                var renderer = avatar.GetComponentInChildren<Renderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterial = id == client.LocalPlayerId ? _localMaterial : _remoteMaterial;
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

            return avatar.transform;
        }
    }
}

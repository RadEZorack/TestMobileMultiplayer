using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

namespace BasicMultiplayer
{
    public sealed class PeerMediaBillboardView : MonoBehaviour
    {
        [SerializeField] private PlayerWorldView playerWorldView;
        [SerializeField] private WebRtcPeerMediaClient mediaClient;
        [SerializeField] private Vector3 localOffset = new(0f, 1.85f, 0f);
        [SerializeField] private Vector2 billboardSize = new(1.15f, 0.78f);

        private readonly Dictionary<int, MediaPanel> _panelsByPlayerId = new();
        private static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTextureProperty = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private Shader _unlitTextureShader;

        private void Awake()
        {
            if (playerWorldView == null)
            {
                playerWorldView = GetComponent<PlayerWorldView>();
            }

            if (mediaClient == null)
            {
                mediaClient = GetComponent<WebRtcPeerMediaClient>();
            }

            _unlitTextureShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
        }

        private void OnEnable()
        {
            if (mediaClient == null)
            {
                return;
            }

            mediaClient.RemoteVideoReceived += SetRemoteVideo;
            mediaClient.RemoteAudioReceived += SetRemoteAudio;
            mediaClient.RemotePeerClosed += RemoveRemoteMedia;
        }

        private void OnDisable()
        {
            if (mediaClient == null)
            {
                return;
            }

            mediaClient.RemoteVideoReceived -= SetRemoteVideo;
            mediaClient.RemoteAudioReceived -= SetRemoteAudio;
            mediaClient.RemotePeerClosed -= RemoveRemoteMedia;
        }

        private void OnDestroy()
        {
            foreach (var panel in _panelsByPlayerId.Values)
            {
                Destroy(panel.Root);
                Destroy(panel.Material);
            }

            _panelsByPlayerId.Clear();
        }

        private void Update()
        {
            var camera = Camera.main;

            foreach (var pair in _panelsByPlayerId)
            {
                var panel = pair.Value;

                if (playerWorldView != null
                    && playerWorldView.TryGetAvatarTransform(pair.Key, out var avatar))
                {
                    if (panel.Root.transform.parent != avatar)
                    {
                        panel.Root.transform.SetParent(avatar, worldPositionStays: false);
                    }

                    panel.Root.transform.localPosition = localOffset;
                    panel.Root.SetActive(panel.HasVideo);
                }
                else
                {
                    panel.Root.SetActive(false);
                }

                if (camera != null && panel.Root.activeSelf)
                {
                    panel.Root.transform.rotation = Quaternion.LookRotation(
                        panel.Root.transform.position - camera.transform.position,
                        Vector3.up);
                }
            }
        }

        private void SetRemoteVideo(int playerId, Texture texture)
        {
            var panel = GetOrCreatePanel(playerId);
            panel.Renderer.sharedMaterial = panel.Material;
            SetMaterialTexture(panel.Material, texture);
            panel.HasVideo = texture != null;
        }

        private void SetRemoteAudio(int playerId, AudioStreamTrack track)
        {
            var panel = GetOrCreatePanel(playerId);
            panel.AudioSource.SetTrack(track);
            panel.AudioSource.loop = true;
            panel.AudioSource.spatialBlend = 1f;
            panel.AudioSource.minDistance = 1.5f;
            panel.AudioSource.maxDistance = 18f;
            panel.AudioSource.Play();
            panel.HasAudio = true;
        }

        private void RemoveRemoteMedia(int playerId)
        {
            if (!_panelsByPlayerId.TryGetValue(playerId, out var panel))
            {
                return;
            }

            _panelsByPlayerId.Remove(playerId);
            Destroy(panel.Root);
            Destroy(panel.Material);
        }

        private MediaPanel GetOrCreatePanel(int playerId)
        {
            if (_panelsByPlayerId.TryGetValue(playerId, out var panel))
            {
                return panel;
            }

            var root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = $"Player {playerId} AV Billboard";
            root.transform.localScale = new Vector3(billboardSize.x, billboardSize.y, 1f);
            root.SetActive(false);

            var renderer = root.GetComponent<MeshRenderer>();
            var material = new Material(_unlitTextureShader)
            {
                color = Color.white
            };
            SetMaterialColor(material, Color.white);
            renderer.sharedMaterial = material;

            var collider = root.GetComponent<Collider>();

            if (collider != null)
            {
                Destroy(collider);
            }

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            panel = new MediaPanel(root, renderer, material, audioSource);
            _panelsByPlayerId[playerId] = panel;
            return panel;
        }

        private static void SetMaterialTexture(Material material, Texture texture)
        {
            material.mainTexture = texture;

            if (material.HasProperty(BaseMapProperty))
            {
                material.SetTexture(BaseMapProperty, texture);
            }

            if (material.HasProperty(MainTextureProperty))
            {
                material.SetTexture(MainTextureProperty, texture);
            }
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;

            if (material.HasProperty(BaseColorProperty))
            {
                material.SetColor(BaseColorProperty, color);
            }

            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }
        }

        private sealed class MediaPanel
        {
            public MediaPanel(GameObject root, Renderer renderer, Material material, AudioSource audioSource)
            {
                Root = root;
                Renderer = renderer;
                Material = material;
                AudioSource = audioSource;
            }

            public GameObject Root { get; }
            public Renderer Renderer { get; }
            public Material Material { get; }
            public AudioSource AudioSource { get; }
            public bool HasVideo { get; set; }
            public bool HasAudio { get; set; }
        }
    }
}

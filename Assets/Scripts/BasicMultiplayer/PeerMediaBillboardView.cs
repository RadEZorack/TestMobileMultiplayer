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
            mediaClient.RemoteVideoLayoutReceived += SetRemoteVideoLayout;
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
            mediaClient.RemoteVideoLayoutReceived -= SetRemoteVideoLayout;
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
                    panel.Root.transform.localScale = GetPanelScale(panel.VideoRotationDegrees);
                    panel.Renderer.enabled = panel.HasVideo;
                    panel.Root.SetActive(panel.HasVideo || panel.HasAudio);
                }
                else
                {
                    panel.Root.SetActive(false);
                }

                if (camera != null && panel.Root.activeSelf)
                {
                    var billboardRotation = Quaternion.LookRotation(
                        panel.Root.transform.position - camera.transform.position,
                        Vector3.up);
                    panel.Root.transform.rotation = billboardRotation
                        * Quaternion.AngleAxis(-panel.VideoRotationDegrees, Vector3.forward);
                }
            }
        }

        private void SetRemoteVideo(int playerId, Texture texture)
        {
            var panel = GetOrCreatePanel(playerId);

            if (mediaClient != null
                && mediaClient.TryGetRemoteVideoLayout(playerId, out var rotationDegrees, out var mirrored))
            {
                ApplyVideoLayout(panel, rotationDegrees, mirrored);
            }

            panel.Renderer.sharedMaterial = panel.Material;
            SetMaterialTexture(panel.Material, texture);
            panel.HasVideo = texture != null;
            panel.Renderer.enabled = panel.HasVideo;
        }

        private void SetRemoteVideoLayout(int playerId, int rotationDegrees, bool mirrored)
        {
            var panel = GetOrCreatePanel(playerId);
            ApplyVideoLayout(panel, rotationDegrees, mirrored);
        }

        private void SetRemoteAudio(int playerId, AudioStreamTrack track)
        {
            IosWebRtcAudioSession.Configure();

            var panel = GetOrCreatePanel(playerId);
            panel.HasAudio = true;
            panel.Root.SetActive(true);
            panel.AudioSource.SetTrack(track);
            panel.AudioSource.loop = true;
            panel.AudioSource.volume = 1f;
            panel.AudioSource.spatialBlend = 0f;
            panel.AudioSource.dopplerLevel = 0f;
            panel.AudioSource.Play();
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

            var renderer = root.GetComponent<MeshRenderer>();
            renderer.enabled = false;
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

        private Vector3 GetPanelScale(int rotationDegrees)
        {
            return rotationDegrees == 90 || rotationDegrees == 270
                ? new Vector3(billboardSize.y, billboardSize.x, 1f)
                : new Vector3(billboardSize.x, billboardSize.y, 1f);
        }

        private void ApplyVideoLayout(MediaPanel panel, int rotationDegrees, bool mirrored)
        {
            panel.VideoRotationDegrees = NormalizeRotation(rotationDegrees);
            panel.VideoMirrored = mirrored;
            panel.Root.transform.localScale = GetPanelScale(panel.VideoRotationDegrees);
        }

        private static int NormalizeRotation(int degrees)
        {
            var normalized = degrees % 360;

            if (normalized < 0)
            {
                normalized += 360;
            }

            return ((normalized + 45) / 90 * 90) % 360;
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
            public int VideoRotationDegrees { get; set; }
            public bool VideoMirrored { get; set; }
        }
    }
}

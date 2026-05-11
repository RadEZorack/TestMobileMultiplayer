using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelPlay;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BasicMultiplayer
{
    public sealed class VoxelPlayMultiplayerDemo : MonoBehaviour
    {
        private const string HighQualityForestWorldResourcePath = "Worlds/HQForest/HQForest";
        private const string FallbackWorldResourcePath = "BasicMultiplayer/VoxelMultiplayerWorld";
        private const string ForestTrailVoxelResourcePath = "Worlds/HQForest/Voxels/Forest/HQ_VoxelForestTop";
        private const string ForestMarkerVoxelResourcePath = "Worlds/HQForest/Voxels/Forest/HQ_VoxelForestDirt";
        private const float CameraYawDegreesPerSecond = 150f;
        private const float CameraPitchDegreesPerSecond = 92f;
        private const float MinCameraPitch = -38f;
        private const float MaxCameraPitch = 42f;

        private enum CameraZoomMode
        {
            Far,
            Close,
            FirstPerson
        }

        [SerializeField] private UdpGameClient client;
        [SerializeField] private bool useHighQualityForestWorld = true;
        [SerializeField] private bool useForestSavedGame = true;
        [SerializeField] private bool cameraFollowsLocalPlayer = true;
        [SerializeField] private bool showCameraZoomButton = true;
        [SerializeField] private CameraZoomMode cameraZoomMode = CameraZoomMode.Far;
        [SerializeField] private bool paintPlayerTrails = false;
        [SerializeField] private int maxTrailCellsPerPlayer = 48;

        private readonly Dictionary<int, Vector3Int> _lastTrailCells = new();
        private readonly Dictionary<int, Queue<Vector3Int>> _trailCellsByPlayer = new();
        private VoxelPlayEnvironment _environment;
        private VoxelDefinition _trailVoxel;
        private VoxelDefinition _markerVoxel;
        private Vector3 _cameraForward = Vector3.forward;
        private float _cameraYaw;
        private float _cameraPitch = 6f;
        private bool _worldReady;

        public bool IsFirstPersonCamera => cameraZoomMode == CameraZoomMode.FirstPerson;

        private void Awake()
        {
            if (client == null)
            {
                client = GetComponent<UdpGameClient>();
            }
        }

        private void Start()
        {
            DestroyPrimitivePrototypeArena();
            ConfigureForestLighting();
            CreateVoxelPlayEnvironment();
        }

        private void Update()
        {
            if (!_worldReady || client == null)
            {
                return;
            }

            HandleCameraToggleInput();

            if (paintPlayerTrails)
            {
                foreach (var pair in client.Players)
                {
                    PaintTrail(pair.Key, pair.Value.Position);
                }
            }

            if (cameraFollowsLocalPlayer)
            {
                UpdateCameraFollow();
            }
        }

        private void OnGUI()
        {
            if (!showCameraZoomButton || !cameraFollowsLocalPlayer)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var uiScale = GetUiScale();
            var safeArea = Screen.safeArea;
            var rightInset = (Screen.width - safeArea.xMax) / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var size = 72f;
            var left = Mathf.Max(12f, (Screen.width / uiScale) - rightInset - size - 12f);
            var top = Mathf.Max(12f, topInset + 12f);

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            if (GUI.Button(new Rect(left, top, size, size), GetCameraButtonLabel()))
            {
                CycleCameraZoomMode();
            }

            GUI.matrix = previousMatrix;
        }

        private void CreateVoxelPlayEnvironment()
        {
            _environment = VoxelPlayEnvironment.instance;

            if (_environment != null)
            {
                ApplyForestSceneSettings(_environment, canChangeWorld: !_environment.initialized);
                HookInitialized();
                return;
            }

            var environmentObject = new GameObject("Voxel Play Multiplayer Environment");
            environmentObject.SetActive(false);

            _environment = environmentObject.AddComponent<VoxelPlayEnvironment>();
            ApplyForestSceneSettings(_environment, canChangeWorld: true);

            HookInitialized();
            environmentObject.SetActive(true);
        }

        private void ApplyForestSceneSettings(VoxelPlayEnvironment environment, bool canChangeWorld)
        {
            if (canChangeWorld)
            {
                environment.world = LoadWorldDefinition();
            }

            var mobile = Application.isMobilePlatform;

            environment.enableConsole = false;
            environment.enableInventory = false;
            environment.enableDebugWindow = false;
            environment.enableStatusBar = false;
            environment.enableLoadingPanel = false;
            environment.welcomeMessage = string.Empty;
            environment.loadSavedGame = useForestSavedGame && IsUsingHighQualityForest(environment.world);
            environment.saveFilename = "forest1";
            environment.restorePlayerPosition = false;

            environment.visibleChunksDistance = mobile ? 5 : 8;
            environment.visibleChunksVerticalDistance = mobile ? 5 : 8;
            environment.forceChunkDistance = mobile ? 2 : 3;
            environment.maxCPUTimePerFrame = mobile ? 14 : 24;
            environment.enableColliders = true;
            environment.enableNavMesh = false;
            environment.enableTrees = true;
            environment.denseTrees = true;
            environment.enableVegetation = true;
            environment.enableClouds = true;
            environment.globalIllumination = !mobile;
            environment.ambientLight = 0.15f;
            environment.diffuseWrap = 0.5f;
            environment.daylightShadowAtten = 0.15f;
            environment.enableSmoothLighting = true;
            environment.enableNormalMap = !mobile;
            environment.enablePBR = !mobile;
            environment.enableReliefMapping = !mobile;
            environment.reliefStrength = 0.05f;
            environment.reliefIterations = mobile ? 6 : 10;
            environment.reliefIterationsBinarySearch = mobile ? 4 : 6;
            environment.reliefMaxDistance = mobile ? 16f : 25f;
            environment.enableFogSkyBlending = true;
            environment.textureSize = mobile ? 256 : 512;
            environment.hqFiltering = true;
            environment.mipMapBias = 0.5f;
            environment.filterMode = FilterMode.Trilinear;
            environment.reflectionProbeUsage = mobile ? ReflectionProbeUsage.Off : ReflectionProbeUsage.BlendProbes;
            environment.enableTinting = false;
            environment.enableShadows = true;
            environment.realisticWater = !mobile;
            environment.multiThreadGeneration = true;
            environment.onlyRenderInFrustum = true;
            environment.hideChunksInHierarchy = true;
            environment.distanceAnchor = Camera.main != null ? Camera.main.transform : null;
            environment.sun = FindOrCreateSun();
            environment.fogAmount = 0.5f;
            environment.fogDistanceAuto = true;
            environment.fogDistance = 200f;
            environment.fogFallOff = 0.8f;
            environment.fogTint = Color.white;
        }

        private void HookInitialized()
        {
            if (_environment == null)
            {
                return;
            }

            if (_environment.initialized)
            {
                OnVoxelPlayInitialized();
            }
            else
            {
                _environment.OnInitialized += OnVoxelPlayInitialized;
            }
        }

        private void OnVoxelPlayInitialized()
        {
            if (_environment == null)
            {
                return;
            }

            _environment.OnInitialized -= OnVoxelPlayInitialized;
            _trailVoxel = Resources.Load<VoxelDefinition>(ForestTrailVoxelResourcePath) ?? _environment.defaultVoxel;
            _markerVoxel = Resources.Load<VoxelDefinition>(ForestMarkerVoxelResourcePath) ?? _trailVoxel;
            _worldReady = _trailVoxel != null;

            if (_worldReady)
            {
                ConfigureCameraForVoxelWorld(_environment);
                BuildSharedVoxelDemo();
            }
            else
            {
                Debug.LogWarning("Voxel Play initialized, but no default voxel is available for the multiplayer demo.");
            }
        }

        private void BuildSharedVoxelDemo()
        {
            var markerColor = new Color(0.42f, 0.36f, 0.26f);
            var accentColor = new Color(0.95f, 0.7f, 0.18f);

            for (var x = -9; x <= 9; x += 3)
            {
                PlaceSurfaceVoxel(x, -5, markerColor);
                PlaceSurfaceVoxel(x, 5, markerColor);
            }

            for (var z = -3; z <= 3; z += 3)
            {
                PlaceSurfaceVoxel(-9, z, markerColor);
                PlaceSurfaceVoxel(9, z, markerColor);
            }

            CreateBeacon(-7, -4, accentColor);
            CreateBeacon(7, -4, accentColor);
            CreateBeacon(-7, 4, accentColor);
            CreateBeacon(7, 4, accentColor);
        }

        private void CreateBeacon(int x, int z, Color color)
        {
            var surface = GetSurfaceCell(x, z);

            for (var height = 0; height < 4; height++)
            {
                PlaceVoxel(new Vector3(surface.x, surface.y + height, surface.z), _markerVoxel, color);
            }
        }

        private void PaintTrail(int playerId, Vector2 position)
        {
            var cell = GetSurfaceCell(position);

            if (_lastTrailCells.TryGetValue(playerId, out var lastCell) && lastCell == cell)
            {
                return;
            }

            _lastTrailCells[playerId] = cell;
            var color = GetPlayerTrailColor(playerId);
            PlaceVoxel(cell, _trailVoxel, color);

            if (!_trailCellsByPlayer.TryGetValue(playerId, out var trailCells))
            {
                trailCells = new Queue<Vector3Int>();
                _trailCellsByPlayer[playerId] = trailCells;
            }

            trailCells.Enqueue(cell);

            while (trailCells.Count > maxTrailCellsPerPlayer)
            {
                var oldCell = trailCells.Dequeue();
                ClearVoxel(oldCell);
            }
        }

        private void PlaceSurfaceVoxel(int x, int z, Color color)
        {
            PlaceVoxel(GetSurfaceCell(x, z), _markerVoxel, color);
        }

        private void PlaceVoxel(Vector3 position, VoxelDefinition voxel, Color color)
        {
            if (_environment == null || voxel == null)
            {
                return;
            }

            _environment.VoxelPlace(position, voxel, color, playSound: false);
        }

        private void ClearVoxel(Vector3 position)
        {
            if (_environment == null)
            {
                return;
            }

            _environment.VoxelDestroy(position);
        }

        private Vector3Int GetSurfaceCell(Vector2 position)
        {
            return GetSurfaceCell(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        }

        private Vector3Int GetSurfaceCell(int x, int z)
        {
            var y = 1;

            if (_environment != null && _environment.initialized)
            {
                y = Mathf.RoundToInt(_environment.GetTerrainHeight(x, z, includeWater: false));
            }

            return new Vector3Int(x, y, z);
        }

        private void UpdateCameraFollow()
        {
            if (client.LocalPlayerId == 0 || !client.Players.TryGetValue(client.LocalPlayerId, out var localPlayer))
            {
                return;
            }

            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            UpdateCameraLook();

            var groundY = GetSurfaceCell(localPlayer.Position).y;
            var focus = new Vector3(localPlayer.Position.x, groundY + 1.35f, localPlayer.Position.y);
            var forward = _cameraForward.sqrMagnitude > 0.001f ? _cameraForward.normalized : Vector3.forward;
            var lookTarget = focus + Vector3.up * Mathf.Clamp(-_cameraPitch * 0.075f, -2f, 2f);
            var positionSpeed = 4f;
            var rotationSpeed = 6f;
            var targetFov = 58f;
            Vector3 desiredPosition;
            Quaternion desiredRotation;

            switch (cameraZoomMode)
            {
                case CameraZoomMode.Close:
                    desiredPosition = focus - forward * 3.75f + Vector3.up * 2.35f;
                    desiredRotation = Quaternion.LookRotation((lookTarget + Vector3.up * 0.25f) - desiredPosition, Vector3.up);
                    targetFov = 62f;
                    positionSpeed = 7f;
                    rotationSpeed = 9f;
                    break;

                case CameraZoomMode.FirstPerson:
                    desiredPosition = new Vector3(localPlayer.Position.x, groundY + 1.65f, localPlayer.Position.y) + forward * 0.18f;
                    desiredRotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                    targetFov = 68f;
                    positionSpeed = 14f;
                    rotationSpeed = 14f;
                    break;

                default:
                    desiredPosition = focus - forward * 13f + Vector3.up * 13f;
                    desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
                    break;
            }

            camera.transform.position = Vector3.Lerp(camera.transform.position, desiredPosition, positionSpeed * Time.deltaTime);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFov, 8f * Time.deltaTime);
        }

        private void UpdateCameraLook()
        {
            if (client == null)
            {
                return;
            }

            var look = client.LookInput;

            if (look.sqrMagnitude > 0.001f)
            {
                _cameraYaw = Mathf.Repeat(_cameraYaw + look.x * CameraYawDegreesPerSecond * Time.deltaTime, 360f);
                _cameraPitch = Mathf.Clamp(
                    _cameraPitch - look.y * CameraPitchDegreesPerSecond * Time.deltaTime,
                    MinCameraPitch,
                    MaxCameraPitch);
            }

            _cameraForward = Quaternion.Euler(0f, _cameraYaw, 0f) * Vector3.forward;
            client.MovementYawDegrees = _cameraYaw;
        }

        private void HandleCameraToggleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                CycleCameraZoomMode();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.C))
            {
                CycleCameraZoomMode();
            }
#endif
        }

        private void CycleCameraZoomMode()
        {
            cameraZoomMode = cameraZoomMode switch
            {
                CameraZoomMode.Far => CameraZoomMode.Close,
                CameraZoomMode.Close => CameraZoomMode.FirstPerson,
                _ => CameraZoomMode.Far
            };
        }

        private string GetCameraButtonLabel()
        {
            return cameraZoomMode switch
            {
                CameraZoomMode.Close => "CAM\n3M",
                CameraZoomMode.FirstPerson => "CAM\n1P",
                _ => "CAM\nFAR"
            };
        }

        private static Color GetPlayerTrailColor(int playerId)
        {
            var hue = Mathf.Repeat(playerId * 0.173f, 1f);
            return Color.HSVToRGB(hue, 0.72f, 1f);
        }

        private static void DestroyPrimitivePrototypeArena()
        {
            DestroyByName("Basic Multiplayer Arena");
            DestroyByName("Top Wall");
            DestroyByName("Bottom Wall");
            DestroyByName("Left Wall");
            DestroyByName("Right Wall");
        }

        private static void DestroyByName(string objectName)
        {
            var gameObject = GameObject.Find(objectName);

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private static void ConfigureForestLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);
            RenderSettings.ambientEquatorColor = new Color(0.114f, 0.125f, 0.133f);
            RenderSettings.ambientGroundColor = new Color(0.047f, 0.043f, 0.035f);
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f);
            RenderSettings.fog = false;
            FindOrCreateSun();
        }

        private static void ConfigureCameraForVoxelWorld(VoxelPlayEnvironment environment)
        {
            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            var terrainHeight = 0f;

            if (environment != null && environment.initialized)
            {
                terrainHeight = environment.GetTerrainHeight(0f, 0f, includeWater: false);
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, terrainHeight + 14f, -14f),
                Quaternion.Euler(56f, 0f, 0f));
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 240f;
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        private WorldDefinition LoadWorldDefinition()
        {
            WorldDefinition world = null;

            if (useHighQualityForestWorld)
            {
                world = Resources.Load<WorldDefinition>(HighQualityForestWorldResourcePath);
            }

            if (world == null)
            {
                world = Resources.Load<WorldDefinition>(FallbackWorldResourcePath);
            }

            return world;
        }

        private static bool IsUsingHighQualityForest(WorldDefinition world)
        {
            return world != null && world.name == "HQForest";
        }

        private static Light FindOrCreateSun()
        {
            Light sun = null;

            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                var sunObject = new GameObject("Sun");
                sun = sunObject.AddComponent<Light>();
            }

            sun.name = "Sun";
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            sun.color = Color.white;
            sun.shadows = LightShadows.Soft;
            sun.transform.SetPositionAndRotation(
                new Vector3(0f, 3f, 0f),
                Quaternion.Euler(60f, 0.085f, -18.493f));
            return sun;
        }

        private static float GetUiScale()
        {
#if UNITY_IOS || UNITY_ANDROID
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 430f, 1.4f, 2.2f);
#else
            return 1f;
#endif
        }
    }
}

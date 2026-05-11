using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelPlay;

namespace BasicMultiplayer
{
    public sealed class VoxelPlayMultiplayerDemo : MonoBehaviour
    {
        private const string HighQualityForestWorldResourcePath = "Worlds/HQForest/HQForest";
        private const string FallbackWorldResourcePath = "BasicMultiplayer/VoxelMultiplayerWorld";
        private const string ForestTrailVoxelResourcePath = "Worlds/HQForest/Voxels/Forest/HQ_VoxelForestTop";
        private const string ForestMarkerVoxelResourcePath = "Worlds/HQForest/Voxels/Forest/HQ_VoxelForestDirt";

        [SerializeField] private UdpGameClient client;
        [SerializeField] private bool useHighQualityForestWorld = true;
        [SerializeField] private bool useForestSavedGame = true;
        [SerializeField] private bool cameraFollowsLocalPlayer = true;
        [SerializeField] private bool paintPlayerTrails = true;
        [SerializeField] private int maxTrailCellsPerPlayer = 48;

        private readonly Dictionary<int, Vector3Int> _lastTrailCells = new();
        private readonly Dictionary<int, Queue<Vector3Int>> _trailCellsByPlayer = new();
        private VoxelPlayEnvironment _environment;
        private VoxelDefinition _trailVoxel;
        private VoxelDefinition _markerVoxel;
        private bool _worldReady;

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

            var groundY = GetSurfaceCell(localPlayer.Position).y;
            var focus = new Vector3(localPlayer.Position.x, groundY + 1.3f, localPlayer.Position.y);
            var desiredPosition = focus + new Vector3(0f, 13f, -13f);
            camera.transform.position = Vector3.Lerp(camera.transform.position, desiredPosition, 4f * Time.deltaTime);
            camera.transform.rotation = Quaternion.Slerp(
                camera.transform.rotation,
                Quaternion.LookRotation(focus - camera.transform.position, Vector3.up),
                6f * Time.deltaTime);
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
    }
}

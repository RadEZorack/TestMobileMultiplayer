using System.Collections.Generic;
using UnityEngine;
using VoxelPlay;

namespace BasicMultiplayer
{
    public sealed class VoxelPlayMultiplayerDemo : MonoBehaviour
    {
        private const string WorldResourcePath = "BasicMultiplayer/VoxelMultiplayerWorld";
        private const float TrailVoxelY = 0.5f;
        private const float PillarBaseY = 0.5f;

        [SerializeField] private UdpGameClient client;
        [SerializeField] private bool paintPlayerTrails = true;
        [SerializeField] private int maxTrailCellsPerPlayer = 48;

        private readonly Dictionary<int, Vector2Int> _lastTrailCells = new();
        private readonly Dictionary<int, Queue<Vector2Int>> _trailCellsByPlayer = new();
        private VoxelPlayEnvironment _environment;
        private VoxelDefinition _trailVoxel;
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
            ConfigureCameraForVoxelWorld();
            CreateVoxelPlayEnvironment();
        }

        private void Update()
        {
            if (!_worldReady || client == null || !paintPlayerTrails)
            {
                return;
            }

            foreach (var pair in client.Players)
            {
                PaintTrail(pair.Key, pair.Value.Position);
            }
        }

        private void CreateVoxelPlayEnvironment()
        {
            _environment = VoxelPlayEnvironment.instance;

            if (_environment != null)
            {
                HookInitialized();
                return;
            }

            var environmentObject = new GameObject("Voxel Play Multiplayer Environment");
            environmentObject.SetActive(false);

            _environment = environmentObject.AddComponent<VoxelPlayEnvironment>();
            _environment.world = Resources.Load<WorldDefinition>(WorldResourcePath);
            _environment.enableConsole = false;
            _environment.enableInventory = false;
            _environment.enableDebugWindow = false;
            _environment.enableStatusBar = false;
            _environment.enableLoadingPanel = false;
            _environment.welcomeMessage = string.Empty;
            _environment.visibleChunksDistance = 5;
            _environment.forceChunkDistance = 2;
            _environment.enableColliders = true;
            _environment.enableNavMesh = false;
            _environment.enableTrees = false;
            _environment.enableVegetation = false;
            _environment.enableClouds = false;
            _environment.globalIllumination = false;
            _environment.enableSmoothLighting = false;
            _environment.enableTinting = true;
            _environment.maxCPUTimePerFrame = 12;
            _environment.distanceAnchor = Camera.main != null ? Camera.main.transform : null;

            var sun = Object.FindAnyObjectByType<Light>();

            if (sun != null)
            {
                _environment.sun = sun;
            }

            HookInitialized();
            environmentObject.SetActive(true);
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
            _trailVoxel = _environment.defaultVoxel;
            _worldReady = _trailVoxel != null;

            if (_worldReady)
            {
                BuildSharedVoxelDemo();
            }
            else
            {
                Debug.LogWarning("Voxel Play initialized, but no default voxel is available for the multiplayer demo.");
            }
        }

        private void BuildSharedVoxelDemo()
        {
            var wallColor = new Color(0.45f, 0.55f, 0.65f);
            var accentColor = new Color(0.95f, 0.74f, 0.24f);

            for (var x = -9; x <= 9; x++)
            {
                PlaceVoxel(new Vector3(x, PillarBaseY, -5), wallColor);
                PlaceVoxel(new Vector3(x, PillarBaseY, 5), wallColor);
            }

            for (var z = -4; z <= 4; z++)
            {
                PlaceVoxel(new Vector3(-9, PillarBaseY, z), wallColor);
                PlaceVoxel(new Vector3(9, PillarBaseY, z), wallColor);
            }

            for (var height = 0; height < 4; height++)
            {
                PlaceVoxel(new Vector3(-4, PillarBaseY + height, 0), accentColor);
                PlaceVoxel(new Vector3(4, PillarBaseY + height, 0), accentColor);
            }
        }

        private void PaintTrail(int playerId, Vector2 position)
        {
            var cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));

            if (_lastTrailCells.TryGetValue(playerId, out var lastCell) && lastCell == cell)
            {
                return;
            }

            _lastTrailCells[playerId] = cell;
            var color = GetPlayerTrailColor(playerId);
            PlaceVoxel(new Vector3(cell.x, TrailVoxelY, cell.y), color);

            if (!_trailCellsByPlayer.TryGetValue(playerId, out var trailCells))
            {
                trailCells = new Queue<Vector2Int>();
                _trailCellsByPlayer[playerId] = trailCells;
            }

            trailCells.Enqueue(cell);

            while (trailCells.Count > maxTrailCellsPerPlayer)
            {
                var oldCell = trailCells.Dequeue();
                ClearVoxel(new Vector3(oldCell.x, TrailVoxelY, oldCell.y));
            }
        }

        private void PlaceVoxel(Vector3 position, Color color)
        {
            if (_environment == null || _trailVoxel == null)
            {
                return;
            }

            _environment.VoxelPlace(position, _trailVoxel, color, playSound: false);
        }

        private void ClearVoxel(Vector3 position)
        {
            if (_environment == null)
            {
                return;
            }

            _environment.VoxelDestroy(position);
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

        private static void ConfigureCameraForVoxelWorld()
        {
            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 15f, -12f),
                Quaternion.Euler(58f, 0f, 0f));
            camera.fieldOfView = 54f;
            camera.farClipPlane = 180f;
        }
    }
}

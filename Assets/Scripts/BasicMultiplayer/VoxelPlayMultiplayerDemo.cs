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
        private const float MinCameraPitch = -88f;
        private const float MaxCameraPitch = 88f;
        private const float BlockActionButtonSize = 64f;
        private const float BlockActionButtonGap = 10f;
        private const float JoystickClickScreenMaxY = 0.58f;
        private const float LeftJoystickClickScreenMaxX = 0.48f;
        private const float RightJoystickClickScreenMinX = 0.52f;
        private const string MarkerVoxelEditType = "marker";
        private const int PlayerClearanceBlocks = 2;
        private const float EstimatedPlayerWalkSpeed = 4.5f;
        public const float PlayerAvatarCenterHeight = 0.75f;
        private const float PlayerCameraFocusHeight = 1.35f;
        private const float PlayerCameraHeadHeight = 1.65f;

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
        [SerializeField] private bool showBlockActionButtons = true;
        [SerializeField] private bool showCenterCrosshair = true;
        [SerializeField] private float blockInteractionDistance = 24f;
        [SerializeField] private Color blockHighlightColor = new Color(1f, 0.92f, 0.02f, 1f);
        [Range(1f, 100f)]
        [SerializeField] private float blockHighlightEdge = 20f;
        [SerializeField] private CameraZoomMode cameraZoomMode = CameraZoomMode.Far;
        [SerializeField] private bool paintPlayerTrails = false;
        [SerializeField] private int maxTrailCellsPerPlayer = 48;
        [SerializeField] private int maxPlayerClimbScanBlocks = 512;
        [SerializeField] private float playerClimbSpeedBlocksPerSecond = 3.25f;
        [SerializeField] private float playerFallSpeedBlocksPerSecond = 12f;
        [SerializeField] private float movementCollisionProbeDistance = 0.35f;
        [SerializeField] private float playerCollisionRadius = 0.38f;
        [SerializeField] private float playerSupportProbeRadiusScale = 0.92f;
        [SerializeField] private float playerFootProbeHeight = 0.35f;
        [SerializeField] private float playerHeadProbeHeight = 1.55f;

        private readonly Dictionary<int, Vector3Int> _lastTrailCells = new();
        private readonly Dictionary<int, Queue<Vector3Int>> _trailCellsByPlayer = new();
        private readonly Queue<VoxelEditMessage> _pendingVoxelEdits = new();
        private static readonly Vector2 PlayerVoxelCenterOffset = new Vector2(0.5f, 0.5f);
        private VoxelPlayEnvironment _environment;
        private VoxelDefinition _trailVoxel;
        private VoxelDefinition _markerVoxel;
        private Vector3 _cameraForward = Vector3.forward;
        private float _cameraYaw;
        private float _cameraPitch = 22f;
        private bool _worldReady;
        private bool _hasTargetHit;
        private bool _highlightActive;
        private bool _hasSmoothedLocalGroundY;
        private bool _hasLocalClimbTarget;
        private float _smoothedLocalGroundY;
        private float _localClimbTargetFootY;
        private int _smoothedLocalGroundFrame = -1;
        private Transform _localPlayerRig;
        private VoxelHitInfo _targetHitInfo;

        public bool IsFirstPersonCamera => cameraZoomMode == CameraZoomMode.FirstPerson;

        private void Awake()
        {
            if (client == null)
            {
                client = GetComponent<UdpGameClient>();
            }

            if (client != null)
            {
                client.VoxelEditReceived += HandleVoxelEditReceived;
                client.MoveInputFilter = FilterMoveInput;
            }
        }

        private void Start()
        {
            DestroyPrimitivePrototypeArena();
            ConfigureForestLighting();
            CreateVoxelPlayEnvironment();
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.VoxelEditReceived -= HandleVoxelEditReceived;
                client.MoveInputFilter = null;
            }

            if (_environment != null)
            {
                _environment.OnInitialized -= OnVoxelPlayInitialized;
            }

            if (_localPlayerRig != null)
            {
                _localPlayerRig.DetachChildren();
                Destroy(_localPlayerRig.gameObject);
                _localPlayerRig = null;
            }
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

            UpdateBlockTarget();
            HandleBlockMouseInput();
        }

        private void OnGUI()
        {
            if ((!showCameraZoomButton || !cameraFollowsLocalPlayer) && !showBlockActionButtons && !showCenterCrosshair)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            var uiScale = GetUiScale();

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            if (showCameraZoomButton && cameraFollowsLocalPlayer)
            {
                if (GUI.Button(GetCameraButtonRect(uiScale), GetCameraButtonLabel()))
                {
                    CycleCameraZoomMode();
                }
            }

            if (showBlockActionButtons)
            {
                GetBlockActionButtonRects(uiScale, reserveCameraButton: showCameraZoomButton && cameraFollowsLocalPlayer, out var leftButton, out var rightButton);

                if (GUI.Button(leftButton, "L"))
                {
                    RemoveTargetBlock();
                }

                if (GUI.Button(rightButton, "R"))
                {
                    PlaceTargetBlock();
                }
            }

            if (showCenterCrosshair)
            {
                DrawCenterCrosshair(uiScale);
            }

            GUI.color = previousColor;
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
                ApplyPendingVoxelEdits();
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
            var cell = GetSurfaceCell(GetPlayerWorldPosition(position));

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

        private void UpdateBlockTarget()
        {
            if (_environment == null || !_environment.initialized)
            {
                _hasTargetHit = false;
                _highlightActive = false;
                return;
            }

            var camera = Camera.main;

            if (camera == null)
            {
                ClearBlockHighlight();
                return;
            }

            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var hasHit = _environment.RayCast(
                ray.origin,
                ray.direction,
                out var hitInfo,
                blockInteractionDistance,
                minOpaque: 1,
                colliderTypes: ColliderTypes.OnlyVoxels,
                createChunksIfNeeded: false,
                microVoxels: false,
                ignoreWater: IgnoreWaterOption.IgnoreWater);

            if (hasHit)
            {
                if (!_highlightActive || hitInfo.voxelCenter != _targetHitInfo.voxelCenter || hitInfo.normal != _targetHitInfo.normal)
                {
                    _environment.RefreshVoxelHighlight();
                }

                _hasTargetHit = true;
                _targetHitInfo = hitInfo;
                _highlightActive = _environment.VoxelHighlight(ref _targetHitInfo, blockHighlightColor, blockHighlightEdge, fadeAmplitude: 0.35f);
            }
            else
            {
                ClearBlockHighlight();
            }
        }

        private void HandleBlockMouseInput()
        {
            if (!_hasTargetHit)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;

            if (mouse != null)
            {
                var position = mouse.position.ReadValue();

                if (mouse.leftButton.wasPressedThisFrame && IsWorldBlockClick(position, uiScale: GetUiScale()))
                {
                    RemoveTargetBlock();
                }

                if (mouse.rightButton.wasPressedThisFrame && IsWorldBlockClick(position, uiScale: GetUiScale()))
                {
                    PlaceTargetBlock();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            var position = (Vector2)Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && IsWorldBlockClick(position, uiScale: GetUiScale()))
            {
                RemoveTargetBlock();
            }

            if (Input.GetMouseButtonDown(1) && IsWorldBlockClick(position, uiScale: GetUiScale()))
            {
                PlaceTargetBlock();
            }
#endif
        }

        private void RemoveTargetBlock()
        {
            if (_environment == null || !_hasTargetHit)
            {
                return;
            }

            var cell = ToVoxelCell(_targetHitInfo.voxelCenter);

            if (_environment.VoxelDestroy(_targetHitInfo.voxelCenter))
            {
                client.SendVoxelEdit(VoxelEditAction.Remove, cell, MarkerVoxelEditType);
                ClearBlockHighlight();
            }
        }

        private void ClearBlockHighlight()
        {
            _hasTargetHit = false;
            _highlightActive = false;

            if (_environment != null)
            {
                _environment.ClearHighlight();
            }
        }

        private void PlaceTargetBlock()
        {
            if (_environment == null || !_hasTargetHit)
            {
                return;
            }

            var voxel = GetBuildVoxel();

            if (voxel == null)
            {
                return;
            }

            var placePosition = _targetHitInfo.voxelCenter + _targetHitInfo.normal;
            var placeCell = ToVoxelCell(placePosition);

            if (_environment.IsVoxelAtPosition(placePosition) || !CanPlaceBlockWithPlayerClearance(placeCell))
            {
                return;
            }

            if (_environment.VoxelPlace(placePosition, voxel, false, default(Color), 1f))
            {
                client.SendVoxelEdit(VoxelEditAction.Place, placeCell, MarkerVoxelEditType);
                UpdateBlockTarget();
            }
        }

        private void HandleVoxelEditReceived(VoxelEditMessage edit)
        {
            if (!_worldReady || _environment == null || !_environment.initialized)
            {
                _pendingVoxelEdits.Enqueue(edit);
                return;
            }

            ApplyVoxelEdit(edit);
        }

        private void ApplyPendingVoxelEdits()
        {
            while (_pendingVoxelEdits.Count > 0)
            {
                ApplyVoxelEdit(_pendingVoxelEdits.Dequeue());
            }
        }

        private void ApplyVoxelEdit(VoxelEditMessage edit)
        {
            if (_environment == null || !_environment.initialized)
            {
                return;
            }

            var position = ToVoxelPosition(edit.Cell);

            if (edit.Action == VoxelEditAction.Remove)
            {
                _environment.VoxelDestroy(position);
                return;
            }

            if (!_environment.IsVoxelAtPosition(position))
            {
                _environment.VoxelPlace(position, GetBuildVoxel(edit.VoxelType));
            }
        }

        private VoxelDefinition GetBuildVoxel()
        {
            return GetBuildVoxel(MarkerVoxelEditType);
        }

        private VoxelDefinition GetBuildVoxel(string voxelType)
        {
            if (voxelType == "trail")
            {
                return _trailVoxel != null ? _trailVoxel : _markerVoxel != null ? _markerVoxel : _environment.defaultVoxel;
            }

            return _markerVoxel != null ? _markerVoxel : _trailVoxel != null ? _trailVoxel : _environment.defaultVoxel;
        }

        private Vector3Int GetSurfaceCell(Vector2 position)
        {
            return GetSurfaceCell(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        private Vector3Int GetSurfaceCell(int x, int z)
        {
            var y = 1;

            if (_environment != null && _environment.initialized)
            {
                y = Mathf.RoundToInt(_environment.GetTerrainHeight(x + 0.5f, z + 0.5f, includeWater: false));
            }

            return new Vector3Int(x, y, z);
        }

        public bool TryGetPlayerFootCell(Vector2 serverPosition, out Vector3Int footCell)
        {
            return TryGetPlayerFootCellAtWorldPosition(GetPlayerWorldPosition(serverPosition), out footCell);
        }

        private bool TryGetPlayerFootCellAtWorldPosition(Vector2 worldPosition, out Vector3Int footCell)
        {
            footCell = default;

            if (_environment == null || !_environment.initialized)
            {
                return false;
            }

            var found = false;
            var bestY = int.MinValue;

            for (var index = 0; index < 9; index++)
            {
                var samplePosition = GetSupportProbePosition(worldPosition, index);

                if (!TryResolvePlayerFootCellAtPoint(samplePosition, out var sampleFootCell)
                    || (found && sampleFootCell.y <= bestY))
                {
                    continue;
                }

                footCell = sampleFootCell;
                bestY = sampleFootCell.y;
                found = true;
            }

            return found;
        }

        private bool TryResolvePlayerFootCellAtPoint(Vector2 worldPosition, out Vector3Int footCell)
        {
            var x = Mathf.FloorToInt(worldPosition.x);
            var z = Mathf.FloorToInt(worldPosition.y);
            var baseY = Mathf.FloorToInt(_environment.GetTerrainHeight(worldPosition.x, worldPosition.y, includeWater: false));
            return TryResolvePlayerFootCell(new Vector3Int(x, baseY, z), out footCell);
        }

        private Vector2 GetSupportProbePosition(Vector2 worldPosition, int index)
        {
            const float Diagonal = 0.70710678f;
            var radius = Mathf.Max(0f, playerCollisionRadius * Mathf.Clamp01(playerSupportProbeRadiusScale));

            return index switch
            {
                1 => worldPosition + new Vector2(radius, 0f),
                2 => worldPosition + new Vector2(-radius, 0f),
                3 => worldPosition + new Vector2(0f, radius),
                4 => worldPosition + new Vector2(0f, -radius),
                5 => worldPosition + new Vector2(radius * Diagonal, radius * Diagonal),
                6 => worldPosition + new Vector2(radius * Diagonal, -radius * Diagonal),
                7 => worldPosition + new Vector2(-radius * Diagonal, radius * Diagonal),
                8 => worldPosition + new Vector2(-radius * Diagonal, -radius * Diagonal),
                _ => worldPosition
            };
        }

        public bool TryGetPlayerTargetFootY(int playerId, Vector2 serverPosition, out float footY)
        {
            if (!TryGetPlayerFootCell(serverPosition, out var footCell))
            {
                footY = default;
                return false;
            }

            footY = footCell.y;

            if (client != null && playerId == client.LocalPlayerId && _hasLocalClimbTarget)
            {
                footY = Mathf.Max(footY, _localClimbTargetFootY);
            }

            return true;
        }

        public Vector2 GetPlayerWorldPosition(Vector2 serverPosition)
        {
            return serverPosition + PlayerVoxelCenterOffset;
        }

        public bool TryGetLocalPlayerRig(int playerId, Vector2 serverPosition, out Transform rig)
        {
            rig = null;

            if (!_worldReady || client == null || playerId != client.LocalPlayerId)
            {
                return false;
            }

            rig = UpdateLocalPlayerRig(serverPosition);
            return rig != null;
        }

        public bool TryGetDisplayedLocalFootY(int playerId, Vector2 serverPosition, out float footY)
        {
            if (client == null || playerId != client.LocalPlayerId)
            {
                footY = default;
                return false;
            }

            if (_localPlayerRig != null)
            {
                footY = _localPlayerRig.position.y;
                return true;
            }

            if (_hasSmoothedLocalGroundY)
            {
                footY = _smoothedLocalGroundY;
                return true;
            }

            return TryGetPlayerTargetFootY(playerId, serverPosition, out footY);
        }

        private bool TryResolvePlayerFootCell(Vector3Int baseFootCell, out Vector3Int resolvedFootCell)
        {
            var maxScan = Mathf.Max(PlayerClearanceBlocks, maxPlayerClimbScanBlocks);

            for (var offset = 0; offset <= maxScan; offset++)
            {
                var candidate = new Vector3Int(baseFootCell.x, baseFootCell.y + offset, baseFootCell.z);

                if (HasPlayerClearance(candidate))
                {
                    resolvedFootCell = candidate;
                    return true;
                }
            }

            resolvedFootCell = baseFootCell;
            return false;
        }

        private bool HasPlayerClearance(Vector3Int footCell)
        {
            for (var height = 0; height < PlayerClearanceBlocks; height++)
            {
                if (IsVoxelCellOccupied(new Vector3Int(footCell.x, footCell.y + height, footCell.z)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanPlaceBlockWithPlayerClearance(Vector3Int placeCell)
        {
            if (client == null)
            {
                return true;
            }

            if (client.TryGetLocalPosition(out var localPosition)
                && !CanPlaceBlockWithPlayerClearanceForPosition(placeCell, localPosition))
            {
                return false;
            }

            foreach (var pair in client.Players)
            {
                if (!CanPlaceBlockWithPlayerClearanceForPosition(placeCell, pair.Value.Position))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanPlaceBlockWithPlayerClearanceForPosition(Vector3Int placeCell, Vector2 playerPosition)
        {
            if (!TryGetPlayerFootCell(playerPosition, out var playerFootCell)
                || placeCell.x != playerFootCell.x
                || placeCell.z != playerFootCell.z
                || placeCell.y < playerFootCell.y
                || placeCell.y >= playerFootCell.y + PlayerClearanceBlocks)
            {
                return true;
            }

            var movedFootCell = new Vector3Int(playerFootCell.x, placeCell.y + 1, playerFootCell.z);
            return HasPlayerClearanceAfterPlacement(movedFootCell, placeCell);
        }

        private bool HasPlayerClearanceAfterPlacement(Vector3Int footCell, Vector3Int placedCell)
        {
            for (var height = 0; height < PlayerClearanceBlocks; height++)
            {
                var checkedCell = new Vector3Int(footCell.x, footCell.y + height, footCell.z);

                if (checkedCell == placedCell || IsVoxelCellOccupied(checkedCell))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsVoxelCellOccupied(Vector3Int cell)
        {
            return _environment != null
                && _environment.initialized
                && _environment.IsVoxelAtPosition(ToVoxelPosition(cell));
        }

        private Vector2 FilterMoveInput(Vector2 input)
        {
            if (!_worldReady
                || _environment == null
                || !_environment.initialized
                || client == null
                || client.LocalPlayerId == 0)
            {
                _hasLocalClimbTarget = false;
                return input;
            }

            if (!client.TryGetLocalPosition(out var localPosition)
                || !TryGetPlayerFootCell(localPosition, out var resolvedFootCell))
            {
                return Vector2.zero;
            }

            var localWorldPosition = GetPlayerWorldPosition(localPosition);
            EnsureLocalGroundInitialized(resolvedFootCell.y);

            if (input.sqrMagnitude < 0.001f)
            {
                _hasLocalClimbTarget = false;
                return input;
            }

            if (resolvedFootCell.y > _smoothedLocalGroundY + 0.1f)
            {
                SetLocalClimbTarget(resolvedFootCell.y);
                return Vector2.zero;
            }

            var currentFootCell = new Vector3Int(
                Mathf.FloorToInt(localWorldPosition.x),
                Mathf.FloorToInt(_smoothedLocalGroundY),
                Mathf.FloorToInt(localWorldPosition.y));
            var filtered = FilterMoveAxis(localWorldPosition, _smoothedLocalGroundY, currentFootCell, input, allowClimbTargetClear: true);

            if (filtered.sqrMagnitude < 0.001f)
            {
                filtered.x = FilterMoveAxis(localWorldPosition, _smoothedLocalGroundY, currentFootCell, new Vector2(input.x, 0f), allowClimbTargetClear: false).x;
                filtered.y = FilterMoveAxis(localWorldPosition, _smoothedLocalGroundY, currentFootCell, new Vector2(0f, input.y), allowClimbTargetClear: false).y;
            }

            if (filtered.sqrMagnitude < 0.001f)
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(filtered, input.magnitude);
        }

        private Vector2 FilterMoveAxis(Vector2 currentPosition, float currentFootY, Vector3Int currentFootCell, Vector2 input, bool allowClimbTargetClear)
        {
            if (input.sqrMagnitude < 0.001f)
            {
                return Vector2.zero;
            }

            if (!TryGetPlayerBodyHit(currentPosition, currentFootY, input, out _))
            {
                if (allowClimbTargetClear)
                {
                    _hasLocalClimbTarget = false;
                }

                return input;
            }

            var probePosition = GetMovementProbePosition(currentPosition, input);

            if (!TryGetPlayerFootCellAtWorldPosition(probePosition, out var probeFootCell))
            {
                return Vector2.zero;
            }

            if (probeFootCell.y <= currentFootCell.y)
            {
                return Vector2.zero;
            }

            SetLocalClimbTarget(probeFootCell.y);

            if (_smoothedLocalGroundY < _localClimbTargetFootY - 0.1f)
            {
                return Vector2.zero;
            }

            var climbScale = Mathf.Clamp01(playerClimbSpeedBlocksPerSecond / EstimatedPlayerWalkSpeed);
            return input * climbScale;
        }

        private void EnsureLocalGroundInitialized(float footY)
        {
            if (_hasSmoothedLocalGroundY)
            {
                return;
            }

            _smoothedLocalGroundY = footY;
            _hasSmoothedLocalGroundY = true;
        }

        private void SetLocalClimbTarget(float footY)
        {
            _localClimbTargetFootY = _hasLocalClimbTarget
                ? Mathf.Max(_localClimbTargetFootY, footY)
                : footY;
            _hasLocalClimbTarget = true;
        }

        private Vector2 GetMovementProbePosition(Vector2 currentPosition, Vector2 input)
        {
            var direction = input.normalized;
            var distance = Mathf.Max(0.05f, playerCollisionRadius)
                + Mathf.Max(0f, movementCollisionProbeDistance) * Mathf.Clamp01(input.magnitude);
            return currentPosition + direction * distance;
        }

        private bool TryGetPlayerBodyHit(Vector2 currentPosition, float currentFootY, Vector2 input, out VoxelHitInfo hitInfo)
        {
            hitInfo = default;

            if (input.sqrMagnitude < 0.001f)
            {
                return false;
            }

            var direction = new Vector3(input.x, 0f, input.y).normalized;
            var distance = Mathf.Max(0.05f, playerCollisionRadius)
                + Mathf.Max(0f, movementCollisionProbeDistance) * Mathf.Clamp01(input.magnitude);
            return TryRayCastPlayerProbe(currentPosition, currentFootY + playerFootProbeHeight, direction, distance, out hitInfo)
                || TryRayCastPlayerProbe(currentPosition, currentFootY + playerHeadProbeHeight, direction, distance, out hitInfo);
        }

        private bool TryRayCastPlayerProbe(Vector2 currentPosition, float probeY, Vector3 direction, float distance, out VoxelHitInfo hitInfo)
        {
            const float Skin = 0.03f;
            var origin = new Vector3(currentPosition.x, probeY, currentPosition.y) - direction * Skin;
            return _environment.RayCast(
                origin,
                direction,
                out hitInfo,
                distance + Skin,
                minOpaque: 1,
                colliderTypes: ColliderTypes.OnlyVoxels,
                createChunksIfNeeded: false,
                microVoxels: false,
                ignoreWater: IgnoreWaterOption.IgnoreWater);
        }

        private static Vector3Int ToVoxelCell(Vector3d position)
        {
            return new Vector3Int(
                Mathf.FloorToInt((float)position.x),
                Mathf.FloorToInt((float)position.y),
                Mathf.FloorToInt((float)position.z));
        }

        private static Vector3 ToVoxelPosition(Vector3Int cell)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f);
        }

        private Transform UpdateLocalPlayerRig(Vector2 serverPosition)
        {
            var rig = GetOrCreateLocalPlayerRig();
            var groundY = GetSmoothedLocalGroundY(serverPosition);
            var playerWorldPosition = GetPlayerWorldPosition(serverPosition);
            rig.SetPositionAndRotation(
                new Vector3(playerWorldPosition.x, groundY, playerWorldPosition.y),
                Quaternion.Euler(0f, _cameraYaw, 0f));
            return rig;
        }

        private Transform GetOrCreateLocalPlayerRig()
        {
            if (_localPlayerRig != null)
            {
                return _localPlayerRig;
            }

            var rigObject = new GameObject("Local Player Rig");
            _localPlayerRig = rigObject.transform;
            return _localPlayerRig;
        }

        private void UpdateCameraFollow()
        {
            if (client.LocalPlayerId == 0 || !client.TryGetLocalPosition(out var localPosition))
            {
                return;
            }

            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            UpdateCameraLook();

            var rig = UpdateLocalPlayerRig(localPosition);
            var cameraWasParented = camera.transform.parent == rig;

            if (camera.transform.parent != rig)
            {
                camera.transform.SetParent(rig, worldPositionStays: false);
            }

            var focus = Vector3.up * PlayerCameraFocusHeight;
            var lookDirection = Quaternion.Euler(_cameraPitch, 0f, 0f) * Vector3.forward;
            lookDirection = lookDirection.sqrMagnitude > 0.001f ? lookDirection.normalized : Vector3.forward;
            var positionSpeed = 4f;
            var rotationSpeed = 6f;
            var targetFov = 58f;
            Vector3 desiredLocalPosition;
            Quaternion desiredLocalRotation;

            switch (cameraZoomMode)
            {
                case CameraZoomMode.Close:
                    desiredLocalPosition = focus - lookDirection * 3.75f;
                    desiredLocalRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                    targetFov = 62f;
                    positionSpeed = 7f;
                    rotationSpeed = 9f;
                    break;

                case CameraZoomMode.FirstPerson:
                    desiredLocalPosition = Vector3.up * PlayerCameraHeadHeight + Vector3.forward * 0.18f;
                    desiredLocalRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                    targetFov = 68f;
                    positionSpeed = 14f;
                    rotationSpeed = 14f;
                    break;

                default:
                    desiredLocalPosition = focus - lookDirection * 13f;
                    desiredLocalRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                    break;
            }

            if (!cameraWasParented || cameraZoomMode == CameraZoomMode.FirstPerson)
            {
                camera.transform.localPosition = desiredLocalPosition;
                camera.transform.localRotation = desiredLocalRotation;
            }
            else
            {
                camera.transform.localPosition = Vector3.Lerp(camera.transform.localPosition, desiredLocalPosition, positionSpeed * Time.deltaTime);
                camera.transform.localRotation = Quaternion.Slerp(camera.transform.localRotation, desiredLocalRotation, rotationSpeed * Time.deltaTime);
            }

            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFov, 8f * Time.deltaTime);
        }

        private float GetSmoothedLocalGroundY(Vector2 localPlayerPosition)
        {
            if (_smoothedLocalGroundFrame == Time.frameCount)
            {
                return _smoothedLocalGroundY;
            }

            var targetGroundY = TryGetPlayerTargetFootY(client.LocalPlayerId, localPlayerPosition, out var footY)
                ? footY
                : GetSurfaceCell(GetPlayerWorldPosition(localPlayerPosition)).y;

            if (!_hasSmoothedLocalGroundY)
            {
                _smoothedLocalGroundY = targetGroundY;
                _hasSmoothedLocalGroundY = true;
                _smoothedLocalGroundFrame = Time.frameCount;
                return _smoothedLocalGroundY;
            }

            var speed = targetGroundY > _smoothedLocalGroundY
                ? playerClimbSpeedBlocksPerSecond
                : playerFallSpeedBlocksPerSecond;
            _smoothedLocalGroundY = Mathf.MoveTowards(_smoothedLocalGroundY, targetGroundY, speed * Time.deltaTime);

            if (_hasLocalClimbTarget && _smoothedLocalGroundY >= _localClimbTargetFootY - 0.1f)
            {
                _hasLocalClimbTarget = false;
            }

            _smoothedLocalGroundFrame = Time.frameCount;
            return _smoothedLocalGroundY;
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

        private static Rect GetCameraButtonRect(float uiScale)
        {
            var safeArea = Screen.safeArea;
            var rightInset = (Screen.width - safeArea.xMax) / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var size = 72f;
            var left = Mathf.Max(12f, (Screen.width / uiScale) - rightInset - size - 12f);
            var top = Mathf.Max(12f, topInset + 12f);
            return new Rect(left, top, size, size);
        }

        private static void GetBlockActionButtonRects(float uiScale, bool reserveCameraButton, out Rect leftButton, out Rect rightButton)
        {
            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width / uiScale;
            var rightInset = (Screen.width - safeArea.xMax) / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var top = Mathf.Max(12f, topInset + 12f);
            var rightEdge = screenWidth - rightInset - 12f;

            if (reserveCameraButton)
            {
                top = Mathf.Max(top, GetCameraButtonRect(uiScale).yMax + BlockActionButtonGap);
            }

            rightButton = new Rect(
                rightEdge - BlockActionButtonSize,
                top,
                BlockActionButtonSize,
                BlockActionButtonSize);
            leftButton = new Rect(
                rightButton.x - BlockActionButtonGap - BlockActionButtonSize,
                top,
                BlockActionButtonSize,
                BlockActionButtonSize);
        }

        private bool IsWorldBlockClick(Vector2 screenPosition, float uiScale)
        {
            if (WorldChatView.IsScreenPositionOverAnyChat(screenPosition))
            {
                return false;
            }

            if (showBlockActionButtons && IsPointerOverBlockActionButtons(screenPosition, uiScale))
            {
                return false;
            }

            if (screenPosition.y <= Screen.height * JoystickClickScreenMaxY)
            {
                return screenPosition.x > Screen.width * LeftJoystickClickScreenMaxX
                    && screenPosition.x < Screen.width * RightJoystickClickScreenMinX;
            }

            return true;
        }

        private bool IsPointerOverBlockActionButtons(Vector2 screenPosition, float uiScale)
        {
            GetBlockActionButtonRects(uiScale, reserveCameraButton: showCameraZoomButton && cameraFollowsLocalPlayer, out var leftButton, out var rightButton);
            var guiPosition = new Vector2(screenPosition.x / uiScale, (Screen.height - screenPosition.y) / uiScale);
            return leftButton.Contains(guiPosition) || rightButton.Contains(guiPosition);
        }

        private static void DrawCenterCrosshair(float uiScale)
        {
            var center = new Vector2(Screen.width / uiScale * 0.5f, Screen.height / uiScale * 0.5f);
            var previousColor = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(center.x - 10f, center.y - 1f, 20f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y - 10f, 2f, 20f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.62f);
            GUI.DrawTexture(new Rect(center.x - 8f, center.y, 16f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x, center.y - 8f, 1f, 16f), Texture2D.whiteTexture);

            GUI.color = previousColor;
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

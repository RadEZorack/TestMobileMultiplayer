using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;
using UnityEngine.Rendering;

namespace VoxelPlay {

    public delegate float SDF (Vector3d position);

    public enum ChunkModifiedFilter {
        All,
        OnlyModified = 1,
        NonModified = 2,
        ModifiedThisSession = 3
    }

    public enum EditorRenderDetail {
        Standard = 0,
        StandardPlusColliders = 1,
        StandardNoDetailGenerators = 2
    }

    public enum ObscuranceMode {
        Faster = 0,
        Custom = 1,
        Lineal
    }

    public enum LogLevel {
        Default = 0,
        Verbose = 1
    }

    public enum UnloadChunkMode {
        ToggleVisibility = 0,
        DestroyIfNotModified = 1,
        Destroy
    }

    public enum InstancingCullingMode {
        Aggressive,
        Gentle,
        Disabled
    }

    public enum NavMeshResolution {
        Default,
        High = 10
    }

    public enum ModelPlacementAlignment {
        Centered,
        NonCentered,
    }

    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class VoxelPlayEnvironment : MonoBehaviour {


        public const int CHUNK_SIZE = 16;
        public const int CHUNK_HALF_SIZE = CHUNK_SIZE / 2;
        public const int CHUNK_SIZE_PLUS_2 = CHUNK_SIZE + 2;
        public const int ONE_Y_ROW = CHUNK_SIZE * CHUNK_SIZE;
        public const int ONE_Z_ROW = CHUNK_SIZE;
        public const int CHUNK_VOXEL_COUNT = CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE;
        public const int CHUNK_SIZE_MINUS_ONE = CHUNK_SIZE - 1;
        public const int VOXELINDEX_X_EDGE_BITWISE = CHUNK_SIZE - 1;
        public const int VOXELINDEX_Z_EDGE_BITWISE = (CHUNK_SIZE - 1) * ONE_Z_ROW;
        public const int VOXELINDEX_Y_EDGE_BITWISE = (CHUNK_SIZE - 1) * ONE_Y_ROW;
        public static Vector3 CHUNK_SIZE_VECTOR = new Vector3(CHUNK_SIZE, CHUNK_SIZE, CHUNK_SIZE);
        public static Vector3 CHUNK_HALF_SIZE_VECTOR = new Vector3(CHUNK_HALF_SIZE, CHUNK_HALF_SIZE, CHUNK_HALF_SIZE);

        /// <summary>
        /// Verbosity level for debug logging. Set to Verbose for maximum debug information.
        /// </summary>
        public LogLevel debugLevel = LogLevel.Default;

        /// <summary>
        /// Contains world generation parameters and biome configurations
        /// </summary>
        public WorldDefinition world;

        /// <summary>
        /// Enables/disables player's ability to use all available voxel definitions and destroy voxels (build mode toggle)
        /// </summary>
        public bool enableBuildMode = true;

        /// <summary>
        /// Current state of build mode.
        /// </summary>
        public bool buildMode;

        public bool enableGeneration = true;
        public bool constructorMode;
        public bool renderInEditor;
        public bool generateAroundCamera = true;
        public EditorRenderDetail renderInEditorDetail = EditorRenderDetail.Standard;
        public Vector3 renderInEditorAreaCenter;
        public Vector3 renderInEditorAreaSize = new Vector3(512, 64, 512);
        public IVoxelPlayCharacterController characterController;
        public bool enableConsole = true;
        public bool showConsole;
        public bool enableInventory = true;
        public float defaultPickupRadius = 2.55f;
        public bool enableDebugWindow = true;
        public bool showFPS;
        public string welcomeMessage = "<color=green>Welcome to <color=white>Voxel Play</color>! Press<color=yellow> F1 </color>for console commands.</color>";
        public float welcomeMessageDuration = 5f;
        public GameObject UICanvasPrefab;
        public GameObject inputControllerPCPrefab, inputControllerMobilePrefab;
        public GameObject crosshairPrefab;
        public Texture2D crosshairTexture;
        public Color consoleBackgroundColor = new Color(0, 0, 0, 82f / 255f);
        public bool enableStatusBar = true;
        public Color statusBarBackgroundColor = new Color(0, 0, 0, 192 / 255f);
        public int layerParticles = 2;
        public int layerVoxels = 1;
        public int layerClouds = 1;
        public bool enableLoadingPanel = true;
        public string loadingText = "Initializing...";
        public float initialWaitTime;
        public string initialWaitText = "Loading World...";

        public bool loadSavedGame;
        public string saveFilename = "save0001";
        public bool restorePlayerPosition = true;

        /// <summary>
        /// If enabled, regions will be loaded dynamically as needed instead of loading the entire world at start.
        /// This improves initial loading time and memory usage for large worlds.
        /// </summary>
        public bool enableDynamicLoad;

        /// <summary>
        /// In case a voxel definition from a savegame file no longer exists, it will be replaced by this fallback voxel definition
        /// </summary>
        public VoxelDefinition fallbackVoxelDefinition;

        /// <summary>
        /// Light spread from voxel to voxel and attenuates as it goes underground
        /// </summary>
        public bool globalIllumination = true;

        /// <summary>
        /// Base ambient light level (0-1). Affects minimum brightness of voxels.
        /// </summary>
        [Range(0f, 1f)]
        public float ambientLight = 0.2f;

        [Range(0f, 1f)]
        public float diffuseWrap = 0.5f;

        [Range(0f, 1f)]
        public float daylightShadowAtten = 0.65f;

        /// <summary>
        /// AO + lighting applied to voxel vertices
        /// </summary>
        public bool enableSmoothLighting = true;

        public ObscuranceMode obscuranceMode = ObscuranceMode.Faster;
        [Range(0f, 3f)]
        public float obscuranceIntensity = 0.5f;

        [Tooltip("Enables normal map effect on opaque voxels.")]
        public bool enableNormalMap;

        [Tooltip("Enables use of PBR (metallic/smoothness/occlusion)")]
        public bool enablePBR;

        [Tooltip("Enables relief mapping effect on opaque voxels.")]
        public bool enableReliefMapping;

        [Range(0f, 0.2f)]
        public float reliefStrength = 0.05f;
        [Range(2, 100)]
        public int reliefIterations = 10;
        [Range(0, 10)]
        public int reliefIterationsBinarySearch = 5;
        public float reliefMaxDistance = 25;

        public bool enableFogSkyBlending = true;

        public int textureSize = 64;

        public int maxChunks = 16000;

        public bool hqFiltering = true;
        [Range(0, 2f)]
        public float mipMapBias = 1;

        public FilterMode filterMode = FilterMode.Point;

        public ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.Off;

        public bool doubleSidedGlass = true;

        public bool transparentBling = true;

        [Tooltip("If true, when a dirt voxel becomes surface it will be rendered using the biome surface voxel definition during meshing.")]
        public bool smartBiomeSurface = true;

        [Tooltip("Enables transitions between biomes by randomly mixing voxel types at biome borders.")]
        public bool enableBiomeMixing;

        [Tooltip("Controls the width of the mixing zone between biomes. Higher values produce wider transitions.")]
        [Range(0.001f, 0.05f)]
        public float biomeMixingSpread = 0.05f;

        /// <summary>
        /// Adds particles when damaging or destroying voxels
        /// </summary>
        public bool damageParticles = true;

        /// <summary>
        /// Enables ComputeBuffers for custom voxels (requires Shader Model 4.5 or later).
        /// </summary>
        public bool useComputeBuffers;

        public InstancingCullingMode instancingCullingMode = InstancingCullingMode.Aggressive;

        public float instancingCullingPadding = 50;

        [Tooltip("Uses a post processing effect to detect and remove white pixels between adjacent edges when using greedy meshing")]
        public bool usePostProcessing;

        /// <summary>
        /// Uses dual touch controller UI in Editor when targeting a mobile platform
        /// </summary>
        public bool previewTouchUIinEditor;

        [NonSerialized]
        public Camera cameraMain;

        /// <summary>
        /// Minimum recommended chunk pool size based on visible distance
        /// </summary>
        public int maxChunksRecommended {
            get {
                int dxz = _visibleChunksDistance * 2 + 1;
                int dy = Mathf.Min(_visibleChunksDistance, visibleChunksVerticalDistance) * 2 + 1;
                return Mathf.Max(3000, dxz * dxz * dy * 2);
            }
        }

        public int prewarmChunksInEditor = 5000;

        public bool enableTinting;
        public bool enableColoredShadows;

        public bool enableBevel;
        [Range(0, 1f)]
        public float bevelWidth = 0.6f;

        public bool enableOutline;
        public Color outlineColor = new Color(1, 1, 1, 0.5f);
        [Range(0, 1f)]
        public float outlineThreshold = 0.49f;

        public bool enableCurvature;

        public bool seeThrough;
        public GameObject seeThroughTarget;
        public float seeThroughRadius = 3f;
        [Range(0f, 1f)]
        public float seeThroughAlpha;

        [SerializeField]
        int _seeThroughHeightOffset = 1;

        public int seeThroughHeightOffset {
            get { return _seeThroughHeightOffset; }
            set {
                if (value != _seeThroughHeightOffset) {
                    _seeThroughHeightOffset = Mathf.Max(0, value);
                    NotifyCameraMove();
                    if (OnSeeThroughHeightOffsetChanged != null) {
                        OnSeeThroughHeightOffsetChanged();
                    }
                }
            }
        }

        public bool enableBrightPointLights;
        public float brightPointsMaxDistance = 10000;

        public bool enableURPNativeLights;

        [Range(1, 40)]
        [SerializeField]
        int _visibleChunksDistance = 10;

        /// <summary>
        /// Maximum render distance in chunks. Higher values impact performance.
        /// Usual range: 1-30. Changing this forces chunk recalculation. For further range, use far chunk rendering option.
        /// </summary>
        public int visibleChunksDistance {
            get { return _visibleChunksDistance; }
            set {
                if (_visibleChunksDistance != value) {
                    _visibleChunksDistance = value;
                    NotifyCameraMove();// forces check chunks in frustum
                    InitOctrees();
                }
            }
        }

        [Range(1, 40)]
        [Tooltip("Maximum vertical render distance in chunks. Higher values increase memory usage and chunk generation. Default is 8.")]
        public int visibleChunksVerticalDistance = 8;

        [Range(1, 8)] public int forceChunkDistance = 3;

#if UNITY_EDITOR
        [Range(1, 5)]
        [Tooltip("Multiplier for visible chunk distances when editing in the Scene View with the World Editor active. Default is 3.")]
        public int editorChunkDistanceMultiplier = 3;
#endif

        int effectiveChunksXZDistance {
            get {
                int d = _visibleChunksDistance;
#if UNITY_EDITOR
                if (renderInEditor && !applicationIsPlaying) {
                    d = Mathf.Min(d * Mathf.Clamp(editorChunkDistanceMultiplier, 1, 5), 40);
                }
#endif
                return d;
            }
        }

        int effectiveChunksYDistance {
            get {
                int d = Mathf.Min(_visibleChunksDistance, visibleChunksVerticalDistance);
#if UNITY_EDITOR
                if (renderInEditor && !applicationIsPlaying) {
                    d = Mathf.Min(d * Mathf.Clamp(editorChunkDistanceMultiplier, 1, 5), 40);
                }
#endif
                return d;
            }
        }

        /// <summary>
        /// Disable the chunk gameObject when it's out of visible distance
        /// </summary>
        public bool unloadFarChunks;

        /// <summary>
        /// 'Toggle visibility' will just hide/show the chunks based on the visible distance parameter. 'Destroy' will actually destroy and release memory of the chunk mesh as well as its collider and NavMesh (if present).
        /// </summary>
        public UnloadChunkMode unloadFarChunksMode = UnloadChunkMode.ToggleVisibility;

        /// <summary>
        /// Allows reusing a chunk navMesh when it's out of visible distance
        /// </summary>
        public bool unloadFarNavMesh;

        /// <summary>
        /// Where the distance is computed from. Usually this is the camera (in first person view) or the character (in third person view)
        /// </summary>
        public Transform distanceAnchor;

        public bool adjustCameraFarClip = true;

        public bool usePixelLights = true;
        public bool enableShadows = true;
        public bool shadowsOnWater;
        public bool realisticWater;
        public long maxCPUTimePerFrame = 30;
        public bool multiThreadGeneration = true;
        public bool lowMemoryMode;
        public bool delayedInitialization;
        public bool onlyRenderInFrustum = true;

        public bool serverMode;
        public bool enableColliders = true;
        public bool enableNavMesh;
        public NavMeshResolution navMeshResolution = NavMeshResolution.Default;
        public bool hideChunksInHierarchy = true;

        public bool enableTrees = true;
        public bool denseTrees = true;
        public bool enableVegetation = true;

        public Light sun;
        [Range(0, 1)]
        public float fogAmount = 0.5f;
        public bool fogDistanceAuto = true;
        public float fogDistance = 300;
        [Range(0, 1)]
        public float fogFallOff = 0.8f;
        public Color fogTint = Color.white;
        public bool enableClouds = true;

        /// <summary>
        /// When enabled, microvoxels space will snap to the nearest microvoxel
        /// </summary>
        [Tooltip("When enabled, microvoxels space will snap to the nearest microvoxel")]
        public bool microVoxelsSnap = true;

        /// <summary>
        /// Default build sound.
        /// </summary>
        public AudioClip defaultBuildSound;

        /// <summary>
        /// Default pick up sound.
        /// </summary>
        public AudioClip defaultPickupSound;

        /// <summary>
        /// Default pick up sound.
        /// </summary>
        public AudioClip defaultDestructionSound;

        /// <summary>
        /// Default impact/hit sound.
        /// </summary>
        public AudioClip defaultImpactSound;

        [Tooltip("Assumed voxel when the voxel definition is missing or placing colors directly on the positions")]
        public VoxelDefinition defaultVoxel;

        [Tooltip("Assumed water voxel definition when the terrain generator doesn't assign one")]
        public VoxelDefinition defaultWaterVoxel;

        /// <summary>
        /// Can be used to force initial loading screen stay until desired
        /// </summary>
        public static bool canHideInitialLoadingScreen = true;


        // far chunks

        public bool enableFarChunksRendering;
        public bool farChunksShadows = true;
        [Range(0, 1)]
        public float farChunksShadowIntensity = 0.8f;
        public bool farChunksWaterReflections = true;
        [Range(0, 1)]
        public float farChunksWaterReflectionsIntensity = 0.5f;
        public bool farChunksWaterColorOverride;
        public Color farChunksWaterColor = new Color(0.247f, 0.396f, 0.745f, 0.87f);
        [ColorUsage(showAlpha: false)]
        public Color farChunksShoreColor = Color.white;
        [Tooltip("Trace below water level")]
        public bool farChunksDeepWater = true;

        #region Public useful state fields

        bool _cameraHasMoved;
        public bool cameraHasMoved {
            get { return _cameraHasMoved || _notifyCameraMove; }
        }

        bool _notifyCameraMove;
        /// <summary>
        /// Notifies the system that camera position changed.
        /// Forces update of visible chunks and environment lighting.
        /// </summary>
        public void NotifyCameraMove () {
            _notifyCameraMove = true;
            _cameraHasMoved = true;
        }

        /// <summary>
        /// Sets new main camera position and notifies the system of position change
        /// </summary>
        public void MoveMainCameraTo (Vector3 position) {
            if (cameraMain == null) return;
            cameraMain.transform.position = position;
            NotifyCameraMove();
        }


        [NonSerialized]
        public int chunksCreated, chunksUsed, chunksInRenderQueueCount, chunksDrawn;
        [NonSerialized]
        public int voxelsCreatedCount;
        [NonSerialized]
        public int treesInCreationQueueCount, treesCreated;
        [NonSerialized]
        public int vegetationInCreationQueueCount, vegetationCreated;

        #endregion

        #region Public Events

        /// <summary>
        /// Allow event dispatching. If captureEvents is set to false, no events will be raised by the APIs.
        /// </summary>
        [NonSerialized]
        public bool captureEvents = true;

        /// <summary>
        /// Allow notification of chunk changes. If notifyChunkChanges is set to false, no chunk change events will be raised by the APIs.
        /// </summary>
        public bool captureChunkChanges {
            get { return VoxelChunk.markModifiedChunks; }
            set { VoxelChunk.markModifiedChunks = value; }
        }

        /// <summary>
        /// Triggered after a saved game is loaded
        /// </summary>
        public event VoxelPlayEvent OnGameLoaded;

        /// <summary>
        /// Triggered just before the world initializes and all systems are created. Useful to set the world seed or change any other setting before it's loaded.
        /// </summary>
        public event VoxelPlayEvent OnBeforeWorldLoad;

        /// <summary>
        /// Triggered when the world has finished loading. This means the savefile game has been loaded, world has been loaded and the engine is idle
        /// </summary>
        public event VoxelPlayEvent OnWorldLoaded;

        /// <summary>
        /// Triggered after Voxel Play has finished loading and initializing stuff
        /// </summary>
        public event VoxelPlayEvent OnInitialized;

        /// <summary>
        /// Triggered when some settings from Voxel Play Environment are changed
        /// </summary>
        public event VoxelPlayEvent OnSettingsChanged;

        /// <summary>
        /// Triggered after a voxel receives damage
        /// </summary>
        public event VoxelHitEvent OnVoxelDamaged;

        /// <summary>
        /// Triggered after a voxel receives damage (provides full HitInfo data)
        /// </summary>
        public event VoxelHitInfoEvent OnVoxelDamagedHitInfo;

        /// <summary>
        /// Triggered after a voxel receives damage
        /// </summary>
        public event VoxelHitAfterEvent OnVoxelAfterDamaged;

        /// <summary>
        /// Triggered after a voxel receives damage (provides full HitInfo data)
        /// </summary>
        public event VoxelHitInfoAfterEvent OnVoxelAfterDamagedHitInfo;

        /// <summary>
        /// Triggered before area damage occurs. It passes a list of potentially affected voxels which can be modified in the event handler.
        /// </summary>
        public event VoxelHitsEvent OnVoxelBeforeAreaDamage;

        /// <summary>
        /// Triggered before area damage occurs. It passes a list of affected voxel indices.
        /// </summary>
        public event VoxelHitsEvent OnVoxelAfterAreaDamage;

        /// <summary>
        /// Triggered just before a voxel is destroyed
        /// </summary>
        public event VoxelCancelableEvent OnVoxelBeforeDestroyed;

        /// <summary>
        /// Triggered after a voxel is destroyed
        /// </summary>
        public event VoxelEvent OnVoxelDestroyed;

        /// <summary>
        /// Tiggered just before a voxel is placed. You can set the voxelType to null to cancel the action.
        /// </summary>
        public event VoxelPlaceEvent OnVoxelBeforePlace;

        /// <summary>
        /// Tiggered just after a voxel is placed before the chunk is refreshed
        /// </summary>
        public event VoxelPositionEvent OnVoxelAfterPlace;

        /// <summary>
        /// Triggered just before a recoverable voxel is created.
        /// </summary>
        public event VoxelDropItemEvent OnVoxelBeforeDropItem;

        /// <summary>
        /// Triggered just after a recoverable voxel is created.
        /// </summary>
        public event VoxelDropItemAfterEvent OnVoxelAfterDropItem;

        /// <summary>
        /// Triggered when clicking on a voxel.
        /// </summary>
        public event VoxelClickEvent OnVoxelClick;

        /// <summary>
        /// Triggered after a torch is placed
        /// </summary>
        public event VoxelTorchEvent OnTorchAttached;

        /// <summary>
        /// Triggered after a torch is removed
        /// </summary>
        public event VoxelTorchEvent OnTorchDetached;

        /// <summary>
        /// Triggered after the contents of a chunk changes (ie. placing a new voxel)
        /// </summary>
        public event VoxelChunkEvent OnChunkChanged;

        /// <summary>
        /// Triggered when a chunk is going to be unloaded (use the canUnload argument to deny the operation)
        /// </summary>
        public event VoxelChunkUnloadEvent OnChunkReuse;

        /// <summary>
        /// Triggered just before the chunk is filled with default contents (terrain, etc.)
        /// Set overrideDefaultContents to fill in the voxels array with your own content
        /// </summary>
        public event VoxelChunkBeforeCreationEvent OnChunkBeforeCreate;

        /// <summary>
        /// Triggered just before calling a detail generator on a chunk
        /// </summary>
        public event VoxelChunkBeforeDetailGenerationEvent OnChunkBeforeDetailGeneration;

        /// <summary>
        /// Triggered just after the chunk has been filled with default contents (terrain, etc.)
        /// </summary>
        public event VoxelChunkEvent OnChunkAfterCreate;

        /// <summary>
        /// Triggered just after the chunk has been rendered for the first time
        /// </summary>
        public event VoxelChunkEvent OnChunkAfterFirstRender;

        /// <summary>
        /// Triggered when chunk mesh is refreshed (updated and uploaded to the GPU)
        /// </summary>
        public event VoxelChunkEvent OnChunkRender;

        /// <summary>
        /// Triggered when a model starts building
        /// </summary>
        public event VoxelModelBuildStartEvent OnModelBuildStart;

        /// <summary>
        /// Triggered when a model ends building
        /// </summary>
        public event VoxelModelBuildEndEvent OnModelBuildEnd;

        /// <summary>
        /// Triggered when a model ends building (receives indices of placed voxels)
        /// </summary>
        public event VoxelModelBuildEndWithIndicesEvent OnModelBuildEndWithIndices;

        /// <summary>
        /// Triggered when the seeThroughClearRoofYPos is changed
        /// </summary>
        public event VoxelPlayEvent OnSeeThroughHeightOffsetChanged;

        /// <summary>
        /// Triggered when a chunk is no longer within visible distance. You can use this event to disable the chunk gameobject
        /// </summary>
        public event VoxelChunkEvent OnChunkExitVisibleDistance;

        /// <summary>
        /// Triggered when a chunk enters the visible distance. You can use this event to re-enable the chunk gameobject
        /// </summary>
        public event VoxelChunkEvent OnChunkEnterVisibleDistance;

        /// <summary>
        /// Occurs when the anchor enters a different chunk (anchor refers to the object referenced by DistanceAnchor property in VoxelPlayEnvironment inspector)
        /// </summary>
        public event VoxelPlayEvent OnPlayerEnterChunk;

        /// <summary>
        /// Triggered just when a tree is being created. Return false to cancel that tree creation.
        /// </summary>
        public event TreeBeforeCreateEvent OnTreeBeforeCreate;

        /// <summary>
        /// Triggered after a tree is created. Receives a list of VoxelIndex elements with positions modified.
        /// </summary>
        public event TreeAfterCreateEvent OnTreeAfterCreate;

        /// <summary>
        /// Triggered when one or more voxels collapse and fall
        /// </summary>
        public event VoxelCollapseEvent OnVoxelCollapse;

        /// <summary>
        /// Triggered just before an origin shift is going to occur. The event handler can return false to cancel the origin shift operation.
        /// </summary>
        public event OriginShiftPreEvent OnOriginPreShift;

        /// <summary>
        /// Triggered just after an origin shift occurs
        /// </summary>
        public event OriginShiftPostEvent OnOriginPostShift;

        /// <summary>
        /// Fired during texture loading, right before session user voxels are processed.
        /// Use this event to call AddVoxelDefinition for runtime/scripted voxels so they are
        /// included in the initial texture arrays without persisting into world.moreVoxels.
        /// </summary>
        public event VoxelPlayEvent OnCollectVoxelDefinitions;

        #endregion



        #region Public API

        static VoxelPlayEnvironment _instance;

        /// <summary>
        /// Enables/disables detail generators
        /// </summary>
        public bool enableDetailGenerators = true;

        /// <summary>
        /// The tiemestamp of this session. Loaded from saved games, 0 at start. Used to detect incremental changes in chunks during saving/loading.
        /// </summary>
        public static int stage;

        /// <summary>
        /// Returns the singleton instance of Voxel Play API.
        /// </summary>
        /// <value>The instance.</value>
        public static VoxelPlayEnvironment instance {
            get {
                if (_instance == null) {
                    _instance = Misc.FindObjectOfType<VoxelPlayEnvironment>(true);
                }
                return _instance;
            }
        }

        /// <summary>
        /// The default value for the light amount on a clear voxel. If global illumination is enabled, this value is 0 (dark). If it's disabled, then this value is 15 (so it does not darken the voxel).
        /// </summary>
        /// <value>The no light value.</value>
        public byte noLightValue {
            get {
                return effectiveGlobalIllumination ? FULL_DARK : FULL_LIGHT;
            }
        }

        /// <summary>
        /// Gets the GameObject of the player
        /// </summary>
        /// <value>The player game object.</value>
        public GameObject playerGameObject {
            get {
                if ((UnityEngine.Object)characterController != null) {
                    return characterController.gameObject;
                } else if (cameraMain != null) {
                    return cameraMain.gameObject;
                } else {
                    return null;
                }
            }
        }

        /// <summary>
        /// Destroyes everything and reloads current assigned world
        /// </summary>
        /// <param name="keepWorldChanges">If set to <c>true</c> any change to chunks will be preserved.</param>
        public void ReloadWorld (bool keepWorldChanges = true) {
            if (!applicationIsPlaying && !renderInEditor)
                return;

            byte[] changes = null;
            if (cachedChunks == null) {
                keepWorldChanges = false;
            }
            if (keepWorldChanges) {
                changes = SaveGameToByteArray();
            }
            LoadWorldInt();
            if (keepWorldChanges) {
                LoadGameFromByteArray(changes, true, false);
            }
            SetInitialized();
            DoWork();
            // Refresh behaviours lighting
            VoxelPlayBehaviour[] bh = Misc.FindObjectsOfType<VoxelPlayBehaviour>();
            for (int k = 0; k < bh.Length; k++) {
                bh[k].UpdateLighting();
            }
        }


        /// <summary>
        /// Clears all chunks in the world and initializes all structures
        /// </summary>
        public void DestroyAllVoxels () {
            bool wasInitialized = initialized;
            LoadWorldInt();
            WarmChunks(null);
            if (wasInitialized) {
                // Restore initialized state for runtime calls
                // (at startup, InitEnd() will set it when OnInitialized fires)
                SetInitialized();
            }
        }


        /// <summary>
        /// Issues a redraw command on all chunks
        /// </summary>
        public void Redraw (bool reloadWorldTextures = false) {
            if (reloadWorldTextures) {
                // reset only texture providers; keep rendering materials and other stuff for speed purposes
                foreach (TextureArrayPacker tap in texturesProviders.Values) {
                    tap.Clear();
                }
                LoadWorldTextures();
            }
            UpdateMaterialProperties();
            if (cachedChunks != null) {
                foreach (KeyValuePair<int, CachedChunk> kv in cachedChunks) {
                    CachedChunk cc = kv.Value;
                    if (cc != null && cc.chunk != null) {
                        ChunkRequestRefresh(cc.chunk, true, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether the chunks root exists and is currently visible.
        /// </summary>
        public bool ChunksVisible {
            get {
                return chunksRoot != null && chunksRoot.gameObject.activeSelf;
            }
        }

        /// <summary>
        /// Toggles on/off chunks visibility
        /// </summary>
        public bool ChunksToggle () {
            if (chunksRoot != null) {
                chunksRoot.gameObject.SetActive(!chunksRoot.gameObject.activeSelf);
                return chunksRoot.gameObject.activeSelf;
            }
            return false;
        }

        /// <summary>
        /// Toggles chunks visibility
        /// </summary>
        public void ChunksToggle (bool visible) {
            if (chunksRoot != null) {
                chunksRoot.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Exports all rendered chunks into regular gameobjects
        /// </summary>
        public void ChunksExport () {
            ChunksExportAll();
            renderInEditor = false;
            if (cachedChunks != null) {
                cachedChunks.Clear();
            }
            keepTexturesOnDestroy = true;
            DisposeAll(removeVoxelPlayEnvironment: true);
        }

        /// <summary>
        /// Casts a ray and applies given damage to any voxel impacted in the direction.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="ray">Ray.</param>
        /// <param name="damage">Damage.</param>
        /// <param name="maxDistance">Max distance of ray.</param>
        /// <param name="addParticles">Add impact particles</param>
        /// <param name="damageRadius">Radius of damage in voxel units</param>
        /// <param name="microVoxels">Hits a microVoxel instead of the full voxel</param>
        /// <param name="slabMode">If set to <c>SlabMode.Bottom</c> or <c>SlabMode.Top</c>, the ray will hit the bottom or top of the voxel respectively.</param>
        public bool RayHit (Rayd ray, int damage, float maxDistance = 0, int damageRadius = 1, bool addParticles = true, bool playSound = true, int microVoxels = 0, float microVoxelDestroyProb = 1f, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition, bool restrictToSlab = false) {
            return RayHit(ray.origin, ray.direction, damage, maxDistance, damageRadius, addParticles, playSound, microVoxels: microVoxels, microVoxelDestroyProb: microVoxelDestroyProb, ignoreWater: ignoreWater, restrictToSlab: restrictToSlab);
        }

        /// <summary>
        /// Casts a ray and applies given damage to any voxel impacted in the direction.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="ray">Ray.</param>
        /// <param name="damage">Damage.</param>
        /// <param name="hitInfo">VoxelHitInfo structure with additional details.</param>
        /// <param name="maxDistance">Max distance of ray.</param>
        /// <param name="microVoxels">Hits a microVoxel instead of the full voxel</param>
        /// <param name="slabMode">If set to <c>SlabMode.Bottom</c> or <c>SlabMode.Top</c>, the ray will hit the bottom or top of the voxel respectively.</param>
        public bool RayHit (Rayd ray, int damage, out VoxelHitInfo hitInfo, float maxDistance = 0, int damageRadius = 1, bool addParticles = true, bool playSound = true, int microVoxels = 0, float microVoxelDestroyProb = 1f, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition, bool restrictToSlab = false) {
            return RayHit(ray.origin, ray.direction, damage, out hitInfo, maxDistance, damageRadius, addParticles: addParticles, playSound: playSound, microVoxels: microVoxels, microVoxelDestroyProb: microVoxelDestroyProb, ignoreWater: ignoreWater, restrictToSlab: restrictToSlab);
        }

        /// <summary>
        /// Casts a ray and applies given damage to any voxel impacted in the direction.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="damage">Damage.</param>
        /// <param name="maxDistance">Max distance of ray.</param>
        /// <param name="microVoxels">Hits a microVoxel instead of the full voxel</param>
        /// <param name="slabMode">If set to <c>SlabMode.Bottom</c> or <c>SlabMode.Top</c>, the ray will hit the bottom or top of the voxel respectively.</param>
        public bool RayHit (Vector3d origin, Vector3 direction, int damage, float maxDistance = 0, int damageRadius = 1, bool addParticles = true, bool playSound = true, int microVoxels = 0, float microVoxelDestroyProb = 1f, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition, bool restrictToSlab = false) {
            bool impact = HitVoxelFast(origin, direction, damage, out _, maxDistance, damageRadius, addParticles, playSound, microVoxels: microVoxels, microVoxelDestroyProb: microVoxelDestroyProb, ignoreWater: ignoreWater, restrictToSlab: restrictToSlab);
            return impact;

        }

        /// <summary>
        /// Casts a ray and applies given damage to any voxel impacted in the direction.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="damage">Damage.</param>
        /// <param name="hitInfo">VoxelHitInfo structure with additional details.</param>
        /// <param name="maxDistance">Max distance of ray.</param>
        /// <param name="microVoxels">Hits a microVoxel instead of the full voxel</param>
        public bool RayHit (Vector3d origin, Vector3 direction, int damage, out VoxelHitInfo hitInfo, float maxDistance = 0, int damageRadius = 1, int layerMask = -1, bool addParticles = true, bool playSound = true, int microVoxels = 0, float microVoxelDestroyProb = 1f, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition, bool restrictToSlab = false) {
            return HitVoxelFast(origin, direction, damage, out hitInfo, maxDistance, damageRadius, layerMask: layerMask, addParticles: addParticles, playSound: playSound, microVoxels: microVoxels, microVoxelDestroyProb: microVoxelDestroyProb, ignoreWater: ignoreWater, restrictToSlab: restrictToSlab);
        }


        /// <summary>
        /// Raycasts in the direction of the ray from ray's origin.
        /// </summary>
        /// <returns><c>true</c>, if a voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="hitInfo">Hit info.</param>
        /// <param name="maxDistance">Max distance.</param>
        /// <param name="minOpaque">Optionally limit the rayhit to voxels with certain opaque factor (15 = solid/full opaque, 3 = cutout, 2 = water, 0 = grass).</param>
        /// <param name="colliderTypes">Optionally specify which colliders can be used</param>
        /// <param name="layerMask">Optional layer mask to filter collider-based (non voxel) objects</param>
        /// <param name="microVoxel">Performs a micro-voxel hit check</param>
        public bool RayCast (Rayd ray, out VoxelHitInfo hitInfo, float maxDistance = 0, int minOpaque = 0, ColliderTypes colliderTypes = ColliderTypes.AnyCollider, int layerMask = -1, bool createChunksIfNeeded = false, bool microVoxels = false, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition) {
            return RayCastFast(ray.origin, ray.direction, out hitInfo, maxDistance, createChunksIfNeeded, (byte)minOpaque, colliderTypes, layerMask, useMicroVoxels: microVoxels, ignoreWater: ignoreWater);
        }

        /// <summary>
        /// Raycasts from a given origin and direction.
        /// </summary>
        /// <returns><c>true</c>, if a voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="ray">Ray.</param>
        /// <param name="hitInfo">Hit info.</param>
        /// <param name="maxDistance">Max distance.</param>
        /// <param name="minOpaque">Optionally limit the rayhit to voxels with certain opaque factor (15 = solid/full opaque, 3 = cutout, 2 = water, 0 = grass).</param>
        /// <param name="colliderTypes">Optionally specify which colliders can be used</param>
        /// <param name="layerMask">Optional layer mask to filter collider-based (non voxel) objects</param>
        /// <param name="microVoxel">Performs a micro-voxel hit check</param>
        public bool RayCast (Vector3d origin, Vector3 direction, out VoxelHitInfo hitInfo, float maxDistance = 0, int minOpaque = 0, ColliderTypes colliderTypes = ColliderTypes.AnyCollider, int layerMask = -1, bool createChunksIfNeeded = false, bool microVoxels = false, IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition) {
            return RayCastFast(origin, direction.normalized, out hitInfo, maxDistance, createChunksIfNeeded, (byte)minOpaque, colliderTypes, layerMask, useMicroVoxels: microVoxels, ignoreWater: ignoreWater);
        }

        /// <summary>
        /// Returns any voxels found between startPosition and endPosition. Invisible voxels are also returned. Use VoxelGetHidden to determine if a voxel is visible or not.
        /// </summary>
        /// <returns>The cast.</returns>
        /// <param name="startPosition">Start position.</param>
        /// <param name="endPosition">End position.</param>
        /// <param name="indices">Indices.</param>
        /// <param name="startIndex">Starting index of the indices array.</param>
        /// <param name="minOpaque">Minimum voxel opaque. Regular solid voxels have an opaque of 15. Tree leaves has an opaque of 3. Water and transaprent voxels has opaque = 2.</param>
        public int LineCast (Vector3d startPosition, Vector3d endPosition, VoxelIndex[] indices, int startIndex = 0, int minOpaque = 0) {
            return LineCastFastVoxel(startPosition, endPosition, indices, startIndex, (byte)minOpaque);
        }


        /// <summary>
        /// Returns any chunk found between startPosition and endPosition.
        /// </summary>
        /// <returns>The cast.</returns>
        /// <param name="startPosition">Start position.</param>
        /// <param name="endPosition">End position.</param>
        /// <param name="chunks">Array of chunks.</param>
        /// <param name="startIndex">Starting index of the indices array.</param>
        public int LineCast (Vector3d startPosition, Vector3d endPosition, VoxelChunk[] chunks, int startIndex = 0) {
            return LineCastFastChunk(startPosition, endPosition, chunks, startIndex);
        }


        /// <summary>
        /// Gets the highest existing voxel position under a given position
        /// </summary>
        public float GetHeight (Vector3d position, byte minOpaque = 0, HashSet<int> allowedVoxelDefinitions = null) {
            float maxAltitude = (object)world.terrainGenerator != null ? world.terrainGenerator.maxHeight : 255;
            float minAltitude = (object)world.terrainGenerator != null ? world.terrainGenerator.minHeight : -255;
            float maxDistance = maxAltitude - minAltitude + 1;
            position.y = maxAltitude;
            if (!RayCastFast(position, Misc.vector3down, out VoxelHitInfo hitInfo, maxDistance, false, minOpaque, ColliderTypes.OnlyVoxels, allowedVoxelDefinitions: allowedVoxelDefinitions)) {
                hitInfo.point.y = float.MinValue;
            }
            return (float)hitInfo.point.y;
        }


        /// <summary>
        /// Gets the highest existing voxel above a given position
        /// </summary>
        public float GetTopMostHeight (Vector3d position, float maxDistance = 128) {
            if (!RayCastFast(position, Misc.vector3up, out VoxelHitInfo hitInfo, maxDistance, false, 0, ColliderTypes.OnlyVoxels)) {
                hitInfo.point.y = float.MinValue;
            }
            return (float)hitInfo.point.y;
        }


        /// <summary>
        /// Returns the voxel chunk where the player is located
        /// </summary>
        public VoxelChunk GetCurrentChunk () {
            if (cameraMain == null)
                return null;
            GetChunkOrCreate(currentAnchorPos, out VoxelChunk chunk);
            return chunk;
        }


        /// </summary>
        /// <returns><c>true</c> if this instance is water at position  (only X/Z values are considered); otherwise, <c>false</c>.</returns>
        public bool IsWaterAtPosition (Vector3d position) {
            return IsWaterAtPosition(position.x, position.z);
        }

        /// <summary>
        /// Returns true if the voxel index corresponds to a voxel position on the edge of a chunk
        /// </summary>
        /// <param name="voxelIndex"></param>
        public bool IsVoxelAtChunkEdge (int voxelIndex) {
            int bx = voxelIndex & VOXELINDEX_X_EDGE_BITWISE;
            int bz = voxelIndex & VOXELINDEX_Z_EDGE_BITWISE;
            int by = voxelIndex & VOXELINDEX_Y_EDGE_BITWISE;
            return (bx == 0 || bx == VOXELINDEX_X_EDGE_BITWISE || by == 0 || by == VOXELINDEX_Y_EDGE_BITWISE || bz == 0 || bz == VOXELINDEX_Z_EDGE_BITWISE);
        }

        /// <summary>
        /// Returns true if water is found at a given position (x/z)
        /// </summary>
        /// <returns><c>true</c> if this instance is water at position; otherwise, <c>false</c>.</returns>
        public bool IsWaterAtPosition (double x, double z) {
            if (heightMapCache == null)
                return false;
            float groundLevel = GetHeightMapInfoFast(x, z).groundLevel;
            if (waterLevel > groundLevel) {
                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Returns the depth of water at a given position
        /// </summary>
        public float GetWaterDepth (Vector3d position) {
            if (heightMapCache == null)
                return 0;
            float groundLevel = GetHeightMapInfoFast(position.x, position.z).groundLevel;
            if (waterLevel > groundLevel) {
                return waterLevel - groundLevel;
            } else {
                return 0;
            }
        }


        /// <summary>
        /// Start flooding at a given position
        /// </summary>
        /// <param name="position">Position.</param>
        public void AddWaterFlood (Vector3d position, VoxelDefinition waterVoxel, int lifeTime = 24) {
            if (enableWaterFlood && lifeTime > 0 && waterVoxel != null) {
                waterFloodSources.Add(ref position, waterVoxel, lifeTime);
            }
        }

        /// <summary>
        /// Used to temporarily disable water flood
        /// </summary>
        [NonSerialized]
        public bool enableWaterFlood = true;

        /// <summary>
        /// Returns true if there a solid block at a position
        /// </summary>
        /// <returns><c>true</c> if this instance is occupied by a solid voxel it returns true; otherwise, <c>false</c>.</returns>
        public bool IsWallAtPosition (Vector3d position) {
            VoxelChunk chunk;
            int voxelIndex;
            if (GetVoxelIndex(position, out chunk, out voxelIndex)) {
                return chunk.voxels[voxelIndex].opaque == FULL_OPAQUE;
            }
            return false;
        }


        /// <summary>
        /// Returns true if terrain at position is rendered and has collider
        /// </summary>
        public bool IsTerrainReadyAtPosition (Vector3d position, bool includeWater) {
            float height = GetTerrainHeight(position, includeWater);
            position.y = height;
            VoxelChunk chunk = GetChunk(position, false);
            if ((object)chunk != null) {
                return enableColliders ? chunk.hasColliderMesh : chunk.isRendered;
            }
            return false;
        }

        /// <summary>
        /// Returns true if there's a voxel at this position
        /// </summary>
        public bool IsVoxelAtPosition (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return chunk.voxels[voxelIndex].hasContent;
            }
            return false;
        }


        /// <summary>
        /// Returns true if position is empty (no voxels)
        /// </summary>
        public bool IsEmptyAtPosition (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return chunk.voxels[voxelIndex].isEmpty;
            }
            return false;
        }

        /// <summary>
        /// Returns true if position is filled with an opaque voxel
        /// </summary>
        public bool IsSolidAtPosition (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return chunk.voxels[voxelIndex].isSolid;
            }
            return false;
        }

        /// <summary>
        /// Returns true if position caontains vegetation
        /// </summary>
        public bool IsVegetationAtPosition (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return voxelDefinitions[chunk.voxels[voxelIndex].typeIndex].isVegetation;
            }
            return false;
        }

        /// <summary>
        /// Returns the chunk at a given position
        /// </summary>
        /// <returns><c>true</c>, if chunk was gotten, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        /// <param name="chunk">Chunk.</param>
        /// <param name="forceCreation">If set to <c>true</c> force creation.</param>
        public bool GetChunk (Vector3d position, out VoxelChunk chunk, bool forceCreation = false) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            return GetChunkFast(chunkX, chunkY, chunkZ, out chunk, forceCreation);
        }


        /// <summary>
        /// Returns the chunk at a given position
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="chunk">Chunk.</param>
        /// <param name="forceCreation">If set to <c>true</c> force creation.</param>
        public VoxelChunk GetChunk (Vector3d position, bool forceCreation = false) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk chunk, forceCreation);
            return chunk;
        }


        /// <summary>
        /// Returns a list of created chunks. 
        /// </summary>
        /// <param name="chunks">User provided list for returning the chunks.</param>
        /// <param name="modifiedFilter">Filter returned chunks by modified flag.</param>
        public void GetChunks (List<VoxelChunk> chunks, ChunkModifiedFilter modifiedFilter = ChunkModifiedFilter.All) {
            chunks.Clear();
            for (int k = 0; k < chunksPoolLoadIndex; k++) {
                VoxelChunk chunk = chunksPool[k];
                if (!chunk.isPopulated) continue;
                if (modifiedFilter == ChunkModifiedFilter.All ||
                    (modifiedFilter == ChunkModifiedFilter.OnlyModified && chunk.modified) ||
                    (modifiedFilter == ChunkModifiedFilter.ModifiedThisSession && chunk.modified && chunk.modifiedTimestamp > stage) ||
                    (modifiedFilter == ChunkModifiedFilter.NonModified && !chunk.modified)) {
                    chunks.Add(chunk);
                }
            }
        }

        /// <summary>
        /// Returns a list of created chunks. 
        /// </summary>
        /// <param name="modifiedFilter">Filter returned chunks by modified flag.</param>
        public List<VoxelChunk> GetChunks (ChunkModifiedFilter modifiedFilter = ChunkModifiedFilter.All) {
            List<VoxelChunk> chunks = new List<VoxelChunk>();
            GetChunks(chunks, modifiedFilter);
            return chunks;
        }

        /// <summary>
        /// Returns the bounds enclosing current visible chunks
        /// </summary>
        public Boundsd GetChunksBounds () {
            Boundsd bounds = new Boundsd();
            Boundsd chunkBounds = new Boundsd();
            bool first = true;
            for (int k = 0; k < chunksPoolLoadIndex; k++) {
                VoxelChunk chunk = chunksPool[k];
                if (chunk.isPopulated) {
                    chunkBounds.center = chunk.position;
                    if (first) {
                        bounds = chunkBounds;
                        first = false;
                    } else {
                        bounds.Encapsulate(chunkBounds);
                    }
                }
            }
            return bounds;
        }

        /// <summary>
        /// Returns the bounds of a given chunk in world space
        /// </summary>
        public Boundsd GetChunkBounds (VoxelChunk chunk) {
            Vector3d center = chunk.position;
            Vector3d min = center - CHUNK_HALF_SIZE_VECTOR;
            Vector3d max = center + CHUNK_HALF_SIZE_VECTOR;
            return new Boundsd((max + min) * 0.5, CHUNK_SIZE_VECTOR);
        }


        /// <summary>
        /// Returns all existing chunks in a given volume
        /// </summary>
        public void GetChunks (Boundsd bounds, List<VoxelChunk> chunks, ChunkModifiedFilter modifiedFilter = ChunkModifiedFilter.All) {
            Vector3d position;
            Vector3d min = bounds.min;
            FastMath.FloorToInt(min.x / CHUNK_SIZE, min.y / CHUNK_SIZE, min.z / CHUNK_SIZE, out int xmin, out int ymin, out int zmin);
            xmin *= CHUNK_SIZE;
            ymin *= CHUNK_SIZE;
            zmin *= CHUNK_SIZE;
            Vector3d max = bounds.max;
            FastMath.FloorToInt(max.x / CHUNK_SIZE, max.y / CHUNK_SIZE, max.z / CHUNK_SIZE, out int xmax, out int ymax, out int zmax);
            xmax *= CHUNK_SIZE;
            ymax *= CHUNK_SIZE;
            zmax *= CHUNK_SIZE;

            chunks.Clear();
            for (int y = ymax; y >= ymin; y -= CHUNK_SIZE) {
                position.y = y;
                for (int z = zmin; z <= zmax; z += CHUNK_SIZE) {
                    position.z = z;
                    for (int x = xmin; x <= xmax; x += CHUNK_SIZE) {
                        position.x = x;
                        if (GetChunk(position, out VoxelChunk chunk, false)) {
                            if (modifiedFilter == ChunkModifiedFilter.All || chunk.modified) {
                                chunks.Add(chunk);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Returns the positions of all chunks within a given bounds
        /// </summary>
        public void GetChunksPositions (Boundsd bounds, List<Vector3d> chunksPositions) {
            Vector3d min = bounds.min;
            FastMath.FloorToInt(min.x / CHUNK_SIZE, min.y / CHUNK_SIZE, min.z / CHUNK_SIZE, out int xmin, out int ymin, out int zmin);
            xmin = xmin * CHUNK_SIZE + CHUNK_HALF_SIZE;
            ymin = ymin * CHUNK_SIZE + CHUNK_HALF_SIZE;
            zmin = zmin * CHUNK_SIZE + CHUNK_HALF_SIZE;
            Vector3d max = bounds.max;
            FastMath.FloorToInt(max.x / CHUNK_SIZE, max.y / CHUNK_SIZE, max.z / CHUNK_SIZE, out int xmax, out int ymax, out int zmax);
            xmax = xmax * CHUNK_SIZE + CHUNK_HALF_SIZE;
            ymax = ymax * CHUNK_SIZE + CHUNK_HALF_SIZE;
            zmax = zmax * CHUNK_SIZE + CHUNK_HALF_SIZE;

            chunksPositions.Clear();
            for (int y = ymax; y >= ymin; y -= CHUNK_SIZE) {
                for (int z = zmin; z <= zmax; z += CHUNK_SIZE) {
                    for (int x = xmin; x <= xmax; x += CHUNK_SIZE) {
                        chunksPositions.Add(new Vector3d(x, y, z));
                    }
                }
            }
        }


        /// <summary>
        /// Returns the chunk at a given position without invoking the terrain generator (the chunk should be empty but only if it's got before it has rendered)
        /// You can use chunk.isPopulated to query if terrain has rendered into this chunk or not
        /// </summary>
        /// <returns><c>true</c>, if chunk was gotten, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public VoxelChunk GetChunkUnpopulated (Vector3d position) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            return GetChunkUnpopulated(chunkX, chunkY, chunkZ);
        }


        /// <summary>
        /// Returns the chunk at a given position without invoking the terrain generator (the chunk should be empty but only if it's got before it has rendered)
        /// You can use chunk.isPopulated to query if terrain has rendered into this chunk or not
        /// </summary>
        /// <returns><c>true</c>, if chunk was gotten, <c>false</c> otherwise.</returns>
        /// <param name="chunkX">X position of chunk / 16. Use FastMath.FloorToInt(chunk.position.x/16)</param>
        /// <param name="chunkY">Y position of chunk / 16.</param>
        /// <param name="chunkZ">Z position of chunk / 16.</param>
        public VoxelChunk GetChunkUnpopulated (int chunkX, int chunkY, int chunkZ) {
            STAGE = 201;
            if (!GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk chunk, createIfNotAvailable: false)) {
                STAGE = 202;
                int hash = GetChunkHash(chunkX, chunkY, chunkZ);
                chunk = CreateChunk(hash, chunkX, chunkY, chunkZ, createEmptyChunk: true, complete: false);
            }
            return chunk;
        }


        /// <summary>
        /// Returns the chunk position that encloses a given position
        /// </summary>
        /// <param name="position">Position.</param>
        public Vector3d GetChunkPosition (Vector3d position) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int x, out int y, out int z);
            x = x * CHUNK_SIZE + CHUNK_HALF_SIZE;
            y = y * CHUNK_SIZE + CHUNK_HALF_SIZE;
            z = z * CHUNK_SIZE + CHUNK_HALF_SIZE;
            return new Vector3d(x, y, z);
        }


        /// <summary>
        /// Gets the voxel at a given position. Returns Voxel.Empty if no voxel found.
        /// </summary>
        /// <returns>The voxel.</returns>
        /// <param name="position">Position.</param>
        /// <param name="createChunkIfNotExists">If set to <c>true</c> create chunk if not exists.</param>
        /// <param name="onlyRenderedVoxels">If set to <c>true</c> the voxel will only be returned if it's rendered. If you're calling GetVoxel as part of a spawning logic, pass true as it will ensure the voxel returned also has the collider in place so your spawned stuff won't fall down.</param>
        public Voxel GetVoxel (Vector3d position, bool createChunkIfNotExists = true, bool onlyRenderedVoxels = false) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk chunk, createChunkIfNotExists);
            if (chunk != null && (!onlyRenderedVoxels || onlyRenderedVoxels && chunk.renderState == ChunkRenderState.RenderingComplete)) {
                Voxel[] voxels = chunk.voxels;
                int px = (int)(position.x - chunkX * CHUNK_SIZE);
                int py = (int)(position.y - chunkY * CHUNK_SIZE);
                int pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                int voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                return voxels[voxelIndex];
            }
            return Voxel.Empty;
        }

        /// <summary>
        /// Returns in indices list all visible voxels inside a volume defined by boxMin and boxMax
        /// </summary>
        /// <returns>Count of all visible voxel indices.</returns>
        /// <param name="boxMin">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="boxMax">Top/right/forward or maximum corner of the enclosing box.</param>
        /// <param name="indices">A list of indices provided by the user to write to.</param>
        /// <param name="minOpaque">Minimum opaque value for a voxel to be considered. Water has an opaque factor of 2, cutout = 3, grass = 0.</param>
        /// <param name="hasContent">Defaults to 1 which will return existing voxels. Pass a 0 to retrieve positions without voxels. Pass -1 to ignore this filter.</param> 
        /// <param name="voxelDefinition">If provided, only voxels of this definition will be returned.</param>
        public int GetVoxelIndices (Vector3d boxMin, Vector3d boxMax, List<VoxelIndex> indices, byte minOpaque = 0, int hasContent = 1, VoxelDefinition voxelDefinition = null) {
            Vector3d chunkPos, voxelPosition;
            VoxelIndex index = new VoxelIndex();
            indices.Clear();

            FastVector.Floor(ref boxMin);
            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);
            FastVector.Floor(ref boxMax);
            boxMax += Vector3d.one;
            Vector3d center = (boxMax - boxMin) * 0.5;

            bool createChunkIfNotExists = hasContent != 1;

            if (hasContent == 0) {
                minOpaque = 0;
            }
            int filterVoxelDefinition = -1;
            if (voxelDefinition != null) {
                filterVoxelDefinition = voxelDefinition.index;
            }

            bool mustHaveContent = hasContent == 1;
            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                chunkPos.y = y;
                int voxelIndexMin = 0;
                if (y == chunkMinPos.y) {
                    int optimalMin = (int)(boxMin.y - (chunkMinPos.y - CHUNK_HALF_SIZE)) * ONE_Y_ROW;
                    if (optimalMin > voxelIndexMin) {
                        voxelIndexMin = optimalMin;
                    }
                }
                int voxelIndexMax = CHUNK_VOXEL_COUNT;
                if (y == chunkMaxPos.y) {
                    int optimalMax = (int)(boxMax.y - (chunkMaxPos.y - CHUNK_HALF_SIZE)) * ONE_Y_ROW;
                    if (optimalMax < voxelIndexMax) {
                        voxelIndexMax = optimalMax;
                    }
                }
                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    chunkPos.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        chunkPos.x = x;
                        if (GetChunk(chunkPos, out VoxelChunk chunk, createChunkIfNotExists)) {
                            for (int v = voxelIndexMin; v < voxelIndexMax; v++) {
                                if (chunk.voxels[v].opaque >= minOpaque && (hasContent == -1 || chunk.voxels[v].hasContent == mustHaveContent) && (filterVoxelDefinition == -1 || chunk.voxels[v].typeIndex == filterVoxelDefinition)) {
                                    int py = v / ONE_Y_ROW;
                                    voxelPosition.y = chunk.position.y - CHUNK_HALF_SIZE + 0.5 + py;
                                    int pz = (v / ONE_Z_ROW) & CHUNK_SIZE_MINUS_ONE;
                                    voxelPosition.z = chunk.position.z - CHUNK_HALF_SIZE + 0.5 + pz;
                                    if (voxelPosition.z >= boxMin.z && voxelPosition.z < boxMax.z) {
                                        int px = v & CHUNK_SIZE_MINUS_ONE;
                                        voxelPosition.x = chunk.position.x - CHUNK_HALF_SIZE + 0.5 + px;
                                        if (voxelPosition.x >= boxMin.x && voxelPosition.x < boxMax.x) {
                                            index.chunk = chunk;
                                            index.voxelIndex = v;
                                            index.position = voxelPosition;
                                            index.sqrDistance = (float)FastVector.SqrDistance(ref voxelPosition, ref center);
                                            indices.Add(index);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return indices.Count;
        }


        /// <summary>
        /// Returns in indices list all visible voxels inside a volume defined by boxMin and boxMax
        /// </summary>
        /// <returns>Count of all visible voxel indices.</returns>
        /// <param name="boxMin">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="boxMax">Top/right/forward or maximum corner of the enclosing box.</param>
        /// <param name="indices">A list of indices provided by the user to write to.</param>
        /// <param name="sdf">A delegate for a method that accepts a world space position and returns a negative value if that position is contained inside an user-defined volume.</param>
        /// <param name="voxelDefinition">If provided, only voxels of this definition will be returned.</param>
        public int GetVoxelIndices (Vector3d boxMin, Vector3d boxMax, List<VoxelIndex> indices, SDF sdf, VoxelDefinition voxelDefinition = null) {
            Vector3d chunkPos, voxelPosition;
            VoxelIndex index = new VoxelIndex();
            indices.Clear();

            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);

            int filterVoxelDefinition = -1;
            if (voxelDefinition != null) {
                filterVoxelDefinition = voxelDefinition.index;
            }

            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                chunkPos.y = y;
                int voxelIndexMin = 0;
                if (y == chunkMinPos.y) {
                    int optimalMin = (int)(boxMin.y - (chunkMinPos.y - CHUNK_HALF_SIZE)) * ONE_Y_ROW;
                    if (optimalMin > 0) {
                        voxelIndexMin = optimalMin;
                    }
                }
                int voxelIndexMax = CHUNK_VOXEL_COUNT;
                if (y == chunkMaxPos.y) {
                    int optimalMax = (int)(boxMax.y - (chunkMaxPos.y - CHUNK_HALF_SIZE) + 1) * ONE_Y_ROW;
                    if (optimalMax < CHUNK_VOXEL_COUNT) {
                        voxelIndexMax = optimalMax;
                    }
                }
                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    chunkPos.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        chunkPos.x = x;
                        if (GetChunk(chunkPos, out VoxelChunk chunk, forceCreation: true)) {
                            for (int v = voxelIndexMin; v < voxelIndexMax; v++) {
                                int py = v / ONE_Y_ROW;
                                voxelPosition.y = chunk.position.y - CHUNK_HALF_SIZE + 0.5 + py;
                                int pz = (v / ONE_Z_ROW) & CHUNK_SIZE_MINUS_ONE;
                                voxelPosition.z = chunk.position.z - CHUNK_HALF_SIZE + 0.5 + pz;
                                if (voxelPosition.z >= boxMin.z && voxelPosition.z < boxMax.z) {
                                    int px = v & CHUNK_SIZE_MINUS_ONE;
                                    voxelPosition.x = chunk.position.x - CHUNK_HALF_SIZE + 0.5 + px;
                                    if (voxelPosition.x >= boxMin.x && voxelPosition.x < boxMax.x) {
                                        if (sdf(voxelPosition) < 0 && (filterVoxelDefinition == -1 || chunk.voxels[v].typeIndex == filterVoxelDefinition)) {
                                            index.chunk = chunk;
                                            index.voxelIndex = v;
                                            index.position = voxelPosition;
                                            indices.Add(index);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return indices.Count;
        }

        /// <summary>
        /// Returns in indices list all visible voxels inside a sphere
        /// </summary>
        /// <returns>Count of all visible voxel indices.</returns>
        /// <param name="center">Center of sphere.</param>
        /// <param name="radiusX">Radius of the sphere in the X axis.</param>
        /// <param name="radiusY">Radius of the sphere in the Y axis.</param>
        /// <param name="radiusZ">Radius of the sphere in the Z axis.</param>
        /// <param name="indices">A list of indices provided by the user to write to.</param>
        /// <param name="minOpaque">Minimum opaque value for a voxel to be considered. Water has opaque = 2, cutout = 3, grass = 0, solid = 15.</param>
        /// <param name="mustHaveContent">Defaults to true which will return existing voxels. Pass false to retrieve positions without voxels.</param>
        /// <param name="voxelDefinition">If provided, only voxels of this definition will be returned.</param>
        public int GetVoxelIndices (Vector3d center, float radius, List<VoxelIndex> indices, byte minOpaque = 0, bool mustHaveContent = true, VoxelDefinition voxelDefinition = null) {
            return GetVoxelIndices(center, radius, radius, radius, indices, minOpaque, mustHaveContent, voxelDefinition);
        }

        /// <summary>
        /// Returns in indices list all visible voxels inside a sphere
        /// </summary>
        /// <returns>Count of all visible voxel indices.</returns>
        /// <param name="center">Center of sphere.</param>
        /// <param name="radius">Radius of the sphere.</param>
        /// <param name="indices">A list of indices provided by the user to write to.</param>
        /// <param name="minOpaque">Minimum opaque value for a voxel to be considered. Water has opaque = 2, cutout = 3, grass = 0, solid = 15.</param>
        /// <param name="mustHaveContent">Defaults to true which will return existing voxels. Pass false to retrieve positions without voxels.</param>
        /// <param name="voxelDefinition">If provided, only voxels of this definition will be returned.</param>
        public int GetVoxelIndices (Vector3d center, float radiusX, float radiusY, float radiusZ, List<VoxelIndex> indices, byte minOpaque = 0, bool mustHaveContent = true, VoxelDefinition voxelDefinition = null) {
            if (indices == null) return 0;

            Vector3d chunkPos, voxelPosition;
            VoxelIndex index = new VoxelIndex();
            indices.Clear();

            center.x = FastMath.FloorToInt(center.x) + 0.5;
            center.y = FastMath.FloorToInt(center.y) + 0.5;
            center.z = FastMath.FloorToInt(center.z) + 0.5;
            Vector3d boxMin = new Vector3d(
                center.x - radiusX,
                center.y - radiusY,
                center.z - radiusZ
            );

            Vector3d boxMax = new Vector3d(
                center.x + radiusX,
                center.y + radiusY,
                center.z + radiusZ
            );
            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);

            bool createIfNotExists = !mustHaveContent;
            if (!mustHaveContent)
                minOpaque = 0;

            int filterVoxelDefinition = -1;
            if (voxelDefinition != null) {
                filterVoxelDefinition = voxelDefinition.index;
            }

            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                chunkPos.y = y;
                int voxelIndexMin = 0;
                if (y == chunkMinPos.y) {
                    int optimalMin = (int)(boxMin.y - (chunkMinPos.y - CHUNK_HALF_SIZE)) * ONE_Y_ROW;
                    if (optimalMin > 0) {
                        voxelIndexMin = optimalMin;
                    }
                }
                int voxelIndexMax = CHUNK_VOXEL_COUNT;
                if (y == chunkMaxPos.y) {
                    int optimalMax = (int)(boxMax.y - (chunkMaxPos.y - CHUNK_HALF_SIZE) + 1) * ONE_Y_ROW;
                    if (optimalMax < CHUNK_VOXEL_COUNT) {
                        voxelIndexMax = optimalMax;
                    }
                }

                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    chunkPos.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        chunkPos.x = x;
                        if (GetChunk(chunkPos, out VoxelChunk chunk, forceCreation: createIfNotExists)) {
                            for (int v = voxelIndexMin; v < voxelIndexMax; v++) {
                                if (chunk.voxels[v].hasContent == mustHaveContent && chunk.voxels[v].type.opaque >= minOpaque && (filterVoxelDefinition == -1 || chunk.voxels[v].typeIndex == filterVoxelDefinition)) {
                                    int py = v / ONE_Y_ROW;
                                    voxelPosition.y = chunk.position.y - CHUNK_HALF_SIZE + 0.5f + py;
                                    int pz = (v / ONE_Z_ROW) & CHUNK_SIZE_MINUS_ONE;
                                    voxelPosition.z = chunk.position.z - CHUNK_HALF_SIZE + 0.5f + pz;
                                    if (voxelPosition.z >= boxMin.z && voxelPosition.z <= boxMax.z) {
                                        int px = v & CHUNK_SIZE_MINUS_ONE;
                                        voxelPosition.x = chunk.position.x - CHUNK_HALF_SIZE + 0.5f + px;
                                        if (voxelPosition.x >= boxMin.x && voxelPosition.x <= boxMax.x) {
                                            double radiusXSqr = radiusX * radiusX;
                                            double radiusYSqr = radiusY * radiusY;
                                            double radiusZSqr = radiusZ * radiusZ;
                                            double distX = (voxelPosition.x - center.x) * (voxelPosition.x - center.x) / radiusXSqr;
                                            double distY = (voxelPosition.y - center.y) * (voxelPosition.y - center.y) / radiusYSqr;
                                            double distZ = (voxelPosition.z - center.z) * (voxelPosition.z - center.z) / radiusZSqr;
                                            double dist = distX + distY + distZ;
                                            if (dist < 1.0) {
                                                index.chunk = chunk;
                                                index.voxelIndex = v;
                                                index.position = voxelPosition;
                                                index.sqrDistance = (float)dist;
                                                indices.Add(index);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return indices.Count;
        }

        /// <summary>
        /// This method converts positions into voxel indices
        /// </summary>
        /// <param name="createChunkIfNotExists">If set to <c>true</c> chunks will be created if they don't exist.</param>
        public void GetVoxelIndices (List<Vector3d> positions, List<VoxelIndex> indices, bool createChunkIfNotExists = false) {
            if (indices == null) return;
            indices.Clear();
            if (positions == null) return;
            int positionsCount = positions.Count;
            VoxelIndex vindex = new VoxelIndex();
            for (int k = 0; k < positionsCount; k++) {
                Vector3d position = positions[k];
                FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
                if (GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk chunk, createChunkIfNotExists)) {
                    int py = (int)(position.y - chunkY * CHUNK_SIZE);
                    int pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                    int px = (int)(position.x - chunkX * CHUNK_SIZE);
                    vindex.chunk = chunk;
                    vindex.voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                    indices.Add(vindex);
                }
            }
        }

        /// <summary>
        /// Writes into 'indices' the sequence of voxel indices intersected by a straight line segment from start to end.
        /// The traversal is cell-accurate (3D DDA) and guarantees no duplicate voxel indices. Chunks are not created unless specified.
        /// </summary>
        /// <param name="start">World position of the line start.</param>
        /// <param name="end">World position of the line end.</param>
        /// <param name="indices">Destination list (will be cleared).</param>
        /// <param name="createChunkIfNotExists">If true, creates chunks along the path as needed.</param>
        /// <returns>Count of indices written.</returns>
        public int GetVoxelIndicesOnPath (Vector3d start, Vector3d end, List<VoxelIndex> indices, bool createChunkIfNotExists = false) {
            if (indices == null) return 0;
            indices.Clear();

            // 2D DDA over XZ, y clamped to terrain surface (ignore water)
            int x = FastMath.FloorToInt(start.x);
            int z = FastMath.FloorToInt(start.z);
            int xEnd = FastMath.FloorToInt(end.x);
            int zEnd = FastMath.FloorToInt(end.z);

            double dx = end.x - start.x;
            double dz = end.z - start.z;

            int stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int stepZ = dz > 0 ? 1 : (dz < 0 ? -1 : 0);

            double invDx = dx != 0 ? 1.0 / System.Math.Abs(dx) : double.PositiveInfinity;
            double invDz = dz != 0 ? 1.0 / System.Math.Abs(dz) : double.PositiveInfinity;

            double tMaxX, tMaxZ;
            if (stepX > 0) tMaxX = ((x + 1) - start.x) * invDx; else if (stepX < 0) tMaxX = (start.x - x) * invDx; else tMaxX = double.PositiveInfinity;
            if (stepZ > 0) tMaxZ = ((z + 1) - start.z) * invDz; else if (stepZ < 0) tMaxZ = (start.z - z) * invDz; else tMaxZ = double.PositiveInfinity;

            double tDeltaX = invDx;
            double tDeltaZ = invDz;

            VoxelIndex last = VoxelIndex.Null;
            Vector3d center;

            // Add first cell at terrain surface
            center.x = x + 0.5;
            center.z = z + 0.5;
            center.y = 0;
            {
                float h = GetTerrainHeight(center, false) - 1f;
                center.y = FastMath.FloorToInt(h) + 0.5;
            }
            if (GetVoxelIndex(center, out VoxelChunk c, out int idx, createChunkIfNotExists)) {
                last.chunk = c; last.voxelIndex = idx; last.position = center;
                indices.Add(last);
            }

            while (x != xEnd || z != zEnd) {
                if (tMaxX < tMaxZ) { x += stepX; tMaxX += tDeltaX; } else { z += stepZ; tMaxZ += tDeltaZ; }

                center.x = x + 0.5;
                center.z = z + 0.5;
                center.y = 0;
                {
                    float h = GetTerrainHeight(center, false) - 1f;
                    center.y = FastMath.FloorToInt(h) + 0.5;
                }
                if (GetVoxelIndex(center, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists)) {
                    if (chunk != last.chunk || voxelIndex != last.voxelIndex) {
                        VoxelIndex vi = new VoxelIndex();
                        vi.chunk = chunk;
                        vi.voxelIndex = voxelIndex;
                        vi.position = center;
                        indices.Add(vi);
                        last = vi;
                    }
                }
            }

            return indices.Count;
        }

        /// <summary>
        /// This method converts positions into an existing indices array.
        /// </summary>
        /// <param name="indices">Must be preallocated</param>
        /// <param name="createChunkIfNotExists">If set to <c>true</c> chunks will be created if they don't exist.</param>
        /// <returns>Returns the number of indices placed into the indices array</returns>
        public int GetVoxelIndices (Vector3d[] positions, VoxelIndex[] indices, bool createChunkIfNotExists = false) {
            if (indices == null) return 0;
            if (positions == null) return 0;
            int positionsLength = positions.Length;
            if (indices.Length < positionsLength) positionsLength = indices.Length;

            VoxelIndex vindex = new VoxelIndex();
            for (int k = 0; k < positionsLength; k++) {
                Vector3d position = positions[k];
                FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
                if (GetChunkFast(chunkX, chunkY, chunkZ, out VoxelChunk chunk, createChunkIfNotExists)) {
                    int py = (int)(position.y - chunkY * CHUNK_SIZE);
                    int pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                    int px = (int)(position.x - chunkX * CHUNK_SIZE);
                    vindex.chunk = chunk;
                    vindex.voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                    indices[k] = vindex;
                }
            }
            return positionsLength;
        }

        /// <summary>
        /// Returns a copy of all voxels in a given volume
        /// </summary>
        /// <param name="boxMin">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="boxMax">Top/right/forward or maximum corner of the enclosing box.</param>
        /// <param name="voxels">User provided 3-dimensional array of voxels (y/z/x). You must allocate enough space before calling this method.</param> 
        /// <param name="createIfNotExists">If set to <c>true</c> chunks will be created if they don't exist.</param>
        public void GetVoxels (Vector3d boxMin, Vector3d boxMax, Voxel[,,] voxels, bool createIfNotExists = false) {

            if (voxels == null) {
                Debug.LogError("You must allocate enough space for the voxels array parameter.");
                return;
            }

            Vector3d position;

            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);

            int minX, minY, minZ, maxX, maxY, maxZ;
            FastMath.FloorToInt(boxMin.x, boxMin.y, boxMin.z, out minX, out minY, out minZ);
            FastMath.FloorToInt(boxMax.x, boxMax.y, boxMax.z, out maxX, out maxY, out maxZ);

            int sizeY = maxY - minY;
            int sizeZ = maxZ - minZ;
            int sizeX = maxX - minX;
            int msizeY = voxels.GetUpperBound(0);
            int msizeZ = voxels.GetUpperBound(1);
            int msizeX = voxels.GetUpperBound(2);
            if (msizeY < sizeY || msizeZ < sizeZ || msizeX < sizeX) {
                Debug.LogError($"Voxels array size does not match volume size. Expected format and size (y,z,x): [{sizeY + 1}, {sizeZ + 1}, {sizeX + 1}");
                return;
            }

            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                position.y = y;
                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    position.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        position.x = x;
                        VoxelChunk chunk;
                        if (GetChunk(position, out chunk, forceCreation: createIfNotExists)) {
                            int chunkMinX, chunkMinY, chunkMinZ;
                            FastMath.FloorToInt(chunk.position.x, chunk.position.y, chunk.position.z, out chunkMinX, out chunkMinY, out chunkMinZ);
                            chunkMinX -= CHUNK_HALF_SIZE;
                            chunkMinY -= CHUNK_HALF_SIZE;
                            chunkMinZ -= CHUNK_HALF_SIZE;
                            for (int vy = 0; vy < CHUNK_SIZE; vy++) {
                                int wy = chunkMinY + vy;
                                if (wy < minY || wy > maxY)
                                    continue;
                                int my = wy - minY;
                                int voxelIndexY = vy * ONE_Y_ROW;
                                for (int vz = 0; vz < CHUNK_SIZE; vz++) {
                                    int wz = chunkMinZ + vz;
                                    if (wz < minZ || wz > maxZ)
                                        continue;
                                    int mz = wz - minZ;
                                    int voxelIndex = voxelIndexY + vz * ONE_Z_ROW;
                                    for (int vx = 0; vx < CHUNK_SIZE; vx++, voxelIndex++) {
                                        int wx = chunkMinX + vx;
                                        if (wx < minX || wx > maxX)
                                            continue;
                                        int mx = wx - minX;
                                        voxels[my, mz, mx] = chunk.voxels[voxelIndex];
                                    }
                                }
                            }
                        } else {
                            int chunkMinY = FastMath.FloorToInt(y) - CHUNK_HALF_SIZE;
                            int chunkMinZ = FastMath.FloorToInt(z) - CHUNK_HALF_SIZE;
                            int chunkMinX = FastMath.FloorToInt(x) - CHUNK_HALF_SIZE;
                            int voxelIndex = 0;
                            for (int vy = 0; vy < CHUNK_SIZE; vy++) {
                                int wy = chunkMinY + vy;
                                if (wy < minY || wy > maxY)
                                    continue;
                                int my = wy - minY;
                                for (int vz = 0; vz < CHUNK_SIZE; vz++) {
                                    int wz = chunkMinZ + vz;
                                    if (wz < minZ || wz > maxZ)
                                        continue;
                                    int mz = wz - minZ;
                                    for (int vx = 0; vx < CHUNK_SIZE; vx++, voxelIndex++) {
                                        int wx = chunkMinX + vx;
                                        if (wx < minX || wx > maxX)
                                            continue;
                                        int mx = wx - minX;
                                        voxels[my, mz, mx] = Voxel.Empty;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Places a volume of voxels given by a 3d array
        /// </summary>
        /// <param name="position">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="voxels">User provided 3-dimensional array of voxels (y/z/x).</param> 
        public void SetVoxels (Vector3d position, Voxel[,,] voxels) {
            int msizeY = voxels.GetUpperBound(0);
            int msizeZ = voxels.GetUpperBound(1);
            int msizeX = voxels.GetUpperBound(2);
            Vector3d boxMax = new Vector3d(position.x + msizeX, position.y + msizeY, position.z + msizeZ);
            SetVoxels(position, boxMax, voxels);
        }


        /// <summary>
        /// Places a volume of voxels given by a 3d array
        /// </summary>
        /// <param name="boxMin">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="boxMax">Top/right/forward or maximum corner of the enclosing box.</param>
        /// <param name="voxels">User provided 3-dimensional array of voxels (y/z/x).</param>
        /// <param name="redraw">Issue a redraw of the affected chunks</param>
        public void SetVoxels (Vector3d boxMin, Vector3d boxMax, Voxel[,,] voxels, bool redraw = true) {

            if (voxels == null) {
                Debug.LogError("SetVoxels: voxels array is null.");
                return;
            }

            Vector3d position;

            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);

            int minX, minY, minZ, maxX, maxY, maxZ;
            FastMath.FloorToInt(boxMin.x, boxMin.y, boxMin.z, out minX, out minY, out minZ);
            FastMath.FloorToInt(boxMax.x, boxMax.y, boxMax.z, out maxX, out maxY, out maxZ);

            int sizeY = maxY - minY;
            int sizeZ = maxZ - minZ;
            int sizeX = maxX - minX;
            int msizeY = voxels.GetUpperBound(0);
            int msizeZ = voxels.GetUpperBound(1);
            int msizeX = voxels.GetUpperBound(2);
            if (msizeY < sizeY || msizeZ < sizeZ || msizeX < sizeX) {
                Debug.LogError("Voxels array size does not match volume size. Expected size: [" + sizeY + ", " + sizeZ + ", " + sizeX + "]");
                return;
            }

            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                position.y = y;
                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    position.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        position.x = x;
                        VoxelChunk chunk = GetChunkUnpopulated(position);
                        if (chunk != null) {
                            int chunkMinX, chunkMinY, chunkMinZ;
                            FastMath.FloorToInt(chunk.position.x, chunk.position.y, chunk.position.z, out chunkMinX, out chunkMinY, out chunkMinZ);
                            chunkMinX -= CHUNK_HALF_SIZE;
                            chunkMinY -= CHUNK_HALF_SIZE;
                            chunkMinZ -= CHUNK_HALF_SIZE;
                            for (int vy = 0; vy < CHUNK_SIZE; vy++) {
                                int wy = chunkMinY + vy;
                                if (wy < minY || wy > maxY)
                                    continue;
                                int my = wy - minY;
                                int voxelIndexY = vy * ONE_Y_ROW;
                                for (int vz = 0; vz < CHUNK_SIZE; vz++) {
                                    int wz = chunkMinZ + vz;
                                    if (wz < minZ || wz > maxZ)
                                        continue;
                                    int mz = wz - minZ;
                                    int voxelIndex = voxelIndexY + vz * ONE_Z_ROW;
                                    for (int vx = 0; vx < CHUNK_SIZE; vx++, voxelIndex++) {
                                        int wx = chunkMinX + vx;
                                        if (wx < minX || wx > maxX)
                                            continue;
                                        int mx = wx - minX;
                                        chunk.voxels[voxelIndex] = voxels[my, mz, mx];
                                    }
                                }
                            }
                        }
                        ChunkRequestRefresh(chunk, true, true);
                    }
                }
            }
        }

        /// <summary>
        /// Replaces voxels
        /// </summary>
        /// <param name="boxMin">Bottom/left/back or minimum corner of the enclosing box.</param>
        /// <param name="boxMax">Top/right/forward or maximum corner of the enclosing box.</param>
        /// <param name="voxels">User provided 3-dimensional array of voxels (y/z/x). You must allocate enough space before calling this method.</param> 
        /// <param name="ignoreEmptyVoxels">Set to true to only place voxels from the array which are not empty</param>
        public void VoxelPlace (Vector3d boxMin, Vector3d boxMax, Voxel[,,] voxels, bool ignoreEmptyVoxels = false) {
            Vector3d position;

            Vector3d chunkMinPos = GetChunkPosition(boxMin);
            Vector3d chunkMaxPos = GetChunkPosition(boxMax);

            int minX, minY, minZ, maxX, maxY, maxZ;
            FastMath.FloorToInt(boxMin.x, boxMin.y, boxMin.z, out minX, out minY, out minZ);
            FastMath.FloorToInt(boxMax.x, boxMax.y, boxMax.z, out maxX, out maxY, out maxZ);

            int sizeY = maxY - minY;
            int sizeZ = maxZ - minZ;
            int sizeX = maxX - minX;
            int msizeY = voxels.GetUpperBound(0);
            int msizeZ = voxels.GetUpperBound(1);
            int msizeX = voxels.GetUpperBound(2);
            if (msizeY < sizeY || msizeZ < sizeZ || msizeX < sizeX) {
                Debug.LogError("Voxels array size does not match volume size. Expected size: [" + sizeY + ", " + sizeZ + ", " + sizeX + "]");
                return;
            }

            for (double y = chunkMinPos.y; y <= chunkMaxPos.y; y += CHUNK_SIZE) {
                position.y = y;
                for (double z = chunkMinPos.z; z <= chunkMaxPos.z; z += CHUNK_SIZE) {
                    position.z = z;
                    for (double x = chunkMinPos.x; x <= chunkMaxPos.x; x += CHUNK_SIZE) {
                        position.x = x;
                        VoxelChunk chunk;
                        if (GetChunk(position, out chunk, true)) {
                            int chunkMinX, chunkMinY, chunkMinZ;
                            FastMath.FloorToInt(chunk.position.x, chunk.position.y, chunk.position.z, out chunkMinX, out chunkMinY, out chunkMinZ);
                            chunkMinX -= CHUNK_HALF_SIZE;
                            chunkMinY -= CHUNK_HALF_SIZE;
                            chunkMinZ -= CHUNK_HALF_SIZE;
                            for (int vy = 0; vy < CHUNK_SIZE; vy++) {
                                int wy = chunkMinY + vy;
                                if (wy < minY || wy > maxY)
                                    continue;
                                int my = wy - minY;
                                int voxelIndexY = vy * ONE_Y_ROW;
                                for (int vz = 0; vz < CHUNK_SIZE; vz++) {
                                    int wz = chunkMinZ + vz;
                                    if (wz < minZ || wz > maxZ)
                                        continue;
                                    int mz = wz - minZ;
                                    int voxelIndex = voxelIndexY + vz * ONE_Z_ROW;
                                    for (int vx = 0; vx < CHUNK_SIZE; vx++, voxelIndex++) {
                                        int wx = chunkMinX + vx;
                                        if (wx < minX || wx > maxX)
                                            continue;
                                        int mx = wx - minX;
                                        if (voxels[my, mz, mx].hasContent || !ignoreEmptyVoxels) {
                                            chunk.voxels[voxelIndex] = voxels[my, mz, mx];
                                        }
                                    }
                                }
                            }
                            ChunkRequestRefresh(chunk, true, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a new voxel definition to the internal dictionary
        /// </summary>
        /// <param name="vd">Vd.</param>
        public void AddVoxelDefinition (VoxelDefinition vd) {
            if (vd == null)
                return;
            // Check if voxelType is not added
            if (vd.index <= 0 && sessionUserVoxels != null) {
                InsertUserVoxelDefinition(vd);
                requireTextureArrayUpdate = true;
            }
        }

        /// <summary>
        /// Returns true if voxel definition is not registered or if it's been registered with AddVoxelDefinition (so it's temporary to this session)
        /// </summary>
        /// <param name="vd"></param>
        /// <returns></returns>
        public bool IsVoxelDefinitionTemporary (VoxelDefinition vd) {
            if (vd == null) return false;
            if (vd.index <= 0) return true;
            if (world.moreVoxels.Contains(vd)) return false;
            return sessionUserVoxels != null && sessionUserVoxels.Contains(vd);
        }

        /// <summary>
        /// Updates textures of an already added voxel definition
        /// </summary>
        /// <param name="vd">Voxel definition to update textures for</param>
        /// <param name="redrawChunks">Whether to redraw chunks after updating textures</param>
        public void UpdateVoxelDefinitionTextures (VoxelDefinition vd, bool redrawChunks = true) {
            if (vd == null || vd.index <= 0 || vd.renderType == RenderType.Custom || vd.textureArrayPacker == null) return;
            TextureArrayPacker packer = vd.textureArrayPacker;
            int requiredSlices = GetRequiredTextureSlices(vd);
            if (NeedsCompaction(packer, requiredSlices)) {
                CompactTextureArrayPacker(packer);
                if (redrawChunks) {
                    Redraw(reloadWorldTextures: false);
                }
                return;
            }
            packer.ForgetTextures(vd);
            AddVoxelTexturesNonCustom(vd);
            vd.dynamicDefinition = null;
            if (vd.connectedTextures != null) {
                vd.connectedTextures.Init();
            }
            if (packer != vd.textureArrayPacker) {
                packer.CreateTextureArray();
            }
            vd.textureArrayPacker.CreateTextureArray();
            SetRenderingMaterialsTextures(createTextureArrays: false);
            if (redrawChunks) {
                ChunkRedrawAll(refreshLightmap: false, refreshMesh: true, ignoreFrustum: true);
            }
        }

        /// <summary>
        /// Updates textures for a list of already-registered voxel definitions, with a single redraw at the end.
        /// </summary>
        public void UpdateVoxelDefinitionTextures (List<VoxelDefinition> vds) {
            if (vds == null || vds.Count == 0) return;

            // Group by packer and check compaction needs
            HashSet<TextureArrayPacker> compactedPackers = new HashSet<TextureArrayPacker>();
            HashSet<TextureArrayPacker> touchedPackers = new HashSet<TextureArrayPacker>();
            bool needsRedraw = false;

            int count = vds.Count;
            for (int i = 0; i < count; i++) {
                VoxelDefinition vd = vds[i];
                if (vd == null || vd.index <= 0 || vd.renderType == RenderType.Custom || vd.textureArrayPacker == null) continue;
                TextureArrayPacker packer = vd.textureArrayPacker;

                // If this packer was already compacted, this VD is already re-registered
                if (compactedPackers.Contains(packer)) continue;

                int requiredSlices = GetRequiredTextureSlices(vd);
                if (NeedsCompaction(packer, requiredSlices)) {
                    // Compaction re-registers ALL VDs of this packer, including this one
                    CompactTextureArrayPacker(packer);
                    compactedPackers.Add(packer);
                    needsRedraw = true;
                    continue;
                }

                // Normal path: forget + re-add
                touchedPackers.Add(packer);
                packer.ForgetTextures(vd);
                AddVoxelTexturesNonCustom(vd);
                vd.dynamicDefinition = null;
                if (vd.connectedTextures != null) {
                    vd.connectedTextures.Init();
                }
                touchedPackers.Add(vd.textureArrayPacker);
                needsRedraw = true;
            }

            if (needsRedraw) {
                foreach (var packer in touchedPackers) {
                    packer.CreateTextureArray();
                }
                SetRenderingMaterialsTextures(createTextureArrays: false);
                Redraw(reloadWorldTextures: false);
            }
        }

        /// <summary>
        /// Adds a list of voxel definitions to the internal dictionary
        /// </summary>
        /// <param name="vd">Vd.</param>
        public void AddVoxelDefinitions (List<VoxelDefinition> vd) {
            if (vd == null)
                return;
            for (int k = 0; k < vd.Count; k++) {
                AddVoxelDefinition(vd[k]);
            }
        }

        /// <summary>
        /// Adds voxel definitions contained in a model to the internal dictionary
        /// </summary>
        public void AddVoxelDefinitions (ModelDefinition model) {
            if (model == null)
                return;
            for (int k = 0; k < model.bits.Length; k++) {
                AddVoxelDefinition(model.bits[k].voxelDefinition);
            }
        }

        /// <summary>
        /// Adds a list of voxel definitions to the internal dictionary
        /// </summary>
        /// <param name="vd">Vd.</param>
        public void AddVoxelDefinitions (params VoxelDefinition[] vd) {
            if (vd == null)
                return;
            for (int k = 0; k < vd.Length; k++) {
                AddVoxelDefinition(vd[k]);
            }
        }

        /// <summary>
        /// Gets the voxel definition by name
        /// </summary>
        /// <returns>The voxel definition.</returns>
        public VoxelDefinition GetVoxelDefinition (string name) {
            if (string.IsNullOrEmpty(name)) return null;
            voxelDefinitionsDict.TryGetValue(name, out VoxelDefinition vd);
            return vd;
        }

        /// <summary>
        /// Gets the voxel definition by index
        /// </summary>
        /// <returns>The voxel definition.</returns>
        /// <param name="index">Index.</param>
        public VoxelDefinition GetVoxelDefinition (int index) {
            if (index >= 0 && index < voxelDefinitionsCount) {
                return voxelDefinitions[index];
            }
            return null;
        }



        /// <summary>
        /// Returns the index of the voxel at a given position inside its chunk.
        /// </summary>
        /// <returns>The voxel index.</returns>
        /// <param name="position">Position in world space.</param>
        public int GetVoxelIndex (Vector3d position) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            chunkX *= CHUNK_SIZE;
            chunkY *= CHUNK_SIZE;
            chunkZ *= CHUNK_SIZE;
            int px = (int)(position.x - chunkX);
            int py = (int)(position.y - chunkY);
            int pz = (int)(position.z - chunkZ);
            int voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
            return voxelIndex;
        }


        /// <summary>
        /// Gets a VoxelIndex struct that locates a voxel position in world space
        /// </summary>
        /// <returns>The voxel index.</returns>
        /// <param name="position">Position in world space.</param>
        /// <param name="createChunkIfNotExists">Pass true to force the chunk creation if it does not exist at that position. Defaults to false.</param>
        public bool GetVoxelIndex (Vector3d position, out VoxelIndex index, bool createChunkIfNotExists = false) {
            index = new VoxelIndex();
            return GetVoxelIndex(position, out index.chunk, out index.voxelIndex, createChunkIfNotExists);
        }

        /// <summary>
        /// Gets the chunk position and voxelIndex corresponding to a given world position (note that the chunk might not exists yet)
        /// </summary>
        /// <param name="position">World position.</param>
        public void GetVoxelIndex (Vector3d position, out Vector3d chunkPosition, out int voxelIndex) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            chunkX *= CHUNK_SIZE;
            chunkY *= CHUNK_SIZE;
            chunkZ *= CHUNK_SIZE;
            chunkPosition.x = chunkX + CHUNK_HALF_SIZE;
            chunkPosition.y = chunkY + CHUNK_HALF_SIZE;
            chunkPosition.z = chunkZ + CHUNK_HALF_SIZE;
            int px = (int)(position.x - chunkX);
            int py = (int)(position.y - chunkY);
            int pz = (int)(position.z - chunkZ);
            voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
        }

        /// <summary>
        /// Gets the chunk and array index of the voxel at a given position
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index in the chunk.voxels array</param>
        public bool GetVoxelIndex (Vector3d position, out VoxelChunk chunk, out int voxelIndex, bool createChunkIfNotExists = true) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            if (GetChunkFast(chunkX, chunkY, chunkZ, out chunk, createChunkIfNotExists)) {
                int py = (int)(position.y - chunkY * CHUNK_SIZE);
                int pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                int px = (int)(position.x - chunkX * CHUNK_SIZE);
                voxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                return true;
            }

            voxelIndex = 0;
            return false;
        }


        /// <summary>
        /// Gets the chunk and array index of the voxel at a given position
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="chunk">Chunk.</param>
        /// <param name="px">Position x inside the chunk</param>
        /// <param name="py">Position y inside the chunk</param>
        /// <param name="pz">Position z inside the chunk</param>
        public bool GetVoxelIndex (Vector3d position, out VoxelChunk chunk, out int px, out int py, out int pz, bool createChunkIfNotExists = true) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            if (GetChunkFast(chunkX, chunkY, chunkZ, out chunk, createChunkIfNotExists)) {
                py = (int)(position.y - chunkY * CHUNK_SIZE);
                pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                px = (int)(position.x - chunkX * CHUNK_SIZE);
                return true;
            }
            px = py = pz = 0;
            return false;
        }

        /// <summary>
        /// Gets the voxel index corresponding to certain offset to another voxel index. Useful to get a safe reference to voxels on top of others, etc.
        /// </summary>
        /// <returns><c>true</c>, if voxel index was gotten, <c>false</c> otherwise.</returns>
        /// <param name="index">Voxel index.</param>
        /// <param name="offsetX">Offset x.</param>
        /// <param name="offsetY">Offset y.</param>
        /// <param name="offsetZ">Offset z.</param>
        /// <param name="otherIndex">Other voxel index.</param>
        /// <param name="createChunkIfNotExists">If set to <c>true</c> create chunk if not exists.</param>
        public bool GetVoxelIndex (ref VoxelIndex index, int offsetX, int offsetY, int offsetZ, out VoxelIndex otherIndex, bool createChunkIfNotExists = false) {
            otherIndex = new VoxelIndex();
            if ((object)index.chunk == null) return false;
            Vector3d pos = index.chunk.position;
            int py = index.voxelIndex / ONE_Y_ROW;
            int pz = (index.voxelIndex / ONE_Z_ROW) & CHUNK_SIZE_MINUS_ONE;
            int px = index.voxelIndex & CHUNK_SIZE_MINUS_ONE;

            pos.y = pos.y - CHUNK_HALF_SIZE + py + offsetY;
            pos.z = pos.z - CHUNK_HALF_SIZE + pz + offsetZ;
            pos.x = pos.x - CHUNK_HALF_SIZE + px + offsetX;

            otherIndex.position = pos;

            return GetVoxelIndex(pos, out otherIndex.chunk, out otherIndex.voxelIndex, createChunkIfNotExists);
        }




        /// <summary>
        /// Gets the index of a voxel by its local x,y,z positions inside a voxel
        /// </summary>
        /// <returns>The voxel index.</returns>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public int GetVoxelIndex (int px, int py, int pz) {
            return py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
        }


        /// <summary>
        /// Gets the chunk and voxel index corresponding to certain offset to another chunk/voxel index. Useful to get a safe reference to voxels on top of others, etc.
        /// </summary>
        /// <returns><c>true</c>, if voxel index was gotten, <c>false</c> otherwise.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="offsetX">Offset x.</param>
        /// <param name="offsetY">Offset y.</param>
        /// <param name="offsetZ">Offset z.</param>
        /// <param name="otherChunk">Other chunk.</param>
        /// <param name="otherVoxelIndex">Other voxel index.</param>
        /// <param name="createChunkIfNotExists">If set to <c>true</c> create chunk if not exists.</param>
        public bool GetVoxelIndex (VoxelChunk chunk, int voxelIndex, int offsetX, int offsetY, int offsetZ, out VoxelChunk otherChunk, out int otherVoxelIndex, bool createChunkIfNotExists = false) {
            GetVoxelChunkCoordinates(voxelIndex, out int px, out int py, out int pz);

            // inside chunk?
            int qx = px + offsetX;
            int qy = py + offsetY;
            int qz = pz + offsetZ;
            if (qx >= 0 && qy >= 0 && qz >= 0 && qx < CHUNK_SIZE && qy < CHUNK_SIZE && qz < CHUNK_SIZE) {
                otherChunk = chunk;
                otherVoxelIndex = qy * ONE_Y_ROW + qz * ONE_Z_ROW + qx;
                return true;
            }

            Vector3d position;
            position.x = chunk.position.x - CHUNK_HALF_SIZE + 0.5 + qx;
            position.y = chunk.position.y - CHUNK_HALF_SIZE + 0.5 + qy;
            position.z = chunk.position.z - CHUNK_HALF_SIZE + 0.5 + qz;
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            if (GetChunkFast(chunkX, chunkY, chunkZ, out otherChunk, createChunkIfNotExists)) {
                py = (int)(position.y - chunkY * CHUNK_SIZE);
                pz = (int)(position.z - chunkZ * CHUNK_SIZE);
                px = (int)(position.x - chunkX * CHUNK_SIZE);
                otherVoxelIndex = py * ONE_Y_ROW + pz * ONE_Z_ROW + px;
                return true;
            } else {
                otherVoxelIndex = 0;
                return false;
            }
        }

        /// <summary>
        /// Returns the 9, 11 or 27 voxels around a position depending on voxelIndices length. Results are returned in an user-provided array of 9, 11 or 27 voxel indices organized in Y/Z/X sequence, starting from bottom to top, back to forward, left to right.
        /// </summary>
        /// <param name="faceOrientation">0 = front, 1 = right, 2 = back, 3 = left</param>
        public void GetVoxelNeighbourhood (Vector3d position, ref VoxelIndex[] voxelIndices, int faceOrientation = 0) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                GetVoxelNeighbourhood(chunk, voxelIndex, ref voxelIndices, faceOrientation);
            } else {
                voxelIndices.Fill(VoxelIndex.Null);
            }
        }

        /// <summary>
        /// Returns the 9, 11 or 27 voxels around a position depending on voxelIndices length. Results are returned in an user-provided array of 9, 11 or 27 voxel indices organized in Y/Z/X sequence, starting from bottom to top, back to forward, left to right.
        /// </summary>
        /// <param name="faceOrientation">0 = front, 1 = right, 2 = back, 3 = left</param>
        public void GetVoxelNeighbourhood (VoxelChunk chunk, int voxelIndex, ref VoxelIndex[] voxelIndices, int faceOrientation = 0) {
            int[] neighbourIndicesOrientationArray;
            switch (faceOrientation) {
                case 1: neighbourIndicesOrientationArray = neighbourIndicesOrientationArrayRight; break;
                case 2: neighbourIndicesOrientationArray = neighbourIndicesOrientationArrayBack; break;
                case 3: neighbourIndicesOrientationArray = neighbourIndicesOrientationArrayLeft; break;
                default: neighbourIndicesOrientationArray = neighbourIndicesOrientationArrayForward; break;
            }
            if (voxelIndices.Length >= 27) {
                GetVoxelNeighbourhood27(chunk, voxelIndex, ref voxelIndices, neighbourIndicesOrientationArray);
            } else if (voxelIndices.Length == 11) {
                GetVoxelNeighbourhood11(chunk, voxelIndex, ref voxelIndices, neighbourIndicesOrientationArray);
            } else {
                GetVoxelNeighbourhood9(chunk, voxelIndex, ref voxelIndices, neighbourIndicesOrientationArray);
            }
        }

        /// <summary>
        /// Gets the voxel position in world space coordinates.
        /// </summary>
        /// <returns>The voxel position.</returns>
        public Vector3d GetVoxelPosition (VoxelIndex voxelIndex) {
            return GetVoxelPosition(voxelIndex.chunk, voxelIndex.voxelIndex);
        }

        /// <summary>
        /// Gets the voxel position in world space coordinates.
        /// </summary>
        /// <returns>The voxel position.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public Vector3d GetVoxelPosition (VoxelChunk chunk, int voxelIndex) {
            GetVoxelChunkCoordinates(voxelIndex, out int px, out int py, out int pz);
            Vector3d position;
            position.x = chunk.position.x - CHUNK_HALF_SIZE + 0.5 + px;
            position.y = chunk.position.y - CHUNK_HALF_SIZE + 0.5 + py;
            position.z = chunk.position.z - CHUNK_HALF_SIZE + 0.5 + pz;
            return position;
        }



        /// <summary>
        /// Gets the corresponding voxel position in world space coordinates (the voxel position is exactly the center of the voxel).
        /// </summary>
        /// <returns>The voxel position.</returns>
        /// <param name="position">Any position in world space coordinates.</param>
        public Vector3d GetVoxelPosition (Vector3d position) {
            position.x = Math.Floor(position.x) + 0.5;
            position.y = Math.Floor(position.y) + 0.5;
            position.z = Math.Floor(position.z) + 0.5;
            return position;
        }



        /// <summary>
        /// Gets the voxel local position inside the chunk
        /// </summary>
        /// <returns>The voxel position.</returns>
        public Vector3 GetVoxelChunkPosition (int voxelIndex) {
            GetVoxelChunkCoordinates(voxelIndex, out int px, out int py, out int pz);
            Vector3 position;
            position.x = px - CHUNK_HALF_SIZE + 0.5f;
            position.y = py - CHUNK_HALF_SIZE + 0.5f;
            position.z = pz - CHUNK_HALF_SIZE + 0.5f;
            return position;
        }


        /// <summary>
        /// Gets the voxel position in world space coordinates.
        /// </summary>
        /// <returns>The voxel position.</returns>
        /// <param name="chunkPosition">Chunk position.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public Vector3d GetVoxelPosition (Vector3d chunkPosition, int voxelIndex) {
            GetVoxelChunkCoordinates(voxelIndex, out int px, out int py, out int pz);
            Vector3d position;
            position.x = chunkPosition.x - CHUNK_HALF_SIZE + 0.5 + px;
            position.y = chunkPosition.y - CHUNK_HALF_SIZE + 0.5 + py;
            position.z = chunkPosition.z - CHUNK_HALF_SIZE + 0.5 + pz;
            return position;
        }



        /// <summary>
        /// Gets the voxel position in world space coordinates.
        /// </summary>
        /// <returns>The voxel position.</returns>
        /// <param name="chunkPosition">Chunk position.</param>
        /// <param name="px">The x index of the voxel in the chunk.</param>
        /// <param name="py">The y index of the voxel in the chunk.</param>
        /// <param name="pz">The z index of the voxel in the chunk.</param>
        public Vector3d GetVoxelPosition (Vector3d chunkPosition, int px, int py, int pz) {
            Vector3d position;
            position.x = chunkPosition.x - CHUNK_HALF_SIZE + 0.5 + px;
            position.y = chunkPosition.y - CHUNK_HALF_SIZE + 0.5 + py;
            position.z = chunkPosition.z - CHUNK_HALF_SIZE + 0.5 + pz;
            return position;
        }

        /// <summary>
        /// Given a voxel index, returns its x, y, z position inside the chunk
        /// </summary>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="px">The x index of the voxel in the chunk.</param>
        /// <param name="py">The y index of the voxel in the chunk.</param>
        /// <param name="pz">The z index of the voxel in the chunk.</param>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public void GetVoxelChunkCoordinates (int voxelIndex, out int px, out int py, out int pz) {
            px = voxelIndex & CHUNK_SIZE_MINUS_ONE;
            py = voxelIndex / ONE_Y_ROW;
            pz = (voxelIndex / ONE_Z_ROW) & CHUNK_SIZE_MINUS_ONE;
        }


        /// <summary>
        /// Given a position, returns its x, y, z position inside the chunk
        /// </summary>
        /// <param name="position">Voxel position.</param>
        /// <param name="px">The x index of the voxel in the chunk.</param>
        /// <param name="py">The y index of the voxel in the chunk.</param>
        /// <param name="pz">The z index of the voxel in the chunk.</param>
        [MethodImpl(256)] // equals to MethodImplOptions.AggressiveInlining
        public void GetVoxelChunkCoordinates (Vector3d position, out int px, out int py, out int pz) {
            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            chunkX *= CHUNK_SIZE;
            chunkY *= CHUNK_SIZE;
            chunkZ *= CHUNK_SIZE;
            px = (int)(position.x - chunkX);
            py = (int)(position.y - chunkY);
            pz = (int)(position.z - chunkZ);
        }


        /// <summary>
        /// Returns true if the specified voxel has content and is visible from any of the 6 surrounding faces
        /// </summary>
        /// <returns><c>true</c>, if voxel is visible, <c>false</c> otherwise.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public bool GetVoxelVisibility (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null || chunk.voxels[voxelIndex].isEmpty)
                return false;

            GetVoxelChunkCoordinates(voxelIndex, out int px, out int py, out int pz);
            return GetVoxelVisibility(chunk, px, py, pz);
        }


        /// <summary>
        /// Returns true if the specified voxel has content and is visible from any of the 6 surrounding faces
        /// </summary>
        /// <returns><c>true</c>, if voxel is visible, <c>false</c> otherwise.</returns>
        /// <param name="chunk">Chunk.</param>
        bool GetVoxelVisibility (VoxelChunk chunk, int px, int py, int pz) {

            for (int o = 0; o < 6 * 3; o += 3) {
                VoxelChunk otherChunk = chunk;
                int ox = px + neighbourOffsets[o];
                int oy = py + neighbourOffsets[o + 1];
                int oz = pz + neighbourOffsets[o + 2];
                if (ox < 0) {
                    otherChunk = chunk.left;
                    ox = CHUNK_SIZE_MINUS_ONE;
                    if ((object)otherChunk == null)
                        return true;
                } else if (ox >= CHUNK_SIZE) {
                    ox = 0;
                    otherChunk = chunk.right;
                    if ((object)otherChunk == null)
                        return true;
                }
                if (oy < 0) {
                    otherChunk = chunk.bottom;
                    oy = CHUNK_SIZE_MINUS_ONE;
                    if ((object)otherChunk == null)
                        return true;
                } else if (oy >= CHUNK_SIZE) {
                    oy = 0;
                    otherChunk = chunk.top;
                    if ((object)otherChunk == null)
                        return true;
                }
                if (oz < 0) {
                    otherChunk = chunk.back;
                    oz = CHUNK_SIZE_MINUS_ONE;
                    if ((object)otherChunk == null)
                        return true;
                } else if (oz >= CHUNK_SIZE) {
                    oy = 0;
                    otherChunk = chunk.forward;
                    if ((object)otherChunk == null)
                        return true;
                }
                int otherIndex = GetVoxelIndex(ox, oy, oz);
                if (otherChunk.voxels[otherIndex].isEmpty)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Requests a refresh of a given chunk. The chunk mesh will be recreated and the lightmap will be computed again.
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="ignoreFrustum">If true, chunk will be rendered regardles of frustum visibility or distance</param>
        public void ChunkRedraw (VoxelChunk chunk, bool includeNeighbours = false, bool refreshLightmap = true, bool refreshMesh = true, bool ignoreFrustum = false) {
            if (includeNeighbours) {
                RefreshNeighbourhood(chunk, refreshMesh, refreshLightmap, ignoreFrustum: ignoreFrustum);
            } else {
                ChunkRequestRefresh(chunk, refreshLightmap, refreshMesh, ignoreFrustum);
            }
        }

        /// <summary>
        /// Requests a refresh of neighbours of a given chunk.
        /// </summary>
        /// <param name="ignoreFrustum">If true, chunk will be rendered regardles of frustum visibility or distance</param>
        public void ChunkRedrawNeighbours (VoxelChunk chunk, bool refreshLightmap = false, bool refreshMesh = true, bool ignoreFrustum = false) {
            RefreshNeighbourhood(chunk, refreshLightmap, refreshMesh, true, ignoreFrustum: ignoreFrustum);

        }

        /// <summary>
        /// Requests a refresh of all chunks within the given bounds. Chunks that don't exist will be created and populated.
        /// </summary>
        /// <param name="bounds">Bounds defining the region to redraw</param>
        /// <param name="ignoreFrustum">If true, chunk will be rendered regardless of frustum visibility or distance</param>
        public void ChunkRedraw (Boundsd bounds, bool refreshLightmap = true, bool refreshMesh = true, bool ignoreFrustum = false) {
            ChunkRequestRefresh(bounds, refreshLightmap, refreshMesh, ignoreFrustum);
        }

        /// <summary>
        /// Requests a global refresh of chunks within visible distance
        /// </summary>
        /// <param name="ignoreFrustum">If true, chunk will be rendered regardles of frustum visibility or distance</param>
        public void ChunkRedrawAll (bool refreshLightmap = true, bool refreshMesh = true, bool ignoreFrustum = false) {

            float d = _visibleChunksDistance * CHUNK_SIZE;
            Vector3 size = new Vector3(d, d, d);
            ChunkRequestRefresh(new Boundsd(currentAnchorPos, size), refreshLightmap, refreshMesh, ignoreFrustum);
        }

        /// <summary>
        /// Gets the voxel under a given position. Returns Voxel.Empty if no voxel found.
        /// </summary>
        /// <returns>The voxel under.</returns>
        /// <param name="position">Position.</param>
        public Voxel GetVoxelUnder (Vector3d position, bool includeWater = false, ColliderTypes colliderTypes = ColliderTypes.AnyCollider) {
            VoxelHitInfo hitinfo;
            byte minOpaque = includeWater ? (byte)255 : (byte)0;
            if (RayCastFast(position, Misc.vector3down, out hitinfo, 0, false, minOpaque, colliderTypes)) {
                return hitinfo.chunk.voxels[hitinfo.voxelIndex];
            }
            return Voxel.Empty;
        }


        /// <summary>
        /// Gets the voxel under a given position. Returns Voxel.Empty if no voxel found.
        /// </summary>
        /// <returns>The voxel under.</returns>
        /// <param name="position">Position.</param>
        public VoxelIndex GetVoxelUnderIndex (Vector3d position, bool includeWater = false, ColliderTypes colliderTypes = ColliderTypes.AnyCollider) {
            VoxelIndex index = new VoxelIndex();
            VoxelHitInfo hitinfo;
            byte minOpaque = includeWater ? (byte)255 : (byte)0;
            if (RayCastFast(position, Misc.vector3down, out hitinfo, 0, false, minOpaque, colliderTypes)) {
                index.chunk = hitinfo.chunk;
                index.voxelIndex = hitinfo.voxelIndex;
                index.position = hitinfo.point;
                index.sqrDistance = hitinfo.sqrDistance;
            }
            return index;
        }

        /// <summary>
        /// Changes/set the tint color of one voxel
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="color">Color.</param>
        public void VoxelSetColor (Vector3d position, Color32 color) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                VoxelSetColor(chunk, voxelIndex, color);
            }
        }

        /// <summary>
        /// Changes/set the tint color of one voxel
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="color">Color.</param>
        public void VoxelSetColor (VoxelChunk chunk, int voxelIndex, Color32 color) {
            if ((object)chunk == null) return;

#if UNITY_EDITOR
            CheckEditorTintColor();
#endif
            chunk.voxels[voxelIndex].color = color;
            RegisterChunkChanges(chunk);
            ChunkRequestRefresh(chunk, false, true);
        }


        /// <summary>
        /// Gets the tint color of one voxel
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public Color32 GetVoxelColor (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null) return Misc.color32White;

#if UNITY_EDITOR
            CheckEditorTintColor();
#endif
            return chunk.voxels[voxelIndex].color;
        }


        /// <summary>
        /// Gets the tint color of one voxel
        /// </summary>
        /// <param name="position">Position.</param>
        public Color32 GetVoxelColor (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                return GetVoxelColor(chunk, voxelIndex);
            }
            return Misc.color32White;
        }

        /// <summary>
        /// Sets if a voxel is hidden or not. By default, all voxels are visible. Hiding voxels is not permanent.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="hidden">If set to <c>true</c> hidden.</param>
        /// <param name="hiddenStyle">Hidden style.</param>
        public void VoxelSetHidden (Vector3 position, bool hidden, HideStyle hiddenStyle = HideStyle.DefinedByVoxelDefinition) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                VoxelSetHidden(chunk, voxelIndex, hidden, hiddenStyle);
            }
        }

        /// <summary>
        /// Sets if a voxel is hidden or not. By default, all voxels are visible. Hiding voxels is not permanent.
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public void VoxelSetHidden (VoxelChunk chunk, int voxelIndex, bool hidden, HideStyle hiddenStyle = HideStyle.DefinedByVoxelDefinition) {
            if ((object)chunk == null || voxelIndex < 0)
                return;

            VoxelSetHiddenOne(chunk, voxelIndex, hidden, hiddenStyle);
            ChunkRequestRefresh(chunk, false, true);
        }

        /// <summary>
        /// Sets if a list of voxels are hidden or not. By default, all voxels are visible. Hiding voxels is not permanent.
        /// </summary>
        /// <param name="indices">List of voxel indices.</param>
        public void VoxelSetHidden (List<VoxelIndex> indices, bool hidden, HideStyle hiddenStyle = HideStyle.DefinedByVoxelDefinition) {
            if (indices == null)
                return;

            VoxelChunk lastChunk = null;
            int count = indices.Count;
            for (int k = 0; k < count; k++) {
                if (indices[k].chunk != null && indices[k].voxelIndex >= 0) {
                    VoxelSetHiddenOne(indices[k].chunk, indices[k].voxelIndex, hidden, hiddenStyle);
                    if (indices[k].chunk != lastChunk) {
                        lastChunk = indices[k].chunk;
                        ChunkRequestRefresh(indices[k].chunk, false, true);
                    }
                }
            }
        }

        /// <summary>
        /// Sets if a list of voxels are hidden or not. By default, all voxels are visible. Hiding voxels is not permanent.
        /// </summary>
        /// <param name="indices">List of voxel indices.</param>
        public void VoxelSetHidden (VoxelIndex[] indices, int count, bool hidden, HideStyle hideStyle = HideStyle.DefinedByVoxelDefinition) {
            if (indices == null)
                return;

            VoxelChunk lastChunk = null;
            for (int k = 0; k < count; k++) {
                VoxelChunk chunk = indices[k].chunk;
                if (chunk != null && indices[k].voxelIndex >= 0) {
                    VoxelSetHiddenOne(chunk, indices[k].voxelIndex, hidden, hideStyle);
                    if (chunk != lastChunk) {
                        lastChunk = chunk;
                        ChunkRequestRefresh(chunk, false, true);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the voxel at the specified world position is hidden
        /// </summary>
        /// <param name="position">World position.</param>
        public bool VoxelIsHidden (Vector3 position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return VoxelIsHidden(chunk, voxelIndex);
            }
            return false;
        }


        /// <summary>
        /// Returns true if the voxel is hidden
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public bool VoxelIsHidden (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null || chunk.voxelsExtraData == null)
                return false;
            VoxelHiddenData data;
            if (chunk.voxelsExtraData.TryGetValue(voxelIndex, out data)) {
                return data.hidden;
            }
            return false;
        }

        #region Voxel user properties

        /// <summary>
        /// Clears any property on a specific voxel
        /// </summary>
        public void VoxelClearProperties (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk != null && chunk.voxelsProperties != null) {
                chunk.voxelsProperties.Remove(voxelIndex);
            }
        }

        /// <summary>
        /// Clears a specific voxel property on a specific voxel
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="voxelIndex"></param>
        /// <param name="propertyName"></param>
        public void VoxelClearProperty (VoxelChunk chunk, int voxelIndex, string propertyName) {
            int propertyId = propertyName.GetHashCode();
            VoxelClearProperty(chunk, voxelIndex, propertyId);
        }

        /// <summary>
        /// Clears a specific voxel property on a specific voxel
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="voxelIndex"></param>
        /// <param name="propertyName"></param>
        public void VoxelClearProperty (VoxelChunk chunk, int voxelIndex, int propertyId) {
            if ((object)chunk == null || chunk.voxelsProperties == null) {
                return;
            }
            FastHashSet<VoxelProperty> voxelProperties;
            if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out voxelProperties)) {
                return;
            }
            voxelProperties.Remove(propertyId);
        }

        /// <summary>
        /// Returns a custom property of float type from a voxel
        /// </summary>
        /// <returns></returns>
        public float GetVoxelPropertyFloat (VoxelChunk chunk, int voxelIndex, string propertyName) {
            int propertyId = propertyName.GetHashCode();
            return GetVoxelPropertyFloat(chunk, voxelIndex, propertyId);
        }


        /// <summary>
        /// Returns a custom property of float type from a voxel
        /// </summary>
        /// <returns></returns>
        public float GetVoxelPropertyFloat (VoxelChunk chunk, int voxelIndex, int propertyId) {
            if ((object)chunk == null || chunk.voxelsProperties == null) {
                return 0;
            }
            VoxelProperty property = VoxelGetProperty(chunk, voxelIndex, propertyId);
            return property.floatValue;
        }


        /// <summary>
        /// Returns a custom property of string type from a voxel
        /// </summary>
        /// <returns></returns>
        public string GetVoxelPropertyString (VoxelChunk chunk, int voxelIndex, string propertyName) {
            int propertyId = propertyName.GetHashCode();
            return GetVoxelPropertyString(chunk, voxelIndex, propertyId);
        }

        /// <summary>
        /// Returns a custom property of string type from a voxel
        /// </summary>
        /// <returns></returns>
        public string GetVoxelPropertyString (VoxelChunk chunk, int voxelIndex, int propertyId) {
            if ((object)chunk == null || chunk.voxelsProperties == null) {
                return null;
            }
            VoxelProperty property = VoxelGetProperty(chunk, voxelIndex, propertyId);
            return property.stringValue;
        }

        /// <summary>
        /// Sets an integer voxel property
        /// </summary>
        public void VoxelSetProperty (VoxelChunk chunk, int voxelIndex, string propertyName, float value) {
            int propertyId = propertyName.GetHashCode();
            VoxelSetProperty(chunk, voxelIndex, propertyId, value);
        }

        /// <summary>
        /// Sets an integer voxel property
        /// </summary>
        public void VoxelSetProperty (VoxelChunk chunk, int voxelIndex, int propertyId, float value) {
            if ((object)chunk == null) {
                return;
            }
            if (chunk.voxelsProperties == null) {
                chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
            }
            FastHashSet<VoxelProperty> voxelProperties;
            if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out voxelProperties)) {
                voxelProperties = new FastHashSet<VoxelProperty>();
                chunk.voxelsProperties[voxelIndex] = voxelProperties;
            }
            voxelProperties.TryGetValue(propertyId, out VoxelProperty prop);
            prop.floatValue = value;
            voxelProperties[propertyId] = prop;
            chunk.SetModified(modificationTag);
            RegisterChunkChanges(chunk);
        }

        /// <summary>
        /// Sets an string voxel property
        /// </summary>
        public void VoxelSetProperty (VoxelChunk chunk, int voxelIndex, string propertyName, string value) {
            int propertyId = propertyName.GetHashCode();
            VoxelSetProperty(chunk, voxelIndex, propertyId, value);
        }

        /// <summary>
        /// Sets an string voxel property
        /// </summary>
        public void VoxelSetProperty (VoxelChunk chunk, int voxelIndex, int propertyId, string value) {
            if ((object)chunk == null) {
                return;
            }
            if (chunk.voxelsProperties == null) {
                chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
            }
            if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                voxelProperties = new FastHashSet<VoxelProperty>();
                chunk.voxelsProperties[voxelIndex] = voxelProperties;
            }
            VoxelProperty property = new VoxelProperty();
            property.stringValue = value;
            voxelProperties[propertyId] = property;
            RegisterChunkChanges(chunk);
        }

        /// <summary>
        /// Sets a voxel property
        /// </summary>
        public void VoxelSetProperty (VoxelChunk chunk, int voxelIndex, int propertyId, VoxelProperty property) {
            if ((object)chunk == null) {
                return;
            }
            if (chunk.voxelsProperties == null) {
                chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
            }
            if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                voxelProperties = new FastHashSet<VoxelProperty>();
                chunk.voxelsProperties[voxelIndex] = voxelProperties;
            }
            voxelProperties[propertyId] = property;
            RegisterChunkChanges(chunk);
        }

        /// <summary>
        /// Class to store a voxel property match result
        /// </summary>
        public class VoxelPropertyMatch {
            public VoxelChunk chunk;
            public int voxelIndex;
            public VoxelProperty property;
        }

        /// <summary>
        /// Queries all loaded chunks for voxels with a specific property and returns their locations and values
        /// </summary>
        /// <param name="propertyName">The name of the property to query</param>
        /// <returns>List of matches containing chunks, indices and property values</returns>
        public void VoxelQueryProperty (string propertyName, List<VoxelPropertyMatch> results) {
            if (results == null) return;
            int propertyId = propertyName.GetHashCode();
            VoxelQueryProperty(propertyId, results);
        }

        /// <summary>
        /// Queries all loaded chunks for voxels with a specific property ID and returns their locations and values
        /// </summary>
        /// <param name="propertyId">The ID of the property to query</param>
        /// <returns>List of matches containing chunks, indices and property values</returns>
        public void VoxelQueryProperty (int propertyId, List<VoxelPropertyMatch> results) {
            if (results == null) return;

            // Get all loaded chunks
            List<VoxelChunk> chunks = BufferPool<VoxelChunk>.Get();

            foreach (VoxelChunk chunk in chunks) {
                if (chunk == null || chunk.voxelsProperties == null) continue;

                // Iterate through all voxels with properties in this chunk
                foreach (KeyValuePair<int, FastHashSet<VoxelProperty>> entry in chunk.voxelsProperties) {

                    int voxelIndex = entry.Key;
                    FastHashSet<VoxelProperty> voxelProperties = entry.Value;

                    if (voxelProperties == null) continue;

                    // Check if this voxel has the requested property
                    if (voxelProperties.TryGetValue(propertyId, out VoxelProperty property)) {
                        // Add to results
                        results.Add(new VoxelPropertyMatch {
                            chunk = chunk,
                            voxelIndex = voxelIndex,
                            property = property
                        });
                    }
                }
            }

            BufferPool<VoxelChunk>.Release(chunks);
        }

        #endregion


        /// <summary>
        /// Places a new voxel on a givel position in world space coordinates. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel.</param>
        public void VoxelPlace (Vector3d position, VoxelDefinition voxelType) {
            VoxelPlace(position, voxelType, false, true);
        }

        /// <summary>
        /// Places a new voxel on a givel position in world space coordinates. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel.</param>
        /// <param name="playSound">If set to <c>true</c> play sound.</param>
        public void VoxelPlace (Vector3d position, VoxelDefinition voxelType, bool playSound) {
            VoxelPlace(position, voxelType, playSound, true);
        }

        /// <summary>
        /// Places a new voxel on a givel position in world space coordinates. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel.</param>
        /// <param name="playSound">If set to <c>true</c> play sound.</param>
        public void VoxelPlace (Vector3d position, VoxelDefinition voxelType, bool playSound = false, bool refresh = true) {
            if (voxelType == null)
                return;
            VoxelPlace(position, voxelType, playSound, voxelType.tintColor, refresh: refresh);
        }

        /// <summary>
        /// Places a new voxel on a givel position in world space coordinates. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel.</param>
        /// <param name="tintColor">Tint color.</param>
        /// <param name="playSound">If set to <c>true</c> play sound.</param>
        public void VoxelPlace (Vector3d position, VoxelDefinition voxelType, Color tintColor, bool playSound = false, bool refresh = true) {
            VoxelPlace(position, voxelType, playSound, tintColor, refresh: refresh);
        }

        /// <summary>
        /// Places a default voxel on a givel position in world space coordinates with specified color. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="tintColor">The tint color for the voxel.</param>
        /// <param name="playSound">If set to <c>true</c> play sound when placing the voxel.</param>
        public void VoxelPlace (Vector3d position, Color tintColor, bool playSound = false, bool refresh = true) {
            VoxelPlace(position, defaultVoxel, playSound, tintColor, refresh: refresh);
        }


        /// <summary>
        /// Places a default voxel on an existing chunk with specified color. Optionally plays a sound.
        /// </summary>
        /// <param name="chunk">The chunk object.</param>
        /// <param name="voxelIndex">The index of the voxel.</param>
        /// <param name="tintColor">The tint color for the voxel.</param>
        /// <param name="playSound">If set to <c>true</c> play sound when placing the voxel.</param>
        public void VoxelPlace (VoxelChunk chunk, int voxelIndex, Color tintColor, bool playSound = false, bool refresh = true) {
            Vector3d position = GetVoxelPosition(chunk, voxelIndex);
            VoxelPlace(position, defaultVoxel, playSound, tintColor, refresh: refresh);
        }

        /// <summary>
        /// Places a list of voxels within a given chunk. List of voxels is given by a list of ModelBit structs.
        /// </summary>
        /// <param name="chunk">The chunk object.</param>
        /// <param name="voxels">The list of voxels to insert into the chunk.</param>
        public void VoxelPlace (VoxelChunk chunk, List<ModelBit> voxels) {
            ModelPlace(chunk, voxels);
        }


        /// <summary>
        /// Places a voxel at a given position.
        /// </summary>
        /// /// <param name="playSound">If set to <c>true</c> play sound when placing the voxel.</param>
        public void VoxelPlace (Vector3d position, Voxel voxel, bool playSound = false, bool refresh = true) {
            VoxelPlace(position, voxelDefinitions[voxel.typeIndex], voxel.color, playSound, refresh: refresh);
        }


        /// <summary>
        /// Places the specified voxel type and color on multiple voxel indices (possibly across multiple chunks).
        /// Returns true if all voxels could be placed, false otherwise.
        /// </summary>
        /// <param name="indices">The list of voxel indices to place the voxel on.</param>
        /// <param name="voxelType">The voxel type to place.</param>
        /// <param name="tintColor">The color to place the voxel in.</param>
        /// <param name="refresh">If true, the chunks will be refreshed.</param>
        /// <param name="placeMicroVoxels">If true, the microvoxels will be placed.</param>
        /// <param name="microVoxels">The microvoxels to place.</param>
        /// <param name="slabMode">If true, the voxel will be placed on the slab mode.</param>
        /// <returns>True if all voxels could be placed, false otherwise.</returns>
        public bool VoxelPlace (List<VoxelIndex> indices, VoxelDefinition voxelType, Color tintColor = default, bool refresh = true, bool placeMicroVoxels = true, MicroVoxels microVoxels = null, bool slabMode = false) {

            if (indices == null || indices.Count == 0 || voxelType == null) return false;

            // Ensure voxel definition is registered
            if (voxelType.index <= 0) AddVoxelDefinition(voxelType);

            // Place using VoxelPlaceFast to keep single-source semantics
            int n = indices.Count;
            bool allPlaced = true;
            modificationTag++;
            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();

            for (int i = 0; i < n; i++) {
                VoxelIndex vi = indices[i];
                VoxelChunk chunk = vi.chunk;
                int voxelIndex = vi.voxelIndex;
                if ((object)chunk == null || voxelIndex < 0 || voxelIndex >= chunk.voxels.Length) { allPlaced = false; continue; }

                Vector3d pos = GetVoxelPosition(chunk, voxelIndex);
                // Defer refresh; we'll refresh all touched chunks in one go below
                bool placed = VoxelPlaceFast(pos, voxelType, out VoxelChunk placedChunk, out int _, tintColor, 1f, 0, refresh: false, placeMicroVoxels: placeMicroVoxels, microVoxels: microVoxels, slabMode: slabMode);
                if (!placed) { allPlaced = false; continue; }

                if (placedChunk != null && placedChunk.SetModified(modificationTag)) {
                    updatedChunks.Add(placedChunk);
                }
            }

            // Batch refresh/light and notify changes once
            if (updatedChunks.Count > 0) {
                RegisterChunkChanges(updatedChunks);
                if (refresh) ChunkRequestRefresh(updatedChunks, true, true);
            }
            BufferPool<VoxelChunk>.Release(updatedChunks);
            return allPlaced;
        }




        /// <summary>
        /// Places a new voxel on a givel position in world space coordinates. Optionally plays a sound.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxelType">Voxel.</param>
        /// <param name="playSound">If set to <c>true</c> play sound when placing the voxel.</param>
        /// <param name="tintColor">The tint color for the voxel.</param>
        /// <param name="amount">Only used to place a specific amount of water-like voxels (0-1).</param>
        /// <param name="rotation">Rotation turns. Can be 0, 1, 2 or 3 and represents clockwise 90-degree step rotations.</param>
        /// <param name="refresh">If affected chunks should be refreshed</param>
        /// <param name="placeMicroVoxel">If true and the voxel definition has microvoxels, place microvoxels.</param>
        /// <param name="microVoxels">Optional microvoxel data to use for placement. If not provided, and placeMicroVoxels is true, the microvoxels associated to the voxel type will be used (if defined).</param>
        /// <param name="slabMode">If true, the voxel will be placed on the slab mode.</param>
        public bool VoxelPlace (Vector3d position, VoxelDefinition voxelType, bool playSound = false, Color tintColor = default, float amount = 1f, int rotation = 0, bool refresh = true, bool placeMicroVoxels = true, MicroVoxels microVoxels = null, bool slabMode = false) {

            if (voxelType == null) {
                return true;
            }

#if UNITY_EDITOR
            if (!enableTinting && tintColor != default && tintColor != Misc.colorWhite) {
                Debug.Log("Option enableTinting is disabled. To use colored voxels, please enable the option in the VoxelPlayEnvironment component inspector.");
            }
#endif

            // Check Connected Voxels rules
            if (voxelType.customVoxelDefinitionProvider != null) {
                voxelType = voxelType.customVoxelDefinitionProvider(position, voxelType, rotation);
                if (voxelType == null) return false;
            }

            // Check if voxelType is known
            if (voxelType.index <= 0) {
                AddVoxelDefinition(voxelType);
            }

            if (captureEvents && OnVoxelBeforePlace != null) {
                if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                    Color32 tintColor32 = tintColor;
                    OnVoxelBeforePlace(position, chunk, voxelIndex, ref voxelType, ref tintColor32);
                    tintColor = tintColor32;
                    if (voxelType == null) return false;
                }
            }

            if (!_bypassPlacementAnim && placementAnimation != PlacementAnimationMode.None && applicationIsPlaying && !serverMode) {
                EnsurePlacementAnimator();
                if (_placementAnimator != null && _placementAnimator.enabled) {
                    var req = new PlacementRequest {
                        position = position,
                        voxelType = voxelType,
                        tintColor = tintColor,
                        amount = amount,
                        rotation = rotation,
                        refresh = refresh,
                        placeMicroVoxels = placeMicroVoxels,
                        microVoxels = microVoxels,
                        slabMode = slabMode,
                        playSound = playSound
                    };
                    return _placementAnimator.Schedule(req);
                }
            }

            if (playSound) {
                PlayBuildSound(voxelType.buildSound, position);
            }

            bool prevBypass = _bypassPlacementAnim;
            _bypassPlacementAnim = true; // ensure OnVoxelBeforePlace is not called
            bool result = VoxelPlaceFast(position, voxelType, out _, out _, tintColor, amount, rotation, refresh, placeMicroVoxels, microVoxels, slabMode);
            _bypassPlacementAnim = prevBypass;
            return result;
        }



        /// <summary>
        /// Returns true if a voxel placed on a given position would overlap any collider.
        /// </summary>
        public bool VoxelOverlaps (Vector3d position, VoxelDefinition type, Quaternion rotation, int layerMask = -1, bool slabMode = false) {
            // Check if the voxel will overlap any collider then 
            if (type.renderType == RenderType.Custom && type.prefabUsesCollider) {

                Bounds bounds = type.prefabColliderBounds;
                Vector3 extents = bounds.extents;
                FastVector.Multiply(ref extents, ref type.scale, 0.9f);

                Quaternion rot = type.GetRotation(position);

                Vector3 localPosition = bounds.center;
                localPosition = rot * localPosition;
                localPosition += type.GetOffset(position);
                localPosition = rotation * localPosition;
                position += localPosition;

                int collidersCount = Physics.OverlapBoxNonAlloc(position, extents, tempColliders, rotation * rot, layerMask, QueryTriggerInteraction.Ignore);
                return collidersCount > 0;
            }
            Vector3 size = new Vector3(0.45f, 0.45f, 0.45f);
            if (slabMode) {
                size.y *= 0.5f;
            }
            return Physics.OverlapBoxNonAlloc(position, size, tempColliders, Misc.quaternionZero, layerMask, QueryTriggerInteraction.Ignore) > 0;
        }


        /// <summary>
        /// Puts lots of voxels in the given positions. Takes care of informing neighbour chunks.
        /// </summary>
        /// <param name="positions">Positions.</param>
        /// <param name="voxelType">Voxel type.</param>
        /// <param name="tintColor">Tint color.</param>
        public void VoxelPlace (List<Vector3d> positions, VoxelDefinition voxelType, Color32 tintColor = default, List<VoxelChunk> modifiedChunks = null) {

            if (tintColor.Equals(default(Color32))) {
                tintColor = Misc.color32White;
            }

            VoxelChunk chunk;
            int voxelIndex;
            int count = positions.Count;

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            if (voxelType == null) {
                for (int k = 0; k < count; k++) {
                    Vector3d position = positions[k];
                    if (GetVoxelIndex(position, out chunk, out voxelIndex, false)) {
                        ClearLightmapAtPosition(chunk, voxelIndex);
                        VoxelDestroyFastSingle(chunk, voxelIndex);
                        if (chunk.SetModified(modificationTag)) {
                            updatedChunks.Add(chunk);
                        }
                    }
                }
            } else {
                for (int k = 0; k < count; k++) {
                    Vector3d position = positions[k];
                    if (GetVoxelIndex(position, out chunk, out voxelIndex)) {
                        ClearLightmapAtPosition(chunk, voxelIndex);
                        if (captureEvents && OnVoxelBeforePlace != null) {
                            OnVoxelBeforePlace(position, chunk, voxelIndex, ref voxelType, ref tintColor);
                            if (voxelType == null)
                                continue;
                        }
                        chunk.voxels[voxelIndex].Set(voxelType, tintColor);

                        if (chunk.SetModified(modificationTag)) {
                            updatedChunks.Add(chunk);
                        }

                        if (captureEvents && OnVoxelAfterPlace != null) {
                            OnVoxelAfterPlace(position, chunk, voxelIndex);
                        }
                    }
                }
            }

            RegisterChunkChanges(updatedChunks);
            ChunkRequestRefresh(updatedChunks, true, true);

            if (modifiedChunks != null) {
                modifiedChunks.AddRange(updatedChunks);
            }

            BufferPool<VoxelChunk>.Release(updatedChunks);
        }



        /// <summary>
        /// Puts lots of voxels in the given positions. Takes care of informing neighbour chunks.
        /// </summary>
        /// <param name="indices">Array of voxel indices for placing.</param>
        /// <param name="voxelType">Voxel type.</param>
        /// <param name="tintColor">Tint color.</param>
        /// <param name="modifiedChunks">Optionally return the list of modified chunks</param>
        public void VoxelPlace (List<VoxelIndex> indices, VoxelDefinition voxelType, Color tintColor = default, List<VoxelChunk> modifiedChunks = null) {

            if (tintColor.Equals(default(Color))) {
                tintColor = Misc.colorWhite;
            }

            int count = indices.Count;

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            if (voxelType == null) {
                byte light = noLightValue;
                for (int k = 0; k < count; k++) {
                    VoxelIndex vi = indices[k];
                    vi.chunk.ClearVoxel(vi.voxelIndex, light);
                    if (vi.chunk.SetModified(modificationTag)) {
                        updatedChunks.Add(vi.chunk);
                    }
                }
            } else {
                for (int k = 0; k < count; k++) {
                    VoxelIndex vi = indices[k];
                    vi.chunk.SetVoxel(vi.voxelIndex, voxelType, tintColor);
                    ClearLightmapAtPosition(vi.chunk, vi.voxelIndex);
                    if (vi.chunk.SetModified(modificationTag)) {
                        updatedChunks.Add(vi.chunk);
                    }
                }
            }

            RegisterChunkChanges(updatedChunks);
            ChunkRequestRefresh(updatedChunks, true, true);

            if (modifiedChunks != null) {
                modifiedChunks.AddRange(updatedChunks);
            }

            BufferPool<VoxelChunk>.Release(updatedChunks);
        }


        /// <summary>
        /// Fills an area with same voxel definition and optional tint color
        /// </summary>
        /// <param name="boxMin">Box minimum.</param>
        /// <param name="boxMax">Box max.</param>
        /// <param name="voxelType">Voxel type.</param>
        /// <param name="modifiedChunks">Optionally return the list of modified chunks</param>
        public void VoxelPlace (Vector3d boxMin, Vector3d boxMax, VoxelDefinition voxelType, List<VoxelChunk> modifiedChunks = null) {
            VoxelPlace(boxMin, boxMax, voxelType, Misc.colorWhite, modifiedChunks);
        }


        /// <summary>
        /// Fills an area with same voxel definition and optional tint color
        /// </summary>
        /// <param name="boxMin">Box minimum.</param>
        /// <param name="boxMax">Box max.</param>
        /// <param name="voxelType">Voxel type.</param>
        /// <param name="tintColor">Tint color.</param>
        /// <param name="modifiedChunks">Optionally return the list of modified chunks</param>
        public void VoxelPlace (Vector3d boxMin, Vector3d boxMax, VoxelDefinition voxelType, Color tintColor, List<VoxelChunk> modifiedChunks = null) {
            List<VoxelIndex> tempVoxelIndices = BufferPool<VoxelIndex>.Get();
            GetVoxelIndices(boxMin, boxMax, tempVoxelIndices, 0, -1);
            VoxelPlace(tempVoxelIndices, voxelType, tintColor, modifiedChunks);
            BufferPool<VoxelIndex>.Release(tempVoxelIndices);
        }


        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, int sizeX, int sizeY, int sizeZ) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, sizeX, sizeY, sizeZ, Misc.vector3zero, Misc.vector3one);
        }

        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        /// <param name="offset">Mesh offset.</param>
        /// <param name="scale">Mesh scale.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, int sizeX, int sizeY, int sizeZ, Vector3 offset, Vector3 scale) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, sizeX, sizeY, sizeZ, offset, scale);
        }


        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="texture">Optional texture for all entries</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        /// <param name="offset">Mesh offset.</param>
        /// <param name="scale">Mesh scale.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, Texture2D texture, int sizeX, int sizeY, int sizeZ, Vector3 offset, Vector3 scale) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, texture, null, sizeX, sizeY, sizeZ, offset, scale);
        }


        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="textures">Optional texture for each entry</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        /// <param name="offset">Mesh offset.</param>
        /// <param name="scale">Mesh scale.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, Texture2D[] textures, int sizeX, int sizeY, int sizeZ, Vector3 offset, Vector3 scale) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, textures, null, sizeX, sizeY, sizeZ, offset, scale);
        }

        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="textures">Optional texture for each entry</param>
        /// <param name="normalMaps">Optional normal maps for each entry</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        /// <param name="offset">Mesh offset.</param>
        /// <param name="scale">Mesh scale.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, Texture2D texture, Texture2D normalMap, int sizeX, int sizeY, int sizeZ, Vector3 offset, Vector3 scale) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, texture, normalMap, sizeX, sizeY, sizeZ, offset, scale);
        }


        /// <summary>
        /// Creates an optimized voxel game object from an array of colors
        /// </summary>
        /// <returns>The create game object.</returns>
        /// <param name="colors">Colors in Y/Z/X distribution.</param>
        /// <param name="textures">Optional texture for each entry</param>
        /// <param name="normalMaps">Optional normal maps for each entry</param>
        /// <param name="sizeX">Size x.</param>
        /// <param name="sizeY">Size y.</param>
        /// <param name="sizeZ">Size z.</param>
        /// <param name="offset">Mesh offset.</param>
        /// <param name="scale">Mesh scale.</param>
        public GameObject VoxelCreateGameObject (Color32[] colors, Texture2D[] textures, Texture2D[] normalMaps, int sizeX, int sizeY, int sizeZ, Vector3 offset, Vector3 scale) {
            return VoxelPlayConverter.GenerateVoxelObject(colors, textures, normalMaps, sizeX, sizeY, sizeZ, offset, scale);
        }


        /// <summary>
        /// Creates an optimized voxel game object from a model definition
        /// </summary>
        /// <param name="modelDefinition"></param>
        /// <param name="offset"></param>
        /// <param name="scale"></param>
        /// <param name="useTextures"></param>
        /// <param name="useNormals"></param>
        /// <returns></returns>
        public GameObject VoxelCreateGameObject (ModelDefinition modelDefinition, bool useTextures = true, bool useNormals = true) {
            return VoxelPlayConverter.GenerateVoxelObject(modelDefinition, Misc.vector3zero, Misc.vector3one, useTextures, useNormals);
        }

        /// <summary>
        /// Creates an optimized voxel game object from a model definition
        /// </summary>
        /// <param name="modelDefinition"></param>
        /// <param name="offset"></param>
        /// <param name="scale"></param>
        /// <param name="useTextures"></param>
        /// <param name="useNormals"></param>
        /// <returns></returns>
        public GameObject VoxelCreateGameObject (ModelDefinition modelDefinition, Vector3 offset, Vector3 scale, bool useTextures = true, bool useNormals = true) {
            return VoxelPlayConverter.GenerateVoxelObject(modelDefinition, offset, scale, useTextures, useNormals);
        }


        /// <summary>
        /// Returns a cube gameobject based on the voxel at a given position
        /// </summary>
        public GameObject VoxelToCube (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return null;
            return MakeCubeFromVoxel(chunk, voxelIndex);
        }

        /// <summary>
        /// Returns a cube gameobject based on the voxel at a given position
        /// </summary>
        public GameObject VoxelToCube (VoxelChunk chunk, int voxelIndex) {
            return MakeCubeFromVoxel(chunk, voxelIndex);
        }

        /// <summary>
        /// Damages a voxel.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public bool VoxelDamage (Vector3d position, int damage, bool playSound = false) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false) || chunk.voxels[voxelIndex].isEmpty)
                return false;
            bool impact = HitVoxelFast(position, Misc.vector3down, damage, out _, 1, 1, false, playSound, ignoreWater: IgnoreWaterOption.IncludeWater);
            return impact;
        }


        /// <summary>
        /// Damages a voxel.
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public bool VoxelDamage (Vector3d position, int damage, bool addParticles, bool playSound) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false) || chunk.voxels[voxelIndex].isEmpty)
                return false;
            bool impact = HitVoxelFast(position, Misc.vector3down, damage, out _, 1, 1, addParticles, playSound, ignoreWater: IgnoreWaterOption.IncludeWater);
            return impact;
        }


        /// <summary>
        /// Damages a voxel from a certain direction
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public bool VoxelDamage (Vector3d position, Vector3 hitDirection, int damage, bool addParticles = false, bool playSound = false) {
            if (!GetVoxelIndex(position, out VoxelChunk _, out _)) {
                return false;
            }

            return HitVoxelFast(position - hitDirection, hitDirection, damage, out _, addParticles: addParticles, playSound: playSound, ignoreWater: IgnoreWaterOption.IncludeWater);
        }



        /// <summary>
        /// Damages a voxel from a certain direction
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        /// <param name="voxelPosition">Position of the voxel.</param>
        /// <param name="hitPoint">Position of the hit.</param>
        /// <param name="normal">Normal of the voxel surface.</param>
        public bool VoxelDamage (Vector3d voxelPosition, Vector3d hitPoint, Vector3 normal, int damage, bool addParticles = false, bool playSound = false) {
            if (!BuildVoxelHitInfo(out VoxelHitInfo hitInfo, voxelPosition, hitPoint, normal)) return false;
            return VoxelDamage(hitInfo, damage, addParticles, playSound);
        }



        /// <summary>
        /// Damages a voxel using data from a VoxelHitInfo struct
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        public bool VoxelDamage (VoxelHitInfo hitInfo, int damage, bool addParticles = false, bool playSound = false) {
            if ((object)hitInfo.chunk == null || hitInfo.voxelIndex < 0)
                return false;
            DamageVoxelFast(ref hitInfo, damage, addParticles, playSound);
            return true;
        }


        /// <summary>
        /// Damages a voxel using data from a VoxelHitInfo struct
        /// </summary>
        /// <returns><c>true</c>, if voxel was hit, <c>false</c> otherwise.</returns>
        public bool VoxelDamage (VoxelHitInfo hitInfo, int damage, bool addParticles = false, bool playSound = false, bool showDamageCracks = true, bool canAddRecoverableVoxel = true) {
            if ((object)hitInfo.chunk == null || hitInfo.voxelIndex < 0)
                return false;
            DamageVoxelFast(ref hitInfo, damage, addParticles, playSound, showDamageCracks, canAddRecoverableVoxel);
            return true;
        }


        /// <summary>
        /// Simulates an explosion at a given position damaging every voxel within radius.
        /// </summary>
        /// <returns><c>int</c>, the number of damaged voxels<c>false</c> otherwise.</returns>
        /// <param name="origin">Explosion origin.</param>
        /// <param name="damage">Maximm damage.</param>
        /// <param name="radius">Radius.</param>
        /// <param name="attenuateDamageWithDistance">If set to <c>true</c> damage will be reduced with distance.</param>
        /// <param name="addParticles">If set to <c>true</c> damage particles will be added.</param>
        /// <param name="showDamageCracks">If set to <c>true</c> damage cracks will be shown.</param>
        /// <param name="canAddRecoverableVoxel">If true, when voxel is destroyed, a floating recoverable voxel can be dropped.</param>
        public int VoxelDamage (Vector3d origin, int damage, int radius, bool attenuateDamageWithDistance, bool addParticles, bool playSound = false, bool showDamageCracks = false, bool canAddRecoverableVoxel = true) {
            return DamageAreaFast(origin, damage, radius, attenuateDamageWithDistance, addParticles, null, playSound, showDamageCracks, canAddRecoverableVoxel);
        }

        /// <summary>
        /// Simulates an explosion at a given position damaging every voxel within radius.
        /// </summary>
        /// <returns><c>int</c>, the number of damaged voxels<c>false</c> otherwise.</returns>
        /// <param name="origin">Explosion origin.</param>
        /// <param name="damage">Maximm damage.</param>
        /// <param name="radius">Radius.</param>
        /// <param name="attenuateDamageWithDistance">If set to <c>true</c> damage will be reduced with distance.</param>
        /// <param name="addParticles">If set to <c>true</c> damage particles will be added.</param>
        /// <param name="damagedVoxels">Pass an already initialized list to return which voxels have been damaged.</param>
        /// <param name="canAddRecoverableVoxel">If true, when voxel is destroyed, a floating recoverable voxel can be dropped.</param>
        public int VoxelDamage (Vector3d origin, int damage, int radius, bool attenuateDamageWithDistance, bool addParticles, List<VoxelIndex> damagedVoxels, bool playSound = false, bool showDamageCracks = false, bool canAddRecoverableVoxel = true) {
            return DamageAreaFast(origin, damage, radius, attenuateDamageWithDistance, addParticles, damagedVoxels, playSound, showDamageCracks, canAddRecoverableVoxel);
        }


        /// <summary>
        /// Clears a voxel.
        /// </summary>
        /// <returns><c>true</c>, if voxel was destroyed, <c>false</c> otherwise.</returns>
        public bool VoxelDestroy (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null)
                return false;
            if (chunk.voxels[voxelIndex].hasContent) {
                VoxelDestroyFast(chunk, voxelIndex, canSpreadWater);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears a voxel.
        /// </summary>
        /// <returns><c>true</c>, if voxel was destroyed, <c>false</c> otherwise.</returns>
        /// <param name="position">Position.</param>
        public bool VoxelDestroy (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                return false;
            }
            if (chunk.voxels[voxelIndex].hasContent) {
                VoxelDestroyFast(chunk, voxelIndex, canSpreadWater);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys all voxels within a given bounds
        /// </summary>
        public bool VoxelDestroy (Bounds bounds) {
            List<VoxelIndex> voxelIndices = BufferPool<VoxelIndex>.Get();
            Vector3d min = bounds.min + Misc.vector3one * 0.1f;
            Vector3d max = bounds.max - Misc.vector3one * 0.1f;
            GetVoxelIndices(min, max, voxelIndices);
            bool spreadWater = canSpreadWater;
            foreach (var vi in voxelIndices) {
                VoxelDestroyFast(vi.chunk, vi.voxelIndex, spreadWater);
            }
            BufferPool<VoxelIndex>.Release(voxelIndices);
            return true;
        }


        /// <summary>
        /// Makes all voxels with "willCollapse" flag on top of certain position to collapse and fall
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="amount">Maximum number of voxels to collapse.</param>
        /// <param name="voxelIndices">If voxelIndices is provided, it will be filled with the collapsing voxels.</param>
        /// <param name="consolidateDelay">If consolidateDelay is greater than 0, collapsed voxels will be either destroyed or converted back to normal voxels after 'duration' in seconds.</param>
        public void VoxelCollapse (Vector3d position, int amount, List<VoxelIndex> voxelIndices = null, float consolidateDelay = 0, bool destroyOnCollapse = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {
            List<VoxelIndex> tempVoxelIndices = BufferPool<VoxelIndex>.Get();
            int count = GetCrumblyVoxelIndices(position, amount, tempVoxelIndices);

            if (voxelIndices != null) {
                voxelIndices.Clear();
                voxelIndices.AddRange(tempVoxelIndices);
            }
            if (count > 0) {
                VoxelGetDynamic(tempVoxelIndices, true, consolidateDelay, destroyOnCollapse, addParticlesOnDestroy, playSoundOnDestroy);
            }
            if (captureEvents && OnVoxelCollapse != null) {
                OnVoxelCollapse(tempVoxelIndices);
            }
            BufferPool<VoxelIndex>.Release(tempVoxelIndices);
        }

        /// <summary>
        /// Returns true if a given voxel is dynamic
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public bool VoxelIsDynamic (VoxelChunk chunk, int voxelIndex) {

            VoxelDefinition vd = GetVoxelDefinition(chunk.voxels[voxelIndex].typeIndex);
            if (vd == null) return false; // No definition = not dynamic

            return vd.isDynamic;
        }


        /// <summary>
        /// Converts a dynamic voxel back to normal voxel. This operation will result in voxel being destroyed if it the current voxel position is already occupied by another voxel.
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="immediate">If true, voxel is updated immediately. If false, voxel will be updated when it's not in camera frustum and at certain distance from the player.</param>
        public bool VoxelCancelDynamic (VoxelChunk chunk, int voxelIndex, bool immediate = true, bool destroy = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {

            // If no voxel there cancel
            if ((object)chunk == null || chunk.voxels[voxelIndex].isEmpty)
                return false;

            // If it a dynamic voxel?
            if (!VoxelIsDynamic(chunk, voxelIndex))
                return false;

            VoxelPlaceholder placeholder = GetVoxelPlaceholder(chunk, voxelIndex, false);
            if (placeholder == null)
                return false;

            if (placeholder.modelMeshFilter == null)
                return false;

            if (immediate) {
                placeholder.CancelDynamicNow(destroy, addParticlesOnDestroy, playSoundOnDestroy);
            } else {
                placeholder.CancelDynamic(destroy, addParticlesOnDestroy, playSoundOnDestroy);
            }
            return true;
        }


        /// <summary>
        /// Converts a dynamic voxel back to normal voxel. This operation will result in voxel being destroyed if it the current voxel position is already occupied by another voxel.
        /// </summary>
        /// <param name="placeholder">Placeholder.</param>
        public bool VoxelCancelDynamic (VoxelPlaceholder placeholder, bool destroy = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {

            // No model instance? Return
            if (placeholder == null || placeholder.modelInstance == null)
                return false;

            // Check if voxel is of dynamic type
            VoxelChunk chunk = placeholder.chunk;
            if ((object)chunk == null)
                return false;

            int voxelIndex = placeholder.voxelIndex;
            if (voxelIndex < 0 || chunk.voxels[voxelIndex].isEmpty)
                return false;

            VoxelDefinition voxelType = chunk.voxels[voxelIndex].type.staticDefinition;
            if (voxelType != null) {

                if (!voxelType.isDynamic)
                    return false;

                Color voxelColor = chunk.voxels[voxelIndex].color;
                Vector3d targetPosition = placeholder.transform.position;

                // Places the voxel if destination target is empty (it could have moved by physics or gravity)
                if (GetVoxelIndex(targetPosition, out VoxelChunk targetChunk, out int targetVoxelIndex, false)) {
                    if (targetVoxelIndex != voxelIndex || targetChunk != chunk) {

                        // Removes old voxel from chunk (this call also removed placeholder gameobject)
                        VoxelDestroyFastSingle(chunk, voxelIndex);

                        // Place only if it's empty at the new position
                        if (targetChunk.voxels[targetVoxelIndex].opaque < 3) {
                            targetChunk.voxels[targetVoxelIndex].Set(voxelType, voxelColor);
                            targetChunk.SetNeedsColliderRebuild();
                            RegisterChunkChanges(targetChunk);

                            // Clear lighting
                            ClearLightmapAtPosition(targetChunk, targetVoxelIndex);
                        }
                    } else {
                        // If it has not moved from original position, just replace with original type
                        targetChunk.voxels[targetVoxelIndex].Set(voxelType, voxelColor);
                        targetChunk.SetNeedsColliderRebuild();
                        RegisterChunkChanges(targetChunk);

                        // Clear lighting
                        ClearLightmapAtPosition(targetChunk, targetVoxelIndex);

                        // Remove placeholder with small delay to prevent flickering between old voxel being removed and the chunk getting refreshed
                        VoxelPlaceholderDestroy(placeholder.chunk, placeholder.voxelIndex, 0.3f);
                    }
                } else {
                    // There's no chunk where the voxel has moved, just remove the voxel
                    VoxelPlaceholderDestroy(placeholder.chunk, placeholder.voxelIndex, 0.3f);
                }

                if (destroy) {
                    VoxelDamage(targetPosition, 999999, addParticlesOnDestroy, playSoundOnDestroy);
                }
            }

            ChunkRequestRefresh(chunk, clearLightmap: false, refreshMesh: true);

            return true;
        }


        /// <summary>
        /// Consolidate all dynamic voxels
        /// </summary>
        private void VoxelCancelDynamicAll () {
            VoxelPlaceholder[] placeholders = Misc.FindObjectsOfType<VoxelPlaceholder>(true);
            for (int i = 0; i < placeholders.Length; i++) {
                VoxelPlaceholder p = placeholders[i];
                if (VoxelIsDynamic(p.chunk, p.voxelIndex)) {
                    VoxelCancelDynamic(p, false, false, false);
                }
            }
        }



        /// <summary>
        /// Converts a list of voxels into dynamic gameobjects.
        /// </summary>
        /// <param name="voxelIndices">Voxel indices.</param>
        /// <param name="addRigidbody">If set to <c>true</c> add rigidbody.</param>
        /// <param name="duration">If duration is greater than 0, voxel will be converted back to normal voxel after 'duration' seconds.</param>
        public void VoxelGetDynamic (List<VoxelIndex> voxelIndices, bool addRigidbody = false, float duration = 0, bool destroyOnCollapse = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {

            List<VoxelChunk> tempChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            int count = voxelIndices.Count;
            for (int k = 0; k < count; k++) {
                VoxelIndex vi = voxelIndices[k];
                VoxelChunk chunk = vi.chunk;
                GameObject obj = VoxelSetDynamic(chunk, vi.voxelIndex, addRigidbody, duration, destroyOnCollapse, addParticlesOnDestroy, playSoundOnDestroy);
                if (obj == null)
                    continue;
                if (chunk.SetModified(modificationTag)) {
                    tempChunks.Add(chunk);
                }
                SpreadLightmapAroundPosition(chunk, vi.voxelIndex);
            }

            ChunkRequestRefresh(tempChunks, false, true);
            RegisterChunkChanges(tempChunks);

            BufferPool<VoxelChunk>.Release(tempChunks);
        }


        /// <summary>
        /// Converts a voxel into a dynamic gameobject. If voxel has already been converted, it just returns the reference to the gameobject.
        /// </summary>
        /// <returns>The get dynamic.</returns>
        /// <param name="position">Voxel position in world space.</param>
        /// <param name="addRigidbody">If set to <c>true</c> add rigidbody.</param>
        /// <param name="duration">If duration is greater than 0, voxel will be either destroyed or converted back to normal voxel after 'duration' seconds.</param>
        public GameObject VoxelGetDynamic (Vector3d position, bool addRigidbody = false, float duration = 0) {
            VoxelChunk chunk;
            int voxelIndex;
            if (!GetVoxelIndex(position, out chunk, out voxelIndex))
                return null;
            return VoxelGetDynamic(chunk, voxelIndex, addRigidbody, duration);
        }


        /// <summary>
        /// Converts a voxel into a dynamic gameobject. If voxel has already been converted, it just returns the reference to the gameobject.
        /// </summary>
        /// <returns>The get dynamic.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="addRigidbody">If set to <c>true</c> add rigidbody.</param>
        /// <param name="duration">If duration is greater than 0, voxel will be either destroyed or converted back to normal voxel after 'duration' seconds.</param>
        public GameObject VoxelGetDynamic (VoxelChunk chunk, int voxelIndex, bool addRigidbody = false, float duration = 0, bool destroyOnCollapse = false, bool addParticlesOnDestroy = true, bool playSoundOnDestroy = true) {

            if ((object)chunk == null || chunk.voxels[voxelIndex].isEmpty)
                return null;

            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (!vd.renderType.supportsDynamic()) {
#if UNITY_EDITOR
                Debug.LogError("Only opaque, transparent, opaque-no-AO and cutout voxel types can be converted to dynamic voxels.");
#endif
                return null;
            }

            GameObject obj = VoxelSetDynamic(chunk, voxelIndex, addRigidbody, duration, destroyOnCollapse, addParticlesOnDestroy, playSoundOnDestroy);
            if (obj == null)
                return null;

            // If voxel is already custom-type, then just returns the placeholder gameobject
            if (vd.renderType == RenderType.Custom)
                return obj;

            // Notify changes
            RegisterChunkChanges(chunk);

            // Refresh chunk
            ChunkRequestRefresh(chunk, true, true);

            return obj;
        }


        /// <summary>
        /// Creates a recoverable voxel and throws it at given position, direction and strength
        /// </summary>
        /// <param name="position">Position in world space.</param>
        /// <param name="direction">Direction.</param>
        /// <param name="voxelType">Voxel definition.</param>
        public GameObject VoxelThrow (Vector3d position, Vector3 direction, float velocity, VoxelDefinition voxelType, Color32 color, float quantity = 1f) {
            GameObject voxelGO = CreateRecoverableVoxel(position, voxelType, color, quantity);
            if (voxelGO == null)
                return null;
            if (!voxelGO.TryGetComponent(out Rigidbody rb)) {
                return null;
            }
            rb.linearVelocity = direction * velocity;
            return voxelGO;
        }

        /// <summary>
        /// Rotates a voxel
        /// </summary>
        /// <param name="position">Voxel position in world space.</param>
        /// <param name="angleX">Angle x in degrees.</param>
        /// <param name="angleY">Angle y in degrees.</param>
        /// <param name="angleZ">Angle z in degrees.</param>
        public void VoxelRotate (Vector3d position, float angleX, float angleY, float angleZ) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                return;
            }
            VoxelRotate(chunk, voxelIndex, angleX, angleY, angleZ);
        }

        /// <summary>
        /// Rotates a voxel
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        /// <param name="angleX">Angle x in degrees.</param>
        /// <param name="angleY">Angle y in degrees.</param>
        /// <param name="angleZ">Angle z in degrees.</param>
        public void VoxelRotate (VoxelChunk chunk, int voxelIndex, float angleX, float angleY, float angleZ) {
            GameObject obj = VoxelGetDynamic(chunk, voxelIndex);
            if (obj != null) {
                obj.transform.Rotate(angleX, angleY, angleZ);
                RegisterChunkChanges(chunk);
            }
        }

        /// <summary>
        /// Sets the rotation of a voxel. Voxel will be converted to dynamic first if needed.
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        /// <param name="angleX">Angle x in degrees.</param>
        /// <param name="angleY">Angle y in degrees.</param>
        /// <param name="angleZ">Angle z in degrees.</param>
        public void VoxelSetRotation (VoxelChunk chunk, int voxelIndex, float angleX, float angleY, float angleZ) {
            GameObject obj = VoxelGetDynamic(chunk, voxelIndex);
            if (obj != null) {
                obj.transform.localRotation = Quaternion.Euler(angleX, angleY, angleZ);
                RegisterChunkChanges(chunk);
            }
        }

        /// <summary>
        /// Sets the rotation of a voxel. Voxel will be converted to dynamic first if needed.
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        public void VoxelSetRotation (VoxelChunk chunk, int voxelIndex, Quaternion rotation) {
            GameObject obj = VoxelGetDynamic(chunk, voxelIndex);
            if (obj != null) {
                obj.transform.localRotation = rotation;
                RegisterChunkChanges(chunk);
            }
        }


        /// <summary>
        /// Sets the rotation of a voxel. Voxel will be converted to dynamic first if needed.
        /// </summary>
        /// <param name="position">Position of the voxel.</param>
        /// <param name="angleX">Angle x in degrees.</param>
        /// <param name="angleY">Angle y in degrees.</param>
        /// <param name="angleZ">Angle  in degrees.</param>
        public void VoxelSetRotation (Vector3d position, float angleX, float angleY, float angleZ) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                VoxelSetRotation(chunk, voxelIndex, angleX, angleY, angleZ);
            }
        }

        /// <summary>
        /// Sets the rotation of a voxel. Voxel will be converted to dynamic first.
        /// </summary>
        /// <param name="position">Position of the voxel.</param>
        public void VoxelSetRotation (Vector3d position, Quaternion rotation) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                VoxelSetRotation(chunk, voxelIndex, rotation);
            }
        }

        /// <summary>
        /// Returns the rotation of a custom or dynamic voxel.
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        public Quaternion GetVoxelRotation (VoxelChunk chunk, int voxelIndex) {
            VoxelPlaceholder placeHolder = GetVoxelPlaceholder(chunk, voxelIndex, false);
            if (placeHolder != null) {
                return placeHolder.transform.localRotation;
            }
            return Misc.quaternionZero;
        }

        /// <summary>
        /// Returns the rotation of a custom or dynamic voxel.
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        public Quaternion GetVoxelRotation (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex, false)) {
                return GetVoxelRotation(chunk, voxelIndex);
            }
            return Misc.quaternionZero;
        }

        /// <summary>
        /// Sets the rotation of the side textures of a voxel.
        /// </summary>
        /// <param name="position">Voxel position in world space.</param>
        /// <param name="rotation">Rotation turns. Can be 0, 1, 2 or 3 and represents clockwise 90-degree step rotations.</param>
        public bool VoxelSetTexturesRotation (Vector3d position, int rotation) {
            VoxelChunk chunk;
            int voxelIndex;
            if (!GetVoxelIndex(position, out chunk, out voxelIndex, false)) {
                return false;
            }
            return VoxelSetTexturesRotation(chunk, voxelIndex, rotation);
        }


        /// <summary>
        /// Sets the rotation of the side textures of a voxel.
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="rotation">Rotation turns. Can be 0, 1, 2 or 3 and represents clockwise 90-degree step rotations.</param>
        public bool VoxelSetTexturesRotation (VoxelChunk chunk, int voxelIndex, int rotation) {
            if ((object)chunk == null || voxelIndex < 0)
                return false;

            VoxelDefinition vd = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            if (vd.allowsTextureRotation && vd.renderType.supportsTextureRotation()) {
                int currentRotation = chunk.voxels[voxelIndex].GetTextureRotation();
                if (currentRotation != rotation) {
                    chunk.voxels[voxelIndex].SetTextureRotation(rotation);
                    RegisterChunkChanges(chunk);
                    ChunkRequestRefresh(chunk, false, true);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the current rotation for the side textures of a voxel
        /// </summary>
        /// <returns>The get texture rotation.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public int GetVoxelTexturesRotation (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk != null && voxelIndex >= 0) {
                return chunk.voxels[voxelIndex].GetTextureRotation();
            }
            return 0;
        }


        /// <summary>
        /// Returns the current rotation for the side textures of a voxel
        /// </summary>
        /// <returns>The get texture rotation.</returns>
        public int GetVoxelTexturesRotation (Vector3d position) {
            if (GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) {
                return GetVoxelTexturesRotation(chunk, voxelIndex);
            } else {
                return 0;
            }
        }



        /// <summary>
        /// Returns the gameobject associated to a custom voxel placed in the scene (if its voxel definition has "Create GameObject" option enabled)
        /// </summary>
        public GameObject GetVoxelGameObject (Vector3d position) {
            if (!GetVoxelIndex(position, out VoxelChunk chunk, out int voxelIndex)) return null;
            return GetVoxelGameObject(chunk, voxelIndex);
        }

        /// <summary>
        /// Returns the gameobject associated to a custom voxel placed in the scene (if its voxel definition has "Create GameObject" option enabled)
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        public GameObject GetVoxelGameObject (VoxelChunk chunk, int voxelIndex) {
            VoxelPlaceholder placeholder = GetVoxelPlaceholder(chunk, voxelIndex, false);
            if (placeholder != null) {
                return placeholder.modelInstance;
            }
            return null;
        }

        /// <summary>
        /// Returns current resistance points for voxel
        /// </summary>
        public int GetVoxelResistancePoints (Vector3d position) {
            VoxelChunk chunk;
            int voxelIndex;
            if (GetVoxelIndex(position, out chunk, out voxelIndex)) {
                return GetVoxelResistancePoints(chunk, voxelIndex);
            }
            return 0;
        }


        /// <summary>
        /// Returns current resistance points for voxel
        /// </summary>
        public int GetVoxelResistancePoints (VoxelChunk chunk, int voxelIndex) {
            if ((object)chunk == null || voxelIndex < 0) return 0;
            VoxelPlaceholder placeholder = GetVoxelPlaceholder(chunk, voxelIndex, false);
            if (placeholder != null) {
                return placeholder.resistancePointsLeft;
            } else {
                return chunk.voxels[voxelIndex].hasContent ? chunk.voxels[voxelIndex].type.resistancePoints : 0;
            }
        }


        /// <summary>
        /// Rotates the side textures of a voxel
        /// </summary>
        /// <param name="position">Voxel position in world space.</param>
        /// <param name="rotation">Turns (0, 1, 2 or 3). Each turn represent a 90º degree rotation.</param>
        public bool VoxelRotateTextures (Vector3d position, int rotation) {
            VoxelChunk chunk;
            int voxelIndex;
            if (!GetVoxelIndex(position, out chunk, out voxelIndex, false)) {
                return false;
            }
            return VoxelRotateTextures(chunk, voxelIndex, rotation);
        }

        /// <summary>
        /// Rotates the side textures of a voxel
        /// </summary>
        /// <param name="chunk">Chunk of the voxel.</param>
        /// <param name="voxelIndex">Index of the voxel in the chunk.</param>
        /// <param name="rotation">Turns (0, 1, 2 or 3). Each turn represent a 90º degree rotation. Can be positive or negative.</param>
        public bool VoxelRotateTextures (VoxelChunk chunk, int voxelIndex, int rotation) {
            if ((object)chunk == null || voxelIndex < 0 || !voxelDefinitions[chunk.voxels[voxelIndex].typeIndex].renderType.supportsTextureRotation())
                return false;

            int currentRotation = chunk.voxels[voxelIndex].GetTextureRotation();
            currentRotation = (currentRotation + rotation + 128000) % 4; // avoids negative values by adding a fairly large number
            chunk.voxels[voxelIndex].SetTextureRotation(currentRotation);
            RegisterChunkChanges(chunk);
            ChunkRequestRefresh(chunk, false, true);
            return true;
        }



        /// <summary>
        /// Clears a chunk given a world space position.
        /// </summary>
        /// <param name="position">Position.</param>
        public bool ChunkDestroy (Vector3d position) {
            GetChunk(position, out VoxelChunk chunk, false);
            return ChunkDestroy(chunk);
        }


        /// <summary>
        /// Clears all existing chunks
        /// </summary>
        public void ChunkDestroyAll () {
            tempChunks.Clear();
            GetChunks(tempChunks, ChunkModifiedFilter.All);
            foreach (var chunk in tempChunks) {
                ChunkDestroy(chunk);
            }
        }


        /// <summary>
        /// Clears a chunk given a world space position.
        /// </summary>
        public bool ChunkDestroy (VoxelChunk chunk) {
            if ((object)chunk == null)
                return false;
            ChunkClearFast(chunk);
            RegisterChunkChanges(chunk);

            // Refresh rendering
            UpdateChunkRR(chunk);

            return true;
        }

        /// <summary>
        /// Destroys chunk gameobject and release it to the pool
        /// </summary>
        public void ChunkRelease (VoxelChunk chunk) {
            if ((object)chunk == null) return;
            chunk.PrepareForReuse(effectiveGlobalIllumination ? FULL_DARK : FULL_LIGHT);
            ReleaseChunkNavMesh(chunk);
            if (chunk.mc != null) {
                Mesh mcMesh = chunk.mc.sharedMesh;
                Misc.DestroyImmediateAndNullify(ref mcMesh);
                chunk.mc.sharedMesh = null;
            }
            Mesh mfMesh = chunk.mf.sharedMesh;
            Misc.DestroyImmediateAndNullify(ref mfMesh);
            chunk.mf.sharedMesh = null;
            SetChunkOctreeIsDirty(chunk.position, true);
        }


        /// <summary>
        /// Clears any user content modification on the chunk, unmark it as modified and calls the terrain generator to fill its contents as regular chunks
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns></returns>
        public bool ChunkReset (VoxelChunk chunk) {
            if ((object)chunk == null)
                return false;
            ChunkDestroy(chunk);

            Vector3d position = chunk.position;
            position.x -= CHUNK_HALF_SIZE;
            position.z -= CHUNK_HALF_SIZE;
            ResetHeightMapCache(position.x, position.z);
            world.terrainGenerator.PaintChunk(chunk);
            ChunkRequestRefresh(chunk, true, true);
            chunk.modified = false;
            return true;
        }

        /// <summary>
        /// Returns true if the underline NavMEs
        /// </summary>
        /// <returns><c>true</c>, if has nav mesh ready was chunked, <c>false</c> otherwise.</returns>
        /// <param name="chunk">Chunk.</param>
        public bool ChunkHasNavMeshReady (VoxelChunk chunk) {
            if ((object)chunk == null) return false;
            return chunk.navMeshSourceIndex >= 0 && chunk.navMeshSourceIndex >= 0 && chunk.navMeshUpdateRequestTime <= navMeshLastBakeTime;
        }

        /// <summary>
        /// Returns true if the chunk has a collider
        /// </summary>
        public bool ChunkHasCollider (VoxelChunk chunk) {
            if ((object)chunk == null) return false;
            return chunk.hasColliderMesh;
        }

        /// <summary>
        /// Returns true if chunk is within camera frustum
        /// </summary>
        public bool ChunkIsInFrustum (VoxelChunk chunk) {
            Vector3d boundsMin;
            boundsMin.x = chunk.position.x - CHUNK_HALF_SIZE;
            boundsMin.y = chunk.position.y - CHUNK_HALF_SIZE;
            boundsMin.z = chunk.position.z - CHUNK_HALF_SIZE;
            Vector3d boundsMax;
            boundsMax.x = chunk.position.x + CHUNK_HALF_SIZE;
            boundsMax.y = chunk.position.y + CHUNK_HALF_SIZE;
            boundsMax.z = chunk.position.z + CHUNK_HALF_SIZE;
            chunk.visibleInFrustum = GeometryUtilityNonAlloc.TestPlanesAABB(frustumPlanesNormals, frustumPlanesDistances, ref boundsMin, ref boundsMax);
            return chunk.visibleInFrustum;
        }


        /// <summary>
        /// Ensures chunks within given bounds are created
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="chunkExtents">Distance in chunk units (each chunk is 16 world units)</param>
        /// <param name="renderChunks">If set to <c>true</c> enable chunk rendering.</param>
        public void ChunkCheckArea (Vector3d position, Vector3 chunkExtents, bool renderChunks = false) {

            FastMath.FloorToInt(position.x / CHUNK_SIZE, position.y / CHUNK_SIZE, position.z / CHUNK_SIZE, out int chunkX, out int chunkY, out int chunkZ);
            int xmin = chunkX - (int)chunkExtents.x;
            int ymin = chunkY - (int)chunkExtents.y;
            int zmin = chunkZ - (int)chunkExtents.z;
            int xmax = chunkX + (int)chunkExtents.x;
            int ymax = chunkY + (int)chunkExtents.y;
            int zmax = chunkZ + (int)chunkExtents.z;

            for (int x = xmin; x <= xmax; x++) {
                int x00 = WORLD_SIZE_DEPTH * WORLD_SIZE_HEIGHT * (x + WORLD_SIZE_WIDTH);
                for (int y = ymin; y <= ymax; y++) {
                    int y00 = WORLD_SIZE_DEPTH * (y + WORLD_SIZE_HEIGHT);
                    int h00 = x00 + y00;
                    for (int z = zmin; z <= zmax; z++) {
                        int hash = h00 + z;
                        if (cachedChunks.TryGetValue(hash, out CachedChunk cachedChunk)) {
                            VoxelChunk chunk = cachedChunk.chunk;
                            if ((object)chunk == null)
                                continue;
                            if (chunk.isPopulated) {
                                if (renderChunks && (chunk.renderState != ChunkRenderState.RenderingComplete || !chunk.mr.enabled)) {
                                    ChunkRequestRefresh(chunk, false, true, true);
                                }
                                continue;
                            }
                        }
                        VoxelChunk newChunk = CreateChunk(hash, x, y, z, false);
                        if (renderChunks) {
                            ChunkRequestRefresh(newChunk, false, true, true);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Forces Voxel Play to execute all pending tasks
        /// </summary>
        public void CompleteWork () {
            executePendingTasks = true;
        }

        // annotates de last found biome index. This is used to avoid searching the same biome again from start.
        [ThreadStatic]
        static int biomeStartLoop;

        /// <summary>
        /// Returns the biome given altitude and moisture.
        /// </summary>
        /// <returns>The biome.</returns>
        /// <param name="altitude">Altitude in world space units.</param>
        /// <param name="moisture">Moisture in the 0-1 range.</param>
        public BiomeDefinition GetBiome (float altitude, float moisture) {
            int biomesCount = world.biomes.Length;
            int biomeEndLoop = biomesCount + biomeStartLoop;
            for (int k = biomeStartLoop; k < biomeEndLoop; k++) {
                BiomeDefinition biome = world.biomes[k % biomesCount];
                int zonesCount = biome.zones.Length;
                for (int j = 0; j < zonesCount; j++) {
                    if (altitude >= biome.zones[j].altitudeMin && altitude <= biome.zones[j].altitudeMax &&
                         moisture >= biome.zones[j].moistureMin && moisture <= biome.zones[j].moistureMax) {
                        biomeStartLoop = k;
                        return biome;
                    }
                }
            }
            return world.defaultBiome;
        }

        /// <summary>
        /// Returns the two closest biomes and a blend weight based on signed distance in altitude/moisture space.
        /// Signed distance is negative inside a zone (deeper = more negative), positive outside.
        /// </summary>
        public void GetBiomeWeights (float altitude, float moisture, out BiomeDefinition primary, out BiomeDefinition secondary, out float blendWeight) {
            primary = null;
            secondary = null;
            blendWeight = 0;
            float bestDist = float.MaxValue;
            float secondDist = float.MaxValue;

            // Normalize altitude to 0-1 range so both dimensions contribute equally
            float maxH = world.terrainGenerator != null ? world.terrainGenerator.maxHeight : 255f;
            if (maxH < 1f) maxH = 1f;
            float altNorm = altitude / maxH;

            int biomesCount = world.biomes.Length;
            for (int k = 0; k < biomesCount; k++) {
                BiomeDefinition biome = world.biomes[k];
                int zonesCount = biome.zones.Length;
                for (int j = 0; j < zonesCount; j++) {
                    BiomeZone zone = biome.zones[j];
                    float zoneMinNorm = zone.altitudeMin / maxH;
                    float zoneMaxNorm = zone.altitudeMax / maxH;

                    // Compute signed distance: negative inside, positive outside
                    float da, dm;
                    bool insideAlt, insideMoi;

                    if (altNorm < zoneMinNorm) { da = zoneMinNorm - altNorm; insideAlt = false; }
                    else if (altNorm > zoneMaxNorm) { da = altNorm - zoneMaxNorm; insideAlt = false; }
                    else { da = 0; insideAlt = true; }

                    if (moisture < zone.moistureMin) { dm = zone.moistureMin - moisture; insideMoi = false; }
                    else if (moisture > zone.moistureMax) { dm = moisture - zone.moistureMax; insideMoi = false; }
                    else { dm = 0; insideMoi = true; }

                    float dist;
                    if (insideAlt && insideMoi) {
                        // Inside zone: signed distance = negative margin to nearest edge
                        float marginAlt = altNorm - zoneMinNorm;
                        float tmp = zoneMaxNorm - altNorm;
                        if (tmp < marginAlt) marginAlt = tmp;
                        float marginMoi = moisture - zone.moistureMin;
                        tmp = zone.moistureMax - moisture;
                        if (tmp < marginMoi) marginMoi = tmp;
                        dist = -(marginAlt < marginMoi ? marginAlt : marginMoi);
                    } else {
                        // Outside zone: positive distance
                        dist = da + dm;
                    }

                    if (dist < bestDist) {
                        if ((object)primary != (object)biome) {
                            secondary = primary;
                            secondDist = bestDist;
                        }
                        primary = biome;
                        bestDist = dist;
                    } else if (dist < secondDist && (object)biome != (object)primary) {
                        secondary = biome;
                        secondDist = dist;
                    }
                }
            }

            // If point is not inside any zone, fall back to default biome (preserves original GetBiome behavior)
            if ((object)primary == null || bestDist > 0) {
                primary = world.defaultBiome;
                secondary = null;
                return;
            }

            // Compute blend weight from secondary's signed distance
            // secondDist > 0: secondary is outside its zone, blend decreases with distance
            // secondDist <= 0: zones overlap, both inside, maximum blend
            if ((object)secondary != null) {
                if (secondDist <= 0) {
                    blendWeight = 0.5f;
                } else if (secondDist < biomeMixingSpread) {
                    blendWeight = (1f - secondDist / biomeMixingSpread) * 0.5f;
                }
            }
        }

        /// <summary>
        /// Returns the biome at a given position
        /// </summary>
        public BiomeDefinition GetBiome (Vector3d position) {
            HeightMapInfo h = GetTerrainInfo(position);
            return h.biome;
        }


        /// <summary>
        /// Gets the terrain height under a given position, optionally including water
        /// </summary>
        public float GetTerrainHeight (double x, double z, bool includeWater = false) {

            if (heightMapCache == null)
                return 0;
            float groundLevel = GetHeightMapInfoFast(x, z).groundLevel;
            if (includeWater && waterLevel > groundLevel) {
                return waterLevel + 0.9f;
            } else {
                return groundLevel + 1f;
            }
        }

        /// <summary>
        /// Gets the terrain height under a given position, optionally including water
        /// </summary>
        public float GetTerrainHeight (Vector3d position, bool includeWater = false) {

            if (heightMapCache == null)
                return 0;
            float groundLevel = GetHeightMapInfoFast(position.x, position.z).groundLevel;
            if (includeWater && waterLevel > groundLevel) {
                return waterLevel + 0.9f;
            } else {
                return groundLevel + 1f;
            }
        }

        /// <summary>
        /// Gets info about the terrain on a given position
        /// </summary>
        public HeightMapInfo GetTerrainInfo (Vector3d position) {
            return GetTerrainInfo(position.x, position.z);
        }

        /// <summary>
        /// Gets info about the terrain on a given position
        /// </summary>
        public HeightMapInfo GetTerrainInfo (double x, double z) {
            if (heightMapCache == null) {
                InitHeightMap();
            }
            return GetHeightMapInfoFast(x, z);
        }


        /// <summary>
        /// Gets the computed light amount at a given position in 0..1 range
        /// </summary>
        /// <returns>The light intensity.</returns>
        public float GetVoxelLight (Vector3d position) {
            return GetVoxelLight(position, out VoxelChunk chunk, out int voxelIndex);
        }


        /// <summary>
        /// Gets the computed light amount at a given position in 0..1 range
        /// </summary>
        /// <returns>The light intensity at the voxel position..</returns>
        public float GetVoxelLight (Vector3d position, out VoxelChunk chunk, out int voxelIndex) {
            chunk = null;
            voxelIndex = 0;
            if (!effectiveGlobalIllumination) {
                return 1f;
            }

            if (GetVoxelIndex(position, out chunk, out voxelIndex, false) && !chunk.needsLightmapRebuild) {
                if (chunk.voxels[voxelIndex].lightOrTorch != 0 || chunk.voxels[voxelIndex].opaque < FULL_OPAQUE) {
                    return chunk.voxels[voxelIndex].lightOrTorch / 15f;
                }
                // voxel has contents try to retrieve light information from nearby voxels
                int nearby = voxelIndex + ONE_Y_ROW;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].lightOrTorch / 15f;
                }
                nearby = voxelIndex - ONE_Z_ROW;
                if (nearby >= 0 && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].lightOrTorch / 15f;
                }
                nearby = voxelIndex - 1;
                if (nearby >= 0 && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].lightOrTorch / 15f;
                }
                nearby = voxelIndex + ONE_Z_ROW;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].lightOrTorch / 15f;
                }
                nearby = voxelIndex + 1;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].lightOrTorch / 15f;
                }
                return chunk.voxels[voxelIndex].lightOrTorch / 15f;
            }

            // Estimate light by height
            float height = GetTerrainHeight(position, false);
            if (height >= position.y) { // is below surface, we assume a lightIntensity of 0
                return 0;
            }
            return 1f;
        }

        /// <summary>
        /// Gets the light amount at a given position in packed format (includes torch + sun light contributions)
        /// </summary>
        /// <returns>The light intensity.</returns>
        public int GetVoxelLightPacked (Vector3d position) {
            return GetVoxelLightPacked(position, out _, out _);
        }


        /// <summary>
        /// Gets the light amount at a given position in packed format (includes torch + sun light contributions)
        /// </summary>
        /// <returns>The light intensity at the voxel position..</returns>
        public int GetVoxelLightPacked (Vector3d position, out VoxelChunk chunk, out int voxelIndex) {
            chunk = null;
            voxelIndex = 0;
            if (!effectiveGlobalIllumination) {
                return FULL_LIGHT;
            }

            if (GetVoxelIndex(position, out chunk, out voxelIndex, false) && !chunk.needsLightmapRebuild) {
                if (chunk.voxels[voxelIndex].lightOrTorch != 0 || chunk.voxels[voxelIndex].opaque < FULL_OPAQUE) {
                    return chunk.voxels[voxelIndex].packedLight;
                }
                // voxel has contents try to retrieve light information from nearby voxels
                int nearby = voxelIndex + ONE_Y_ROW;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].packedLight;
                }
                nearby = voxelIndex - ONE_Z_ROW;
                if (nearby >= 0 && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].packedLight;
                }
                nearby = voxelIndex - 1;
                if (nearby >= 0 && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].packedLight;
                }
                nearby = voxelIndex + ONE_Z_ROW;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].packedLight;
                }
                nearby = voxelIndex + 1;
                if (nearby < chunk.voxels.Length && chunk.voxels[nearby].opaque < FULL_OPAQUE) {
                    return chunk.voxels[nearby].packedLight;
                }
                return chunk.voxels[voxelIndex].packedLight;
            }

            // Estimate light by height
            float height = GetTerrainHeight(position, false);
            if (height >= position.y) { // is below surface, we assume a lightIntensity of 0
                return 0;
            }
            return FULL_LIGHT;
        }



        /// <summary>
        /// Creates a placeholder for a given voxel.
        /// </summary>
        /// <returns>The voxel placeholder.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="createIfNotExists">If set to <c>true</c> create if not exists.</param>
        public VoxelPlaceholder GetVoxelPlaceholder (VoxelChunk chunk, int voxelIndex, bool createIfNotExists = true) {
            if (voxelIndex < 0 || (object)chunk == null)
                return null;

            VoxelDefinition type = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex];
            return GetVoxelPlaceholder(chunk, voxelIndex, type, createIfNotExists);
        }


        /// <summary>
        /// Creates a placeholder for a given voxel.
        /// </summary>
        /// <returns>The voxel placeholder.</returns>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="createIfNotExists">If set to <c>true</c> create if not exists.</param>
        public VoxelPlaceholder GetVoxelPlaceholder (VoxelChunk chunk, int voxelIndex, VoxelDefinition voxelDefinition, bool createIfNotExists = true) {

            if (voxelIndex < 0 || (object)chunk == null)
                return null;

            bool phArrayCreated = chunk.placeholders != null;
            bool renderingDifferentVoxel = chunk.voxels[voxelIndex].typeIndex != voxelDefinition.index;
            float rotationDegrees = renderingDifferentVoxel ? 0 : chunk.voxels[voxelIndex].GetTextureRotationDegrees();

            if (phArrayCreated) {
                if (chunk.placeholders.TryGetValue(voxelIndex, out VoxelPlaceholder ph)) {
                    // has rotation changes? Update if needed (not if this voxel type is result of a connected voxel rule)
                    if (ph.currentRotationDegrees != rotationDegrees || renderingDifferentVoxel) {
                        ph.currentRotationDegrees = rotationDegrees;
                        UpdatePlaceholderTransform(ph.transform, chunk, voxelIndex, voxelDefinition, rotationDegrees);
                    }
                    return ph;
                }
            }

            // Create placeholder
            if (createIfNotExists) {
                if (!phArrayCreated) {
                    chunk.placeholders = new FastHashSet<VoxelPlaceholder>();
                }
                GameObject placeholderGO = Instantiate(voxelPlaceholderPrefab, chunk.transform, false);
                if (!placeholderGO.TryGetComponent(out VoxelPlaceholder placeholder)) return null;
                Transform phTransform = placeholderGO.transform;
                phTransform.localPosition = GetVoxelChunkPosition(voxelIndex);
                placeholder.chunk = chunk;
                placeholder.voxelIndex = voxelIndex;
                placeholder.bounds = Misc.bounds1;
                if (chunk.voxels[voxelIndex].hasContent) {
                    placeholder.resistancePointsLeft = voxelDefinitions[chunk.voxels[voxelIndex].typeIndex].resistancePoints;

                    // Bounds for highlighting
                    Mesh mesh = voxelDefinition.mesh;
                    if ((object)mesh != null) {
                        Bounds bounds = mesh.bounds;
                        bounds.size = new Vector3(bounds.size.x * voxelDefinition.scale.x, bounds.size.y * voxelDefinition.scale.y, bounds.size.z * voxelDefinition.scale.z);
                        placeholder.bounds = bounds;
                    }

                    // if this rendering type is result of a connected voxel, ignore any existing rotation
                    placeholder.currentRotationDegrees = rotationDegrees;
                    UpdatePlaceholderTransform(phTransform, chunk, voxelIndex, voxelDefinition, rotationDegrees);
                }
                chunk.placeholders.Add(voxelIndex, placeholder);
                return placeholder;
            }

            return null;
        }

        void UpdatePlaceholderTransform (Transform phTransform, VoxelChunk chunk, int voxelIndex, VoxelDefinition voxelDefinition, float rotationDegrees) {
            // Custom rotation
            Vector3d position = GetVoxelPosition(chunk.position, voxelIndex);
            phTransform.localRotation = voxelDefinition.GetRotation(position);

            if (!voxelDefinition.isDynamic) {

                Vector3 localPosition = GetVoxelChunkPosition(voxelIndex); // needs to be in local coordinates because chunk might still be at -10000

                // User rotation in word space
                Quaternion placementRotation;
                if (rotationDegrees != 0) {
                    placementRotation = Quaternion.Euler(0, rotationDegrees, 0);
                    phTransform.localRotation = placementRotation * phTransform.localRotation;
                    phTransform.localPosition = localPosition + placementRotation * voxelDefinition.GetOffset(position);
                } else {
                    phTransform.localPosition = localPosition + voxelDefinition.GetOffset(position);
                }
            }
        }

        /// <summary>
        /// Destroys a voxel placeholder
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        /// <param name="voxelIndex">Voxel index.</param>
        /// <param name="placeholderObjectRemovalDelay">Used to delay actual placeholder gameobject destruction.</param>
        public void VoxelPlaceholderDestroy (VoxelChunk chunk, int voxelIndex, float placeholderObjectRemovalDelay = 0f) {

            if ((object)chunk == null)
                return;

            bool phArrayCreated = (object)chunk.placeholders != null;

            if (phArrayCreated) {
                VoxelPlaceholder ph;
                if (chunk.placeholders.TryGetValue(voxelIndex, out ph) && ph != null) {
                    if (ph.gameObject != null) {
                        Misc.DestroySafely(ph.gameObject, placeholderObjectRemovalDelay);
                    }
                    chunk.placeholders.Remove(voxelIndex);
                }
            }
        }


        /// <summary>
        /// Array with all available item definitions in current world
        /// </summary>
        [NonSerialized]
        public List<InventoryItem> allItems;


        /// <summary>
        /// Places a model defined by a matrix of colors in the world at the given position. Colors with zero alpha will be skipped.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="colors">3-dimensional array of colors (y/z/x).</param>
        public void ModelPlace (Vector3d position, Color[,,] colors, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {
#if UNITY_EDITOR
            CheckEditorTintColor();
#endif

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            Vector3d pos;
            int maxY = colors.GetUpperBound(0) + 1;
            int maxZ = colors.GetUpperBound(1) + 1;
            int maxX = colors.GetUpperBound(2) + 1;
            int halfZ, halfX;
            if (alignment == ModelPlacementAlignment.Centered) {
                halfZ = maxZ / 2;
                halfX = maxX / 2;
            } else {
                halfZ = halfX = 0;
            }
            VoxelDefinition vd = defaultVoxel;
            for (int y = 0; y < maxY; y++) {
                pos.y = position.y + y;
                for (int z = 0; z < maxZ; z++) {
                    pos.z = position.z + z - halfZ;
                    for (int x = 0; x < maxX; x++) {
                        Color32 color = colors[y, z, x];
                        if (color.a == 0)
                            continue;
                        pos.x = position.x + x - halfX;
                        VoxelChunk chunk;
                        int voxelIndex;
                        if (GetVoxelIndex(pos, out chunk, out voxelIndex)) {
                            if (!previewMode) {
                                chunk.voxels[voxelIndex].Set(vd, color);
                            }
                            if (chunk.SetModified(modificationTag)) {
                                updatedChunks.Add(chunk);
                            }
                        }
                    }
                }
            }

            if (!previewMode) {
                RegisterChunkChanges(updatedChunks);
                Boundsd bounds = new Boundsd(new Vector3d(position.x, position.y + maxY / 2, position.z), new Vector3(maxX + 2, maxY + 2, maxZ + 2));
                ChunkRequestRefresh(bounds, true, true);
            }

            BufferPool<VoxelChunk>.Release(updatedChunks);

        }


        /// <summary>
        /// Places a model defined by a matrix of colors in the world at the given position. Colors with zero alpha will be skipped.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxels">3-dimensional array of voxel definitions (y/z/x).</param>
        /// <param name="colors">3-dimensional array of colors (y/z/x).</param>
        public void ModelPlace (Vector3d position, VoxelDefinition[,,] voxels, Color[,,] colors = null, bool fitTerrain = false, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered) {
            Vector3d pos;
            int maxY = voxels.GetUpperBound(0) + 1;
            int maxZ = voxels.GetUpperBound(1) + 1;
            int maxX = voxels.GetUpperBound(2) + 1;
            int halfZ, halfX;
            if (alignment == ModelPlacementAlignment.Centered) {
                halfZ = maxZ / 2;
                halfX = maxX / 2;
            } else {
                halfZ = halfX = 0;
            }
            bool hasColors = colors != null;
            if (hasColors) {
#if UNITY_EDITOR
                CheckEditorTintColor();
#endif
                if (colors.GetUpperBound(0) != voxels.GetUpperBound(0) || colors.GetUpperBound(1) != voxels.GetUpperBound(1) || colors.GetUpperBound(2) != voxels.GetUpperBound(2)) {
                    Debug.LogError("Colors array dimensions must match those of voxels array.");
                    return;
                }
            }

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            for (int y = 0; y < maxY; y++) {
                pos.y = position.y + y;
                for (int z = 0; z < maxZ; z++) {
                    pos.z = position.z + z - halfZ;
                    for (int x = 0; x < maxX; x++) {
                        VoxelDefinition vd = voxels[y, z, x];
                        if ((object)vd != null) {
                            pos.x = position.x + x - halfX;
                            if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                                if (hasColors) {
                                    chunk.SetVoxel(voxelIndex, vd, colors[y, z, x]);
                                } else {
                                    chunk.SetVoxel(voxelIndex, vd);
                                }
                                if (chunk.SetModified(modificationTag)) {
                                    updatedChunks.Add(chunk);
                                }
                                if (fitTerrain) {
                                    // Fill beneath row 1
                                    if (y == 0) {
                                        Vector3d under = pos;
                                        under.y -= 1;
                                        float terrainAltitude = GetTerrainHeight(under);
                                        for (int k = 0; k < 100; k++, under.y--) {
                                            GetVoxelIndex(under, out VoxelChunk lowChunk, out int vindex);
                                            if (under.y < terrainAltitude || (object)lowChunk == null || lowChunk.voxels[vindex].opaque == FULL_OPAQUE) break;
                                            if (hasColors) {
                                                lowChunk.SetVoxel(vindex, vd, colors[y, z, x]);
                                            } else {
                                                lowChunk.SetVoxel(vindex, vd);
                                            }

                                            if (lowChunk.SetModified(modificationTag)) {
                                                updatedChunks.Add(lowChunk);
                                            }

                                            if (!lowChunk.inqueue) {
                                                ChunkRequestRefresh(lowChunk, true, true);
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
            }

            RegisterChunkChanges(updatedChunks);
            BufferPool<VoxelChunk>.Release(updatedChunks);

            Boundsd bounds = new Boundsd(new Vector3d(position.x, position.y + maxY / 2, position.z), new Vector3(maxX + 2, maxY + 2, maxZ + 2));
            ChunkRequestRefresh(bounds, true, true);
        }


        /// <summary>
        /// Places a model defined by an array of voxels in the world at the given position.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="voxels">3-dimensional array of voxels (y/z/x).</param>
        /// <param name="useUnpopulatedChunks">3-places the model in unpopulated chunks, before terrain generator fills them.</param>
        public void ModelPlace (Vector3d position, Voxel[,,] voxels, bool useUnpopulatedChunks, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered) {
            Vector3d pos;
            int maxY = voxels.GetUpperBound(0) + 1;
            int maxZ = voxels.GetUpperBound(1) + 1;
            int maxX = voxels.GetUpperBound(2) + 1;
            int halfZ, halfX;
            if (alignment == ModelPlacementAlignment.Centered) {
                halfZ = maxZ / 2;
                halfX = maxX / 2;
            } else {
                halfZ = halfX = 0;
            }

            List<VoxelChunk> updatedChunks = BufferPool<VoxelChunk>.Get();
            modificationTag++;

            for (int y = 0; y < maxY; y++) {
                pos.y = position.y + y;
                for (int z = 0; z < maxZ; z++) {
                    pos.z = position.z + z - halfZ;
                    for (int x = 0; x < maxX; x++) {
                        if (voxels[y, z, x].isEmpty) continue;
                        pos.x = position.x + x - halfX;
                        if (useUnpopulatedChunks) {
                            GetChunkUnpopulated(pos);
                        }
                        if (GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex)) {
                            chunk.voxels[voxelIndex] = voxels[y, z, x];

                            if (chunk.SetModified(modificationTag)) {
                                updatedChunks.Add(chunk);
                            }
                        }
                    }
                }
            }

            RegisterChunkChanges(updatedChunks);
            BufferPool<VoxelChunk>.Release(updatedChunks);

            Boundsd bounds = new Boundsd(new Vector3d(position.x, position.y + maxY / 2, position.z), new Vector3(maxX + 2, maxY + 2, maxZ + 2));
            ChunkRequestRefresh(bounds, true, true);
        }

        /// <summary>
        /// Places a list of voxels given by a list of ModelBits within a given chunk
        /// </summary>
        /// <param name="bits">List of voxels described by a list of modelbit structs</param>
        public void ModelPlace (VoxelChunk chunk, List<ModelBit> bits) {
            int count = bits.Count;
            for (int b = 0; b < count; b++) {
                int voxelIndex = bits[b].voxelIndex;
                if (bits[b].isEmpty) {
                    chunk.voxels[voxelIndex] = Voxel.Empty;
                    continue;
                }
                VoxelDefinition vd = bits[b].voxelDefinition;
                if (vd == null) {
                    vd = defaultVoxel;
                }
                chunk.voxels[voxelIndex].Set(vd, bits[b].finalColor);
                float rotation = bits[b].rotation;
                if (rotation != 0) {
                    chunk.voxels[voxelIndex].SetTextureRotation(Voxel.GetTextureRotationFromDegrees(rotation));
                }
                if (bits[b].microVoxels != null) {
                    chunk.SetMicroVoxels(voxelIndex, bits[b].microVoxels);
                } else if (vd.usesMicroVoxels) {
                    chunk.SetMicroVoxels(voxelIndex, vd.microVoxels);
                }

                // Restore voxel properties
                List<ModelBitProperty> properties = bits[b].properties;
                int propertyCount = properties.Count;
                if (properties != null && propertyCount > 0) {
                    for (int p = 0; p < propertyCount; p++) {
                        ModelBitProperty property = properties[p];
                        if (string.IsNullOrEmpty(property.stringValue)) {
                            VoxelSetProperty(chunk, voxelIndex, property.propertyId, property.floatValue);
                        } else {
                            VoxelSetProperty(chunk, voxelIndex, property.propertyId, property.stringValue);
                        }
                    }
                }
            }
            RegisterChunkChanges(chunk);
            RefreshNeighbourhood(chunk);
        }

        /// <summary>
        /// Places a model in the world at the given position iteratively
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="model">Model Definition.</param>
        /// <param name="rotationDegrees">0, 90, 180 or 270 degree rotation. A value of 360 means random rotation.</param>
        /// <param name="colorBrightness">Optionally pass a color brightness value. This value is multiplied by the voxel color.</param>
        /// <param name="fitTerrain">If set to true, vegetation and trees are prevented and some space is flattened around the model.</param>
        /// <param name="callback">User defined function called when the model has finished building in the scene.</param>
        public void ModelPlace (Vector3d position, ModelDefinition model, float buildDuration, int rotationDegrees = 0, float colorBrightness = 1f, bool fitTerrain = false, VoxelModelBuildEndEvent callback = null, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {
            if (buildDuration <= 0) {
                ModelPlace(position, model, rotationDegrees, colorBrightness, fitTerrain, alignment: alignment, previewMode: previewMode);
            } else {
                int rotation = Voxel.GetTextureRotationFromDegrees(rotationDegrees);
                StartCoroutine(ModelPlaceWithDuration(position, model, buildDuration, rotation, colorBrightness, fitTerrain, callback, alignment, previewMode));
            }
        }

        /// <summary>
        /// Places a model in the world at the given position
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="model">Model Definition.</param>
        /// <param name="rotationDegrees">0, 90, 180 or 270 degree rotation. A value of 360 means random rotation.</param>
        /// <param name="colorBrightness">Optionally pass a color brightness value. This value is multiplied by the voxel color.</param>
        /// <param name="fitTerrain">If set to true, vegetation and trees are prevented and some space is flattened around the model.</param>
        /// <param name="indices">Optional user-provided list. If provided, it will contain the indices and positions of all visible voxels in the model</param>
        public void ModelPlace (Vector3d position, ModelDefinition model, int rotationDegrees = 0, float colorBrightness = 1f, bool fitTerrain = false, List<VoxelIndex> indices = null, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {
            ModelPlace(position, model, out _, rotationDegrees, colorBrightness, fitTerrain, indices, alignment, previewMode);
        }

        /// <summary>
        /// Places a model in the world at the given position
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="model">Model Definition.</param>
        /// <param name="bounds">The bounds of the placed model.</param>
        /// <param name="rotationDegrees">0, 90, 180 or 270 degree rotation. A value of 360 means random rotation.</param>
        /// <param name="colorBrightness">Optionally pass a color brightness value. This value is multiplied by the voxel color.</param>
        /// <param name="fitTerrain">If set to true, vegetation and trees are prevented and some space is flattened around the model.</param>
        /// <param name="indices">Optional user-provided list. If provided, it will contain the indices and positions of all visible voxels in the model</param>
        public void ModelPlace (Vector3d position, ModelDefinition model, out Boundsd bounds, int rotationDegrees = 0, float colorBrightness = 1f, bool fitTerrain = false, List<VoxelIndex> indices = null, ModelPlacementAlignment alignment = ModelPlacementAlignment.Centered, bool previewMode = false) {

            if (model == null) {
                bounds = Boundsd.empty;
                return;
            }

            if (!previewMode && OnModelBuildStart != null) {
                OnModelBuildStart(model, position, out bool cancel);
                if (cancel) {
                    bounds = Boundsd.empty;
                    return;
                }
            }

            bool usesOnModelBuildEndWithIndices = OnModelBuildEndWithIndices != null;
            if (usesOnModelBuildEndWithIndices && indices == null) {
                indices = BufferPool<VoxelIndex>.Get();
            }

            Boundsd dummyBounds = Boundsd.empty;
            int rotation;
            if (rotationDegrees == 360) {
                rotation = WorldRand.Range(0, 4);
            } else {
                rotation = Voxel.GetTextureRotationFromDegrees(rotationDegrees);
            }
            bool result = ModelPlace(position, model, ref dummyBounds, rotation, colorBrightness, fitTerrain, indices, -1, -1, alignment: alignment, previewMode: previewMode);
            if (result && !previewMode) {
                ModelPlaceTorches(position, model, rotation, alignment);
            }
            bounds = dummyBounds;

            if (OnModelBuildEnd != null) {
                OnModelBuildEnd(model, position);
            }
            if (usesOnModelBuildEndWithIndices) {
                OnModelBuildEndWithIndices(model, position, indices);
                BufferPool<VoxelIndex>.Release(indices);
            }
        }


        /// <summary>
        /// Fills empty voxels inside the model with Empty blocks so they clear any other existing voxel when placing the model
        /// </summary>
        /// <param name="model">Model.</param>
        public void ModelFillInside (ModelDefinition model) {

            if (model == null)
                return;
            int sx = model.sizeX;
            int sy = model.sizeY;
            int sz = model.sizeZ;
            Voxel[] voxels = new Voxel[sy * sz * sx];

            List<ModelBit> newBits = new List<ModelBit>();

            // Load model inside the temporary voxel array
            int bitsLength = model.bits.Length;
            for (int k = 0; k < bitsLength; k++) {
                int voxelIndex = model.bits[k].voxelIndex;
                if (!model.bits[k].isEmpty) {
                    voxels[voxelIndex].isEmpty = false;
                    newBits.Add(model.bits[k]);
                }
            }

            // Fill inside
            ModelBit empty = new ModelBit();
            empty.isEmpty = true;
            int yrow = sz * sx;
            for (int z = 0; z < sz; z++) {
                for (int x = 0; x < sx; x++) {
                    int bindex = -1;
                    int voxelIndex = z * sx + x;
                    for (int y = 0; y < sy; y++, voxelIndex += yrow) {
                        if (voxels[voxelIndex].isEmpty) {
                            if (bindex < 0) {
                                bindex = voxelIndex;
                            }
                        } else if (bindex >= 0) {
                            while (bindex < voxelIndex) {
                                if (voxels[bindex].isEmpty) {
                                    empty.voxelIndex = bindex;
                                    newBits.Add(empty);
                                }
                                bindex += yrow;
                            }
                            bindex = -1;
                        }
                    }
                }
            }
            model.bits = newBits.ToArray();
        }


        /// <summary>
        /// Converts a model definition into a regular gameobject
        /// </summary>
        /// <param name="updateGameObject">The gameobject to update or pass null to generate a new gameobject</param>
        public static GameObject ModelCreateGameObject (ModelDefinition modelDefinition, GameObject updateGameObject = null) {
            return VoxelPlayConverter.GenerateVoxelObject(modelDefinition, Misc.vector3zero, Misc.vector3one, updateGameObject: updateGameObject);
        }


        /// <summary>
        /// Converts a model definition into a regular gameobject
        /// </summary>
        /// <param name="updateGameObject">The gameobject to update or pass null to generate a new gameobject</param>
        public static GameObject ModelCreateGameObject (ModelDefinition modelDefinition, Vector3 offset, Vector3 scale, GameObject updateGameObject = null) {
            return VoxelPlayConverter.GenerateVoxelObject(modelDefinition, offset, scale, updateGameObject: updateGameObject);
        }

        /// <summary>
        /// Converts a model definition into a regular gameobject
        /// </summary>
        /// <param name="updateGameObject">The gameobject to update or pass null to generate a new gameobject</param>
        public static GameObject ModelCreateGameObject (ModelDefinition modelDefinition, Vector3 offset, Vector3 scale, bool useTextures, bool useNormalMaps = true, GameObject updateGameObject = null) {
            return VoxelPlayConverter.GenerateVoxelObject(modelDefinition, offset, scale, useTextures: useTextures, useNormalMaps: useNormalMaps, updateGameObject: updateGameObject);
        }


        /// <summary>
        /// Captures a portion of the world and creates a model definition with those contents
        /// </summary>
        /// <returns>The model definition containing the world contents in a given boundary</returns>
        public ModelDefinition ModelWorldCapture (Bounds bounds, bool captureVoxelTintColor = true, bool captureEmptySpaces = false) {

            if (bounds.size.x < 1 || bounds.size.y < 1 || bounds.size.z < 1) return null;

            ModelDefinition md = ScriptableObject.CreateInstance<ModelDefinition>();
            int sizeX = (int)bounds.size.x;
            int sizeY = (int)bounds.size.y;
            int sizeZ = (int)bounds.size.z;
            md.sizeX = sizeX;
            md.sizeY = sizeY;
            md.sizeZ = sizeZ;
            Vector3 min = bounds.min;
            List<ModelBit> bits = BufferPool<ModelBit>.Get();
            List<TorchBit> torchBits = new List<TorchBit>();
            for (int y = 0; y < sizeY; y++) {
                for (int z = 0; z < sizeZ; z++) {
                    for (int x = 0; x < sizeX; x++) {
                        int modelIndex = y * sizeZ * sizeX + z * sizeX + x;
                        Vector3 pos = new Vector3(min.x + x, min.y + y, min.z + z);
                        if (!GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, false)) {
                            if (captureEmptySpaces) {
                                ModelBit emptyBit = new ModelBit();
                                emptyBit.voxelIndex = modelIndex;
                                emptyBit.isEmpty = true;
                                bits.Add(emptyBit);
                            }
                            continue;
                        }
                        if (chunk.voxels[voxelIndex].hasContent) {
                            VoxelDefinition vd = chunk.voxels[voxelIndex].type;
                            if (!vd.isDynamic) {
                                ModelBit bit = new ModelBit();
                                bit.voxelIndex = modelIndex;
                                bit.voxelDefinition = vd;
                                bit.color = captureVoxelTintColor ? chunk.voxels[voxelIndex].color : Misc.color32White;
                                bit.rotation = chunk.voxels[voxelIndex].GetTextureRotationDegrees();
                                bit.microVoxels = null;
                                if (chunk.usesMicroVoxels && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels microVoxels)) {
                                    // Only store microvoxels in the model if they differ from the voxel definition's microvoxels
                                    // This allows the placement system to reuse microvoxels from the voxel definition when they match
                                    if (vd.microVoxels == null || !microVoxels.Equals(vd.microVoxels)) {
                                        bit.microVoxels = microVoxels.Clone();
                                    }
                                }

                                // Capture voxel properties
                                if (chunk.voxelsProperties != null && chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                                    List<KeyValuePair<int, VoxelProperty>> properties = BufferPool<KeyValuePair<int, VoxelProperty>>.Get();
                                    try {
                                        voxelProperties.GetValues(properties);

                                        foreach (KeyValuePair<int, VoxelProperty> item in properties) {
                                            if (string.IsNullOrEmpty(item.Value.stringValue)) {
                                                bit.SetProperty(item.Key, item.Value.floatValue);
                                            } else {
                                                bit.SetProperty(item.Key, item.Value.stringValue);
                                            }
                                        }
                                    }
                                    finally {
                                        BufferPool<KeyValuePair<int, VoxelProperty>>.Release(properties);
                                    }
                                }

                                bits.Add(bit);
                            }
                        } else if (captureEmptySpaces) {
                            ModelBit emptyBit = new ModelBit();
                            emptyBit.voxelIndex = modelIndex;
                            emptyBit.isEmpty = true;
                            bits.Add(emptyBit);
                        }
                        // Check for light sources (torches)
                        LightSource ls = chunk.GetLightSource(voxelIndex);
                        if ((object)ls != null) {
                            TorchBit torchBit = new TorchBit();
                            torchBit.itemDefinition = ls.itemDefinition;
                            torchBit.voxelIndex = modelIndex;
                            torchBit.normal = ls.hitInfo.normal;
                            torchBits.Add(torchBit);
                        }
                    }
                }
            }
            md.SetBits(bits.ToArray());
            md.torches = torchBits.ToArray();
            BufferPool<ModelBit>.Release(bits);
            return md;
        }


        /// <summary>
        /// Shows/hides current voxel highlight
        /// </summary>
        /// <returns><c>true</c>, if highlight was voxeled, <c>false</c> otherwise.</returns>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public void VoxelHighlight (bool visible) {
            foreach (var vh in highlightedVoxels) {
                if (vh != null) {
                    vh.SetActive(visible);
                }
            }
            if (!visible) {
                RefreshVoxelHighlight();
            }
        }

        /// <summary>
        /// Shows an hologram of a model definition at a given position
        /// </summary>
        /// <returns>The highlight.</returns>
        /// <param name="modelDefinition">Model definition.</param>
        /// <param name="position">Position.</param>
        public GameObject ModelHighlight (ModelDefinition modelDefinition, Vector3d position, float rotationDegrees = 0) {
            if (modelDefinition == null) return null;

            if (modelDefinition.modelGameObject == null) {
                modelDefinition.modelGameObject = ModelCreateGameObject(modelDefinition);
            }
            GameObject modelGO = modelDefinition.modelGameObject;
            if (modelGO == null) {
                return null;
            }

            if (modelGO.TryGetComponent(out MeshRenderer renderer)) {
                Material mat = renderer.sharedMaterial;
                modelHighlightMat.SetTexture(ShaderParams.MainTex, mat.GetTexture(ShaderParams.MainTex));
                renderer.sharedMaterial = modelHighlightMat;
            }

            int halfSizeX = modelDefinition.sizeX / 2;
            int halfSizeZ = modelDefinition.sizeZ / 2;

            Vector3d corner1 = Quaternion.Euler(0, rotationDegrees, 0) * new Vector3(-halfSizeX, 0, -halfSizeZ);
            Vector3d corner2 = corner1 + Quaternion.Euler(0, rotationDegrees, 0) * new Vector3(modelDefinition.sizeX - 1, 0, modelDefinition.sizeZ - 1);
            Vector3d previewPos = new Vector3d((corner1.x + corner2.x) * 0.5f + position.x, position.y - 0.5f, (corner1.z + corner2.z) * 0.5f + position.z);

            modelGO.transform.SetParent(worldRoot, false);
            modelGO.transform.localPosition = previewPos + modelDefinition.offset;
            modelGO.transform.localRotation = Quaternion.Euler(0, rotationDegrees, 0);

            modelGO.SetActive(true);

            return modelGO;
        }


        /// <summary>
        /// Reloads all world textures
        /// </summary>
        public void ReloadTextures () {
            // reset texture providers
            DisposeTextures();
            InitRenderingMaterials();
            LoadWorldTextures();
        }


        /// <summary>
        /// Sets or cancel build mode
        /// </summary>
        /// <param name="buildMode">If set to <c>true</c> build mode.</param>
        public void SetBuildMode (bool buildMode) {
            if (!enableBuildMode)
                return;

            // Remember current selected item
            InventoryItem currentItem = InventoryItem.Null;
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player != null) {
                currentItem = player.GetSelectedItem();
            }

            this.buildMode = buildMode;

            // refresh inventory
            VoxelPlayUI ui = VoxelPlayUI.instance;
            if (ui != null) {
                ui.RefreshInventoryContents();
            }

            // Reselect item
            if (player != null && currentItem != InventoryItem.Null) {
                if (!player.SetSelectedItem(currentItem)) {
                    player.SetSelectedItem(0);
                }
            }
        }

        /// <summary>
        /// Shows an error message in the console
        /// </summary>
        /// <param name="errorMessage">Error message.</param>
        public void ShowError (string errorMessage, bool openConsole = false) {
            if (applicationIsPlaying) {
                ShowMessage(errorMessage, 4, false, openConsole);
            }
            Debug.LogError(errorMessage);
        }

        /// <summary>
        /// Shows a custom message into the status text.
        /// </summary>
        /// <param name="txt">Text.</param>
        public void ShowMessage (string txt, float displayDuration = 4, bool flashEffect = false, bool openConsole = false, bool allowDuplicatedMessage = false, string colorName = null) {
            if (!allowDuplicatedMessage && lastMessage == txt)
                return;
            lastMessage = txt;

            if (VoxelPlayUI.instance != null) {
                ExecuteInMainThread(delegate () {
                    if (!string.IsNullOrEmpty(colorName)) {
                        txt = "<color=" + colorName + ">" + txt + "</color>";
                    }
                    VoxelPlayUI.instance.AddMessage(txt, displayDuration, flashEffect, openConsole);
                });
            }
        }

        /// <summary>
        /// Logs and adds a message if debug level is set to verbose
        /// </summary>
        /// <param name="txt"></param>
        public void LogMessage (string txt, bool bold = false) {
            if (debugLevel == LogLevel.Verbose && Application.isPlaying) {
                if (bold) {
                    txt = "<b>" + txt + "</b>";
                }
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "<color=green>Voxel Play {0:yyyy-MM-dd HH:mm:ss}: {1}</color>", DateTime.Now, txt);
            }
        }


        /// <summary>
        /// Get an item from the allItems array of a given category and voxel type
        /// </summary>
        /// <returns>The item of requested category and type.</returns>
        /// <param name="category">Category.</param>
        /// <param name="voxelType">Voxel type.</param>
        public ItemDefinition GetItemDefinition (ItemCategory category, VoxelDefinition voxelType = null) {
            if (allItems == null)
                return null;
            int allItemsCount = allItems.Count;
            for (int k = 0; k < allItemsCount; k++) {
                if (allItems[k].item.category == category && (allItems[k].item.voxelType == voxelType || voxelType == null)) {
                    return allItems[k].item;
                }
            }
            return null;
        }

        /// <summary>
        /// Get an item from the allItems array of a given category and voxel type
        /// </summary>
        /// <returns>The item of requested category and type.</returns>
        /// <param name="category">Category.</param>
        /// <param name="objName">Voxel type name or item name.</param>
        public ItemDefinition GetItemDefinition (ItemCategory category, string objName) {
            if (category == ItemCategory.Voxel) {
                VoxelDefinition voxelDefinition = GetVoxelDefinition(objName);
                return GetItemDefinition(ItemCategory.Voxel, voxelDefinition);
            }

            return GetItemDefinition(objName);
        }


        /// <summary>
        /// Returns the item definition by its name
        /// </summary>
        public ItemDefinition GetItemDefinition (string name) {
            if (string.IsNullOrEmpty(name)) return null;
            itemDefinitionsDict.TryGetValue(name, out ItemDefinition id);
            return id;
        }

        /// <summary>
        /// Creates a recoverable item and throws it at given position, direction and strength
        /// </summary>
        /// <param name="position">Position in world space.</param>
        /// <param name="direction">Direction.</param>
        /// <param name="itemDefinition">Item definition.</param>
        public GameObject ItemThrow (Vector3d position, Vector3 direction, float velocity, ItemDefinition itemDefinition) {
            GameObject itemGO = CreateRecoverableItem(position, itemDefinition);
            if (itemGO == null)
                return null;
            if (itemGO.TryGetComponent(out Rigidbody rb)) {
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.linearVelocity = direction * velocity;
            }
            return itemGO;
        }


        /// <summary>
        /// Creates a persistent item by name
        /// </summary>
        /// <returns><c>true</c>, if item was spawned, <c>false</c> otherwise.</returns>
        public GameObject ItemSpawn (string itemDefinitionName, Vector3d position, float quantity = 1) {
            ItemDefinition id = GetItemDefinition(itemDefinitionName);
            return CreateRecoverableItem(position, id, quantity);
        }

        /// <summary>
        /// Adds a torch.
        /// </summary>
        /// <param name="hitInfo">Information about the hit position where the torch should be attached.</param>
        /// <param name="torchDefinition">Desired torch item</param>
        public GameObject TorchAttach (VoxelHitInfo hitInfo, ItemDefinition torchDefinition = null, bool refreshChunks = true) {
            if ((object)torchDefinition != null && torchDefinition.category != ItemCategory.Torch) {
                //Not a torch item
                return null;
            }

            return TorchAttachInt(hitInfo, torchDefinition, refreshChunks);
        }

        /// <summary>
        /// Attach a torch to a given voxel by its world position on the face defined by a normal
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public GameObject TorchAttach (Vector3d worldPos, Vector3 normal, ItemDefinition torchDefinition = null, bool refreshChunks = true) {
            if ((object)torchDefinition != null && torchDefinition.category != ItemCategory.Torch) {
                //Not a torch item
                return null;
            }

            VoxelChunk chunk;
            int voxelIndex;
            if (GetVoxelIndex(worldPos, out chunk, out voxelIndex, false)) {
                VoxelHitInfo hitInfo = new VoxelHitInfo();
                Vector3d voxelCenter = GetVoxelPosition(chunk, voxelIndex);
                hitInfo.voxelCenter = voxelCenter;
                hitInfo.normal = normal;
                hitInfo.chunk = chunk;
                hitInfo.voxelIndex = voxelIndex;
                return TorchAttachInt(hitInfo, torchDefinition, refreshChunks);
            } else {
                return null;
            }

        }

        /// <summary>
        /// Removes an existing torch.
        /// </summary>
        /// <param name="chunk">Chunk where the torch is currently attached.</param>
        /// <param name="gameObject">Game object of the torch itself.</param>
        public void TorchDetach (VoxelChunk chunk, GameObject gameObject) {
            TorchDetachInt(chunk, gameObject);
        }


        /// <summary>
        /// Voxel Play Input manager reference.
        /// </summary>
        [NonSerialized]
        public VoxelPlayInputController input;


        /// <summary>
        /// Sets current time of day in 24h numeric format
        /// </summary>
        /// <param name="time">Time in 0-24h range</param>
        /// <param name="azimuth">Optional Sun azimuth. If no value is provided, the azimuth value specified in the world definition is used.</param>
        public void SetTimeOfDay (float time, float azimuth = -1) {
            Vector3 r;
            r.x = 360 * (time / 24f) + 270;
            r.y = azimuth < 0 ? world.azimuth : azimuth;
            r.z = 0;
            Transform t = sun.transform;
            t.rotation = Quaternion.Euler(r);
            sunStartRotation = t.rotation;
            sunStartDirectionTimestamp = Time.time;
        }

        /// <summary>
        /// Sets current time of day in HH:MM string format
        /// </summary>
        /// <param name="time">HH:MM string (HH=24 hour format, MM=minutes)</param>
        /// <param name="azimuth"></param>
        public void SetTimeOfDay (string time, float azimuth = -1) {
            if (string.IsNullOrEmpty(time)) return;
            string[] t = time.Trim().Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 2) {
                if (float.TryParse(t[0], out float hour) && float.TryParse(t[1], out float minute))
                    SetTimeOfDay(hour + minute / 6000f, azimuth);
            }
        }

        /// <summary>
        /// Returns the time of day in 0-24h range
        /// </summary>
        /// <returns></returns>
        public float GetTimeOfDay () {
            Vector3 sunDirection = sun.transform.forward;
            Vector3 azimuthDirection = Quaternion.Euler(0, world.azimuth, 0) * Vector3.right;
            float angle = Vector3.SignedAngle(Vector3.up, sunDirection, azimuthDirection);
            if (angle < 0) angle += 360f;
            float time = (angle % 360f) * 24f / 360f;
            return time;
        }

        /// <summary>
        /// Sets the Sun direction.
        /// </summary>
        public void SetSunRotation (Quaternion rotation) {
            if (sun == null) return;
            sunStartRotation = rotation;
            sunStartDirectionTimestamp = Time.time;
            sun.transform.rotation = sunStartRotation;
        }


        /// <summary>
        /// Triggers a voxel click event. This function can be invoked by external classes to inform that a click on a voxel has been produced (ie. player hits a voxel)
        /// </summary>
        public void TriggerVoxelClickEvent (VoxelChunk chunk, int voxelIndex, int mouseButtonIndex) {
            if (captureEvents && OnVoxelClick != null) {
                OnVoxelClick(chunk, voxelIndex, mouseButtonIndex);
            }
        }

        #endregion

    }

}

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxelPlay {

    public abstract partial class VoxelPlayCharacterControllerBase : MonoBehaviour, IVoxelPlayCharacterController {

        [HideInInspector]
        public bool useThirdPartyController;

        [Header("Start Position")]
        [Tooltip("Places player on a random position in world which is flat. If this option is not enabled, the current gameobject transform position will be used.")]
        public bool startOnFlat = true;

        [Tooltip("Number of terrain checks to determine a flat position. The more iterations the lower the resulting starting position.")]
        [Range(1, 100)]
        public int startOnFlatIterations = 50;

        [Header("State Flags (Informative)")]
        [Tooltip("Player is flying - can go up/down with E and Q keys.")]
        public bool isFlying;

        [Tooltip("Player is thrusting - can apply vertical thrust using X key.")]
        public bool isThrusting;

        [Tooltip("Player is moving (walk or run)")]
        public bool isMoving;

        [Tooltip("Player is pressing any move key")]
        public bool isPressingMoveKeys;

        [Tooltip("Player is running")]
        public bool isRunning;

        [Tooltip("Player is either on water surface or under water")]
        public bool isInWater;

        [Tooltip("Player is on water surface.")]
        public bool isSwimming;

        [Tooltip("Player is below water surface.")]
        public bool isUnderwater;

        [Tooltip("Player is on ground.")]
        public bool isGrounded;

        [Tooltip("Player is crouched.")]
        public bool isCrouched;

        [Tooltip("Player is underground or undercover.")]
        public bool isUnderground;

        [Tooltip("Voxel under character feet")]
        public VoxelDefinition voxelUnder;

        [Header("Managed Actions By This Controller")]
        [Tooltip("Allows this character controller to attack (defaults to left mouse button or tap)")]
        public bool manageAttack = true;
        [Tooltip("Allows this character controller to crouch (defaults to C key)")]
        public bool manageCrouch = true;
        [Tooltip("Allows this character controller to jump (defaults to Space key)")]
        public bool manageJump = true;
        [Tooltip("Allows this character controller to build/place voxels (defaults to right mouse button)")]
        public bool manageBuild = true;
        [Tooltip("Allows this character controller to place voxels (defaults to right mouse button)")]
        public bool managePlaceVoxels = true;
        [Tooltip("Allows this character controller to select a voxel clicking on the world (defaults to middle mouse button)")]
        public bool manageSelectVoxel = true;
        [Tooltip("Allows this character controller to fly (defaults to F key)")]
        public bool manageFly = true;
        [Tooltip("Allows this character controller to activate vertical propulsion (defaults to X key)")]
        public bool manageThrust;
        [Tooltip("Allows this character controller to rotate voxels (defaults to R key)")]
        public bool manageVoxelRotation;

        [Header("Sounds")]
        [SerializeField] AudioSource m_AudioSource;

        // the sound played when character enters water.
        public AudioClip waterSplash;
        // an array of swim stroke sounds that will be randomly selected from.
        public AudioClip[] swimStrokeSounds;
        public float swimStrokeInterval = 8;

        // an array of footstep sounds that will be randomly selected from.
        public AudioClip[] footstepSounds;

        [Range(0f, 1f)] public float runstepLenghten = 0.7f;
        public float footStepInterval = 5;

        // the sound played when character leaves the ground.
        public AudioClip jumpSound;

        // the sound played when character touches back on ground.
        public AudioClip landSound;

        public AudioClip cancelSound;

        [Header("World Limits")]
        public bool limitBoundsEnabled;
        public Bounds limitBounds;
        [Tooltip("Detects collisions and moves player back or ontop a safe position")]
        public bool unstuck = true;

        [Header("Crosshair")]
        [Tooltip("Note: crosshair is currently disabled in VR")]
        public bool enableCrosshair = true;
        [Tooltip("Microvoxels mode. 0=disabled. Value means the microvoxels count per axis to be placed (not the world size).")]
        public int microVoxels;
        [Tooltip("Max distance from character to selection")]
        public float microVoxelsProb = 1f;
        public float crosshairMaxDistance = 30f;
        public float crosshairScale = 0.1f;
        public float targetAnimationSpeed = 0.75f;
        public float targetAnimationScale = 0.2f;
        public Color crosshairOnTargetColor = Color.yellow;
        public Color crosshairNormalColor = Color.white;
        public LayerMask crosshairHitLayerMask = -1;
        [Tooltip("Crosshair will change over a reachable voxel.")]
        public bool changeOnBlock = true;
        [Tooltip("Enable move crosshair on screen")]
        public bool freeMode;
        [Tooltip("When enabled, crosshair colors invert according to background color to enhance visibility. This feature uses GrabPass which can be expensive on mobile.")]
        public bool autoInvertColors = true;
        [Tooltip("Used to shift elevation of the preview when placing a model")]
        public float wheelSensibility = 10f;

        [Header("Voxel Highlight")]
        public bool voxelHighlight = true;
        public Color voxelHighlightColor = Color.yellow;
        [Range(1f, 100f)]
        public float voxelHighlightEdge = 20f;
        public IgnoreWaterOption ignoreWater = IgnoreWaterOption.DefinedByWaterVoxelDefinition;
        public bool slabMode;

        /// <summary>
        /// Triggered when player enters a voxel if that voxel definition has triggerEnterEvent = true
        /// </summary>
        public event VoxelEvent OnVoxelEnter;

        /// <summary>
        /// Triggered when a player walks over a voxel if that voxel definition has triggerWalkEvent = true
        /// </summary>
        public event VoxelEvent OnVoxelWalk;


        // internal fields
        int lastPosX, lastPosY, lastPosZ;
        int lastVoxelTypeIndex;
        float nextPlayerDamageTime;
        float lastDiveTime;
        float m_StepCycle;
        float m_NextStep;
        bool modelBuildPreview;
        ModelDefinition modelBuildItem;
        bool modelBuildInProgress;
        Vector3d modelBuildPreviewPosition;
        int buildRotationDegrees;
        GameObject modelBuildPreviewGO;
        float modelBuildPreviewOffset;


        protected VoxelHitInfo _crosshairHitInfo;
        public VoxelHitInfo crosshairHitInfo => _crosshairHitInfo;

        [NonSerialized]
        public bool crosshairOnBlock;
        Vector3 m_LastNonCollidingCharacterPos;
        protected IVoxelPlayPlayer _player;
        [SerializeField, HideInInspector] protected float _characterHeight = 1.8f;
        protected VoxelPlayInputController input;

        int lastPositionX, lastPositionZ;

        const float MIN_DIST = 0.5f;
        const float MICROVOXEL_PLACING_QUANTITY = 1f / MicroVoxels.COUNT_PER_VOXEL;
        const float MICROVOXEL_PLACING_SLAB_QUANTITY = 0.5f;

        public virtual float GetCharacterHeight () {
            return _characterHeight;
        }


        public IVoxelPlayPlayer player {
            get {
                if (_player == null) {
                    _player = transform.root.GetComponentInChildren<IVoxelPlayPlayer>();
                    if (_player == null) {
                        _player = transform.root.gameObject.AddComponent<VoxelPlayPlayer>();
                    }
                }
                return _player;
            }
        }

        [NonSerialized]
        public VoxelPlayEnvironment env;



        protected void Init () {
            m_AudioSource = GetComponent<AudioSource>();
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle / 2f;
            m_LastNonCollidingCharacterPos = Misc.vector3max;

            env = VoxelPlayEnvironment.instance;
            if (env == null) {
                Debug.LogError("Voxel Play Environment must be added first.");
            } else {
                env.characterController = this;
                env.OnInitialized += () => InitDelayed();
            }
        }

        void InitDelayed () {
            input = env.input;
#if UNITY_EDITOR
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, env.layerVoxels)) {
                Debug.LogError("Player currently can't collide with voxels. Please check physics collision matrix in Project settings or change Voxels Layer in VoxelPlayEnvironment component.");
            }
#endif
            SetupSelectedItemMicroVoxelSize();
            player.OnItemSelectedChanged += (_, _) => SetupSelectedItemMicroVoxelSize();
        }


        /// <summary>
        /// Enables microvoxels if item enables it
        /// </summary>
        protected virtual bool SetupSelectedItemMicroVoxelSize () {
            InventoryItem activeItem = player.GetSelectedItem();
            ItemDefinition item = activeItem.item;
            int microVoxels = 0;
            float microVoxelsProb = 1f;
            if (item != null) {
                microVoxels = item.GetPropertyValue<int>("microVoxels");
                microVoxelsProb = item.GetPropertyValue<float>("microVoxelsProb", 1f);
            }
            SetMicroVoxelSize(microVoxels, microVoxelsProb);
            return microVoxels > 0;
        }

        /// <summary>
        /// Updates internal rotation variables based on current character and camera transforms
        /// </summary>
        public abstract void UpdateLook ();

        public abstract bool isReady { get; }

        private void OnApplicationFocus (bool focus) {
            if (input != null) {
                input.focused = focus;
            }
        }


        /// <summary>
        /// Sets microvoxels mode
        /// </summary>
        /// <param name="state"></param>
        public virtual void SetMicroVoxelSize (int size, float probability) {
            microVoxels = size;
            microVoxelsProb = probability;
        }


        /// <summary>
        /// Toggles on/off microvoxels mode
        /// </summary>
        public virtual bool ToggleMicroVoxels () {
            if (microVoxels > 0) {
                // if microvoxels are already enabled, disable them
                SetMicroVoxelSize(0, 0f);
                return true;
            }

            bool changed = SetupSelectedItemMicroVoxelSize();
            if (!changed) {
                InventoryItem selectedInventoryItem = player.GetSelectedItem();
                ItemDefinition item = selectedInventoryItem.item;
                if (item != null && item.category == ItemCategory.Voxel) {
                    microVoxels = 1;
                    microVoxelsProb = 1f;
                    changed = true;
                }
            }
            return changed;
        }

        public virtual bool ToggleMicroVoxelSize () {
            // Only toggle microvoxel size if the currently selected item is of a voxel type
            InventoryItem selectedInventoryItem = player.GetSelectedItem();
            ItemDefinition currentItem = selectedInventoryItem.item;

            if (currentItem != null && currentItem.category == ItemCategory.Voxel) {
                // Cycle through powers of 2 from 0 to COUNT_PER_AXIS
                if (microVoxels == 0) {
                    microVoxels = 1;
                } else if (microVoxels * 2 < MicroVoxels.COUNT_PER_AXIS) {
                    microVoxels *= 2;
                } else {
                    microVoxels = 0;
                }
                microVoxelsProb = 1f;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Toggles slab placement mode ON/OFF.
        /// </summary>
        public virtual void ToggleSlabMode () {
            slabMode = !slabMode;
            if (slabMode) {
                env.ShowMessage("<color=green>Slab Mode <color=yellow>ON</color></color>");
            } else {
                env.ShowMessage("<color=green>Slab Mode <color=yellow>OFF</color></color>");
            }
        }

        /// <summary>
        /// Toggles on/off character light
        /// </summary>
        public virtual void ToggleCharacterLight () {
            Light light = GetComponentInChildren<Light>();
            if (light != null) {
                ToggleCharacterLight(!light.enabled);
            }
        }

        /// <summary>
        /// Toggles on/off character light
        /// </summary>
        public virtual void ToggleCharacterLight (bool state) {
            Light light = GetComponentInChildren<Light>();
            if (light != null && light.enabled != state) {
                light.enabled = state;
                if (light.enabled) {
                    env.ShowMessage("<color=green>Player torch <color=yellow>ON</color></color>");
                } else {
                    env.ShowMessage("<color=green>Player torch <color=yellow>OFF</color></color>");
                }
            }
        }

        protected virtual void CheckFootfalls () {
            if (isGrounded) {
                Vector3 curPos = transform.position;
                int x = (int)curPos.x;
                int y = (int)curPos.y;
                int z = (int)curPos.z;
                if (x != lastPosX || y != lastPosY || z != lastPosZ || voxelUnder == null) {
                    lastPosX = x;
                    lastPosY = y;
                    lastPosZ = z;
                    curPos.y = FastMath.FloorToInt(curPos.y) - 0.5f;
                    VoxelIndex index = env.GetVoxelUnderIndex(curPos, true, ColliderTypes.IgnorePlayer);
                    if (index.typeIndex != lastVoxelTypeIndex || voxelUnder == null) {
                        lastVoxelTypeIndex = index.typeIndex;
                        if (lastVoxelTypeIndex != 0) {
                            voxelUnder = index.type;
                            SetFootstepSounds(voxelUnder.footfalls, voxelUnder.landingSound, voxelUnder.jumpSound);
                            if (voxelUnder.triggerWalkEvent && OnVoxelWalk != null) {
                                OnVoxelWalk(index.chunk, index.voxelIndex);
                            }
                        }
                    }
                }
            }
            if (isGrounded || isInWater) {
                CheckDamage(voxelUnder);
            }
        }

        protected virtual void CheckDamage (VoxelDefinition voxelType) {
            if (voxelType == null)
                return;
            int playerDamage = voxelType.playerDamage;
            if (playerDamage > 0 && Time.time > nextPlayerDamageTime) {
                nextPlayerDamageTime = Time.time + voxelType.playerDamageDelay;
                player.DamageToPlayer(playerDamage);
            }
        }

        protected virtual void CheckEnterTrigger (VoxelChunk chunk, int voxelIndex) {
            if (chunk != null && env.voxelDefinitions[chunk.voxels[voxelIndex].typeIndex].triggerEnterEvent && OnVoxelEnter != null) {
                OnVoxelEnter(chunk, voxelIndex);
            }
        }

        public virtual void SetFootstepSounds (AudioClip[] footStepsSounds, AudioClip jumpSound, AudioClip landSound) {
            this.footstepSounds = footStepsSounds;
            this.jumpSound = jumpSound;
            this.landSound = landSound;
        }

        public virtual void PlayLandingSound () {
            if (isInWater || m_AudioSource == null)
                return;
            m_AudioSource.clip = landSound;
            m_AudioSource.Play();
            m_NextStep = m_StepCycle + .5f;
        }



        public virtual void PlayJumpSound () {
            if (isInWater || isFlying || m_AudioSource == null)
                return;
            m_AudioSource.clip = jumpSound;
            m_AudioSource.Play();
        }


        public virtual void PlayCancelSound () {
            if (m_AudioSource == null)
                return;
            m_AudioSource.clip = cancelSound;
            m_AudioSource.Play();
        }


        public virtual void PlayWaterSplashSound () {
            if (Time.time - lastDiveTime < 1f)
                return;
            lastDiveTime = Time.time;
            m_NextStep = m_StepCycle + swimStrokeInterval;
            if (waterSplash != null && m_AudioSource != null) {
                m_AudioSource.clip = waterSplash;
                m_AudioSource.Play();
            }
        }

        /// <summary>
        /// Plays a sound at character position
        /// </summary>
        public virtual void PlayCustomSound (AudioClip sound) {
            if (sound != null && m_AudioSource != null) {
                m_AudioSource.clip = sound;
                m_AudioSource.Play();
            }
        }


        protected virtual void ProgressStepCycle (float velocityMagnitude, float speed) {
            if (velocityMagnitude > 0 && isPressingMoveKeys) {
                m_StepCycle += (velocityMagnitude + (speed * (isMoving ? 1f : runstepLenghten))) * Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep)) {
                return;
            }

            m_NextStep = m_StepCycle + footStepInterval;

            PlayFootStepAudio();
        }



        protected virtual void PlayFootStepAudio () {
            if (!isGrounded || m_AudioSource == null) {
                return;
            }
            if (footstepSounds == null || footstepSounds.Length == 0)
                return;
            // pick & play a random footstep sound from the array,
            // excluding sound at index 0
            int n;
            if (footstepSounds.Length == 1) {
                n = 0;
            } else {
                n = Random.Range(1, footstepSounds.Length);
            }
            m_AudioSource.clip = footstepSounds[n];
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
            // move picked sound to index 0 so it's not picked next time
            footstepSounds[n] = footstepSounds[0];
            footstepSounds[0] = m_AudioSource.clip;
        }


        protected virtual void ProgressSwimCycle (Vector3 velocity, float speed) {
            if (velocity.sqrMagnitude > 0 && isPressingMoveKeys) {
                m_StepCycle += (velocity.magnitude + speed) * Time.fixedDeltaTime;
            }

            if (!(m_StepCycle > m_NextStep)) {
                return;
            }

            m_NextStep = m_StepCycle + swimStrokeInterval;

            if (!isUnderwater) {
                PlaySwimStrokeAudio();
            }
        }


        protected virtual void PlaySwimStrokeAudio () {
            if (swimStrokeSounds == null || swimStrokeSounds.Length == 0 || m_AudioSource == null)
                return;
            // pick & play a random swim stroke sound from the array,
            // excluding sound at index 0
            int n;
            if (swimStrokeSounds.Length == 1) {
                n = 0;
            } else {
                n = Random.Range(1, swimStrokeSounds.Length);
            }
            m_AudioSource.clip = swimStrokeSounds[n];
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
            // move picked sound to index 0 so it's not picked next time
            swimStrokeSounds[n] = swimStrokeSounds[0];
            swimStrokeSounds[0] = m_AudioSource.clip;
        }


        /// <summary>
        /// Moves character controller to a new position. Use this method instead of changing the transform position
        /// </summary>
        public abstract void MoveTo (Vector3 newPosition);

        protected void StartOnFlat () {
            if (env.world == null || env.world.terrainGenerator == null)
                return;
            float minAltitude = env.world.terrainGenerator.maxHeight;
            Vector3 flatPos = transform.position;
            Vector3 randomPos = flatPos;

            // Use terrain generator bounds if available (e.g. Unity Terrain Generator)
            bool hasTerrainBounds = env.world.terrainGenerator.GetTerrainBounds(out Vector3 terrainCenter, out Vector3 terrainSize);

            for (int k = 0; k < startOnFlatIterations; k++) {
                if (hasTerrainBounds) {
                    randomPos.x = Random.Range(terrainCenter.x - terrainSize.x * 0.5f, terrainCenter.x + terrainSize.x * 0.5f);
                    randomPos.z = Random.Range(terrainCenter.z - terrainSize.z * 0.5f, terrainCenter.z + terrainSize.z * 0.5f);
                } else if (env.world.infinite) {
                    randomPos = Random.insideUnitSphere * 1000;
                } else {
                    randomPos.x = Random.Range(env.world.center.x - env.world.extents.x, env.world.center.x + env.world.extents.x);
                    randomPos.z = Random.Range(env.world.center.z - env.world.extents.z, env.world.center.z + env.world.extents.z);
                }
                float alt = env.GetTerrainHeight(randomPos);
                if (alt < minAltitude && alt >= env.waterLevel + 1) {
                    minAltitude = alt;
                    randomPos.y = alt + GetCharacterHeight() * 0.5f + 0.1f;
                    flatPos = randomPos;
                }
            }
            transform.position = flatPos;
        }


        /// <summary>
        /// Moves character controller by a distance. Use this method instead of changing the transform position
        /// </summary>
        public virtual void Move (Vector3 newPosition) { }


        protected virtual void ControllerUpdate () {

            // Toggles MicroVoxels mode
            if (input.GetButtonDown(InputButtonNames.MicroVoxels)) {
                if (ToggleMicroVoxels()) {
                    if (microVoxels > 0) {
                        env.ShowMessage("<color=green>MicroVoxels <color=yellow>ON</color></color>");
                    } else {
                        env.ShowMessage("<color=green>MicroVoxels <color=yellow>OFF</color></color>");
                    }
                } else {
                    env.ShowMessage("<color=green>Current item does not support microvoxels</color>");
                }
            }

            if (input.GetButtonDown(InputButtonNames.ToggleMicroVoxelSize)) {
                if (ToggleMicroVoxelSize()) {
                    env.ShowMessage(string.Format("<color=green>Microvoxel size: <color=yellow>{0}</color></color>", microVoxels == 0 ? "OFF" : microVoxels.ToString()));
                } else {
                    env.ShowMessage("<color=green>Current item does not support changing microvoxel size</color>");
                }
            }

            // Toggle Slab Mode
            if (input.GetButtonDown(InputButtonNames.ToggleSlabMode)) {
                ToggleSlabMode();
            }

            if (input.GetButtonDown(InputButtonNames.Rotate)) {
                buildRotationDegrees = (buildRotationDegrees + 90) % 360;
            }
            ModelBuildPreviewUpdate();

            // Check if character has changed X/Z voxel position
            Vector3 pos = transform.position;
            int posX = (int)pos.x;
            int posZ = (int)pos.z;
            if (posX != lastPositionX || posZ != lastPositionZ) {
                lastPositionZ = posZ;
                lastPositionX = posX;
                CharacterChangedXZPosition(pos);
            }
        }

        protected virtual void CharacterChangedXZPosition (Vector3 newPosition) { }


        protected virtual void MoveBackAfterPlacing (Vector3d camPos, Vector3d placePos, float distance) {
            // Moves back character controller if voxel is put just on its position
            float distSqr = Vector3.SqrMagnitude(camPos - placePos);
            if (distSqr < MIN_DIST * MIN_DIST) {
                MoveTo(transform.position + _crosshairHitInfo.normal * distance);
            }
        }

        /// <summary>
        /// Implements building stuff
        /// </summary>
        /// <param name="camPos">The camera position OR the character position in a 3rd person controller</param>">
        protected virtual void DoBuild (Vector3 camPos, Vector3 forward, Vector3d hintedPlacePos) {
            if (player.selectedItemIndex < 0 || player.selectedItemIndex >= player.items.Count)
                return;

            InventoryItem inventoryItem = player.GetSelectedItem();
            ItemDefinition currentItem = inventoryItem.item;

            if (microVoxels > 0 && !slabMode) {
                // If the crosshair is on vegetation, destroy it instead of placing microvoxels
                if (_crosshairHitInfo.voxel.type != null && _crosshairHitInfo.voxel.type.isVegetation) {
                    env.VoxelDestroy(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex);
                    return;
                }

                VoxelDefinition placingVoxelType = null;

                // if we're placing a micro voxel on a voxel position which already has microvoxels, add a microvoxel of that type
                if (env.IsMicroVoxelAtPosition(ref _crosshairHitInfo)) {
                    // player needs the resource in non-build mode
                    if (currentItem != null && currentItem.category == ItemCategory.Voxel) {
                        placingVoxelType = currentItem.voxelType;
                    } else {
                        placingVoxelType = _crosshairHitInfo.voxel.type;
                    }
                    if (!env.buildMode) {
                        inventoryItem = player.GetInventoryItem(placingVoxelType);
                        if (inventoryItem == null || inventoryItem.quantity < MICROVOXEL_PLACING_QUANTITY) {
                            PlayCancelSound();
                            return;
                        }
                        player.ConsumeItem(inventoryItem.item, MICROVOXEL_PLACING_QUANTITY);
                    }
                    int rotation = _crosshairHitInfo.voxel.GetTextureRotation();
                    env.SetNextPlacementAnimation(_crosshairHitInfo.point, _crosshairHitInfo.normal);
                    env.MicroVoxelPlace(ref _crosshairHitInfo, microVoxels, placingVoxelType, probability: microVoxelsProb);
                    MoveBackAfterPlacing(camPos, _crosshairHitInfo.voxelCenter, MicroVoxels.SIZE);
                    return;
                }

                // check if current item can place microvoxels
                if (currentItem != null && inventoryItem.quantity >= MICROVOXEL_PLACING_QUANTITY) {
                    placingVoxelType = currentItem.voxelType;
                }

                // if not, check if the rayhit voxel definition supports micro-voxels, then use it
                if (placingVoxelType == null || !placingVoxelType.supportsMicroVoxels) {
                    placingVoxelType = _crosshairHitInfo.voxel.type;
                }

                // ensure that the selected item is compatible with micro-voxels
                if (placingVoxelType == null || !placingVoxelType.supportsMicroVoxels) {
                    PlayCancelSound();
                    return;
                }

                // place a voxel + microvoxel
                inventoryItem = player.GetInventoryItem(placingVoxelType);
                if (!env.buildMode) {
                    player.ConsumeItem(inventoryItem.item, MICROVOXEL_PLACING_QUANTITY);
                }
                int textureRotation = GetTextureRotationForPlacement(placingVoxelType, forward);
                env.SetNextPlacementAnimation(_crosshairHitInfo.point, _crosshairHitInfo.normal);
                if (env.MicroVoxelPlace(ref _crosshairHitInfo, microVoxels, placingVoxelType, probability: microVoxelsProb, placingVoxelType.tintColor, rotation: textureRotation)) {
                    MoveBackAfterPlacing(camPos, _crosshairHitInfo.voxelCenter, MicroVoxels.SIZE);
                }
                return;
            }

            switch (currentItem.category) {
                case ItemCategory.Voxel:

                    // Basic placement rules
                    bool canPlace = crosshairOnBlock;
                    Voxel existingVoxel = _crosshairHitInfo.voxel;
                    VoxelDefinition existingVoxelType = existingVoxel.type;
                    Vector3d placePos;

                    VoxelDefinition placeVoxelType = currentItem.voxelType;
                    if (placeVoxelType == null) return;
                    bool placingSlab = slabMode && placeVoxelType.supportsMicroVoxels;

                    Vector3 hitNormal = _crosshairHitInfo.normal;
                    bool isVertical = Math.Abs(hitNormal.y) > 0.9f;
                    if (currentItem.voxelType.isLiquid && !canPlace) {
                        canPlace = true; // water can be poured anywhere
                        placePos = camPos + forward * 3f;
                    } else {
                        placePos = env.GetVoxelPosition(_crosshairHitInfo.voxelCenter);
                        if (canPlace && isVertical) {
                            // Make sure there's a valid voxel under position (ie. do not build a voxel on top of grass)
                            canPlace = existingVoxelType != null && existingVoxelType.renderType != RenderType.CutoutCross && (!existingVoxelType.isLiquid || currentItem.voxelType.isLiquid);
                            // Non-slab mode: if clicking the top face of a bottom half voxel, override to place a top slab within the same voxel
                            if (!placingSlab && canPlace && !currentItem.voxelType.isLiquid && placeVoxelType.supportsMicroVoxels) {
                                if (hitNormal.y > 0.5f && env.IsBottomHalfAtPosition(placePos)) {
                                    placingSlab = true;
                                } else if (hitNormal.y < 0.5f && env.IsTopHalfAtPosition(placePos)) {
                                    placingSlab = true;
                                }
                            }
                        }
                        // Slab mode placement: adjust target position so slabs can be stacked within the same voxel
                        if (existingVoxelType.renderType != RenderType.CutoutCross) {
                            placePos.x += hitNormal.x;
                            placePos.z += hitNormal.z;
                            if (placingSlab && canPlace && !currentItem.voxelType.isLiquid) {
                                if (isVertical) {
                                    placePos.y = _crosshairHitInfo.point.y;
                                    placePos.y += hitNormal.y * 0.25;
                                } else {
                                    placePos.y = Math.Floor(_crosshairHitInfo.point.y * 2) / 2 + 0.25f;
                                }
                            } else {
                                placePos.y += hitNormal.y;
                            }
                        }
                    }

                    VoxelDefinition existingVoxelOnPlacePos = env.GetVoxel(placePos).type;
                    float distanceFromCenter = (float)(_crosshairHitInfo.point.y - env.GetVoxelPosition(_crosshairHitInfo.voxelCenter).y);
                    bool isOverCenter = false;

                    if (placeVoxelType.allowUpsideDownVoxel && placeVoxelType.upsideDownVoxel != null) {
                        isOverCenter = distanceFromCenter > 0 ? true : false;

                        isOverCenter = isVertical ? false : isOverCenter;
                        isOverCenter = !isVertical ? true : isOverCenter;

                        if (isOverCenter) {
                            placeVoxelType = placeVoxelType.upsideDownVoxel;
                        }
                    }

                    bool isPromoting = false;
                    // Check voxel promotion
                    if (canPlace) {
                        if (existingVoxelType == placeVoxelType || existingVoxelType == placeVoxelType.upsideDownVoxel) {
                            if (existingVoxelType.promotesTo != null) {
                                if (existingVoxelType.isUpsideDown) {
                                    if (!isVertical) {
                                        placePos = env.GetVoxelPosition(_crosshairHitInfo.voxelCenter);
                                        placeVoxelType = existingVoxelType.promotesTo;
                                        isPromoting = true;
                                    }
                                } else {
                                    if (isVertical) {
                                        placePos = env.GetVoxelPosition(_crosshairHitInfo.voxelCenter);
                                        placeVoxelType = existingVoxelType.promotesTo;
                                        isPromoting = true;
                                    }
                                }
                            }
                        }
                        if ((existingVoxelOnPlacePos == placeVoxelType || existingVoxelOnPlacePos == placeVoxelType.upsideDownVoxel) && !isPromoting) {
                            if (existingVoxelOnPlacePos.promotesTo != null) {
                                if (existingVoxelOnPlacePos.isUpsideDown) {
                                    if (distanceFromCenter < 0f || isVertical) {
                                        placeVoxelType = existingVoxelOnPlacePos.promotesTo;
                                        isPromoting = true;
                                    }
                                } else {
                                    if (distanceFromCenter > 0f || !isVertical) {
                                        placeVoxelType = existingVoxelOnPlacePos.promotesTo;
                                        isPromoting = true;
                                    }
                                }
                            }
                        }
                    }

                    // Compute rotation
                    int textureRotation = 0;
                    if (placeVoxelType.placeOnWall) {
                        if (!existingVoxelType.supportsDecorations) {
                            canPlace = false;
                        } else if (hitNormal.z > 0) {
                            textureRotation = 2;
                        } else if (hitNormal.z < 0) {
                            textureRotation = 0;
                        } else if (hitNormal.x > 0) {
                            textureRotation = 3;
                        } else if (hitNormal.x < 0) {
                            textureRotation = 1;
                        } else {
                            canPlace = false;
                        }
                        if (canPlace) {
                            if (existingVoxelOnPlacePos.hasContent) {
                                env.VoxelDestroy(placePos);
                            }
                        } else {
                            PlayCancelSound();
                        }
                    } else {
                        textureRotation = GetTextureRotationForPlacement(placeVoxelType, forward);
                    }

                    // Final check, does it overlap existing geometry?
                    if (canPlace && !isPromoting && !placingSlab) {
                        Quaternion rotationQ = Quaternion.Euler(0, Voxel.GetTextureRotationDegrees(textureRotation), 0);
                        canPlace = !env.VoxelOverlaps(placePos, placeVoxelType, rotationQ, 1 << env.layerVoxels, placingSlab);
                        if (!canPlace) {
                            PlayCancelSound();
                        }
                    }

#if UNITY_EDITOR
                    else if (env.constructorMode) {
                        placePos = hintedPlacePos;
                        placeVoxelType = currentItem.voxelType;
                        canPlace = true;
                    }
#endif

                    // Finally place the voxel
                    if (canPlace) {
                        // Consume item first
                        if (!env.buildMode) {
                            player.ConsumeItem();
                        }
                        // Place it
                        float amount = inventoryItem.quantity < 1f ? inventoryItem.quantity : 1f;
                        env.SetNextPlacementAnimation(_crosshairHitInfo.point, _crosshairHitInfo.normal);
                        if (env.VoxelPlace(placePos, placeVoxelType, playSound: true, player.selectedItemTintColor, amount, textureRotation, slabMode: placingSlab)) {
                            MoveBackAfterPlacing(camPos, placePos, 1f);
                        }
                    }
                    break;
                case ItemCategory.Torch:
                    if (crosshairOnBlock) {
                        GameObject torchAttached = env.TorchAttach(_crosshairHitInfo, currentItem);
                        if (!env.buildMode && torchAttached != null) {
                            player.ConsumeItem();
                        }
                    }
                    break;
                case ItemCategory.Model:
                    if (!modelBuildInProgress) {
                        if (modelBuildPreview) {
                            ModelPreviewCancel();
                            // check if building position is in frustum, otherwise cancel building
                            Vector3 viewportPos = env.cameraMain.WorldToViewportPoint(modelBuildPreviewPosition);
                            if (viewportPos.x < 0 || viewportPos.x > 1f || viewportPos.y < 0 || viewportPos.y > 1f || viewportPos.z < 0) {
                                return;
                            }
                            if (currentItem.model.buildDuration > 0) {
                                modelBuildInProgress = true;
                                env.ModelPlace(modelBuildPreviewPosition, currentItem.model, currentItem.model.buildDuration, buildRotationDegrees, 1f, fitTerrain: currentItem.model.fitToTerrain, FinishBuilding);
                            } else {
                                env.ModelPlace(modelBuildPreviewPosition, currentItem.model, buildRotationDegrees, colorBrightness: 1f, fitTerrain: currentItem.model.fitToTerrain);
                            }
                            player.ConsumeItem();
                        } else {
                            modelBuildPreview = true;
                            modelBuildItem = currentItem.model;
                            ModelBuildPreviewUpdate();
                        }
                    }
                    break;
                case ItemCategory.General:
                    ThrowCurrentItem(camPos, forward);
                    break;
            }
        }

        int GetTextureRotationForPlacement (VoxelDefinition vd, Vector3 forward) {
            if (!vd.placeFacingPlayer) return 0;

            // Orient voxel to player
            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z)) {
                if (forward.x > 0) {
                    return 1;
                } else {
                    return 3;
                }
            } else if (forward.z < 0) {
                return 2;
            }
            return 0;
        }

        protected virtual void ModelBuildPreviewUpdate () {
            if (!modelBuildPreview || !crosshairOnBlock || modelBuildItem == null)
                return;

            modelBuildPreviewPosition = _crosshairHitInfo.voxelCenter;

            modelBuildPreviewOffset += Input.GetAxis("Mouse ScrollWheel") * wheelSensibility;
            if (modelBuildPreviewOffset < 0) modelBuildPreviewOffset = 0;

            modelBuildPreviewPosition.y += (int)modelBuildPreviewOffset;

            modelBuildPreviewGO = env.ModelHighlight(modelBuildItem, modelBuildPreviewPosition, buildRotationDegrees);
        }

        /// <summary>
        /// Shows a highlight on the selected voxel
        /// </summary>
        protected virtual void DoHighlight () {
            if (!voxelHighlight) return;
            env.VoxelHighlight(ref _crosshairHitInfo, voxelHighlightColor, voxelHighlightEdge, microVoxelSize: microVoxels, slabMode: slabMode);
        }


        /// <summary>
        /// Removes an unit from current item in player inventory and throws it into the scene
        /// </summary>
        public virtual void ThrowCurrentItem (Vector3 throwPosition, Vector3 direction) {
            InventoryItem inventoryItem = player.ConsumeItem();
            if (inventoryItem == InventoryItem.Null)
                return;

            if (inventoryItem.item.category == ItemCategory.Voxel) {
                env.VoxelThrow(throwPosition, direction, 15f, inventoryItem.item.voxelType, Misc.color32White);
            } else if (inventoryItem.item.category == ItemCategory.General) {
                env.ItemThrow(throwPosition, direction, 15f, inventoryItem.item);
            }
        }


        protected virtual void FinishBuilding (ModelDefinition modelDefinition, Vector3d position) {
            modelBuildInProgress = false;
        }

        public virtual bool ModelPreviewCancel () {
            if (modelBuildPreview) {
                modelBuildPreview = false;
                if (modelBuildPreviewGO != null) {
                    modelBuildPreviewGO.SetActive(false);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ensures player is above terrain
        /// </summary>
        public virtual void Unstuck (bool toSurface = true) {
#if UNITY_EDITOR
            if (env.constructorMode) return;
#endif

            if (!unstuck) return;

            Vector3 transformPosition = transform.position;
            if (env.CheckCollision(env.cameraMain.transform.position) || env.CheckCollision(transformPosition)) {
                // try to move to last good position
                if (m_LastNonCollidingCharacterPos.y < float.MaxValue && !env.CheckCollision(m_LastNonCollidingCharacterPos)) {
                    MoveTo(m_LastNonCollidingCharacterPos);
                    return;
                }
                // try up or surface
                float minAltitude = Mathf.FloorToInt(transformPosition.y) + 1.1f;
                if (toSurface) {
                    minAltitude = Mathf.Max(env.GetTerrainHeight(transformPosition), minAltitude);
                }
                Vector3 pos = new Vector3(transformPosition.x, minAltitude + GetCharacterHeight() * 0.5f, transformPosition.z);
                MoveTo(pos);
            }
        }

        public virtual void AnnotateNonCollidingPosition (Vector3 position) {
            m_LastNonCollidingCharacterPos = position;
        }

        public virtual void ForceUpdateCrosshair(int frames = 2) { }

    }
}

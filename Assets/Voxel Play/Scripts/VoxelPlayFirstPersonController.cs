using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxelPlay {

    [ExecuteInEditMode]
    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public partial class VoxelPlayFirstPersonController : VoxelPlayCharacterControllerBase {

        [Header("Movement")]
        public float walkSpeed = 5f;
        public float runSpeed = 10f;
        public float flySpeed = 20f;
        public float swimSpeed = 3.7f;
        public float jumpSpeed = 10f;
        public float stickToGroundForce = 10f;
        public float gravityMultiplier = 2f;

        [Header("Climbing")]
        [Tooltip("If player can climb hills without requiring jumping over the voxels")]
        public bool canClimb = true;
        [Tooltip("Maximum step height allowed for climbing")]
        public float climbMaxStepHeight = 1.1f;
        [Tooltip("Makes the climb transition smooth instead of doing a 'quick hop'")]
        public bool smoothClimb = true;
        [Tooltip("The minimum altitude difference to start smooth climbing")]
        public float climbYThreshold = 0.5f;
        [Tooltip("The speed for the smooth climb")]
        public float climbSpeed = 4f;

        [Header("Thrust")]
        public float thrustPower = 23f;
        public float thrustMaxSpeed = 2f;
        public float thrustMaxAltitude = 100f;

        [Header("Aiming & HeadBob")]
        public MouseLook mouseLook;
        public bool useFovKick = true;
        [SerializeField] private FOVKick m_FovKick = new FOVKick();
        public bool useHeadBob = true;
        [SerializeField] private CurveControlledBob m_HeadBob = new CurveControlledBob();
        [SerializeField] private LerpControlledBob m_JumpBob = new LerpControlledBob();


        public override float GetCharacterHeight () {
            return hasCharacterController ? _characterController.height : _characterHeight;
        }

        [Header("Free Camera")]
        public bool freeCamMode;
        public bool fixedLookDirection;
        public Vector3 lookAt;
        public float minDistance = 1f;
        public float maxDistance = 100f;

        // internal fields
        protected Camera m_Camera;

        bool jump;
        Vector3 m_Input;
        Vector3 moveDir = Misc.vector3zero;
        CharacterController _characterController;
        CollisionFlags m_CollisionFlags;
        bool m_PreviouslyGrounded;
        Vector3 m_OriginalCameraPosition;
        float prevCrouchYPos;
        float prevCrouchTime;
        bool movingSmooth;
        float thrustAmount;
        float moveStartTime;

        bool jumping;
        protected float lastHitButtonPressed;
        GameObject underwaterPanel;
        Material underWaterMat;
        Transform crouch;

        Vector3 curPos;
        float waterLevelTop;

        const float SWITCH_DURATION = 2f;
        bool firePressed;
        bool switching;
        float switchingStartTime;
        float switchingLapsed;

        float lastUserCameraNearClipPlane;

        [Header("Camera Orbit Mode")]
        public bool cameraOrbitMode;
        public float cameraOrbitModeSpeed = 4f;
        public float cameraOrbitModeDistanceV = 40f;
        public float cameraOrbitModeDistanceH = 35f;

        float cameraOrbitAngle;

        Camera cameraOrbit;

        public virtual void SetCameraOrbitMode (bool state) {
            if (state == cameraOrbitMode) return;
            cameraOrbitMode = state;
            VoxelPlayUI ui = VoxelPlayUI.instance;
            if (ui != null) {
                ui.ToggleConsoleVisibility(false);
                ui.ToggleInventoryVisibility(false);
                ui.gameObject.SetActive(!state);
            }
            crosshair.gameObject.SetActive(!state);
            if (cameraOrbitMode) {
                if (cameraOrbit == null) {
                    GameObject go = new GameObject("Camera Orbit", typeof(Camera));
                    cameraOrbit = go.GetComponent<Camera>();
                    cameraOrbit.CopyFrom(m_Camera);
                }
            } else {
                if (cameraOrbit != null) {
                    DestroyImmediate(cameraOrbit.gameObject);
                }
            }
        }

        static VoxelPlayFirstPersonController _firstPersonController;
        public bool hasCharacterController;
        bool hasThrusted;
        Vector3 lastMoveVelocity;
        float lastMoveVelocityMagnitude;

        Vector3 externalForce;
        /// <summary>
        /// Used to provide a temporary push in some direction
        /// </summary>
        public virtual void AddExternalForce (Vector3 force) {
            externalForce = force;
        }

        /// <summary>
        /// This method will check if a character controller is attached to the gameobject and update the public "hasCharacterController" field.
        /// </summary>
        /// <returns></returns>
        public virtual bool CheckCharacterController () {
            if (this == null) return false;
            _characterController = GetComponent<CharacterController>();
            hasCharacterController = !useThirdPartyController && _characterController != null;
            return hasCharacterController;
        }

        public virtual CharacterController characterController {
            get { return _characterController; }
        }

        public static VoxelPlayFirstPersonController instance {
            get {
                if (_firstPersonController == null) {
                    _firstPersonController = VoxelPlayEnvironment.instance.characterController as VoxelPlayFirstPersonController;
                }
                return _firstPersonController;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitSingleton()
        {
            _firstPersonController = null;
        }

        public override bool isReady {
            get {
                CheckCharacterController();
                return _characterController != null && _characterController.enabled;
            }
        }

        public virtual void OnEnable () {
            CheckCharacterController();
            if (hasCharacterController) {
                _characterController.stepOffset = 0.4f;
            }
            env = VoxelPlayEnvironment.instance;
            if (env != null) {
                env.characterController = this;
                env.OnOriginPreShift -= OnOriginPreShift;
                env.OnOriginPreShift += OnOriginPreShift;
                env.OnOriginPostShift -= OnOriginPostShift;
                env.OnOriginPostShift += OnOriginPostShift;
            }
            crouch = transform.Find("Crouch");
            if (crouch == null) {
                GameObject crouchGO = new GameObject("Crouch");
                crouch = crouchGO.transform;
                crouch.transform.SetParent(transform, false);
            }
        }

        private bool OnOriginPreShift (Vector3 shift) {
            ToggleCharacterController(false);
            return true;
        }

        private void OnOriginPostShift (Vector3 shift) {
            ToggleCharacterController(true);
        }

        public virtual void OnDestroy () {
            if (env != null) {
                env.OnOriginPreShift -= OnOriginPreShift;
                env.OnOriginPostShift -= OnOriginPostShift;
            }
            if (underWaterMat != null) {
                DestroyImmediate(underWaterMat);
                underWaterMat = null;
            }
            if (crosshairMat != null) {
                DestroyImmediate(crosshairMat);
                crosshairMat = null;
            }
            if (underwaterPanel != null) {
                DestroyImmediate(underwaterPanel);
                underwaterPanel = null;
            }
        }

        public virtual void Start () {
            Init();
            m_Camera = GetComponentInChildren<Camera>();
            if (m_Camera == null) {
                // cover the case where the camera is not part of this prefab but it's in the scene. In this case, we'll steal the camera and put it as a child
                m_Camera = Camera.main;
                if (m_Camera == null) m_Camera = Misc.FindObjectOfType<Camera>();
                if (m_Camera != null) {
                    m_Camera.transform.SetParent(crouch, false);
                    m_Camera.transform.localPosition = new Vector3(0, 0.8f, 0f);
                    m_Camera.transform.localRotation = Misc.quaternionZero;
                }
            }
            if (m_Camera != null) {
                if (env != null) {
                    env.cameraMain = m_Camera;
                }
                m_OriginalCameraPosition = m_Camera.transform.localPosition;
                if (hasCharacterController) {
                    m_FovKick.Setup(m_Camera);
                    m_HeadBob.Setup(m_Camera, footStepInterval);
                }
            }
            jumping = false;

            if (env == null || !env.applicationIsPlaying)
                return;

            InitUnderwaterEffect();

            ToggleCharacterController(false);

            // Position character on ground
            if (startOnFlat && !env.saveFileIsLoaded) {
                StartOnFlat();
            }

            InitCrosshair();

            if (env.initialized) {
                LateInit();
            } else {
                env.OnInitialized += () => LateInit();
            }
        }


        protected virtual void LateInit () {
            if (hasCharacterController) {
                SetFreeCamMode(freeCamMode);
                mouseLook.Init(transform, m_Camera.transform, input);
            }
            WaitForCurrentChunk();
        }

        void InitUnderwaterEffect () {
            underwaterPanel = Instantiate(Resources.Load<GameObject>("VoxelPlay/Prefabs/UnderwaterPanel"), m_Camera.transform);
            underwaterPanel.name = "UnderwaterPanel";
            Renderer underwaterRenderer = underwaterPanel.GetComponent<Renderer>();
            underWaterMat = Resources.Load<Material>(env.realisticWater ? "VoxelPlay/Materials/VP UnderWater Realistic" : "VoxelPlay/Materials/VP UnderWater");
            underWaterMat = Instantiate(underWaterMat);
            underwaterRenderer.sharedMaterial = underWaterMat;

            underwaterPanel.transform.localPosition = new Vector3(0, 0, m_Camera.nearClipPlane);
            underwaterPanel.SetActive(false);
        }


        public override void UpdateLook () {
            // Pass initial rotation to mouseLook script
            if (m_Camera != null) {
                mouseLook.Init(characterController.transform, m_Camera.transform, null);
            }
        }


        /// <summary>
        /// Disables character controller until chunk is ready
        /// </summary>
        public virtual void WaitForCurrentChunk () {
            ToggleCharacterController(false);
            StartCoroutine(WaitForCurrentChunkCoroutine());
        }

        /// <summary>
        /// Enables/disables character controller
        /// </summary>
        /// <param name="state">If set to <c>true</c> state.</param>
        public virtual void ToggleCharacterController (bool state) {
            if (hasCharacterController) {
                _characterController.enabled = state;
            }
            enabled = state;
        }

        /// <summary>
        /// Ensures player chunk is finished before allow player movement / interaction with colliders
        /// </summary>
        IEnumerator WaitForCurrentChunkCoroutine () {
            // Wait until current player chunk is rendered
            WaitForSeconds w = new WaitForSeconds(0.2f);
            for (int k = 0; k < 20; k++) {
                VoxelChunk chunk = env.GetCurrentChunk();
                if (chunk != null && chunk.isRendered) {
                    break;
                }
                yield return w;
            }
            Unstuck(true);
            prevCrouchYPos = crouch.position.y;
            ToggleCharacterController(true);
            if (!hasCharacterController) {
                switchingLapsed = 1f;
            }
        }

        public virtual void Update () {
            UpdateImpl();
        }

        protected virtual void UpdateImpl () {
            if (env == null || !env.applicationIsPlaying || !env.initialized || input == null)
                return;

            curPos = transform.position;

            if (hasCharacterController) {
                UpdateWithCharacterController();
                UpdateCrouch();
                if (smoothClimb) {
                    SmoothClimb();
                }
            } else {
                UpdateSimple();
            }

            ControllerUpdate();

            if (cameraOrbitMode) {
                UpdateCameraOrbit();
            }
        }

        protected virtual void UpdateWithCharacterController () {

            CheckFootfalls();

            RotateView();

            if (freeCamMode)
                isFlying = true;

            // the jump state needs to read here to make sure it is not missed
            if (!jump && !isFlying && manageJump) {
                jump = input.GetButtonDown(InputButtonNames.Jump);
            }

            if (!m_PreviouslyGrounded && _characterController.isGrounded) {
                StartCoroutine(m_JumpBob.DoBobCycle());
                PlayLandingSound();
                moveDir.y = 0f;
                jumping = false;
            }
            if (!_characterController.isGrounded && !jumping && m_PreviouslyGrounded) {
                moveDir.y = 0f;
            }

            m_PreviouslyGrounded = _characterController.isGrounded;

            // Process click events
            if (input.focused && input.enabled) {
                bool leftAltPressed = input.GetButton(InputButtonNames.LeftAlt);
                bool leftShiftPressed = input.GetButton(InputButtonNames.LeftShift);
                bool leftControlPressed = input.GetButton(InputButtonNames.LeftControl);
                bool fire1Clicked = manageAttack && input.GetButtonDown(InputButtonNames.Button1);

                if (crosshairOnBlock && input.GetButtonClick(InputButtonNames.Button1)) {
                    env.TriggerVoxelClickEvent(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex, 0);
                } else if (crosshairOnBlock && input.GetButtonClick(InputButtonNames.Button2)) {
                    env.TriggerVoxelClickEvent(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex, 1);
                } else if (crosshairOnBlock && input.GetButtonClick(InputButtonNames.MiddleButton)) {
                    env.TriggerVoxelClickEvent(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex, 2);
                }

                if (fire1Clicked) {
                    firePressed = true;
                    if (ModelPreviewCancel()) {
                        firePressed = false;
                        lastHitButtonPressed = Time.time + 0.5f;
                    }
                } else if (!input.GetButton(InputButtonNames.Button1)) {
                    firePressed = false;
                }

                bool fire2Clicked = manageBuild && input.GetButtonDown(InputButtonNames.Button2);
                if (!leftShiftPressed && !leftAltPressed && !leftControlPressed) {
                    if (Time.time - lastHitButtonPressed > player.GetHitDelay()) {
                        if (firePressed) {
                            if (_crosshairHitInfo.item != null) {
                                _crosshairHitInfo.item.PickItem(player);
                                crosshairOnBlock = false;
                                firePressed = false;
                            } else {
                                DoHit(env.buildMode ? 255 : player.GetHitDamage());
                            }
                        }
                    }
                }

                bool middleButton = manageSelectVoxel && input.GetButtonDown(InputButtonNames.MiddleButton);
                if (crosshairOnBlock && middleButton) {
                    if (_crosshairHitInfo.voxel.type.allowUpsideDownVoxel && _crosshairHitInfo.voxel.type.upsideDownVoxel != null) {
                        player.SetSelectedItem(_crosshairHitInfo.voxel.type.hidden ? _crosshairHitInfo.voxel.type.upsideDownVoxel : _crosshairHitInfo.voxel.type);
                    } else {
                        player.SetSelectedItem(_crosshairHitInfo.voxel.type);
                    }
                    player.selectedItemTintColor = _crosshairHitInfo.voxel.color;
                }

                if (fire2Clicked && !leftAltPressed && !leftShiftPressed) {
#if UNITY_EDITOR
                    DoBuild(m_Camera.transform.position, m_Camera.transform.forward, voxelHighlightBuilder != null ? (Vector3d)voxelHighlightBuilder.transform.position : Vector3d.zero);
#else
                    DoBuild(m_Camera.transform.position, m_Camera.transform.forward, Vector3d.zero);
#endif
                }

                VoxelPlayUI ui = VoxelPlayUI.instance;
                if (ui != null) {
                    if (ui.IsInventoryVisible) return;
                }

                if (manageBuild) {
                    if (input.GetButtonDown(InputButtonNames.Build)) {
                        env.SetBuildMode(!env.buildMode);
                        if (env.buildMode) {
                            env.ShowMessage("<color=green>Entered <color=yellow>Build Mode</color>. Press <color=white>B</color> to cancel.</color>");
                        } else {
                            env.ShowMessage("<color=green>Back to <color=yellow>Normal Mode</color>.</color>");
                        }
                    } else if (manageVoxelRotation && input.GetButtonDown(InputButtonNames.Rotate)) {
                        if (_crosshairHitInfo.voxel.type.allowsTextureRotation) {
                            int rotation = env.GetVoxelTexturesRotation(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex);
                            rotation = (rotation + 1) % 4;
                            env.VoxelSetTexturesRotation(_crosshairHitInfo.chunk, _crosshairHitInfo.voxelIndex, rotation);
                        }
                    }
                }

                // Toggles Flight mode
                if (manageFly && input.GetButtonDown(InputButtonNames.Fly)) {
                    isFlying = !isFlying;
                    if (isFlying) {
                        jumping = false;
                        env.ShowMessage("<color=green>Flying <color=yellow>ON</color></color>");
                    } else {
                        env.ShowMessage("<color=green>Flying <color=yellow>OFF</color></color>");
                    }
                }

                if (isGrounded && !isCrouched && input.GetButtonDown(InputButtonNames.LeftControl)) {
                    isCrouched = true;
                } else if (isGrounded && isCrouched && input.GetButtonUp(InputButtonNames.LeftControl)) {
                    isCrouched = false;
                } else if (isGrounded && manageCrouch && input.GetButtonDown(InputButtonNames.Crouch)) {
                    isCrouched = !isCrouched;
                } else if (input.GetButtonDown(InputButtonNames.Light)) {
                    ToggleCharacterLight();
                } else if (input.GetButtonDown(InputButtonNames.ThrowItem)) {
                    ThrowCurrentItem(m_Camera.transform.position, m_Camera.transform.forward);
                }
            }

            if (!movingSmooth) {
                CheckWaterStatus();
            }

#if UNITY_EDITOR
            UpdateConstructor();
#endif

        }

        protected virtual void UpdateSimple () {
            // Check water
            CheckWaterStatus();

        }

        public virtual void SetFreeCamMode (bool enableFreeCam) {
            if (freeCamMode != enableFreeCam) {
                freeCamMode = enableFreeCam;
                switching = true;
                switchingStartTime = Time.time;
                freeMode = freeCamMode;
            }
        }


        protected virtual void UpdateCrouch () {
            if (isInWater) {
                crouch.transform.localPosition = new Vector3(0, -0.6f, 0);
                return;
            }

            if (isCrouched && crouch.localPosition.y == 0) {
                crouch.transform.localPosition = Misc.vector3down;
                _characterController.stepOffset = 0.4f;
            } else if (!isCrouched && crouch.localPosition.y != 0) {
                crouch.transform.localPosition = Misc.vector3zero;
                _characterController.stepOffset = 1f;
            }
        }


        protected virtual void CheckWaterStatus () {

            if (env.realisticWater) {
                underWaterMat.SetMatrix(ShaderParams.InverseView, m_Camera.cameraToWorldMatrix);
            }

            underwaterPanel.transform.localPosition = new Vector3(0, 0, m_Camera.nearClipPlane);
            Vector3 nearClipPos = m_Camera.transform.position + m_Camera.transform.forward * m_Camera.nearClipPlane;

            bool wasInWater = isInWater;

            isInWater = false;
            isSwimming = false;
            isUnderwater = false;

            // Check water on character controller position
            Voxel voxelCh;
            if (env.GetVoxelIndex(curPos, out VoxelChunk chunk, out int voxelIndex, false)) {
                voxelCh = chunk.voxels[voxelIndex];
            } else {
                voxelCh = Voxel.Empty;
            }
            VoxelDefinition voxelChType = env.voxelDefinitions[voxelCh.typeIndex];
            if (voxelCh.hasContent) {
                CheckEnterTrigger(chunk, voxelIndex);
                CheckDamage(voxelChType);
            }


            int cameraWaterLevel = 0;

            // Safety check; if voxel at character position is solid, move character on top of terrain
            float waterCausticsLevelTop = 99999;
            if (voxelCh.isSolid) {
                Unstuck(false);
            } else {
                AnnotateNonCollidingPosition(curPos);
                // Check if water surrounds camera
                Voxel voxelCamera = env.GetVoxel(nearClipPos, false);
                VoxelDefinition voxelCameraType = env.voxelDefinitions[voxelCamera.typeIndex];
                if (voxelCamera.hasContent) {
                    CheckEnterTrigger(chunk, voxelIndex);
                    CheckDamage(voxelCameraType);
                }

                cameraWaterLevel = voxelCamera.GetWaterLevel();
                Color waterColor = env.world.waterColor;
                if (cameraWaterLevel > 0) {
                    // More water on top?
                    Vector3 pos1Up = nearClipPos;
                    int wl = cameraWaterLevel;
                    pos1Up.y += 1f;
                    Voxel voxel1Up = Voxel.Empty;
                    while (wl > 0) {
                        voxel1Up = env.GetVoxel(pos1Up);
                        wl = voxel1Up.GetWaterLevel();
                        if (wl > 0) {
                            pos1Up.y++;
                            cameraWaterLevel = wl;
                        }
                    }
                    pos1Up.y -= 1f;
                    waterCausticsLevelTop = waterLevelTop = FastMath.FloorToInt(pos1Up.y) + cameraWaterLevel / 15f;
                    if (waterLevelTop >= nearClipPos.y) {
                        isUnderwater = true;
                    } else {
                        isSwimming = cameraWaterLevel > 7;
                    }
                    waterColor = voxelCameraType.diveColor;

                    // continue until open air if there's a solid block
                    while (voxel1Up.hasContent) {
                        pos1Up.y += 1f;
                        voxel1Up = env.GetVoxel(pos1Up);
                        wl = voxel1Up.GetWaterLevel();
                        if (wl > 0) {
                            waterCausticsLevelTop = FastMath.FloorToInt(pos1Up.y) + wl / 15f;
                        }
                    }
                } else {
                    int voxelChWaterLevel = voxelCh.GetWaterLevel();
                    if (voxelChWaterLevel > 7) {
                        isSwimming = true;
                        waterCausticsLevelTop = waterLevelTop = FastMath.FloorToInt(curPos.y) + voxelChWaterLevel / 15f;
                        waterColor = voxelChType.diveColor;
                    }
                }
                underWaterMat.SetFloat(ShaderParams.WaterLevel, waterLevelTop);
                underWaterMat.SetFloat(ShaderParams.WaterCausticsLevel, waterCausticsLevelTop);
                underWaterMat.SetFloat(ShaderParams.WaveAmplitude, env.world.waveAmplitude);
                underWaterMat.SetFloat(ShaderParams.WaveSpeed, env.world.waveAnimationSpeed);
                underWaterMat.SetColor(ShaderParams.UnderWaterFogColor, env.world.underWaterFogColor);
                underWaterMat.SetColor(ShaderParams.Color, waterColor);

            }

            isInWater = isSwimming || isUnderwater;
            if (!wasInWater && isInWater) {
                PlayWaterSplashSound();
            }

            // Show/hide underwater panel
            bool showUnderWaterPanel = isInWater || cameraWaterLevel > 0;
            if (showUnderWaterPanel && !underwaterPanel.activeSelf) {
                underwaterPanel.SetActive(true);
            } else if (!showUnderWaterPanel && underwaterPanel.activeSelf) {
                underwaterPanel.SetActive(false);
            }

        }

        protected override void CharacterChangedXZPosition (Vector3 newPosition) {
            // Check if underground and adjust camera near clip plane
            float alt = env.GetTerrainHeight(newPosition);
            if (newPosition.y >= alt) {
                alt = env.GetTopMostHeight(newPosition);
            }
            isUnderground = newPosition.y < alt;
            if (isUnderground) {
                if (env.cameraMain.nearClipPlane > 0.081f) {
                    lastUserCameraNearClipPlane = env.cameraMain.nearClipPlane;
                    env.cameraMain.nearClipPlane = 0.08f;
                }
            } else if (env.cameraMain.nearClipPlane < lastUserCameraNearClipPlane) {
                env.cameraMain.nearClipPlane = lastUserCameraNearClipPlane;
            }

        }

        protected virtual void DoHit (int damage) {
            lastHitButtonPressed = Time.time;

            // Check item sound
            InventoryItem inventoryItem = player.GetSelectedItem();
            if (inventoryItem != InventoryItem.Null) {
                ItemDefinition currentItem = inventoryItem.item;
                PlayCustomSound(currentItem.useSound);
            }

            Ray ray = GetCameraRay();
            float maxDistance = player.GetHitRange();
            if (env.buildMode) {
                maxDistance = Mathf.Max(crosshairMaxDistance, maxDistance);
            }
            env.RayHit(ray, damage, maxDistance, player.GetHitDamageRadius(), microVoxels: microVoxels, microVoxelDestroyProb: microVoxelsProb, ignoreWater: ignoreWater, restrictToSlab: slabMode);
        }


        private void FixedUpdate () {
            if (env.initialized) {
                FixedUpdateImpl();
            }
        }

        protected virtual void FixedUpdateImpl () {

            if (!hasCharacterController)
                return;

            GetInput(out float speed);

            Vector3 pos = transform.position;

            moveDir += externalForce;
            externalForce = Misc.vector3zero;

            if (thrustAmount > 0.001f) {
                hasThrusted = true;
                Vector3 impulseVector = transform.forward * m_Input.y + transform.right * m_Input.x + transform.up * thrustAmount;
                impulseVector.x *= thrustAmount;
                impulseVector.z *= thrustAmount;
                impulseVector += Physics.gravity * gravityMultiplier;
                moveDir += impulseVector * Time.fixedDeltaTime;
                float velocity = moveDir.magnitude;
                if (velocity > thrustMaxSpeed) {
                    moveDir = moveDir.normalized * thrustMaxSpeed;
                }
            } else if (isFlying || isInWater) {
                Transform camTransform = m_Camera.transform;
                moveDir = camTransform.forward * m_Input.y + camTransform.right * m_Input.x + camTransform.up * m_Input.z;
                moveDir *= speed;
                if (!isFlying) {
                    if (moveDir.y < 0) {
                        moveDir.y += 0.1f * Time.fixedDeltaTime;
                    }
                    if (jump) {
                        // Check if player is next to terrain
                        if (env.CheckCollision(new Vector3(pos.x + camTransform.forward.x, pos.y, pos.z + camTransform.forward.z))) {
                            moveDir.y = jumpSpeed * 0.5f;
                            jumping = true;
                        }
                        jump = false;
                    } else {
                        moveDir += Physics.gravity * gravityMultiplier * Time.fixedDeltaTime * 0.5f;
                    }
                    if (pos.y > waterLevelTop && moveDir.y > 0) {
                        moveDir.y = 0; // do not exit water
                    }
                    ProgressSwimCycle(_characterController.velocity, swimSpeed);
                }
            } else {
                // always move along the camera forward as it is the direction that it being aimed at
                Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

                // get a normal for the surface that is being touched to move along it
                Physics.SphereCast(pos, _characterController.radius, Misc.vector3down, out RaycastHit hitInfo,
                    GetCharacterHeight() / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

                if (!hasThrusted) {
                    moveDir.x = desiredMove.x * speed;
                    moveDir.z = desiredMove.z * speed;
                }
                if (_characterController.isGrounded) {
                    hasThrusted = false;
                    moveDir.y = -stickToGroundForce;
                    if (jump) {
                        // if player is crouching, cancel it
                        if (isCrouched) {
                            isCrouched = false;
                            UpdateCrouch();
                        }
                        moveDir.y = jumpSpeed;
                        PlayJumpSound();
                        jump = false;
                        jumping = true;
                    }
                } else {
                    moveDir += Physics.gravity * gravityMultiplier * Time.fixedDeltaTime;
                }

                UpdateCameraPosition(speed);
                ProgressStepCycle(lastMoveVelocityMagnitude, speed);
            }


            Vector3 finalMove = moveDir * Time.fixedDeltaTime;
            Vector3 newPos = pos + finalMove;
            bool canMove = true;
            if (m_PreviouslyGrounded && !isFlying && isCrouched) {
                // check if player is beyond the edge
                Ray ray = new Ray(newPos, Misc.vector3down);
                canMove = Physics.SphereCast(ray, 0.3f, 1f);
                // if player can't move, clamp movement along the edge and check again
                if (!canMove) {
                    if (Mathf.Abs(moveDir.z) > Mathf.Abs(moveDir.x)) {
                        moveDir.x = 0;
                    } else {
                        moveDir.z = 0;
                    }
                    finalMove = moveDir * Time.fixedDeltaTime;
                    newPos = pos + finalMove;
                    ray.origin = newPos;
                    canMove = Physics.SphereCast(ray, 0.3f, 1f);
                }
            }

            // if constructor is enabled, disable any movement if control key is pressed (reserved for special constructor actions)
            if (env.constructorMode && input.GetButton(InputButtonNames.LeftControl)) {
                canMove = false;
            } else if (!_characterController.enabled) {
                canMove = false;
            }
            lastMoveVelocity = Misc.vector3zero;
            lastMoveVelocityMagnitude = 0;
            if (canMove && isActiveAndEnabled) {
                // autoclimb
                if (canClimb) {
                    Vector3 dir = new Vector3(moveDir.x, 0, moveDir.z);
                    float characterHeight = GetCharacterHeight();
                    Vector3 basePos = new Vector3(pos.x, pos.y - characterHeight * 0.25f, pos.z);
                    Ray ray = new Ray(basePos, dir);
                    if (Physics.SphereCast(ray, 0.3f, 1f)) {
                        _characterController.stepOffset = Mathf.Min(characterHeight, climbMaxStepHeight);
                    } else {
                        _characterController.stepOffset = 0.2f;
                    }
                }
                m_CollisionFlags = _characterController.Move(finalMove);
                // check limits
                if (limitBoundsEnabled) {
                    pos = _characterController.transform.position;
                    bool clamp = false;
                    if (pos.x > limitBounds.max.x) { pos.x = limitBounds.max.x; clamp = true; } else if (pos.x < limitBounds.min.x) { pos.x = limitBounds.min.x; clamp = true; }
                    if (pos.y > limitBounds.max.y) { pos.y = limitBounds.max.y; clamp = true; } else if (pos.y < limitBounds.min.y) { pos.y = limitBounds.min.y; clamp = true; }
                    if (pos.z > limitBounds.max.z) { pos.z = limitBounds.max.z; clamp = true; } else if (pos.z < limitBounds.min.z) { pos.z = limitBounds.min.z; clamp = true; }
                    if (clamp) {
                        MoveTo(pos);
                    }
                }

                lastMoveVelocity = _characterController.velocity;
                lastMoveVelocityMagnitude = lastMoveVelocity.magnitude;
            }
            isGrounded = _characterController.isGrounded;

            // Check limits
            if (freeCamMode && fixedLookDirection) {
                if (FastVector.ClampDistance(ref lookAt, ref pos, minDistance, maxDistance)) {
                    _characterController.transform.position = pos;
                }
            }

            mouseLook.UpdateCursorLock();

            if (!isGrounded && !isFlying) {
                // Check current chunk
                VoxelChunk chunk = env.GetCurrentChunk();
                if (chunk != null && !chunk.isRendered) {
                    WaitForCurrentChunk();
                    return;
                }
            }
        }


        void UpdateCameraOrbit () {
            Vector3 pos = transform.position;
            pos.y = env.GetTerrainHeight(transform.position, true);
            cameraOrbit.transform.position = pos + Vector3.up * cameraOrbitModeDistanceV + Vector3.back * cameraOrbitModeDistanceH;
            cameraOrbit.transform.RotateAround(pos, Vector3.up, cameraOrbitAngle);
            cameraOrbit.transform.LookAt(pos, Vector3.up);

            cameraOrbitAngle += Time.deltaTime * cameraOrbitModeSpeed;
        }

        protected virtual void UpdateCameraPosition (float speed) {

            Vector3 newCameraPosition;
            if (!useHeadBob) {
                return;
            }

            if (lastMoveVelocityMagnitude > 0 && _characterController.isGrounded) {
                float strength = (Time.time - moveStartTime) * 2f;
                if (strength > 1f) strength = 1f;

                float sequence = lastMoveVelocityMagnitude + (speed * (isMoving ? 1f : runstepLenghten));
                newCameraPosition = m_HeadBob.DoHeadBob(sequence, strength);
                newCameraPosition.y -= m_JumpBob.Offset();
            } else {
                newCameraPosition = m_Camera.transform.localPosition;
                newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
            }
            m_Camera.transform.localPosition = newCameraPosition;
        }

        protected virtual Ray GetCameraRay () {
            Ray ray;
            if (freeMode || switching) {
                ray = m_Camera.ScreenPointToRay(input.screenPos);
            } else {
                ray = m_Camera.ViewportPointToRay(Misc.vector2half);
            }
            ray.origin = m_Camera.transform.position + ray.direction * 0.3f;
            return ray;
        }


        protected virtual void SmoothClimb () {
            if (!movingSmooth) {
                if (crouch.position.y - prevCrouchYPos >= climbYThreshold && !isFlying && !isThrusting) {
                    prevCrouchTime = Time.time;
                    movingSmooth = true;
                    isCrouched = false;
                    UpdateCrouch();
                } else {
                    prevCrouchYPos = crouch.position.y;
                }
            }

            if (movingSmooth) {
                float t = (Time.time - prevCrouchTime) * climbSpeed;
                if (t > 1f) {
                    t = 1f;
                    movingSmooth = false;
                    prevCrouchYPos = crouch.position.y;
                }
                Vector3 pos = crouch.position;
                pos.y = prevCrouchYPos * (1f - t) + crouch.position.y * t;
                crouch.position = pos;
            }
        }

        protected virtual void GetInput (out float speed) {
            float up = 0;
            bool wasRunning = isRunning;
            VoxelPlayUI ui = VoxelPlayUI.instance;
            if (input == null || !input.enabled || (ui != null && ui.IsInventoryVisible)) {
                speed = 0;
                return;
            }

            if (input.GetButton(InputButtonNames.Up)) {
                up = 1f;
            } else if (input.GetButton(InputButtonNames.Down)) {
                up = -1f;
            }

            bool leftShiftPressed = input.GetButton(InputButtonNames.LeftShift);

            // set the desired speed to be walking or running
            if (isFlying) {
                speed = leftShiftPressed ? flySpeed * 2 : flySpeed;
            } else if (isInWater) {
                speed = swimSpeed;
            } else if (isCrouched) {
                speed = walkSpeed * 0.25f;
            } else if (!leftShiftPressed) {
                speed = walkSpeed;
            } else {
                speed = runSpeed;
            }
            m_Input = new Vector3(input.horizontalAxis, input.verticalAxis, up);

            // normalize input if it exceeds 1 in combined length:
            if (m_Input.sqrMagnitude > 1) {
                m_Input.Normalize();
            }

            isMoving = _characterController.velocity.sqrMagnitude > 0;

            bool wasPressingMoveKeys = isPressingMoveKeys;
            isPressingMoveKeys = input.anyAxisButtonPressed;
            if (isPressingMoveKeys) {
                if (!wasPressingMoveKeys) {
                    moveStartTime = Time.time;
                }
                isRunning = leftShiftPressed && isMoving;
            } else {
                isRunning = false;
                if (isGrounded) {
                    speed = 0;
                }
            }

            // thrust
            if (manageThrust && input.GetButton(InputButtonNames.Thrust)) {
                float atmos = 1f / (1.0f + Mathf.Max(0, transform.position.y - thrustMaxAltitude));
                thrustAmount = thrustPower * atmos;
                isThrusting = true;
            } else {
                thrustAmount = 0;
                isThrusting = false;
            }

            // handle speed change to give an fov kick
            // only if the player is going to a run, is running and the fovkick is to be used

            if (useFovKick && isRunning != wasRunning && (isMoving || m_FovKick.isFOVUp)) {
                StopAllCoroutines();
                StartCoroutine(isRunning ? m_FovKick.FOVKickUp() : m_FovKick.FOVKickDown(speed == 0 ? 5f : 1f));
            }

        }


        private void RotateView () {
            if (switching) {
                switchingLapsed = (Time.time - switchingStartTime) / SWITCH_DURATION;
                if (switchingLapsed > 1f) {
                    switchingLapsed = 1f;
                    switching = false;
                }
            } else {
                switchingLapsed = 1;
            }

            if (input.enabled) {
                if (input.GetButtonUp(InputButtonNames.Button1)) {
                    mouseLook.SetCursorLock(true);
                    Invoke(nameof(SetFocus), 0.5f);
                } else if (input.GetButtonDown(InputButtonNames.Escape)) {
                    input.focused = false;
                }
                if (input.focused) {
                    mouseLook.LookRotation(transform, m_Camera.transform, freeCamMode && fixedLookDirection, lookAt, switchingLapsed);
                }
            }
        }

        void SetFocus () {
            input.focused = true;
        }


        protected virtual void OnControllerColliderHit (ControllerColliderHit hit) {

            Rigidbody body = hit.collider.attachedRigidbody;
            //dont move the rigidbody if the character is on top of it
            if (m_CollisionFlags == CollisionFlags.Below) {
                return;
            }
            if (body == null || body.isKinematic) {
                return;
            }
            body.AddForceAtPosition(_characterController.velocity * 0.1f, hit.point, ForceMode.Impulse);
        }

        /// <summary>
        /// Moves character controller to a new position. Use this method instead of changing the transform position
        /// </summary>
        public override void MoveTo (Vector3 newPosition) {
            CheckCharacterController();
            if (_characterController != null) {
                _characterController.enabled = false;
                transform.position = newPosition;
                _characterController.enabled = true;
            } else {
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// Moves character controller by a distance. Use this method instead of changing the transform position
        /// </summary>
        public override void Move (Vector3 deltaPosition) {
            CheckCharacterController();
            if (_characterController != null) {
                _characterController.enabled = false;
                transform.position += deltaPosition;
                _characterController.enabled = true;
            } else {
                transform.position += deltaPosition;
            }
        }

        public override bool ToggleMicroVoxels () {
            ResetCrosshairPosition();
            return base.ToggleMicroVoxels();
        }

        public override void SetMicroVoxelSize (int size, float probability) {
            ResetCrosshairPosition();
            base.SetMicroVoxelSize(size, probability);
        }

        public override bool ToggleMicroVoxelSize () {
            ResetCrosshairPosition();
            return base.ToggleMicroVoxelSize();
        }

        public override void ToggleSlabMode () {
            ResetCrosshairPosition();
            base.ToggleSlabMode();
        }


    }
}

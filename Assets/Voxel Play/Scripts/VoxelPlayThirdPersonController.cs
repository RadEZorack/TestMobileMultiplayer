// Third person controller. Derived and expanded version from Unity standard asset's third person controller

using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;

namespace VoxelPlay {

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]

    public partial class VoxelPlayThirdPersonController : VoxelPlayCharacterControllerBase {

        [Header("Camera")]
        public Camera m_Camera;
        public Vector2 cameraFixedRotationAngles;
        public float cameraDistance = 5f;
        public float cameraMinDistance = 3f;
        public float cameraMaxDistance = 20f;
        public float cameraZoomMultiplier = 10f;
        public float cameraZoomDuration = 0.9f;
        public float cameraXSpeed = 2f;
        public float cameraYSpeed = 2f;
        public float cameraYMinLimit = -20f;
        public float cameraYMaxLimit = 80f;
        public float cameraOrthoMinSize = 1;
        public float cameraOrthoMaxSize = 20;
        public float cameraOrthoDistance = 150;
        public bool avoidObstacles;

        [Header("Movement")]
        [Tooltip("Disable camera & movement options to allow other third person controllers")]
        public bool alwaysRun = true;

        [FormerlySerializedAs("m_MovingTurnSpeed")]
        public float movingTurnSpeed = 360;
        [FormerlySerializedAs("m_StationaryTurnSpeed")]
        public float stationaryTurnSpeed = 180;

        [FormerlySerializedAs("m_RunCycleLegOffset")]
        public float runCycleLegOffset = 0.2f;

        [FormerlySerializedAs("m_MoveSpeedMultiplier")]
        public float moveSpeedMultiplier = 1f;
        [FormerlySerializedAs("m_AnimSpeedMultiplier")]
        public float animSpeedMultiplier = 1f;
        [FormerlySerializedAs("m_GroundCheckDistance")]
        public float groundCheckDistance = 0.1f;

        [FormerlySerializedAs("m_JumpPower")]
        public float jumpPower = 12f;
        [FormerlySerializedAs("m_ClimbSpeed")]
        public float climbSpeed = 2f;
        public float flySpeed = 20f;
        [FormerlySerializedAs("m_GravityMultiplier")]
        [Range(1f, 4f)] public float gravityMultiplier = 2f;

        public string attackAnimationState;

        public override float GetCharacterHeight () {
            return capsuleHeight;
        }


        bool jump;
        float lastHitButtonPressed;


        Rigidbody rb;
        Animator animator;
        float origGroundCheckDistance;
        const float K_HALF = 0.5f;
        float turnAmount;
        float forwardAmount;
        float capsuleHeight;
        Vector3 capsuleCenter;
        float capsuleRadius;
        CapsuleCollider capsule;
        Vector3 camForward;
        Vector3 moveDir;
        bool climbing;
        bool falling;
        float fallAltitude;
        Vector3 curPos;
        float cameraX, cameraY;
        bool seeking;
        float zoomStartTime;
        float wheel = 0;
        float lastReachDistance, timeSeeking;
        VoxelHitInfo seekTarget;
        bool firePressed;

        enum SeekAction {
            Hit,
            Move
        }

        SeekAction seekAction;


        static VoxelPlayThirdPersonController _thirdPersonController;


        public static VoxelPlayThirdPersonController instance {
            get {
                if (_thirdPersonController == null) {
                    _thirdPersonController = VoxelPlayEnvironment.instance.characterController as VoxelPlayThirdPersonController;
                }
                return _thirdPersonController;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitSingleton()
        {
            _thirdPersonController = null;
        }

        protected virtual Vector3 characterCenter {
            get {
                return transform.position + new Vector3(0, GetCharacterHeight() * 0.5f * transform.lossyScale.y, 0);
            }
        }


        public override bool isReady {
            get {
                return enabled;
            }
        }


        public virtual void OnEnable () {
            env = VoxelPlayEnvironment.instance;
            if (env == null) {
                Debug.LogError("Voxel Play Environment must be added first.");
            } else {
                env.characterController = this;
            }
        }

        public virtual void Start () {
            Init();

            capsule = GetComponent<CapsuleCollider>();
            if (capsule != null) {
                capsuleHeight = capsule.height * transform.lossyScale.y;
                capsuleCenter = capsule.center;

                if (Application.isPlaying && !useThirdPartyController && capsule.sharedMaterial == null) {
                    // ZeroFriction physic material is used to make easier climbing - however it's only used by this controller so if you're using other controllers do not touch the collider
#if UNITY_6000_0_OR_NEWER
                    PhysicsMaterial mat = Resources.Load<PhysicsMaterial>("VoxelPlay/Materials/ZeroFriction");
#else
                    PhysicMaterial mat = Resources.Load<PhysicMaterial>("VoxelPlay/Materials/ZeroFriction");
#endif
                    capsule.sharedMaterial = mat;
                }
            }

            rb = GetComponent<Rigidbody>();
            if (rb != null) {
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            }
            origGroundCheckDistance = groundCheckDistance;
            animator = GetComponentInChildren<Animator>();

            // Try to assign our designed camera
            if (m_Camera == null) {
                m_Camera = Camera.main;
            }
            if (env != null && m_Camera != null) {
                env.cameraMain = m_Camera;
            }
            // If no camera assigned, get Voxel Play Environment available camera
            if (m_Camera == null) {
                m_Camera = env.cameraMain;
            }

            if (env == null || !env.applicationIsPlaying)
                return;

            ToggleCharacterController(false);

            // Position character on ground
            if (startOnFlat && !env.saveFileIsLoaded) {
                StartOnFlat();
            }

            input = env.input;

            UpdateLook();

            InitCrosshair();
            if (!useThirdPartyController) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = false;
            }

            if (!env.initialized) {
                env.OnInitialized += () => WaitForCurrentChunk();
            } else {
                WaitForCurrentChunk();
            }
        }

        public override void UpdateLook () {
            if (m_Camera != null) {
                Vector3 angles = m_Camera.transform.eulerAngles;
                cameraX = angles.y;
                cameraY = angles.x;
                UpdateCamera(false);
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
            if (rb != null) {
                rb.isKinematic = !state;
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
            Unstuck();
            ToggleCharacterController(true);
        }


        public virtual void Update () {
            UpdateImpl();
        }

        protected virtual void UpdateImpl () {

            curPos = transform.position;
            CheckWaterStatus();

            if (useThirdPartyController) {
                UpdateSimplified();
            } else {
                UpdateWithCharacterController();
            }

            ControllerUpdate();
        }

        protected virtual void UpdateSimplified () {
            if (input == null || !input.focused || !input.enabled)
                return;

            CheckCommonKeys();
        }


        protected virtual void UpdateWithCharacterController () {

            if (input == null || !input.enabled)
                return;

            capsuleRadius = capsule.radius * transform.lossyScale.x;

            if (!jump && !isFlying) {
                jump = input.GetButtonDown(InputButtonNames.Jump);
            }

            CheckFootfalls();

            UpdateCamera(true);

            VoxelPlayUI ui = VoxelPlayUI.instance;
            if (ui != null) {
                if (ui.IsInventoryVisible) return;
            }

            // Process click events
            if (input.focused && input.enabled) {

                CheckCommonKeys();

                // Toggles Flight mode
                if (manageFly && input.GetButtonDown(InputButtonNames.Fly)) {
                    isFlying = !isFlying;
                    if (isFlying) {
                        env.ShowMessage("<color=green>Flying <color=yellow>ON</color></color>");
                        rb.useGravity = false;
                    } else {
                        env.ShowMessage("<color=green>Flying <color=yellow>OFF</color></color>");
                        rb.useGravity = true;
                    }
                }

                if (isGrounded && manageCrouch && input.GetButtonDown(InputButtonNames.Crouch)) {
                    SetCrouching(!isCrouched);
                } else if (input.GetButtonDown(InputButtonNames.Light)) {
                    ToggleCharacterLight();
                } else if (input.GetButtonDown(InputButtonNames.ThrowItem)) {
                    Vector3 direction = transform.forward;
                    direction.y = 1f;
                    ThrowCurrentItem(characterCenter, direction);
                }
            }
        }

        protected virtual void CheckCommonKeys () {

            bool leftAltPressed = input.GetButton(InputButtonNames.LeftAlt);
            bool leftShiftPressed = input.GetButton(InputButtonNames.LeftShift);
            bool leftControlPressed = input.GetButton(InputButtonNames.LeftControl);

            bool fire1Clicked = false;
            bool fire2Clicked = false;

            EventSystem e = EventSystem.current;
            bool overUI = e != null && e.IsPointerOverGameObject();
            if (!overUI) {
                fire1Clicked = manageAttack && input.GetButtonDown(InputButtonNames.Button1);
                fire2Clicked = manageBuild && input.GetButtonClick(InputButtonNames.Button2);
            }

            if (!leftShiftPressed && !leftAltPressed && !leftControlPressed) {
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
                    if (firePressed && Time.time - lastHitButtonPressed > player.GetHitDelay()) {
                        timeSeeking = Time.time;
                        lastReachDistance = float.MaxValue;
                        if (_crosshairHitInfo.item != null) {
                            _crosshairHitInfo.item.PickItem(player);
                            crosshairOnBlock = false;
                            firePressed = false;
                        } else {
                            DoHit(player.GetHitDamage());
                        }
                    }
                }

                if (fire2Clicked) {
                    timeSeeking = Time.time;
                    lastReachDistance = float.MaxValue;
                    seekTarget = _crosshairHitInfo;
                    // make character approach target if needed
                    seeking = !TargetIsReachable(true);
                    if (seeking) {
                        seekAction = SeekAction.Move;
                    } else {
                        DoBuild(curPos, transform.forward, _crosshairHitInfo.voxelCenter);
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
            }


            if (manageBuild && input.GetButtonDown(InputButtonNames.Build)) {
                env.SetBuildMode(!env.buildMode);
                if (env.buildMode) {
                    env.ShowMessage("<color=green>Entered <color=yellow>Build Mode</color>. Press <color=white>B</color> to cancel.</color>");
                } else {
                    env.ShowMessage("<color=green>Back to <color=yellow>Normal Mode</color>.</color>");
                }
            } else if (input.GetButtonDown(InputButtonNames.SeeThroughUp)) {
                env.seeThroughHeightOffset++;
            } else if (input.GetButtonDown(InputButtonNames.SeeThroughDown)) {
                env.seeThroughHeightOffset--;
            }
        }

        // Fixed update is called in sync with physics
        public virtual void FixedUpdate () {
            FixedUpdateImpl();
        }

        protected virtual void FixedUpdateImpl () {
            if (useThirdPartyController || input == null || !input.enabled)
                return;

            // read inputs
            float h = input.horizontalAxis;
            float v = input.verticalAxis;
            float up = 0;

            if (input.GetButton(InputButtonNames.Up)) {
                up = 1f;
            } else if (input.GetButton(InputButtonNames.Down)) {
                up = -1f;
            }

            isPressingMoveKeys = h != 0 || v != 0 || up != 0;

            // if seeking target, change move
            if (isPressingMoveKeys) {
                seeking = false;
            }

            if (seeking) {
                // Check orientation
                if (TargetIsReachable()) {
                    seeking = false;
                    if (seekAction == SeekAction.Hit) {
                        StartCoroutine(CompleteHit(player.GetHitDamage()));
                    }
                    moveDir = Vector3.zero;
                } else {
                    moveDir = seekTarget.voxelCenter - transform.position;
                    moveDir.y = 0;
                    moveDir.Normalize();
                }
            } else {
                // calculate move direction to pass to character
                // calculate camera relative direction to move:
                camForward = Vector3.Scale(m_Camera.transform.forward, new Vector3(1, 0, 1)).normalized;
                moveDir = v * camForward + h * m_Camera.transform.right;
            }

            if (moveDir.sqrMagnitude > 0) {
                moveDir.Normalize();
                isMoving = true;
            } else if (up != 0) {
                isMoving = true;
            } else {
                isMoving = false;
            }
            isRunning = false;

            // run speed multiplier
            bool leftShiftPressed = input.GetButton(InputButtonNames.LeftShift);

            float speed = 0;
            if (isMoving) {
                if (isFlying) {
                    speed = leftShiftPressed ? flySpeed * 2 : flySpeed;
                } else if (isInWater) {
                    speed = 0.4f;
                } else {
                    if (leftShiftPressed) {
                        if (alwaysRun) {
                            speed = 0.5f;
                        } else {
                            speed = 2f;
                            isRunning = true;
                        }
                    } else {
                        if (alwaysRun) {
                            speed = 2f;
                            isRunning = true;
                        } else {
                            speed = 0.5f;
                        }
                    }
                }
                moveDir *= speed;
                up *= speed;

                // allow character to walk over voxels that are one position above current altitude
                if (isGrounded && !isFlying) {
                    Vector3 dir = moveDir.normalized;
                    dir.y = 0.1f;
                    Vector3d frontPos = (transform.position + dir * (0.5f + capsuleRadius));
                    if (env.IsWallAtPosition(frontPos)) {
                        // Make sure there's a walkable voxel in front of player
                        frontPos.y++;
                        if (!env.IsWallAtPosition(frontPos)) {
                            dir = moveDir;
                            dir.y = 1f;
                            rb.linearVelocity = dir * climbSpeed;
                            if (!climbing) {
                                climbing = true;
                            }
                        }
                    }
                }
            } else {
                climbing = false;
            }

            // pass all parameters to the character control script
            Move(moveDir, jump, up);
            jump = false;

            if (isInWater) {
                ProgressSwimCycle(rb.linearVelocity, speed);
            } else if (!climbing) {
                ProgressStepCycle(rb.linearVelocity.magnitude, speed);
            }
        }


        public virtual void Move (Vector3 move, bool jump, float up) {

            // convert the world relative moveInput vector into a local-relative
            // turn amount and forward amount required to head in the desired
            // direction.
            if (move.magnitude > 1f)
                move.Normalize();
            move = transform.InverseTransformDirection(move);
            CheckGroundStatus();

            Vector3 curPos = rb.position;

            move = Vector3.ProjectOnPlane(move, Misc.vector3up);
            turnAmount = Mathf.Atan2(move.x, move.z);
            forwardAmount = move.z;

            ApplyExtraTurnRotation();

            if (!climbing) {

                if (isFlying) {
                    HandleFlyMovement(up);
                } else if (isGrounded) {
                    HandleGroundedMovement(jump);
                } else {
                    HandleAirborneMovement();
                }

                // Check limits
                if (limitBoundsEnabled && !limitBounds.Contains(rb.position)) {
                    rb.position = curPos;
                }
            }

            // send input and other state parameters to the animator
            UpdateAnimator(move);
        }


        protected virtual void SetCrouching (bool state) {
            isCrouched = state;
            ScaleCapsuleForCrouching(isCrouched);
        }


        protected virtual void ScaleCapsuleForCrouching (bool crouch) {
            if (isGrounded && crouch) {
                if (isCrouched)
                    return;
                capsule.height = capsule.height / 2f;
                capsule.center = capsule.center / 2f;
            } else {
                Ray crouchRay = new Ray(rb.position + Vector3.up * capsuleRadius * K_HALF, Vector3.up);
                float crouchRayLength = capsuleHeight - capsuleRadius * K_HALF;
                if (Physics.SphereCast(crouchRay, capsuleRadius * K_HALF, crouchRayLength, Physics.AllLayers, QueryTriggerInteraction.Ignore)) {
                    isCrouched = true;
                    return;
                }
                capsule.height = capsuleHeight / transform.lossyScale.y;
                capsule.center = capsuleCenter;
            }
        }



        protected virtual void UpdateAnimator (Vector3 move) {
            // update the animator parameters
            animator.SetFloat("Forward", forwardAmount, 0.1f, Time.deltaTime);
            animator.SetFloat("Turn", turnAmount, 0.1f, Time.deltaTime);
            animator.SetBool("Crouch", isCrouched);
            animator.SetBool("OnGround", isGrounded);
            if (!isGrounded && !isFlying) {
                animator.SetFloat("Jump", rb.linearVelocity.y);
            }

            // calculate which leg is behind, so as to leave that leg trailing in the jump animation
            // (This code is reliant on the specific run cycle offset in our animations,
            // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
            float runCycle =
                Mathf.Repeat(
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime + runCycleLegOffset, 1);
            float jumpLeg = (runCycle < K_HALF ? 1 : -1) * forwardAmount;
            if (isGrounded) {
                animator.SetFloat("JumpLeg", jumpLeg);
            }

            // the anim speed multiplier allows the overall speed of walking/running to be tweaked in the inspector,
            // which affects the movement speed because of the root motion.
            if (isFlying || (isGrounded && move.sqrMagnitude > 0)) {
                animator.speed = animSpeedMultiplier;
            } else {
                // don't use that while airborne
                animator.speed = 1;
            }
        }


        protected virtual void HandleAirborneMovement () {
            // apply extra gravity from multiplier:
            Vector3 extraGravityForce = (Physics.gravity * gravityMultiplier) - Physics.gravity;
            extraGravityForce += moveDir * 4.0f;
            rb.AddForce(extraGravityForce);
            groundCheckDistance = rb.linearVelocity.y < 0 ? origGroundCheckDistance : 0.01f;
        }

        protected virtual void HandleFlyMovement (float up) {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, up, rb.linearVelocity.z);
            animator.applyRootMotion = false;
        }


        protected virtual void HandleGroundedMovement (bool jump) {
            // check whether conditions are right to allow a jump:
            if (jump && !isCrouched && animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded")) {
                // jump!
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpPower, rb.linearVelocity.z);
                isGrounded = false;
                animator.applyRootMotion = false;
                groundCheckDistance = 0.1f;
                PlayJumpSound();
            }
        }


        protected virtual void ApplyExtraTurnRotation () {
            // help the character turn faster (this is in addition to root rotation in the animation)
            float turnSpeed = Mathf.Lerp(stationaryTurnSpeed, movingTurnSpeed, forwardAmount);
            transform.Rotate(0, turnAmount * turnSpeed * Time.deltaTime, 0);
        }


        public virtual void OnAnimatorMove () {
            // we implement this function to override the default root motion.
            // this allows us to modify the positional speed before it's applied.
            if (isGrounded && Time.deltaTime > 0) {
                Vector3 v = (animator.deltaPosition * moveSpeedMultiplier) / Time.deltaTime;

                // we preserve the existing y part of the current velocity.
                v.y = rb.linearVelocity.y;
                rb.linearVelocity = v;
            }
        }


        protected virtual void CheckGroundStatus () {
            if (isFlying) {
                climbing = falling = false;
                return;
            }

            if (GroundCheck()) {
                climbing = false;
                if (!isGrounded && !isInWater) {
                    PlayLandingSound();
                }
                isGrounded = true;
                falling = false;
                animator.applyRootMotion = true;
            } else if (!climbing) {
                // Annotate fall distance
                if (falling) {
                    float fallDistance = fallAltitude - transform.position.y;
                    if (fallDistance > 1f) {
                        isGrounded = false;
                        animator.applyRootMotion = false;
                    }
                } else {
                    falling = true;
                    fallAltitude = transform.position.y;
                }
            }
        }


        protected virtual bool GroundCheck () {
            // 0.1f is a small offset to start the ray from inside the character
            // it is also good to note that the transform position in the sample assets is at the base of the character
            Vector3 pos = transform.position + (Vector3.up * 0.1f * transform.lossyScale.y);
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(pos, pos + (Vector3.down * groundCheckDistance), Color.yellow);
#endif
            if (Physics.Raycast(pos, Vector3.down, groundCheckDistance)) {
                return true;
            }
            pos.x += capsuleRadius;
            pos.z += capsuleRadius;
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(pos, pos + (Vector3.down * groundCheckDistance), Color.yellow);
#endif
            if (Physics.Raycast(pos, Vector3.down, groundCheckDistance))
                return true;
            pos.z -= capsuleRadius * 2f;
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(pos, pos + (Vector3.down * groundCheckDistance), Color.yellow);
#endif
            if (Physics.Raycast(pos, Vector3.down, groundCheckDistance))
                return true;
            pos.x -= capsuleRadius * 2f;
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(pos, pos + (Vector3.down * groundCheckDistance), Color.yellow);
#endif
            if (Physics.Raycast(pos, Vector3.down, groundCheckDistance))
                return true;
            pos.z += capsuleRadius * 2f;
#if UNITY_EDITOR
            // helper to visualise the ground check ray in the scene view
            Debug.DrawLine(pos, pos + (Vector3.down * groundCheckDistance), Color.yellow);
#endif
            if (Physics.Raycast(pos, Vector3.down, groundCheckDistance))
                return true;

            return false;
        }

        protected virtual void CheckWaterStatus () {

            bool wasInWater = isInWater;

            isInWater = false;
            isSwimming = false;
            isUnderwater = false;

            if (!env.hasWater)
                return;

            // Check water on character controller position (which is at base of character)
            Vector3 aboveCurPos = curPos + new Vector3(0, 0.3f, 0);
            Voxel voxelCh = env.GetVoxel(aboveCurPos);
            CheckDamage(voxelCh.type);

            // Safety check to avoid character go under terrain
            if (voxelCh.isSolid) {
                Unstuck();
                return;
            }

            AnnotateNonCollidingPosition(curPos);

            if (voxelCh.GetWaterLevel() > 7) {
                isSwimming = true;
            }

            isInWater = isSwimming || isUnderwater;
            if (!wasInWater && isInWater) {
                PlayWaterSplashSound();
            }
        }


        protected virtual void UpdateCamera (bool smooth) {

            if (useThirdPartyController)
                return;

            float oldCameraX = cameraX;
            float oldCameraY = cameraY;

            if (input != null) {
                float w = input.mouseScrollWheel * cameraZoomMultiplier;
                if (w != 0) {
                    zoomStartTime = Time.time;
                    wheel += w;
                }
            }
            wheel *= 0.9f;
            if (wheel < 0.001f && wheel > -0.001f) {
                wheel = 0;
            }

            Quaternion rotation;
            if (input.GetButton(InputButtonNames.Button2)) {
                cameraX += input.mouseX * cameraXSpeed;
                cameraY -= input.mouseY * cameraYSpeed;
                cameraY = ClampAngle(cameraY, cameraYMinLimit, cameraYMaxLimit);
                smooth = false;
            }
            if (cameraFixedRotationAngles.x != 0) {
                cameraY = cameraFixedRotationAngles.x;
            }
            if (cameraFixedRotationAngles.y != 0) {
                cameraX = cameraFixedRotationAngles.y;
            }
            rotation = Quaternion.Euler(cameraY, cameraX, 0);

            Vector3 targetPos = transform.position + Misc.vector3up * (capsuleHeight * 0.5f);
            Vector3 position;
            // orthographic support
            if (m_Camera.orthographic) {
                float newSize = Mathf.Lerp(m_Camera.orthographicSize, m_Camera.orthographicSize + wheel, Time.deltaTime);
                newSize = Mathf.Clamp(newSize, cameraOrthoMinSize, cameraOrthoMaxSize);
                m_Camera.orthographicSize = newSize;
                Vector3 negDistance = new Vector3(0.0f, 0.0f, -cameraOrthoDistance);
                position = rotation * negDistance + targetPos;
            } else {
                cameraDistance += wheel;
                VoxelHitInfo hitInfo;
                float distance = Vector3.Distance(targetPos, m_Camera.transform.position);
                Vector3 direction = (targetPos - m_Camera.transform.position) / distance;
                if (avoidObstacles && env.RayCast(m_Camera.transform.position, direction, out hitInfo, distance, 3, ColliderTypes.IgnorePlayer)) {
                    cameraDistance -= hitInfo.distance + 0.1f;
                }
                cameraDistance = Mathf.Clamp(cameraDistance, cameraMinDistance, cameraMaxDistance);

                Vector3 negDistance = new Vector3(0.0f, 0.0f, -cameraDistance);
                position = rotation * negDistance + targetPos;

                // check there's no voxel under camera to avoid clipping with ground
                Vector3 pos = position;
                pos.y -= 0.25f;
                if (env.IsWallAtPosition(pos)) {
                    cameraX = oldCameraX;
                    cameraY = oldCameraY;
                    rotation = Quaternion.Euler(cameraY, cameraX, 0);
                    position = rotation * negDistance + targetPos;
                }
            }

            // move camera
            m_Camera.transform.rotation = rotation;
            if (smooth) {
                float t = (Time.time - zoomStartTime) / cameraZoomDuration;
                if (t > 1) {
                    t = 1;
                }
                m_Camera.transform.position = Vector3.Lerp(m_Camera.transform.position, position, t);
            } else {
                m_Camera.transform.position = position;
            }
        }

        public static float ClampAngle (float angle, float min, float max) {
            if (angle < -360F)
                angle += 360F;
            if (angle > 360F)
                angle -= 360F;
            return Mathf.Clamp(angle, min, max);
        }

        public virtual void OnDestroy() {
            if (crosshairMat != null) {
                DestroyImmediate(crosshairMat);
                crosshairMat = null;
            }
        }

        protected virtual void DoHit (int damage) {

            if (_crosshairHitInfo.voxelIndex < 0) {
                return;
            }

            lastHitButtonPressed = Time.time;
            seekTarget = _crosshairHitInfo;
            // make character approach target if needed
            seeking = !TargetIsReachable();
            if (seeking) {
                seekAction = SeekAction.Hit;
            } else {
                StartCoroutine(CompleteHit(damage));
            }
        }

        protected virtual bool TargetIsReachable (bool ignoreAngle = false) {
            Vector3 v = seekTarget.voxelCenter - characterCenter;
            float sqrDistance = v.sqrMagnitude;
            if (sqrDistance <= 3) {
                if (ignoreAngle) return true;
                v.y = 0;
                float angle = Vector3.Angle(v, transform.forward);
                if (angle < 30)
                    return true;

            }
            // Check if character can still move towards the target
            bool hasElapsed = (Time.time - timeSeeking) > 1f;
            if (sqrDistance < lastReachDistance) {
                lastReachDistance = sqrDistance;
                if (hasElapsed) {
                    timeSeeking = Time.time;
                }
            } else if (hasElapsed) {
                seeking = false;
            }

            return false;
        }


        readonly WaitForSeconds waitABit = new WaitForSeconds(0.3f);

        protected virtual IEnumerator CompleteHit (int damage) {
            seeking = false;

            if (attackAnimationState != null) {
                animator.Play(attackAnimationState);
                yield return waitABit;
            }
            int hitDamage = player.GetHitDamage();
            if (env.buildMode) {
                hitDamage = 255;
            }
            env.VoxelDamage(seekTarget, hitDamage, addParticles: true, playSound: true);
        }


        /// <summary>
        /// Moves character controller to a new position. Use this method instead of changing the transform position
        /// </summary>
        public override void MoveTo (Vector3 newPosition) {
            rb.position = newPosition;
        }


        public override bool ToggleMicroVoxels () {
            ResetCrosshairPosition();
            return base.ToggleMicroVoxels();
        }

        public override void SetMicroVoxelSize (int size, float probability) {
            base.SetMicroVoxelSize(size, probability);
            ResetCrosshairPosition();
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

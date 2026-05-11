using UnityEngine;

namespace VoxelPlay {
    public partial class VoxelPlayFirstPersonController : VoxelPlayCharacterControllerBase {
        Transform crosshair;
        const string CROSSHAIR_NAME = "Crosshair";
        Material crosshairMat;
        int forceUpdateCrosshair;

        protected virtual void InitCrosshair () {
            if (env.crosshairPrefab == null) {
                Debug.LogError("Crosshair prefab not assigned to this world.");
                return;
            }
            GameObject obj = Instantiate<GameObject>(env.crosshairPrefab);
            obj.name = CROSSHAIR_NAME;
            crosshair = obj.transform;
            crosshair.SetParent(m_Camera.transform, false);
            if (autoInvertColors && !env.isMobilePlatform) {
                crosshairMat = Resources.Load<Material>("VoxelPlay/Materials/VP Crosshair");
            } else {
                crosshairMat = Resources.Load<Material>("VoxelPlay/Materials/VP Crosshair No GrabPass");
            }
            crosshairMat = Instantiate(crosshairMat);
            obj.GetComponent<Renderer>().sharedMaterial = crosshairMat;
            if (env.crosshairTexture != null) {
                crosshairMat.mainTexture = env.crosshairTexture;
            }
            ResetCrosshairPosition();
            if (!enableCrosshair || VRCheck.isVrRunning) {
                crosshair.gameObject.SetActive(false);
            }
            // ensure crosshair gets updated when a chunk changes on screen (including custom voxels which are created when rendering the chunk)
            env.OnChunkRender += (VoxelChunk chunk) => {
                forceUpdateCrosshair = 2; // physics may not be updated in same frame so retry raycast next frame (forceUpdateCrosshair is the number of frames remaining for trying raycasting)
            };
            
            // Also trigger crosshair update when a voxel is about to be placed to ensure ghost collider is detected immediately
            env.OnVoxelBeforePlace += (Vector3d position, VoxelChunk chunk, int voxelIndex, ref VoxelDefinition voxelType, ref Color32 tintColor) => {
                forceUpdateCrosshair = 2;
            };
        }

        public virtual void ResetCrosshairPosition () {
            UpdateCrosshairScreenPosition();
            crosshair.localRotation = Misc.quaternionZero;
            crosshair.localScale = Misc.vector3one * crosshairScale;
            crosshairMat.color = crosshairNormalColor;
            forceUpdateCrosshair++;
            env.RefreshVoxelHighlight();
        }

        public override void ForceUpdateCrosshair(int frames = 2) {
            forceUpdateCrosshair = frames;
        }

        protected virtual void UpdateCrosshairScreenPosition () {

            if (freeMode) {
                if (input != null) {
                    Vector3 scrPos = input.screenPos;
                    scrPos.x = Mathf.Clamp(scrPos.x, 0, Screen.width);
                    scrPos.y = Mathf.Clamp(scrPos.y, 0, Screen.height);
                    scrPos.z = 1f;
                    Vector3 newPosition = m_Camera.ScreenToWorldPoint(scrPos);
                    if (switchingLapsed < 1f) {
                        crosshair.position = Vector3.Lerp(crosshair.position, newPosition, switchingLapsed);
                    } else {
                        crosshair.position = newPosition;
                    }
                }
            } else {
                if (switchingLapsed < 1f) {
                    crosshair.localPosition = Vector3.Lerp(crosshair.localPosition, Misc.vector3forward, switchingLapsed);
                } else {
                    crosshair.localPosition = Misc.vector3forward;
                }
            }
        }


        void LateUpdate () {
            if (env != null && env.initialized) {
                LateUpdateImpl();
            }
        }

        protected virtual void LateUpdateImpl () {

            if (env == null || !env.applicationIsPlaying || crosshair == null)
                return;

            if (freeMode || switching) {
                UpdateCrosshairScreenPosition();
                forceUpdateCrosshair = 1;
            }

            if (env.cameraHasMoved || forceUpdateCrosshair > 0) {
                if (forceUpdateCrosshair > 0) {
                    forceUpdateCrosshair--;
                }

                Ray ray = GetCameraRay();

                // Check if there's a voxel in range
                float hitRange = player.GetHitRange();
                if (env.buildMode) hitRange = Mathf.Max(crosshairMaxDistance, hitRange);
                crosshairOnBlock = env.RayCast(ray, out _crosshairHitInfo, hitRange, colliderTypes: ColliderTypes.IgnorePlayer, layerMask: crosshairHitLayerMask, microVoxels: microVoxels > 0 && !slabMode, ignoreWater: ignoreWater) && _crosshairHitInfo.voxelIndex >= 0;
                if (changeOnBlock) {
                    if (crosshairOnBlock) {
                        // Puts crosshair over the voxel but do it only if crosshair won't disappear because of the angle or it's switching from orbit to free mode (or viceversa)
                        float d = -1;
                        if (_crosshairHitInfo.sqrDistance > 6f) {
                            d = Vector3.Dot(ray.direction, _crosshairHitInfo.normal);
                        }
                        if (d < -0.2f && switchingLapsed >= 1f) {
                            Vector3 crosshairPosition = _crosshairHitInfo.point;
                            crosshair.position = crosshairPosition;
                            crosshair.LookAt(crosshairPosition + _crosshairHitInfo.normal);
                        } else {
                            crosshair.localRotation = Misc.quaternionZero;
                        }
                        crosshairMat.color = crosshairOnTargetColor;
                    } else {
                        ResetCrosshairPosition();
                    }
                }
            }
            if (crosshairOnBlock) {
                crosshair.localScale = Misc.vector3one * (crosshairScale * (1f - targetAnimationScale * 0.5f + Mathf.PingPong(Time.time * targetAnimationSpeed, targetAnimationScale)));
                DoHighlight();
            } else {
                env.VoxelHighlight(false);
            }

        }
    }




}


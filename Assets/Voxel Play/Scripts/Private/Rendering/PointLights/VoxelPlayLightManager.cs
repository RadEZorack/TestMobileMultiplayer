using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelPlay.GPULighting {

    public class VoxelPlayLightManager : MonoBehaviour {


        static List<VoxelPlayLight> lights = new List<VoxelPlayLight>();
        static bool shouldSortLights;

        const int MAX_LIGHTS = 32; // also given by shader buffer length
        int lastX, lastY, lastZ;
        Vector3 camPos;
        Vector4[] lightPosBuffer;
        Vector4[] lightColorBuffer;
        VoxelPlayEnvironment env;

        public static class ShaderParams {
            public static int GlobalLightPositionsArray = Shader.PropertyToID("_VPPointLightPosition");
            public static int GlobalLightColorsArray = Shader.PropertyToID("_VPPointLightColor");
            public static int GlobalLightCount = Shader.PropertyToID("_VPPointLightCount");
        }

        public static void RegisterLight(VoxelPlayLight light) {
            if (light == null) return;

            if (!light.virtualLight) {

                if (light.pointLight == null) {
                    Debug.LogWarning("There's no Light component attached to the GameObject '" + light.name + "'. Fix it or enable the 'Virtual Light' option.", light.gameObject);
                    return;
                }
                if (light.pointLight.type != LightType.Point) {
                    Debug.LogWarning("Only point lights are supported by the Voxel Play Light component in GameObject '" + light.name + "'. Change the light type or remove the Voxel Play Light component.", light.gameObject);
                    return;
                }
            }
            if (!lights.Contains(light)) {
                lights.Add(light);
                shouldSortLights = true;
            }
        }

        public static void UnregisterLight(VoxelPlayLight light) {
            if (light != null && lights.Contains(light)) {
                lights.Remove(light);
                shouldSortLights = true;
            }
        }

        void OnEnable() {
            if (lightPosBuffer == null || lightPosBuffer.Length < MAX_LIGHTS) {
                lightPosBuffer = new Vector4[MAX_LIGHTS];
            }
            if (lightColorBuffer == null || lightColorBuffer.Length < MAX_LIGHTS) {
                lightColorBuffer = new Vector4[MAX_LIGHTS];
            }
            shouldSortLights = true;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        // for URP
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {
            CheckAnchorPosition();
        }

        // for Built-in
        void OnPreRender() {
            CheckAnchorPosition();
        }

        private void Start() {
            env = VoxelPlayEnvironment.instance;
            if (env == null || !VoxelPlayEnvironment.supportsBrightPointLights || VoxelPlayEnvironment.supportsURPNativeLights) {
                DestroyImmediate(this);
                return;
            }
        }

        void CheckAnchorPosition() {
            if (env == null) return;
            camPos = env.currentAnchorPosWS;
            FastMath.FloorToInt(camPos.x, camPos.y, camPos.z, out int x, out int y, out int z);
            x >>= 3;
            y >>= 3;
            z >>= 3;
            if (lastX == x && lastY == y && lastZ == z)
                return;
            lastX = x;
            lastY = y;
            lastZ = z;
            shouldSortLights = true;
        }

        void LateUpdate() {
            if (shouldSortLights) {
                shouldSortLights = false;
                lights.Sort(distanceComparer);
            }
            UpdateLights();
        }

        void UpdateLights() {
            if (env == null) return;
            float worldLightIntensity = Mathf.Max(env.world.lightIntensityMultiplier, 0);
            float worldLightScattering = Mathf.Max(env.world.lightScattering, 0);
            int i = 0;
            int lightCount = lights.Count;
            Camera cam = env.currentCamera;
            Vector3 camPos = Vector3.zero, camForward = Vector3.one;
            bool excludeLightsBehind = false;
            if (cam != null) {
                camPos = cam.transform.position;
                camForward = cam.transform.forward;
                excludeLightsBehind = true;
            }

            for (int k = 0; k < lightCount; k++) {

                VoxelPlayLight vpLight = lights[k];
                if (vpLight == null) {
                    lights.RemoveAt(k);
                    k--;
                    continue;
                }

                if (!vpLight.isActiveAndEnabled) continue;

                float lightRange;
                float lightIntensity;
                Color lightColor;

                if (vpLight.virtualLight) {
                    lightRange = vpLight.lightRange;
                    lightIntensity = vpLight.lightIntensity;
                    lightColor = vpLight.lightColor;
                } else {
                    Light light = lights[k].pointLight;
                    if (light == null) continue;
                    lightRange = light.range;
                    lightIntensity = light.intensity;
                    lightColor = light.color;
                }

                // ignore light if it's behind camera + range
                Vector3 lightPos = vpLight.transform.position;
                float range = 0.0001f + lightRange * worldLightScattering;
                if (excludeLightsBehind) {
                    Vector3 toLight = lightPos - camPos;
                    float dot = Vector3.Dot(camForward, lightPos - camPos);
                    if (dot < 0 && toLight.sqrMagnitude > range * range) {
                        continue;
                    }
                }

                // ignore if intensity is zero
                float intensity = lightIntensity * worldLightIntensity;
                if (intensity <= 0) continue;

                lightPosBuffer[i].x = lightPos.x;
                lightPosBuffer[i].y = lightPos.y;
                lightPosBuffer[i].z = lightPos.z;
                lightPosBuffer[i].w = range;
                lightColorBuffer[i].x = lightColor.r * intensity;
                lightColorBuffer[i].y = lightColor.g * intensity;
                lightColorBuffer[i].z = lightColor.b * intensity;
                lightColorBuffer[i].w = lightColor.a;
                i++;
                if (i >= MAX_LIGHTS) break;
            }

            while (i < MAX_LIGHTS) {
                lightPosBuffer[i].x = float.MaxValue;
                lightPosBuffer[i].y = float.MaxValue;
                lightPosBuffer[i].z = float.MaxValue;
                lightPosBuffer[i].w = 1.0f;
                lightColorBuffer[i].x = 0;
                lightColorBuffer[i].y = 0;
                lightColorBuffer[i].z = 0;
                lightColorBuffer[i].w = 0;
                i++;
            }
            Shader.SetGlobalVectorArray(ShaderParams.GlobalLightPositionsArray, lightPosBuffer);
            Shader.SetGlobalVectorArray(ShaderParams.GlobalLightColorsArray, lightColorBuffer);
            Shader.SetGlobalInt(ShaderParams.GlobalLightCount, i);
        }

        int distanceComparer(VoxelPlayLight a, VoxelPlayLight b) {
            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;
            float distA = FastVector.SqrDistance(ref camPos, ref posA);
            float distB = FastVector.SqrDistance(ref camPos, ref posB);
            if (distA < distB)
                return -1;
            if (distA > distB)
                return 1;
            return 0;
        }

    }

}

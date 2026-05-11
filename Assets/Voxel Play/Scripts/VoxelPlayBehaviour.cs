// Voxel Play 
// Created by Ramiro Oliva (Kronnect)
// Voxel Play Behaviour - attach this script to any moving object that should receive voxel global illumination

using UnityEngine;
using System.Collections.Generic;

namespace VoxelPlay {

    [HelpURL("https://kronnect.com/docs/voxel-play/")]
    public class VoxelPlayBehaviour : MonoBehaviour {

        [Tooltip("Keeps the material color or intensity of this object updated with voxel light information.")]
        public bool enableVoxelLight = true;

        [Tooltip("Automatically replaces Standard material by Voxel Play optimized materials")]
        public bool useVoxelPlayMaterials;

        [Tooltip("Ensures the object don't get into another voxel by falling into it or crossing it.")]
        public bool forceUnstuck = true;

        [Tooltip("Vertical shift over the ground applied to this gameobject when unstuck occurs")]
        public float unstuckOffsetY;

        [Tooltip("If nearby chunks should be checked and created if they don't exist. This option is required to avoid this object to fall down.")]
        public bool checkNearChunks = true;

        [Tooltip("Extents around this object to be checked")]
        public Vector3 chunkExtents;

        [Tooltip("If chunks created around this object should also be rendered")]
        public bool renderChunks = true;

        [Tooltip("If this gameobject will be shifted when origin shift occurs")]
        public bool useOriginShift = true;

        VoxelPlayEnvironment env;
        int lastX, lastY, lastZ;
        int lastChunkX, lastChunkY, lastChunkZ;
        Vector3d lastPosition;
        bool requireUpdateLighting;

        static readonly List<Renderer> rr = new List<Renderer>();

        struct MaterialData {
            public Material mat;
            public Color normalMatColor;
            public bool useMaterialColor;
        }

        struct RendererData {
            public MaterialData[] materials;
        }

        RendererData[] rd;
        Rigidbody rb;
        static readonly Dictionary<Material, Material> upgradedMaterials = new Dictionary<Material, Material>();

        static class ShaderParams {
            public static int VoxelLight = Shader.PropertyToID("_VoxelLight");
            public static int Color = Shader.PropertyToID("_Color");
            public static int BumpMap = Shader.PropertyToID("_BumpMap");
        }

        public virtual void Start () {
            env = VoxelPlayEnvironment.instance;
            if (env == null) {
                DestroyImmediate(this);
                return;
            }
            env.OnChunkRender += ChunkRender;
            lastPosition = transform.position;
            lastX = int.MaxValue;
            rb = GetComponent<Rigidbody>();

            if (useVoxelPlayMaterials) {
                Shader vpShaderOpaque = Shader.Find("Voxel Play/Models/Texture/Opaque");
                Shader vpShaderTransp = Shader.Find("Voxel Play/Models/Texture/Alpha");
                Shader vpShaderOpaqueBumpMap = Shader.Find("Voxel Play/Models/Texture/Opaque BumpMap");
                if (vpShaderOpaque == null || vpShaderTransp == null) {
                    Debug.LogError("Could not find Voxel Play/Models/Texture/Opaque shader.");
                } else {
                    Renderer[] renderers = GetComponentsInChildren<Renderer>();
                    for (int k = 0; k < renderers.Length; k++) {
                        Material[] mats = renderers[k].sharedMaterials;
                        if (mats == null) continue;
                        for (int m = 0; m < mats.Length; m++) {
                            Material mat = mats[m];
                            if (mat != null && !mat.shader.name.Contains("Voxel Play")) {
                                if (!upgradedMaterials.TryGetValue(mat, out Material upgradedMaterial)) {
                                    upgradedMaterial = Instantiate(mat);
                                    if (mat.renderQueue >= 3000 && mat.HasProperty(ShaderParams.Color) && mat.color.a < 1f) {
                                        upgradedMaterial.shader = vpShaderTransp;
                                    } else if (mat.HasProperty(ShaderParams.BumpMap)) {
                                        upgradedMaterial.shader = vpShaderOpaqueBumpMap;
                                    } else {
                                        upgradedMaterial.shader = vpShaderOpaque;
                                    }
                                    upgradedMaterials[mat] = upgradedMaterial;
                                }
                                mats[m] = upgradedMaterial;
                            }
                        }
                        renderers[k].sharedMaterials = mats;
                    }
                }
            }

            if (enableVoxelLight) {
                FetchMaterials();
            }

            if (useOriginShift) {
                env.RegisterOriginShiftTransform(transform.root);
            }

            if (forceUnstuck) {
                CheckStuck();
            }

            if (checkNearChunks) {
                CheckNearChunks(lastPosition);
            }

        }

        void FetchMaterials() {
            GetComponentsInChildren(true, rr);
            int count = rr.Count;
            rd = new RendererData[count];
            for (int k = 0; k < count; k++) {
                Renderer mr = rr[k];
                if (mr.sharedMaterials == null) continue;
                Material[] mats = mr.sharedMaterials;
                int matsLength = mats.Length;
                rd[k].materials = new MaterialData[matsLength];

                if (matsLength == 1) {
                    // Optimized path for single material to avoid new array allocation for newMats
                    Material mat = mats[0];
                    if (mat == null) continue;
                    rd[k].materials[0].useMaterialColor = !mat.shader.name.Contains("Voxel Play/Models");
                    mat = Instantiate(mat);
                    rd[k].materials[0].normalMatColor = mat.HasProperty(ShaderParams.Color) ? mat.color : Misc.colorWhite;
                    mat.DisableKeyword(VoxelPlayEnvironment.SKW_VOXELPLAY_GPU_INSTANCING);
                    rd[k].materials[0].mat = mat;
                    mr.sharedMaterial = mat;
                } else if (matsLength > 1) {
                    // Original path for multiple materials
                    Material[] newMats = new Material[matsLength];
                    for (int j = 0; j < matsLength; j++) {
                        Material mat = mats[j];
                        if (mat == null) continue;
                        rd[k].materials[j].useMaterialColor = !mat.shader.name.Contains("Voxel Play/Models");
                        mat = Instantiate(mat);
                        newMats[j] = mat;
                        rd[k].materials[j].normalMatColor = mat.HasProperty(ShaderParams.Color) ? mat.color : Misc.colorWhite;
                        mat.DisableKeyword(VoxelPlayEnvironment.SKW_VOXELPLAY_GPU_INSTANCING);
                        rd[k].materials[j].mat = mat;
                    }
                    mr.sharedMaterials = newMats;
                }
            }
            requireUpdateLighting = true;
        }

        public virtual void OnDestroy () {
            if (env == null) return;

            env.OnChunkRender -= ChunkRender;

            if (rd != null) {
                for (int k = 0; k < rd.Length; k++) {
                    if (rd[k].materials != null) {
                        for (int j = 0; j < rd[k].materials.Length; j++) {
                            Material mat = rd[k].materials[j].mat;
                            Misc.DestroyAndNullify(ref mat);
                            rd[k].materials[j].mat = null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears all static cached data. Should be called when disposing the VoxelPlayEnvironment.
        /// </summary>
        public static void Dispose() {
            if (upgradedMaterials.Count > 0) {
                foreach (var kvp in upgradedMaterials) {
                    Material mat = kvp.Value;
                    Misc.DestroyImmediateAndNullify(ref mat);
                }
                upgradedMaterials.Clear();
            }
        }

        void ChunkRender (VoxelChunk chunk) {
            if (FastVector.SqrMinDistanceXZ((Vector3)chunk.position, transform.position) < 32 * 32) {
                requireUpdateLighting = true;
            }
        }

        public void Refresh () {
            lastX = int.MaxValue;
            lastChunkX = int.MaxValue;
        }

        public virtual void LateUpdate () {

            if (!env.initialized)
                return;

            // Check if position has changed since previous
            Vector3d position = transform.position;
            FastMath.FloorToInt(position.x, position.y, position.z, out int x, out int y, out int z);

            if (lastX != x || lastY != y || lastZ != z) {
                requireUpdateLighting = true;

                lastPosition = position;
                lastX = x;
                lastY = y;
                lastZ = z;

                if (forceUnstuck) {
                    CheckStuck();
                }

                if (checkNearChunks) {
                    CheckNearChunks(position);
                }
            }
            if (requireUpdateLighting) {
                requireUpdateLighting = false;
                UpdateLightingNow();
            }
        }


        void CheckStuck () {
            Vector3 pos = transform.position;
            pos.y += unstuckOffsetY + 0.1f;
            if (env.CheckCollision(pos)) {
                float deltaY = FastMath.FloorToInt(pos.y) + 1.01f - pos.y;
                pos.y += deltaY;
                if (rb != null) {
                    rb.position = pos;
                    if (!rb.isKinematic) {
                        rb.linearVelocity = Misc.vector3zero;
                    }
                } else {
                    transform.position = pos;
                }
                lastX--;
            }
        }

        void CheckNearChunks (Vector3d position) {
            int chunkX, chunkY, chunkZ;
            FastMath.FloorToInt(position.x / VoxelPlayEnvironment.CHUNK_SIZE, position.y / VoxelPlayEnvironment.CHUNK_SIZE, position.z / VoxelPlayEnvironment.CHUNK_SIZE, out chunkX, out chunkY, out chunkZ);
            if (lastChunkX != chunkX || lastChunkY != chunkY || lastChunkZ != chunkZ) {
                lastChunkX = chunkX;
                lastChunkY = chunkY;
                lastChunkZ = chunkZ;
                // Ensure area is rendered
                env.ChunkCheckArea(position, chunkExtents, renderChunks);
            }
        }


        public void UpdateLighting () {
            requireUpdateLighting = true;
        }

        void UpdateLightingNow () {
            if (!enableVoxelLight) return;
            if (rd == null || rd.Length == 0) {
                FetchMaterials();
            }
            Vector3d pos = lastPosition;
            // center of voxel
            pos.x += 0.5f;
            pos.y += 0.5f;
            pos.z += 0.5f;
            float light = -1;
            int packedLight = -1;

            int rdLength = rd.Length;
            for (int k = 0; k < rdLength; k++) {
                int matsLength = rd[k].materials.Length;
                for (int j = 0; j < matsLength; j++) {
                    Material mat = rd[k].materials[j].mat;
                    if (mat == null) continue;

                    if (rd[k].materials[j].useMaterialColor) {
                        if (light < 0) {
                            light = env.GetVoxelLight(pos);
                        }
                        Color normalMatColor = rd[k].materials[j].normalMatColor;
                        Color newColor = new Color(normalMatColor.r * light, normalMatColor.g * light, normalMatColor.b * light, normalMatColor.a);
                        mat.color = newColor;
                    } else {
                        if (packedLight < 0) {
                            packedLight = env.GetVoxelLightPacked(pos);
                        }
                        mat.SetInt(ShaderParams.VoxelLight, packedLight);
                    }
                }
            }
        }

    }
}
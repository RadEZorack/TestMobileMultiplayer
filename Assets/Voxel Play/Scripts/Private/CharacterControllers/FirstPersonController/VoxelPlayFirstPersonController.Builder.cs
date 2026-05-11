using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;

namespace VoxelPlay {

    public partial class VoxelPlayFirstPersonController : VoxelPlayCharacterControllerBase {

        GameObject voxelHighlightBuilder;

#if UNITY_EDITOR
        [HideInInspector]
        public ModelDefinition constructorModel;

        [HideInInspector]
        public Vector3 constructorSize = new Vector3Int(15, 15, 15);

        [NonSerialized]
        public string constructorModelFilename = "NewModelDefinition";

        GameObject grid;
        const string GRID_NAME = "Voxel Play Builder Grid";
        Vector3d buildingPosition;

        // playmode state
        Vector3 beforeConstructorPlayerPosition;
        Quaternion beforeConstructorPlayerRotation, beforeConstructorCameraRotation;
        bool beforeOrbitMode, beforeFreeMode, beforeIsFlying, beforeEnableColliders, beforeBuildMode;

        // constructor state
        Vector3 constructorLastPlayerPosition;
        Quaternion constructorLastPlayerRotation, constructorLastCameraRotation;

        public virtual void ToggleConstructor () {
            env.constructorMode = !env.constructorMode;
            if (env.constructorMode) {
                GetModelSize();
                StorePlayState();
                env.buildMode = true;
                env.enableColliders = false;
                freeCamMode = false;
                freeMode = false;
                isFlying = true;
                UpdateConstructorEnvironment();
                if (constructorLastPlayerPosition != Misc.vector3zero) {
                    MoveTo(constructorLastPlayerPosition);
                    transform.rotation = constructorLastPlayerRotation;
                    m_Camera.transform.rotation = constructorLastCameraRotation;
                } else {
                    ResetPlayerPosition();
                }
            } else {
                constructorLastPlayerPosition = transform.position;
                constructorLastPlayerRotation = transform.rotation;
                constructorLastCameraRotation = m_Camera.transform.rotation;
                RestorePlayState();
            }
            if (env.constructorMode) {
                env.ShowMessage("<color=green>Entered </color><color=yellow>The Constructor</color>.");
            } else {
                env.ShowMessage("<color=green>Back to normal world. Press <color=white>B</color> to cancel <color=yellow>Build Mode</color>.</color>");
            }
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }




        protected virtual void UpdateConstructorEnvironment () {
            if (env.constructorMode) {
                if (grid == null) {
                    grid = Instantiate(Resources.Load<GameObject>("VoxelPlay/Prefabs/Grid"));
                    grid.name = GRID_NAME;
                } else {
                    grid.SetActive(true);
                }
                constructorSize = new Vector3((int)constructorSize.x, (int)constructorSize.y, (int)constructorSize.z);
                buildingPosition = Vector3d.one * 1608f;
                if (constructorSize.x % 2 != 0) { buildingPosition.x += 0.5f; }
                if (constructorSize.z % 2 != 0) { buildingPosition.z += 0.5f; }

                grid.transform.localScale = constructorSize;
                Vector3d gridPos = buildingPosition + new Vector3d(0, constructorSize.y / 2, 0);
                grid.transform.position = gridPos;
                grid.GetComponent<Renderer>().sharedMaterial.SetVector("_Size", new Vector4(constructorSize.x, constructorSize.y, constructorSize.z, 0));
                Transform gridPivot = grid.transform.Find("GridPivot");
                if (gridPivot != null) {
                    gridPivot.transform.localScale = new Vector3(0.1f / constructorSize.x, 0.1f / constructorSize.y, 0.1f / constructorSize.z);
                }
                limitBounds = new Bounds(gridPos, new Vector3(constructorSize.x - 1, constructorSize.y - 1, constructorSize.z - 1));
                UpdateVoxelHighlight();
                voxelHighlightBuilder.SetActive(true);
            } else {
                if (grid != null) {
                    grid.SetActive(false);
                }
                if (voxelHighlightBuilder != null) {
                    voxelHighlightBuilder.SetActive(false);
                }
            }
        }

        protected virtual void UpdateConstructor () {

            if (!env.buildMode || !env.constructorMode)
                return;

            UpdateVoxelHighlight();
        }

        protected virtual void UpdateVoxelHighlight () {
            if (voxelHighlightBuilder == null) {
                voxelHighlightBuilder = Instantiate<GameObject>(Resources.Load<GameObject>("VoxelPlay/Prefabs/VoxelHighlight"));
            }

            Vector3 rawPos;
            if (crosshairOnBlock) {
                rawPos = (_crosshairHitInfo.voxelCenter + _crosshairHitInfo.normal);
            } else {
                if (freeMode) {
                    Ray ray = m_Camera.ScreenPointToRay(Input.mousePosition);
                    rawPos = ray.origin + ray.direction * 4f;
                } else {
                    rawPos = m_Camera.transform.position + m_Camera.transform.forward * 4f;
                }
            }

            // Bound check
            for (int i = 0; i < 50; i++) {
                if (limitBounds.Contains(rawPos))
                    break;
                rawPos -= m_Camera.transform.forward * 0.1f;
            }

            rawPos.x = FastMath.FloorToInt(rawPos.x) + 0.5f;
            if (rawPos.x > limitBounds.max.x)
                rawPos.x = limitBounds.max.x - 0.5f;
            if (rawPos.x < limitBounds.min.x)
                rawPos.x = limitBounds.min.x + 0.5f;
            rawPos.y = FastMath.FloorToInt(rawPos.y) + 0.5f;
            if (rawPos.y > limitBounds.max.y)
                rawPos.y = limitBounds.max.y - 0.5f;
            if (rawPos.y < limitBounds.min.y)
                rawPos.y = limitBounds.min.y + 0.5f;
            rawPos.z = FastMath.FloorToInt(rawPos.z) + 0.5f;
            if (rawPos.z > limitBounds.max.z)
                rawPos.z = limitBounds.max.z - 0.5f;
            if (rawPos.z < limitBounds.min.z)
                rawPos.z = limitBounds.min.z + 0.5f;
            voxelHighlightBuilder.transform.position = rawPos;
        }


        public virtual bool NewModel () {
            if (!env.constructorMode) return false;

            if (!DisplayDialog("New Model", "Discard any change?", "Ok", "Cancel")) {
                return false;
            }

            ClearConstructionArea();
            constructorModel = null;
            UpdateConstructorEnvironment();
            return true;
        }

        public virtual bool LoadModel (ModelDefinition model) {
            if (!env.constructorMode || model == null) {
                return false;
            }

            if (!DisplayDialog("Load Model", "Discard any change and load the model definition?", "Ok", "Cancel")) {
                return false;
            }

            ClearConstructionArea();
            constructorModel = model;
            GetModelSize();
            UpdateConstructorEnvironment();

            // Loads model content
            Vector3d pos = buildingPosition - new Vector3(constructorModel.offsetX, constructorModel.offsetY, constructorModel.offsetZ); // ignore offset
            env.ModelPlace(pos, constructorModel);

            return true;
        }

        protected virtual void ClearConstructionArea () {
            for (int y = -1; y <= constructorSize.y; y += VoxelPlayEnvironment.CHUNK_SIZE) {
                for (int z = -1; z <= constructorSize.z; z += VoxelPlayEnvironment.CHUNK_SIZE) {
                    for (int x = -1; x <= constructorSize.x; x += VoxelPlayEnvironment.CHUNK_SIZE) {
                        Vector3d destroyPosition = buildingPosition + new Vector3(x - constructorSize.x / 2, y, z - constructorSize.z / 2);
                        env.ChunkDestroy(destroyPosition);
                    }
                }
            }
        }

        protected virtual void ResetPlayerPosition () {
            MoveTo(grid.transform.position);
            constructorLastPlayerPosition = transform.position;
            constructorLastPlayerRotation = transform.rotation;
            constructorLastCameraRotation = m_Camera.transform.rotation;
        }

        public virtual bool SaveModel (bool saveAsNew) {

            if (!env.constructorMode) return false;

            string modelFilename;
            bool isNew = (saveAsNew || constructorModel == null);
            if (isNew) {
                modelFilename = "Assets/" + constructorModelFilename + ".asset";
            } else {
                modelFilename = AssetDatabase.GetAssetPath(constructorModel);
            }
            if (!DisplayDialog("Save Model?", "Save current model to file " + modelFilename + "?", "Yes", "No"))
                return false;


            if (isNew) {
                constructorModel = ScriptableObject.CreateInstance<ModelDefinition>();
            }
            List<ModelBit> bits = new List<ModelBit>();
            List<TorchBit> torchBits = new List<TorchBit>();
            int sy = (int)constructorSize.y;
            int sz = (int)constructorSize.z;
            int sx = (int)constructorSize.x;

            for (int y = 0; y < sy; y++) {
                for (int z = 0; z < sz; z++) {
                    for (int x = 0; x < sx; x++) {
                        Vector3d pos = buildingPosition + new Vector3(x - constructorSize.x / 2, y, z - constructorSize.z / 2);
                        if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, false)) continue;
                        Voxel voxel = chunk.voxels[voxelIndex];
                        if (voxel.hasContent && !voxel.type.isDynamic) {
                            int k = y * sz * sx + z * sx + x;
                            ModelBit bit = new ModelBit();
                            bit.voxelIndex = k;
                            bit.voxelDefinition = voxel.type;
                            bit.color = voxel.color;
                            bit.rotation = voxel.GetTextureRotationDegrees();
                            bit.microVoxels = null;
                            if (chunk.usesMicroVoxels && chunk.microVoxels.TryGetValue(voxelIndex, out MicroVoxels microVoxels)) {
                                bit.microVoxels = microVoxels;
                            }
                            // Store voxel properties if any exist
                            if (chunk.voxelsProperties != null && chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                                List<KeyValuePair<int, VoxelProperty>> props = BufferPool<KeyValuePair<int, VoxelProperty>>.Get();
                                voxelProperties.GetValues(props);
                                foreach (var prop in props) {
                                    if (!string.IsNullOrEmpty(prop.Value.stringValue)) {
                                        bit.SetProperty(prop.Key, prop.Value.stringValue);
                                    } else {
                                        bit.SetProperty(prop.Key, prop.Value.floatValue);
                                    }
                                }
                                BufferPool<KeyValuePair<int, VoxelProperty>>.Release(props);
                            }
                            bits.Add(bit);
                        }
                        LightSource ls = chunk.GetLightSource(voxelIndex);
                        if ((object)ls != null) {
                            int k = y * sz * sx + z * sx + x;
                            TorchBit torchBit = new TorchBit();
                            torchBit.itemDefinition = ls.itemDefinition;
                            torchBit.voxelIndex = k;
                            torchBit.normal = ls.hitInfo.normal;
                            torchBits.Add(torchBit);
                        }
                    }
                }
            }
            constructorModel.SetBits(bits.ToArray());
            constructorModel.torches = torchBits.ToArray();
            constructorModel.sizeX = sx;
            constructorModel.sizeY = sy;
            constructorModel.sizeZ = sz;

            if (isNew) {
                modelFilename = AssetDatabase.GenerateUniqueAssetPath("Assets/" + constructorModelFilename + ".asset");
                AssetDatabase.CreateAsset(constructorModel, modelFilename);
            }
            EditorUtility.SetDirty(constructorModel);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            env.Redraw();

            if (isNew) {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = constructorModel;
                EditorUtility.DisplayDialog("Save Model", "New model file created successfully in " + modelFilename + ".", "Ok");
            }

            return true;
        }

        protected virtual void GetModelSize () {
            if (constructorModel != null) {
                constructorSize.x = constructorModel.sizeX;
                constructorSize.y = constructorModel.sizeY;
                constructorSize.z = constructorModel.sizeZ;
            }
        }

        public virtual void DisplaceModel (int dx, int dy, int dz) {
            int sy = (int)constructorSize.y;
            int sz = (int)constructorSize.z;
            int sx = (int)constructorSize.x;

            Voxel[] newContents = new Voxel[sy * sz * sx];
            int ny, nz, nx;
            for (int y = 0; y < sy; y++) {
                ny = y + dy;
                if (ny >= sy)
                    ny -= sy;
                else if (ny < 0)
                    ny += sy;
                for (int z = 0; z < sz; z++) {
                    nz = z + dz;
                    if (nz >= sz)
                        nz -= sz;
                    else if (nz < 0)
                        nz += sz;
                    for (int x = 0; x < sx; x++) {
                        Vector3d buildPos = buildingPosition + new Vector3(x - constructorSize.x / 2, y, z - constructorSize.z / 2);
                        Voxel voxel = env.GetVoxel(buildPos);
                        if (voxel.hasContent) {
                            nx = x + dx;
                            if (nx >= sx)
                                nx -= sx;
                            else if (nx < 0)
                                nx += sx;
                            newContents[ny * sz * sx + nz * sx + nx] = voxel;
                        }
                    }
                }
            }

            // Replace voxels
            ClearConstructionArea();
            for (int y = 0; y < sy; y++) {
                for (int z = 0; z < sz; z++) {
                    for (int x = 0; x < sx; x++) {
                        int voxelIndex = y * sz * sx + z * sx + x;
                        if (!newContents[voxelIndex].isEmpty) {
                            Vector3d placePos = buildingPosition + new Vector3(x - constructorSize.x / 2, y, z - constructorSize.z / 2);
                            env.VoxelPlace(placePos, newContents[voxelIndex]);
                            env.VoxelSetTexturesRotation(placePos, newContents[voxelIndex].GetTextureRotation());
                        }
                    }
                }
            }
        }


        public virtual void ResizeModel (int dx, int dy, int dz) {
            constructorSize.x += dx;
            if (constructorSize.x < 1) constructorSize.x = 1;
            constructorSize.y += dy;
            if (constructorSize.y < 1) constructorSize.y = 1;
            constructorSize.z += dz;
            if (constructorSize.z < 1) constructorSize.z = 1;
            UpdateConstructorEnvironment();
        }

        protected virtual void StorePlayState () {
            beforeConstructorPlayerPosition = transform.position;
            beforeConstructorPlayerRotation = transform.rotation;
            beforeConstructorCameraRotation = m_Camera.transform.rotation;
            beforeOrbitMode = freeCamMode;
            beforeFreeMode = freeMode;
            beforeEnableColliders = env.enableColliders;
            beforeBuildMode = env.buildMode;
            beforeIsFlying = isFlying;
        }

        protected virtual void RestorePlayState () {
            freeCamMode = beforeOrbitMode;
            freeMode = beforeFreeMode;
            env.enableColliders = beforeEnableColliders;
            env.buildMode = beforeBuildMode;
            isFlying = beforeIsFlying;
            MoveTo(beforeConstructorPlayerPosition);
            transform.rotation = beforeConstructorPlayerRotation;
            m_Camera.transform.rotation = beforeConstructorCameraRotation;
        }

        protected virtual bool DisplayDialog (string title, string message, string ok, string cancel = null) {
            mouseLook.SetCursorLock(false);
            bool res = EditorUtility.DisplayDialog(title, message, ok, cancel);
            mouseLook.SetCursorLock(true);
            return res;
        }

#endif

    }
}

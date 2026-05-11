using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

    public class WorldEditorToolElevateTerrain : WorldEditorTool {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolElevation");
        public override string title => "Raise or lower the terrain";
        public override string instructions => "Hold shift to lower terrain.";
        public override int priority => 1;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;
        public override bool showRecentVoxels => true;

        protected bool clampAltitude = false;
        protected bool enableHalfStepSurface;

        static Dictionary<VoxelDefinition, BiomeDefinition> _biomeByVoxel;

        protected void EnsureBiomeLookup () {
            if (_biomeByVoxel != null) return;
            _biomeByVoxel = new Dictionary<VoxelDefinition, BiomeDefinition>(64);
            var biomes = env?.world?.biomes;
            if (biomes == null) return;
            for (int i = 0; i < biomes.Length; i++) {
                var b = biomes[i];
                if (b == null) continue;
                if (b.voxelTop != null) _biomeByVoxel[b.voxelTop] = b;
                if (b.voxelDirt != null) _biomeByVoxel[b.voxelDirt] = b;
                if (b.voxelLakeBed != null) _biomeByVoxel[b.voxelLakeBed] = b;
                if (b.voxelTopAdditional != null) {
                    for (int j = 0; j < b.voxelTopAdditional.Length; j++) {
                        var v = b.voxelTopAdditional[j].voxelDefinition;
                        if (v != null) _biomeByVoxel[v] = b;
                    }
                }
                if (b.voxelDirtAdditional != null) {
                    for (int j = 0; j < b.voxelDirtAdditional.Length; j++) {
                        var v = b.voxelDirtAdditional[j].voxelDefinition;
                        if (v != null) _biomeByVoxel[v] = b;
                    }
                }
            }
        }

        protected BiomeDefinition GetBiomeForColumn (Vector3d pos, VoxelChunk chunk, int elevationIndex, BiomeDefinition current) {
            int ny = chunk.terrainInfo[elevationIndex].groundLevel;
            Vector3d sPos = pos; sPos.y = ny;
            if (env.GetVoxelIndex(sPos, out VoxelChunk sc, out int si, createChunkIfNotExists: false)) {
                VoxelDefinition surface = sc.voxels[si].type;
                if (surface != null) {
                    EnsureBiomeLookup();
                    if (_biomeByVoxel != null && _biomeByVoxel.TryGetValue(surface, out BiomeDefinition b) && (object)b != null) {
                        if (!ReferenceEquals(b, current)) {
                            current = b;
                            chunk.terrainInfo[elevationIndex].biome = b;
                        }
                    }
                }
            }
            return current;
        }


        public WorldEditorToolElevateTerrain () : base() {
            if (env == null || env.world == null) {
                return;
            }
            if (env.world.terrainGenerator is TerrainDefaultGenerator terrainGenerator) {
                enableHalfStepSurface = terrainGenerator.enableHalfStepSurface;
            }
        }

        public override void DrawInspector () {
            base.DrawInspector();
            enableHalfStepSurface = UnityEditor.EditorGUILayout.Toggle("Enable Half Step Surface", enableHalfStepSurface);

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Voxel Override", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUI.BeginChangeCheck();
            env.sceneEditorElevateVoxelSurface = (VoxelDefinition)UnityEditor.EditorGUILayout.ObjectField(
                new GUIContent("Surface Voxel", "Override biome surface voxel when raising/lowering terrain. Leave empty to use biome default."),
                env.sceneEditorElevateVoxelSurface, typeof(VoxelDefinition), false);
            env.sceneEditorElevateVoxelFill = (VoxelDefinition)UnityEditor.EditorGUILayout.ObjectField(
                new GUIContent("Fill Voxel", "Override biome underground voxel when raising terrain. Leave empty to use biome default."),
                env.sceneEditorElevateVoxelFill, typeof(VoxelDefinition), false);
            if (UnityEditor.EditorGUI.EndChangeCheck()) {
                RegisterOverrideVoxels();
                if (env.sceneEditorElevateVoxelSurface != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv?.AddRecentVoxel(env.sceneEditorElevateVoxelSurface);
                }
                if (env.sceneEditorElevateVoxelFill != null) {
                    VoxelPlayEnvironmentEditor.currentEditingEnv?.AddRecentVoxel(env.sceneEditorElevateVoxelFill);
                }
            }

            DrawOverrideRecentVoxelsGrid();
        }

        void DrawOverrideRecentVoxelsGrid () {
            if (VoxelPlayEnvironmentEditor.currentEditingEnv == null) return;
            var recentVoxels = VoxelPlayEnvironmentEditor.currentEditingEnv.GetRecentVoxels(env);
            if (recentVoxels.Count == 0) return;

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.LabelField("Recent Voxels (drag into slots above)", UnityEditor.EditorStyles.boldLabel);

            int columns = 4;
            int rows = Mathf.CeilToInt(recentVoxels.Count / (float)columns);
            float cellHeight = 70;
            float gridHeight = rows * cellHeight;
            Rect gridRect = GUILayoutUtility.GetRect(0, gridHeight, GUILayout.ExpandWidth(true));
            float cellWidth = gridRect.width / columns;

            Event evt = Event.current;

            for (int i = 0; i < recentVoxels.Count; i++) {
                int col = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(gridRect.x + col * cellWidth, gridRect.y + row * cellHeight, cellWidth, cellHeight);

                VoxelDefinition vd = recentVoxels[i];
                Texture2D icon = null;
                if (vd.icon != null) icon = vd.icon;
                else if (vd.textureSample != null) icon = vd.textureSample;
                else if (vd.textureSide != null) icon = vd.textureSide;
                else if (vd.textureTop != null) icon = vd.textureTop;

                if (evt.type == EventType.MouseDown && cellRect.Contains(evt.mousePosition)) {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    _dragSourceIndex = i;
                    evt.Use();
                }

                if (evt.type == EventType.MouseDrag && _dragSourceIndex == i) {
                    UnityEditor.DragAndDrop.PrepareStartDrag();
                    UnityEditor.DragAndDrop.objectReferences = new Object[] { vd };
                    UnityEditor.DragAndDrop.StartDrag(vd.name);
                    _dragSourceIndex = -1;
                    evt.Use();
                }

                // Click-to-select: assign to Surface Voxel slot
                if (evt.type == EventType.MouseUp && _dragSourceIndex == i && cellRect.Contains(evt.mousePosition)) {
                    env.sceneEditorElevateVoxelSurface = vd;
                    RegisterOverrideVoxels();
                    VoxelPlayEnvironmentEditor.currentEditingEnv?.AddRecentVoxel(vd);
                    _dragSourceIndex = -1;
                    evt.Use();
                }

                GUI.Button(cellRect, new GUIContent(icon, vd.name));
            }

            if (evt.type == EventType.MouseUp) {
                _dragSourceIndex = -1;
            }

            UnityEditor.EditorGUILayout.Space();
        }

        int _dragSourceIndex = -1;

        protected bool HasVoxelOverride => env.sceneEditorElevateVoxelSurface != null || env.sceneEditorElevateVoxelFill != null;

        protected void RegisterOverrideVoxels () {
            if (env.sceneEditorElevateVoxelSurface != null) {
                env.AddVoxelDefinition(env.sceneEditorElevateVoxelSurface);
                if (env.sceneEditorElevateVoxelSurface.index > 0) {
                    terrainVoxelDefinitions?.Add(env.sceneEditorElevateVoxelSurface.index);
                }
            }
            if (env.sceneEditorElevateVoxelFill != null) {
                env.AddVoxelDefinition(env.sceneEditorElevateVoxelFill);
                if (env.sceneEditorElevateVoxelFill.index > 0) {
                    terrainVoxelDefinitions?.Add(env.sceneEditorElevateVoxelFill.index);
                }
            }
        }

        public override void DrawGizmos (VoxelHitInfo hitInfo, List<VoxelIndex> voxelIndices) {
            if (shift) {
                DrawArrow(hitInfo.center + Vector3.up, Vector3.down, 0.3f);
            } else {
                DrawArrow(hitInfo.center + Vector3.up * 0.5f, Vector3.up, 0.3f);
            }
        }

        public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
            VoxelIndex vi = new VoxelIndex();
            voxelIndices.Clear();
            int size = brushSize * 2 - 1;
            int count = size * size;
            Vector3d corner = hitInfo.center;
            corner.x -= brushSize - 1;
            corner.z -= brushSize - 1;

            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                if (count > 1) {
                    float mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
                    if (mask <= 0) continue;
                }

                Vector3d pos = corner;
                pos.z += pz;
                pos.x += px;
                pos.y = env.GetHeight(pos, allowedVoxelDefinitions: terrainVoxelDefinitions) - 0.1;

                if (env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: true)) {
                    if (chunk.voxels[voxelIndex].type.isSolid) {
                        vi.chunk = chunk;
                        vi.voxelIndex = voxelIndex;
                        voxelIndices.Add(vi);
                    }
                }
            }
        }

        protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {

            List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
            HashSet<VoxelChunk> modifiedSet = new HashSet<VoxelChunk>();

            Vector3d center = hitInfo.center;
            int size = brushSize * 2 - 1;
            int count = size * size;
            Vector3d corner = center;
            corner.x -= brushSize - 1;
            corner.z -= brushSize - 1;

            float altitude = 0;
            if (clampAltitude) {
                if (env.sceneEditorUseCenterVoxelAltitude) {
                    altitude = (float)startHitInfo.center.y;
                } else {
                    altitude = env.sceneEditorAltitude;
                }
            }

            bool hasOverride = HasVoxelOverride;
            RegisterOverrideVoxels();

            for (int k = 0; k < count; k++) {
                int pz = k / size;
                int px = k % size;

                float mask = 1;
                if (count > 1) {
                    mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
                    if (mask <= 0) continue;
                }

                Vector3d pos = corner;
                pos.z += pz;
                pos.x += px;
                pos.y = env.GetHeight(pos, allowedVoxelDefinitions: terrainVoxelDefinitions) - 0.1f;

                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) continue;

                UpdateChunkElevation(chunk);

                int z = (voxelIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
                int x = voxelIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;

                int elevationIndex = z * VoxelPlayEnvironment.CHUNK_SIZE + x;

                pos.y = chunk.terrainInfo[elevationIndex].groundLevel;

                float factor = 1;
                if (count > 1) {
                    factor = mask * brushStrength;
                }

                BiomeDefinition biome = hasOverride ? null : chunk.terrainInfo[elevationIndex].biome;
                if (!hasOverride) {
                    biome = GetBiomeForColumn(pos, chunk, elevationIndex, biome);
                }
                bool canPlaceHalfSurface = enableHalfStepSurface;

                VoxelDefinition surfaceVoxel = env.sceneEditorElevateVoxelSurface;
                VoxelDefinition fillVoxel = env.sceneEditorElevateVoxelFill;

                VoxelChunk otherChunk;
                int otherIndex;
                if (shift) { // lower terrain
                    if (clampAltitude && chunk.terrainInfo[elevationIndex].height <= altitude) continue;

                    undoManager.SaveChunk(chunk);
                    chunk.terrainInfo[elevationIndex].height -= factor;
                    if (clampAltitude && chunk.terrainInfo[elevationIndex].height < altitude) {
                        chunk.terrainInfo[elevationIndex].height = altitude;
                    }

                    int ny = chunk.terrainInfo[elevationIndex].groundLevel;

                    Vector3d bottomPos = pos;
                    bottomPos.y = ny;

                    // remove vegetation above terrain
                    ClearVegetationAbove(bottomPos, undoManager, modifiedChunks);

                    if (!env.GetVoxelIndex(bottomPos, out otherChunk, out otherIndex, createChunkIfNotExists: true)) continue;

                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                    if (hasOverride) {
                        VoxelDefinition lowerSurface = surfaceVoxel ?? fillVoxel;
                        if (lowerSurface != null) {
                            undoManager.SaveChunk(otherChunk);
                            otherChunk.voxels[otherIndex].Set(lowerSurface);
                        }
                    } else if ((object)biome != null) {
						undoManager.SaveChunk(otherChunk);
						otherChunk.voxels[otherIndex].Set(biome.voxelTop);
						bool placedVegLower = PlaceVegetationAbove(bottomPos, otherChunk, otherIndex, biome, modifiedChunks);
						if (enableHalfStepSurface) {
							canPlaceHalfSurface = !placedVegLower;
						}
                    }
                } else { // raise terrain
                    if (clampAltitude && chunk.terrainInfo[elevationIndex].height >= altitude) continue;

                    undoManager.SaveChunk(chunk);
                    chunk.terrainInfo[elevationIndex].height += factor;
                    if (clampAltitude && chunk.terrainInfo[elevationIndex].height > altitude) {
                        chunk.terrainInfo[elevationIndex].height = altitude;
                    }

                    int ny = chunk.terrainInfo[elevationIndex].groundLevel;

                    // only applies to voxels which have a non opaque voxel on top
                    Vector3d abovePos = pos;
                    abovePos.y = ny;
                    if (!env.GetVoxelIndex(abovePos, out otherChunk, out otherIndex, createChunkIfNotExists: true)) continue;
                    if (otherChunk.voxels[otherIndex].opaque >= VoxelPlayEnvironment.FULL_OPAQUE) continue;

                    if (hasOverride) {
                        undoManager.SaveChunk(otherChunk);
                        VoxelDefinition raiseFill = fillVoxel ?? surfaceVoxel;
                        VoxelDefinition raiseSurface = surfaceVoxel ?? fillVoxel;
                        if (raiseFill != null) {
                            chunk.SetVoxel(voxelIndex, raiseFill);
                        }
                        if (raiseSurface != null) {
                            otherChunk.SetVoxel(otherIndex, raiseSurface);
                        }
                    } else if ((object)biome != null) {
						undoManager.SaveChunk(otherChunk);
						chunk.SetVoxel(voxelIndex, biome.voxelDirt);
						otherChunk.SetVoxel(otherIndex, biome.voxelTop);
						bool placedVegRaise = PlaceVegetationAbove(abovePos, otherChunk, otherIndex, biome, modifiedChunks);
						if (enableHalfStepSurface) {
							canPlaceHalfSurface = !placedVegRaise;
						}
                    } else {
                        // simply repeats voxel
                        undoManager.SaveChunk(otherChunk);
                        otherChunk.SetVoxel(otherIndex, chunk.voxels[voxelIndex].type);
                    }
                }
                if (canPlaceHalfSurface) {
                    // Get fractional height to determine if we should place a half-voxel
                    float groundLevel = chunk.terrainInfo[elevationIndex].groundLevel;
                    float frac = chunk.terrainInfo[elevationIndex].height - groundLevel;
                        if (frac < 0.5f) {
                            otherChunk.SetMicroVoxels(otherIndex, MicroVoxels.halfSurfaceVoxelTemplate);
                        }
                }
                if (modifiedSet.Add(chunk)) {
                    modifiedChunks.Add(chunk);
                }
            }

            int modifiedCount = modifiedChunks.Count;
            RefreshModifiedChunks(modifiedChunks);

            BufferPool<VoxelChunk>.Release(modifiedChunks);

            return modifiedCount > 0;

        }

        protected void ClearVegetationAbove (Vector3d pos, UndoManager undoManager, List<VoxelChunk> modifiedChunks) {
            for (int k = 0; k < 8; k++) {
                if (!env.GetVoxelIndex(pos, out VoxelChunk chunk, out int voxelIndex, createChunkIfNotExists: false)) return;
                if (chunk.voxels[voxelIndex].isEmpty) return;
                if (chunk.voxels[voxelIndex].type.isVegetation) {
                    undoManager.SaveChunk(chunk);
                    chunk.ClearVoxel(voxelIndex, VoxelPlayEnvironment.FULL_LIGHT);
                    if (!modifiedChunks.Contains(chunk)) {
                        modifiedChunks.Add(chunk);
                    }
                }
                pos.y++;
            }
        }

        protected bool PlaceVegetationAbove (Vector3d pos, VoxelChunk chunk, int voxelIndex, BiomeDefinition biome, List<VoxelChunk> modifiedChunks) {
            if (env.enableVegetation && pos.y > env.waterLevel) {
                float rn = WorldRand.GetValue(pos);
                if (biome.vegetationDensity > 0 && rn < biome.vegetationDensity && biome.vegetation.Length > 0) {
                    if (voxelIndex < VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE * VoxelPlayEnvironment.ONE_Y_ROW) {
                        chunk.SetVoxel(voxelIndex + VoxelPlayEnvironment.ONE_Y_ROW, env.GetVegetation(biome.vegetation, rn / biome.vegetationDensity));
                        if (!modifiedChunks.Contains(chunk)) {
                            modifiedChunks.Add(chunk);
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }

}
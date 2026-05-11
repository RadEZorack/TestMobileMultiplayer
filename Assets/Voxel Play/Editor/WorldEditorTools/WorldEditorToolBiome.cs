using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

	public class WorldEditorToolBiome : WorldEditorTool {

		public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolBiome");
		public override string title => "Paint terrain with a biome definition";
		public override string instructions => "Assign a biome to paint top/dirt; optionally add vegetation and trees.";
		public override int priority => 75;
		public override bool supportsContinuousMode => true;
		public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;
        public override int minOpaque => 1;

		public override void DrawInspector () {
			// Brush common UI (size + continuous)
			env.sceneEditorBrushSize = EditorGUILayout.IntSlider("Brush Size", env.sceneEditorBrushSize, 1, 32);
			env.sceneEditorBrushContinuousMode = EditorGUILayout.Toggle(new GUIContent("Continuous Mode", "Hold left button mouse to operate"), env.sceneEditorBrushContinuousMode);
			if (env.sceneEditorBrushContinuousMode) {
				EditorGUI.indentLevel++;
				env.sceneEditorBrushSpeed = EditorGUILayout.Slider("Speed", env.sceneEditorBrushSpeed, 0, 1);
				EditorGUI.indentLevel--;
			}

			EditorGUI.BeginChangeCheck();
			env.sceneEditorBiomeDefinition = (BiomeDefinition)EditorGUILayout.ObjectField("Biome Definition", env.sceneEditorBiomeDefinition, typeof(BiomeDefinition), false);
			if (EditorGUI.EndChangeCheck() && env.sceneEditorBiomeDefinition != null) {
				BiomeDefinition biome = env.sceneEditorBiomeDefinition;
				biome.Init();
			}

			env.sceneEditorBiomeIncludeVegetation = EditorGUILayout.Toggle("Include Vegetation", env.sceneEditorBiomeIncludeVegetation);
			env.sceneEditorBiomeIncludeTrees = EditorGUILayout.Toggle("Include Trees", env.sceneEditorBiomeIncludeTrees);
		}

		public override void SelectVoxels (ref VoxelHitInfo hitInfo, int brushSize, List<VoxelIndex> voxelIndices) {
			List<VoxelIndex> tempVoxels = BufferPool<VoxelIndex>.Get();
			if (tempVoxels == null) return;
			voxelIndices.Clear();

			try {
				Vector3d center = hitInfo.center;
				env.GetVoxelIndices(center, brushSize, tempVoxels, mustHaveContent: true);

				int size = 1 + (brushSize - 1) * 2;
				Vector3d camPos = GetSceneViewCameraPosition();

				int count = tempVoxels.Count;
				for (int k = 0; k < count; k++) {
					VoxelIndex vi = tempVoxels[k];
					Vector3d pos = env.GetVoxelPosition(vi);

					int pz = FastMath.FloorToInt(pos.z - center.z + size / 2.0);
					int px = FastMath.FloorToInt(pos.x - center.x + size / 2.0);
					float mask = ComputeMaskFactor(pz, px, size) - ROUNDNESS;
					if (mask <= 0) continue;

					// Only target terrain surface voxels
					if (!terrainVoxelDefinitions.Contains(vi.chunk.voxels[vi.voxelIndex].typeIndex)) continue;

					// Ensure visibility and no solid immediately above
					Vector3d cpos = pos + hitInfo.normal * 0.48f;
					Vector3d toCam = (camPos - cpos).normalized;
					if (env.IsSolidAtPosition(cpos + toCam)) continue;

					vi.sqrDistance = mask * env.sceneEditorBrushStrength;
					voxelIndices.Add(vi);
				}
			}
			finally {
				BufferPool<VoxelIndex>.Release(tempVoxels);
			}
		}

		protected override bool Execute (ref VoxelHitInfo hitInfo, int brushSize, float brushStrength, List<VoxelIndex> indices) {
			BiomeDefinition biome = env.sceneEditorBiomeDefinition;
			if (biome == null || indices.Count == 0) return false;
			// Ensure biome internal tables are prepared
			biome.Init();

			// get bedrock voxel if available in current terrain generator
			VoxelDefinition bedrockVoxel = null;
			if (env.world != null && env.world.terrainGenerator is TerrainDefaultGenerator tg) {
				bedrockVoxel = tg.bedrockVoxel;
			}

			List<VoxelChunk> modifiedChunks = BufferPool<VoxelChunk>.Get();
			int modifiedCount = 0;
			try {
				int count = indices.Count;
				Vector3d brushCenter = hitInfo.center;
				int maskSize = 1 + (brushSize - 1) * 2;
				int clearRadius = Mathf.Min(8, Mathf.Max(0, env.sceneEditorBrushSize - 1));
				int clearRadiusSqr = clearRadius * clearRadius;
				for (int k = 0; k < count; k++) {
					VoxelIndex vi = indices[k];
					// No randomness for painting; use mask as deterministic gating only
					if (vi.sqrDistance <= 0) continue;

					Vector3d pos = env.GetVoxelPosition(vi);

					// First pass: clear vegetation/trees from the existing biome within the brush area above the column
					UpdateChunkElevation(vi.chunk);
					int z = (vi.voxelIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
					int x = vi.voxelIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
					int elevationIndex = z * VoxelPlayEnvironment.CHUNK_SIZE + x;
					{
						Vector3d abovePos = pos; abovePos.y++;
						Vector3d scanPos = abovePos;
						int guard2 = 0;
						int emptyStreak = 0;
						const int emptyStreakLimit = 12;
						const int maxVerticalScan = 64;
						while (guard2++ < maxVerticalScan) {
							bool clearedThisLevel = false;
							for (int dz = -clearRadius; dz <= clearRadius; dz++) {
								int dz2 = dz * dz;
								for (int dx = -clearRadius; dx <= clearRadius; dx++) {
									if (dx * dx + dz2 > clearRadiusSqr) continue;
									Vector3d p = scanPos; p.x += dx; p.z += dz;
									// ensure we only clear within the brush mask
									int pmz = FastMath.FloorToInt(p.z - brushCenter.z + maskSize * 0.5);
									int pmx = FastMath.FloorToInt(p.x - brushCenter.x + maskSize * 0.5);
									float m = ComputeMaskFactor(pmz, pmx, maskSize) - ROUNDNESS;
									if (m <= 0) continue;
									if (!env.GetVoxelIndex(p, out VoxelChunk aChunk, out int aIndex, createChunkIfNotExists: false)) continue;
									var vclear = aChunk.voxels[aIndex];
									if (vclear.isEmpty) continue;
									VoxelDefinition vdType = vclear.type;
									if (vdType.isVegetation || vdType.isTree) {
										if (aChunk.terrainInfo == null) {
											UpdateChunkElevation(aChunk);
										}
										int zLocal = (aIndex / VoxelPlayEnvironment.CHUNK_SIZE) & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
										int xLocal = aIndex & VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
										int elevIdx2 = zLocal * VoxelPlayEnvironment.CHUNK_SIZE + xLocal;
										BiomeDefinition existingBiome = aChunk.terrainInfo[elevIdx2].biome;
										if (!object.ReferenceEquals(existingBiome, biome)) {
											undoManager.SaveChunk(aChunk);
											aChunk.ClearVoxel(aIndex, VoxelPlayEnvironment.FULL_LIGHT);
											modifiedChunks.Add(aChunk);
											clearedThisLevel = true;
										}
									}
								}
							}
							if (!clearedThisLevel) {
								emptyStreak++;
								if (emptyStreak >= emptyStreakLimit) break;
							} else {
								emptyStreak = 0;
							}
							scanPos.y++;
						}
					}

					// Second pass: paint new biome surface and underground
					VoxelDefinition topVD = biome.GetVoxelTop((Vector3)pos);
					if (topVD == null) continue;
					undoManager.SaveChunk(vi.chunk);
					vi.chunk.terrainInfo[elevationIndex].biome = biome;
					// refresh voxel flags/opacity while leaving any existing microvoxels intact
					vi.chunk.voxels[vi.voxelIndex].Set(topVD);
					modifiedChunks.Add(vi.chunk);

					// Replace dirt voxels underneath through loaded chunks until bedrock/solid non-terrain (bounded)
					Vector3d belowPos = pos;
					for (int guard = 0; guard < 256; guard++) {
						belowPos.y--;
						if (!env.GetVoxelIndex(belowPos, out VoxelChunk belowChunk, out int belowIndex, createChunkIfNotExists: false)) break;
						var v = belowChunk.voxels[belowIndex];
						if (!v.isEmpty && !terrainVoxelDefinitions.Contains(v.typeIndex)) break;
						if (bedrockVoxel != null && v.type == bedrockVoxel) break;
						VoxelDefinition dirtVD = biome.GetVoxelDirt((Vector3)belowPos);
						if (dirtVD == null) break;
						undoManager.SaveChunk(belowChunk);
						belowChunk.SetVoxel(belowIndex, dirtVD);
						modifiedChunks.Add(belowChunk);
					}

					// Place trees/vegetation using same gating/randomization as the terrain generator
					if (pos.y > env.waterLevel) {
						float rn = WorldRand.GetValue(pos);
						bool treePlaced = false;
						if (env.sceneEditorBiomeIncludeTrees && biome.trees != null && biome.trees.Length > 0 && biome.treeDensity > 0 && rn < biome.treeDensity) {
							ModelDefinition treeModel = env.GetTree(biome.trees, rn / Mathf.Max(biome.treeDensity, 0.0001f));
							if (treeModel != null) {
								if (_previewChunks == null) _previewChunks = BufferPool<VoxelChunk>.Get();
								_previewChunks.Clear();
								env.TreePlace(pos, treeModel, _previewChunks, canPlantOnModifiedChunks: true, previewMode: true);
								foreach (var mchunk in _previewChunks) { undoManager.SaveChunk(mchunk); }
								env.TreePlace(pos, treeModel, _previewChunks, canPlantOnModifiedChunks: true, previewMode: false);
								modifiedChunks.AddRange(_previewChunks);
								treePlaced = true;
							}
						}
						if (!treePlaced && env.enableVegetation && env.sceneEditorBiomeIncludeVegetation && biome.vegetation != null && biome.vegetation.Length > 0 && biome.vegetationDensity > 0 && rn < biome.vegetationDensity) {
							Vector3d placeAbove = pos; placeAbove.y++;
							if (env.GetVoxelIndex(placeAbove, out VoxelChunk vchunk, out int vindex, createChunkIfNotExists: true)) {
								if (vchunk.voxels[vindex].isEmpty) {
									undoManager.SaveChunk(vchunk);
									vchunk.SetVoxel(vindex, env.GetVegetation(biome.vegetation, rn / Mathf.Max(biome.vegetationDensity, 0.0001f)));
									modifiedChunks.Add(vchunk);
								}
							}
						}
					}
				}

				modifiedCount = modifiedChunks.Count;
				RefreshModifiedChunks(modifiedChunks);
			}
			finally {
				BufferPool<VoxelChunk>.Release(modifiedChunks);
				if (_previewChunks != null) { BufferPool<VoxelChunk>.Release(_previewChunks); _previewChunks = null; }
			}

			return modifiedCount > 0;
		}

		List<VoxelChunk> _previewChunks;

	}
}



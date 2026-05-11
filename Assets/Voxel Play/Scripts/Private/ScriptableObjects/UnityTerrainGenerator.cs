using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	[CreateAssetMenu(menuName = "Voxel Play/Terrain Generators/Unity Terrain Generator", fileName = "UnityTerrainGenerator", order = 102)]
	[HelpURL("https://kronnect.com/docs/voxel-play/")]
	public class UnityTerrainGenerator : VoxelPlayTerrainGenerator {
		public enum TerrainResourceAction {
			Create,
			Assigned,
			Ignore
		}

		[Serializable]
		public struct TerrainVoxelDefinitionMapping {
			public Texture2D preview;
			public int dirtWith;
			public float blendPower;
			public VoxelDefinition top, dirt;
			public TerrainResourceAction action;
			public float smoothPower;
			[Tooltip("Added to the alphamap weight when selecting the dominant layer. Use to shift selection in blended areas.")]
			public float weightBias;
		}

		[Serializable]
		public struct VegetationVoxelDefinitionMapping {
			public Texture2D preview;
			public string previewName;
			public VoxelDefinition vd;
			public TerrainResourceAction action;
		}

		[Serializable]
		public struct TerrainModelDefinitionMapping {
			public Texture2D preview;
			public ModelDefinition md;
			public TerrainResourceAction action;
			public float smoothPower;
		}

		public TerrainVoxelDefinitionMapping[] splatSettings;
		public VegetationVoxelDefinitionMapping[] detailSettings;
		public TerrainModelDefinitionMapping[] treeSettings;
		public TerrainData terrainData;
		[Range(0, 1)]
		public float vegetationDensity = 1f;
		public VoxelDefinition waterVoxel;
		public VoxelDefinition bedrockVoxel;

		public bool enableHalfStepSurface;

		[Tooltip("When enabled and Add Water is active, chunks beyond the terrain bounds are filled with water up to the water level.")]
		public bool extendWaterBeyondTerrain = true;

		struct DetailLayerInfo {
			public int[,] detailLayer;
		}

		DetailLayerInfo[] detailLayers;

		struct TerrainHeightInfo {
			public float altitude;
			public VoxelDefinition terrainVoxelTop, terrainVoxelDirt, vegetationVoxel;
		}

		TerrainHeightInfo[] heights;
		Dictionary<long, ModelDefinition> treeMap;
		TerrainData lastTerrainDataLoaded;

		[HideInInspector]
		public Vector3 terrainPos = Vector3.zero;

		int terrainDataHeightmapResolution;
		float terrainDataSizeX, terrainDataSizeZ;
		MicroVoxels halfSurfaceVoxelTemplate;

		private void OnDestroy () {
			heights = null;
		}

		public void InvalidateCache () {
			lastTerrainDataLoaded = null;
			heights = null;
			treeMap = null;
		}

		public static Terrain FindMatchingTerrain (TerrainData td) {
			if (td == null) return null;
			foreach (Terrain t in Terrain.activeTerrains) {
				if (t.terrainData == td) return t;
			}
			return Terrain.activeTerrain;
		}

		public override void GetTerrainVoxelDefinitions (List<VoxelDefinition> vds) {
			if (bedrockVoxel != null) vds.Add(bedrockVoxel);
			if (splatSettings == null) return;
			foreach (var sp in splatSettings) {
				if (sp.top != null) vds.Add(sp.top);
				if (sp.dirt != null) vds.Add(sp.dirt);
			}
		}

		protected override void Init () {

			int splatCount = terrainData != null ? terrainData.terrainLayers.Length : 0;
			int detailCount = terrainData != null ? terrainData.detailPrototypes.Length : 0;
			int treeCount = terrainData != null ? terrainData.treePrototypes.Length : 0;

			if (splatSettings == null || splatSettings.Length < splatCount) {
				var old = splatSettings;
				splatSettings = new TerrainVoxelDefinitionMapping[Mathf.Max(splatCount, 1)];
				if (old != null) Array.Copy(old, splatSettings, Mathf.Min(old.Length, splatSettings.Length));
			}
			if (detailSettings == null || detailSettings.Length < detailCount) {
				var old = detailSettings;
				detailSettings = new VegetationVoxelDefinitionMapping[Mathf.Max(detailCount, 1)];
				if (old != null) Array.Copy(old, detailSettings, Mathf.Min(old.Length, detailSettings.Length));
			}
			if (treeSettings == null || treeSettings.Length < treeCount) {
				var old = treeSettings;
				treeSettings = new TerrainModelDefinitionMapping[Mathf.Max(treeCount, 1)];
				if (old != null) Array.Copy(old, treeSettings, Mathf.Min(old.Length, treeSettings.Length));
			}
			if (detailLayers == null || detailLayers.Length < detailCount) {
				detailLayers = new DetailLayerInfo[Mathf.Max(detailCount, 1)];
			}
			if (waterVoxel == null) {
				waterVoxel = Resources.Load<VoxelDefinition>("VoxelPlay/Defaults/Water/VoxelWaterSea");
			}
			env.AddVoxelDefinition(bedrockVoxel);
			for (int d = 0; d < detailSettings.Length; d++) {
				env.AddVoxelDefinition(detailSettings[d].vd);
			}

			halfSurfaceVoxelTemplate = MicroVoxels.halfSurfaceVoxelTemplate;

#if UNITY_EDITOR
			if (world != null && world.terrainGenerator == null) {
				world.terrainGenerator = this;
			}
			if (terrainData == null) {
				Terrain activeTerrain = Terrain.activeTerrain;
				if (activeTerrain != null) {
					terrainData = activeTerrain.terrainData;
					terrainPos = activeTerrain.GetPosition();
					ExamineTerrainData();
				}
			} else {
				Terrain matching = FindMatchingTerrain(terrainData);
				if (matching != null) {
					terrainPos = matching.GetPosition();
				}
			}
#endif

			if (terrainData == null) {
				return;
			}

			if (lastTerrainDataLoaded != null && lastTerrainDataLoaded == terrainData && heights != null && heights.Length > 0) {
				return;
			}

			lastTerrainDataLoaded = terrainData;
			maxHeight = terrainData.size.y;

			int th = terrainData.heightmapResolution;
			int tw = terrainData.heightmapResolution;
			int len = tw * th;
			if (heights == null || heights.Length != len) {
				heights = new TerrainHeightInfo[len];
			} else {
				Array.Clear(heights, 0, heights.Length);
			}

			// Re-link biome counterparts at runtime (these are [NonSerialized])
			for (int k = 0; k < splatSettings.Length; k++) {
				if (splatSettings[k].top != null && splatSettings[k].dirt != null) {
					splatSettings[k].top.biomeDirtCounterpart = splatSettings[k].dirt;
					splatSettings[k].dirt.biomeSurfaceCounterpart = splatSettings[k].top;
				}
			}

			float[,,] heightInfo = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
			int detailLayerCount = Mathf.Min(terrainData.detailPrototypes.Length, detailLayers.Length, detailSettings.Length);
			for (int d = 0; d < detailLayerCount; d++) {
				detailLayers[d].detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, d);
			}

			// Batch height reads
			float[,] heightmap = terrainData.GetHeights(0, 0, tw, th);

			int i = 0;
			int alphaMapsLayerCount = Mathf.Min(heightInfo.GetUpperBound(2), splatSettings.Length - 1);

			// Endpoint-aware coordinate conversion
			int hmMax = th - 1;
			int alphamapMaxY = terrainData.alphamapHeight - 1;
			int alphamapMaxX = terrainData.alphamapWidth - 1;
			int detailW = terrainData.detailWidth;
			int detailH = terrainData.detailHeight;

			for (int y = 0; y < th; y++) {
				int alphamapY = Mathf.Clamp(y * alphamapMaxY / hmMax, 0, alphamapMaxY);
				int detailY0 = y * detailH / th;
				int detailY1 = Mathf.Min((y + 1) * detailH / th, detailH);
				if (detailY1 <= detailY0) detailY1 = detailY0 + 1;
				if (detailY1 > detailH) detailY1 = detailH;
				for (int x = 0; x < tw; x++, i++) {
					int alphamapX = Mathf.Clamp(x * alphamapMaxX / hmMax, 0, alphamapMaxX);
					heights[i].altitude = heightmap[y, x];
					float maxBlend = -1;
					for (int a = 0; a <= alphaMapsLayerCount; a++) {
						float alphamapValue = heightInfo[alphamapY, alphamapX, a] + splatSettings[a].weightBias;
						if (alphamapValue > maxBlend) {
							maxBlend = alphamapValue;
							heights[i].terrainVoxelTop = splatSettings[a].top;
							heights[i].terrainVoxelDirt = splatSettings[a].dirt;
							if (maxBlend >= 1f)
								break;
						}
					}

					// Deterministic weighted vegetation selection with area sampling
					if (detailLayerCount > 0) {
						int detailX0 = x * detailW / tw;
						int detailX1 = Mathf.Min((x + 1) * detailW / tw, detailW);
						if (detailX1 <= detailX0) detailX1 = detailX0 + 1;
						if (detailX1 > detailW) detailX1 = detailW;

						int totalDensity = 0;
						int nonZeroCells = 0;
						int cellCount = (detailY1 - detailY0) * (detailX1 - detailX0);
						for (int dy = detailY0; dy < detailY1; dy++) {
							for (int dx = detailX0; dx < detailX1; dx++) {
								int cellDensity = 0;
								for (int d = 0; d < detailLayerCount; d++) {
									if ((object)detailSettings[d].vd == null) continue;
									cellDensity += detailLayers[d].detailLayer[dy, dx];
								}
								totalDensity += cellDensity;
								if (cellDensity > 0) nonZeroCells++;
							}
						}

						if (totalDensity > 0 && vegetationDensity > 0) {
							float coverageRatio = (float)nonZeroCells / cellCount;
							float adjustedDensity = vegetationDensity * coverageRatio;
							float rn = ((x * 31 + y * 17) & 0xFF) / 255f;
							if (rn < adjustedDensity) {
								int target = (int)(rn / adjustedDensity * totalDensity) % totalDensity;
								int cumulative = 0;
								for (int d = 0; d < detailLayerCount; d++) {
									if ((object)detailSettings[d].vd == null) continue;
									for (int dy = detailY0; dy < detailY1; dy++) {
										for (int dx = detailX0; dx < detailX1; dx++) {
											cumulative += detailLayers[d].detailLayer[dy, dx];
										}
									}
									if (cumulative > target) {
										heights[i].vegetationVoxel = detailSettings[d].vd;
										break;
									}
								}
							}
						}
					}
				}
			}

			terrainDataHeightmapResolution = terrainData.heightmapResolution;
			terrainDataSizeX = terrainData.size.x;
			terrainDataSizeZ = terrainData.size.z;

			TreeInstance[] treeInstances = terrainData.treeInstances;
			int treeInstancesLength = treeInstances.Length;
			if (treeMap == null) {
				treeMap = new Dictionary<long, ModelDefinition>(treeInstancesLength);
			} else {
				treeMap.Clear();
			}
			for (int t = 0; t < treeInstancesLength; t++) {
				TreeInstance ti = treeInstances[t];
				if (ti.prototypeIndex < 0 || ti.prototypeIndex >= treeSettings.Length) continue;
				ModelDefinition md = treeSettings[ti.prototypeIndex].md;
				if (md == null) continue;
				Vector3 treePosition = ti.position;
				int vx = Mathf.FloorToInt(terrainPos.x + treePosition.x * terrainDataSizeX);
				int vz = Mathf.FloorToInt(terrainPos.z + treePosition.z * terrainDataSizeZ);
				treeMap[TreeKey(vx, vz)] = md;
			}
		}

		public void ExamineTerrainData () {
			InvalidateCache();
#if UNITY_EDITOR
			if (terrainData == null)
				return;
			Terrain matching = FindMatchingTerrain(terrainData);
			if (matching != null) {
				terrainPos = matching.GetPosition();
			}
			int layerCount = terrainData.terrainLayers.Length;
			for (int k = 0; k < layerCount && k < splatSettings.Length; k++) {
				splatSettings[k].preview = TextureTools.GetSolidTexture(terrainData.terrainLayers[k].diffuseTexture);
				if (splatSettings[k].dirtWith == 0) {
					splatSettings[k].dirtWith = (k + 1);
					splatSettings[k].blendPower = 0.5f;
				}
				// Clamp dirtWith to valid range after layer removal/reordering
				splatSettings[k].dirtWith = Mathf.Clamp(splatSettings[k].dirtWith, 1, layerCount);
				if (splatSettings[k].preview == null) {
					splatSettings[k].action = TerrainResourceAction.Ignore;
				} else if ((splatSettings[k].top == null || splatSettings[k].dirt == null) && splatSettings[k].action == TerrainResourceAction.Assigned) {
					splatSettings[k].action = TerrainResourceAction.Create;
				}
			}
			for (int k = 0; k < terrainData.treePrototypes.Length && k < treeSettings.Length; k++) {
				treeSettings[k].preview = UnityEditor.AssetPreview.GetAssetPreview(terrainData.treePrototypes[k].prefab);
				if (treeSettings[k].preview == null) {
					treeSettings[k].action = TerrainResourceAction.Ignore;
				} else if (treeSettings[k].md == null && treeSettings[k].action == TerrainResourceAction.Assigned) {
					treeSettings[k].action = TerrainResourceAction.Create;
				}
			}
			for (int k = 0; k < terrainData.detailPrototypes.Length && k < detailSettings.Length; k++) {
				if (terrainData.detailPrototypes[k].prototype != null) {
					Texture2D preview = UnityEditor.AssetPreview.GetAssetPreview(terrainData.detailPrototypes[k].prototype);
					detailSettings[k].previewName = terrainData.detailPrototypes[k].prototype.name;
					if (preview != null) {
						detailSettings[k].preview = preview;
					}
				} else if (terrainData.detailPrototypes[k].prototypeTexture != null) {
					detailSettings[k].preview = terrainData.detailPrototypes[k].prototypeTexture;
					detailSettings[k].previewName = terrainData.detailPrototypes[k].prototypeTexture.name;
				}
				if (detailSettings[k].vd != null) {
					detailSettings[k].action = TerrainResourceAction.Assigned;
				} else if (detailSettings[k].vd == null && detailSettings[k].action == TerrainResourceAction.Assigned) {
					detailSettings[k].action = TerrainResourceAction.Create;
				} else if (detailSettings[k].preview == null) {
					detailSettings[k].action = TerrainResourceAction.Ignore;
				}
			}
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}

		public override bool GetTerrainBounds (out Vector3 center, out Vector3 size) {
			if (terrainData != null) {
				Vector3 terrainSize = terrainData.size;
				center = terrainPos + terrainSize * 0.5f;
				size = terrainSize;
				return true;
			}
			center = Vector3.zero;
			size = Vector3.zero;
			return false;
		}

		int GetHeightIndex (double x, double z) {
			int w = terrainDataHeightmapResolution;
			int h = terrainDataHeightmapResolution;
			float fx = (w - 1) / terrainDataSizeX;
			float fz = (h - 1) / terrainDataSizeZ;
			int tx = (int)((x - terrainPos.x) * fx + 0.5f);
			if (tx < 0 || tx >= w) return -1;
			int ty = (int)((z - terrainPos.z) * fz + 0.5f);
			if (ty < 0 || ty >= h) return -1;
			return ty * w + tx;
		}

		static long TreeKey (int x, int z) {
			return ((long)x << 32) | (long)(uint)z;
		}

		/// <summary>
		/// Gets the altitude and moisture (in 0-1 range).
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		/// <param name="altitude">Altitude.</param>
		/// <param name="moisture">Moisture.</param>
		public override void GetHeightAndMoisture (double x, double z, out float altitude, out float moisture) {
			if (heights == null) {
				altitude = 0;
				moisture = 0;
				return;
			}
			int heightIndex = GetHeightIndex(x, z);
			if (heightIndex >= 0) {
				altitude = heights[heightIndex].altitude;
			} else if (addWater && extendWaterBeyondTerrain && maxHeight > 0) {
				altitude = (waterLevel - 1) / maxHeight;
			} else {
				altitude = 0;
			}
			moisture = 0;
		}

		/// <summary>
		/// Paints the terrain inside the chunk defined by its central "position"
		/// </summary>
		/// <returns><c>true</c>, if terrain was painted, <c>false</c> otherwise.</returns>
		public override bool PaintChunk (VoxelChunk chunk) {
			if (heights == null) return false;

			Vector3d position = chunk.position;

			if (position.y + VoxelPlayEnvironment.CHUNK_HALF_SIZE < minHeight) {
				chunk.isAboveSurface = false;
				return false;
			}

			int bedrockRow = -1;
			bool usesBedrockVoxel = bedrockVoxel != null;
			if (position.y < minHeight + VoxelPlayEnvironment.CHUNK_HALF_SIZE) {
				bedrockRow = (int)(minHeight - (position.y - VoxelPlayEnvironment.CHUNK_HALF_SIZE) + 1) * ONE_Y_ROW - 1;
			}

			position.x -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
			position.y -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
			position.z -= VoxelPlayEnvironment.CHUNK_HALF_SIZE;
			Vector3d pos;

			int waterLevel = env.waterLevel;
			Voxel[] voxels = chunk.voxels;

			bool hasContent = false;
			bool isAboveSurface = false;

			env.GetHeightMapInfoFast(position.x, position.z, out HeightMapInfo[] heightMapInfos);

			for (int z = 0; z < VoxelPlayEnvironment.CHUNK_SIZE; z++) {
				pos.z = position.z + z;
				int arrayZIndex = z * ONE_Z_ROW;
				for (int x = 0; x < VoxelPlayEnvironment.CHUNK_SIZE; x++) {
					HeightMapInfo heightMapInfo = heightMapInfos[arrayZIndex + x];

					float groundLevel = heightMapInfo.groundLevel;
					float surfaceLevel = waterLevel > groundLevel ? waterLevel : groundLevel;
					if (surfaceLevel < position.y) {
						// position is above terrain or water
						isAboveSurface = true;
						continue;
					}

					pos.x = position.x + x;

					int hindex = GetHeightIndex(pos.x, pos.z);
					if (hindex < 0) {
						// Beyond terrain bounds - fill with water if enabled
						if (addWater && extendWaterBeyondTerrain && waterLevel >= position.y) {
							int wy = (int)(waterLevel - position.y);
							if (wy >= VoxelPlayEnvironment.CHUNK_SIZE) {
								wy = VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
							}
							int wi = wy * ONE_Y_ROW + arrayZIndex + x;
							while (wi >= 0) {
								voxels[wi].Set(waterVoxel);
								wi -= ONE_Y_ROW;
							}
							if (wy == (int)(waterLevel - position.y)) {
								isAboveSurface = true;
							}
							hasContent = true;
						}
						continue;
					}
					VoxelDefinition vd = heights[hindex].terrainVoxelTop;
					if ((object)vd == null)
						continue;

					int y = (int)(surfaceLevel - position.y);
					if (y >= VoxelPlayEnvironment.CHUNK_SIZE) {
						y = VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE;
					}
					pos.y = position.y + y;

					// Place voxels
					int voxelIndex = y * ONE_Y_ROW + arrayZIndex + x;
					if (pos.y > groundLevel) {
						// water above terrain
						if (pos.y == surfaceLevel) {
							isAboveSurface = true;
						}
						while (pos.y > groundLevel && voxelIndex >= 0) {
							voxels[voxelIndex].Set(waterVoxel);
							voxelIndex -= ONE_Y_ROW;
							pos.y--;
						}
					} else if (pos.y == groundLevel) {
						isAboveSurface = true;
						if (voxels[voxelIndex].typeIndex == 0) {
							chunk.SetVoxel(voxelIndex, vd);

							bool allowHalfStep = enableHalfStepSurface;
							if (pos.y > waterLevel) {
								ModelDefinition treeModel;
								if (env.enableTrees && treeMap != null && treeMap.TryGetValue(TreeKey((int)pos.x, (int)pos.z), out treeModel)) {
									env.RequestTreeCreation(chunk, pos, treeModel);
									allowHalfStep = false;
								} else if (env.enableVegetation) {
									VoxelDefinition vegetation = heights[hindex].vegetationVoxel;
									if (vegetation != null) {
										if (voxelIndex >= VoxelPlayEnvironment.CHUNK_SIZE_MINUS_ONE * ONE_Y_ROW) {
											Vector3d abovePos = pos;
											abovePos.y++;
											env.RequestVegetationCreation(abovePos, vegetation);
										} else {
											chunk.SetVoxel(voxelIndex + ONE_Y_ROW, vegetation);
										}
										env.vegetationCreated++;
									}
								}
							}

							if (allowHalfStep) {
								float frac = heightMapInfo.height - groundLevel;
								if (frac < 0.5f) {
									chunk.SetMicroVoxels(voxelIndex, halfSurfaceVoxelTemplate);
								}
							}
						}
						voxelIndex -= ONE_Y_ROW;
					}

					// Continue filling down
					vd = heights[hindex].terrainVoxelDirt;
					while (voxelIndex > bedrockRow) {
						if (voxels[voxelIndex].typeIndex == 0) {
							voxels[voxelIndex].SetFastOpaque(vd);
						}
						voxelIndex -= ONE_Y_ROW;
					}
					if (bedrockRow >= 0 && usesBedrockVoxel) {
						voxels[voxelIndex].SetFastOpaque(bedrockVoxel);
					}
					hasContent = true;
				}
			}

			chunk.isAboveSurface = isAboveSurface;
			return hasContent;
		}



	}

}

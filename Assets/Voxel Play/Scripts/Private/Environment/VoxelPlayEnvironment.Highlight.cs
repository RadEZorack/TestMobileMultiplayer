using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	public partial class VoxelPlayEnvironment : MonoBehaviour {

		const string VOXEL_HIGHLIGHT_NAME = "VoxelHighlight";

		[NonSerialized]
		public VoxelHitInfo lastHitInfo, lastHighlightInfo;

		[NonSerialized]
		public Material voxelHighlightMaterial;

		readonly List<VoxelHighlight> highlightedVoxels = new List<VoxelHighlight>();

		readonly Stack<VoxelHighlight> highlightPool = new Stack<VoxelHighlight>();

		/// <summary>
		/// Forces a refresh of the highlight box of a voxel
		/// </summary>
		public void RefreshVoxelHighlight () {
			lastHighlightInfo.voxelCenter.y = float.MinValue;
		}


		/// <summary>
		/// Adds a highlight effect to a group of voxels.
		/// </summary>
		/// <returns><c>true</c>, if highlight was executed, <c>false</c> otherwise.</returns>
		/// <param name="fadeAmplitude">Range of the pulse effect</param>
		public bool VoxelHighlight (Vector3d position, Color color, float edgeWidth = 0f, float fadeAmplitude = 1f) {

			internal_ReleaseHighlightedVoxels();

			if (BuildVoxelHitInfo(out VoxelHitInfo hitInfo, position, position)) {
				internal_VoxelHighlight(ref hitInfo, color, edgeWidth, microVoxelSize: 0, fadeAmplitude);
				return true;
			}

			return false;
		}


		/// <summary>
		/// Adds a highlight effect to a group of voxels.
		/// </summary>
		/// <returns><c>true</c>, if highlight was executed, <c>false</c> otherwise.</returns>
		/// <param name="hitInfo">A voxelHitInfo struct with information about the location of the highlighted voxel.</param>
		/// <param name="fadeAmplitude">Range of the pulse effect</param>
		public bool VoxelHighlight (List<VoxelIndex> voxelIndices, Color color, float edgeWidth = 0f, float fadeAmplitude = 1f) {
			int viCount = voxelIndices.Count;

			internal_ReleaseHighlightedVoxels();

			for (int k = 0; k < viCount; k++) {
				Vector3d pos = GetVoxelPosition(voxelIndices[k]);
				if (BuildVoxelHitInfo(out VoxelHitInfo hitInfo, pos, pos)) {
					internal_VoxelHighlight(ref hitInfo, color, edgeWidth, microVoxelSize: 0, fadeAmplitude);
				}
			}

			return highlightedVoxels.Count > 0;
		}

		/// <summary>
		/// Removes the highilght from the voxels
		/// </summary>
		public void ClearHighlight () {
			internal_ReleaseHighlightedVoxels();
		}

		/// <summary>
		/// Adds a highlight effect to a voxel at a given position. If there's no voxel at that position this method returns false.
		/// </summary>
		/// <returns><c>true</c>, if highlight was executed, <c>false</c> otherwise.</returns>
		/// <param name="hitInfo">A voxelHitInfo struct with information about the location of the highlighted voxel.</param>
		/// <param name="microVoxel">Highlights a microvoxel. Value equals to the size of teh selection</param>
		/// <param name="fadeAmplitude">Range of the pulse effect</param>
		/// <param name="customBounds">Optional custom bounds to use for the highlight (for example, when highlighting a slab instead of the whole voxel)</param>
		public bool VoxelHighlight (ref VoxelHitInfo hitInfo, Color color, float edgeWidth = 0f, int microVoxelSize = 0, float fadeAmplitude = 1f, bool slabMode = false) {
			if (hitInfo.voxelCenter == lastHighlightInfo.voxelCenter && hitInfo.placeholder == lastHighlightInfo.placeholder && (!slabMode || hitInfo.slab == lastHighlightInfo.slab)) return true;
			lastHighlightInfo = hitInfo;
			internal_ReleaseHighlightedVoxels();
			return internal_VoxelHighlight(ref hitInfo, color, edgeWidth, microVoxelSize, fadeAmplitude, slabMode);
		}

		void internal_ReleaseHighlightedVoxels () {
			foreach (var v in highlightedVoxels) {
				if (v != null) {
					v.SetActive(false);
					highlightPool.Push(v);
				}
			}
			highlightedVoxels.Clear();
		}

		void VoxelHighlightDispose () {
			foreach (var h in highlightPool) {
				if (h != null) DestroyImmediate(h.gameObject);
			}
			highlightPool.Clear();
			foreach (var h in highlightedVoxels) {
				if (h != null) DestroyImmediate(h.gameObject);
			}
			highlightedVoxels.Clear();
			// find any residual object
			VoxelHighlight[] hh = Misc.FindObjectsOfType<VoxelHighlight>(true);
			foreach (var h in hh) {
				DestroyImmediate(h.gameObject);
			}
			Misc.DestroyImmediateAndNullify(ref voxelHighlightMaterial);
		}

		public Boundsd GetHighlightBounds (ref VoxelHitInfo hitInfo, int microVoxelSize) {
			if (microVoxelSize > 0) {
				return GetMicroVoxelBounds(ref hitInfo, microVoxelSize, 0.005f);
			}
			// highlight default voxel bounds; if voxel use microvoxel, use the prototype bounds
			if (hitInfo.chunk != null && hitInfo.chunk.usesMicroVoxels && hitInfo.chunk.microVoxels.TryGetValue(hitInfo.voxelIndex, out MicroVoxels mv) && mv.prototype != null && !mv.isFull && !mv.isEmpty) {
				double x = hitInfo.center.x + mv.prototype.bounds.center.x;
				double y = hitInfo.center.y + mv.prototype.bounds.center.y;
				double z = hitInfo.center.z + mv.prototype.bounds.center.z;
				return new Boundsd(new Vector3d(x, y, z), mv.prototype.bounds.size);
			}
			return new Boundsd(hitInfo.center, Misc.vector3one);
		}

		/// <summary>
		/// Returns the resolved voxel definition at a position, using ConnectedVoxel rules.
		/// Prefers render-time rules (meshing-time) and falls back to place-time rules if needed.
		/// </summary>
		public VoxelDefinition GetResolvedVoxelDefinition (Vector3d position, VoxelDefinition vd, int rotation = 0) {
			if (vd == null) return null;

			// 1) Always resolve place-time rules first
			VoxelDefinition resolved = vd;
			if (vd.customVoxelDefinitionProvider != null) {
				vd = resolved.customVoxelDefinitionProvider(position, resolved, rotation);
				if (vd == null) return null;
			}

			// 2) Optionally resolve render-time rules using the result of (1)
			if (vd.customVoxelDefinitionForRendering != null) {
				VoxelIndex[] neighbours = new VoxelIndex[27];
				GetVoxelNeighbourhood(position, ref neighbours, rotation);

				int forwardLeftTypeIndex = neighbours[15].typeIndex;
				int forwardTypeIndex = neighbours[16].typeIndex;
				int forwardRightTypeIndex = neighbours[17].typeIndex;
				int leftTypeIndex = neighbours[12].typeIndex;
				int rightTypeIndex = neighbours[14].typeIndex;
				int backLeftTypeIndex = neighbours[9].typeIndex;
				int backTypeIndex = neighbours[10].typeIndex;
				int backRightTypeIndex = neighbours[11].typeIndex;
				int topCenterTypeIndex = neighbours[22].typeIndex;
				int bottomCenterTypeIndex = neighbours[4].typeIndex;
				int topBackLeftTypeIndex = neighbours[18].typeIndex;
				int topBackTypeIndex = neighbours[19].typeIndex;
				int topBackRightTypeIndex = neighbours[20].typeIndex;
				int topLeftTypeIndex = neighbours[21].typeIndex;
				int topRightTypeIndex = neighbours[23].typeIndex;
				int topForwardLeftTypeIndex = neighbours[24].typeIndex;
				int topForwardTypeIndex = neighbours[25].typeIndex;
				int topForwardRightTypeIndex = neighbours[26].typeIndex;
				int bottomBackLeftTypeIndex = neighbours[0].typeIndex;
				int bottomBackTypeIndex = neighbours[1].typeIndex;
				int bottomBackRightTypeIndex = neighbours[2].typeIndex;
				int bottomLeftTypeIndex = neighbours[3].typeIndex;
				int bottomRightTypeIndex = neighbours[5].typeIndex;
				int bottomForwardLeftTypeIndex = neighbours[6].typeIndex;
				int bottomForwardTypeIndex = neighbours[7].typeIndex;
				int bottomForwardRightTypeIndex = neighbours[8].typeIndex;

				vd = resolved.customVoxelDefinitionForRendering(
					position, resolved,
					topCenterTypeIndex, bottomCenterTypeIndex,
					backLeftTypeIndex, backTypeIndex, backRightTypeIndex,
					leftTypeIndex, rightTypeIndex,
					forwardLeftTypeIndex, forwardTypeIndex, forwardRightTypeIndex,
					topBackLeftTypeIndex, topBackTypeIndex, topBackRightTypeIndex,
					topLeftTypeIndex, topRightTypeIndex,
					topForwardLeftTypeIndex, topForwardTypeIndex, topForwardRightTypeIndex,
					bottomBackLeftTypeIndex, bottomBackTypeIndex, bottomBackRightTypeIndex,
					bottomLeftTypeIndex, bottomRightTypeIndex,
					bottomForwardLeftTypeIndex, bottomForwardTypeIndex, bottomForwardRightTypeIndex
				);
			}

			return vd;
		}

		bool internal_VoxelHighlight (ref VoxelHitInfo hitInfo, Color color, float edgeWidth = 0f, int microVoxelSize = 0, float fadeAmplitude = 1f, bool slabMode = false) {

			VoxelHighlight voxelHighlight = GetHighlightFromPool();
			if (voxelHighlight == null) return false;

			if (slabMode) microVoxelSize = 0;
			
			GameObject voxelHighlightGO = voxelHighlight.gameObject;

			voxelHighlightMaterial.color = color;
			if (edgeWidth > 0f) {
				voxelHighlightMaterial.SetFloat(ShaderParams.Width, 1f / edgeWidth);
			}
			voxelHighlightMaterial.SetFloat(ShaderParams.FadeAmplitude, fadeAmplitude);

			Transform ht = voxelHighlightGO.transform;
			VoxelDefinition vd = voxelDefinitions[hitInfo.voxel.typeIndex];

			if (hitInfo.placeholder != null) {
				voxelHighlight.SetTarget(hitInfo.placeholder.transform);
				ht.SetParent(hitInfo.placeholder.transform, false);
				if (hitInfo.placeholder.modelMeshRenderers != null && hitInfo.placeholder.modelMeshRenderers.Length > 0 && hitInfo.placeholder.modelMeshRenderers[0] != null) {
					Bounds bounds = hitInfo.placeholder.modelMeshRenderers[0].bounds;
					ht.position = bounds.center;
					ht.localScale = bounds.size;
				} else {
					Bounds bounds = hitInfo.placeholder.bounds;
					ht.localPosition = bounds.center;
					ht.localScale = bounds.size;
				}
				ht.rotation = Misc.quaternionZero;
			} else if (hitInfo.item != null) {
				Transform itemTransform = hitInfo.item.transform;
				voxelHighlight.SetTarget(itemTransform);
				if (itemTransform.TryGetComponent(out BoxCollider bc)) {
					ht.SetParent(itemTransform, false);
					ht.localScale = bc.size;
					ht.localPosition = bc.center;
				}
			} else {
				voxelHighlight.SetTarget(null);
				ht.SetParent(null);
				Bounds htBounds = default;
				if (slabMode && !hitInfo.hasMicroVoxels) {
					int s = hitInfo.slab; // -1 bottom, 1 top
					if (s < 0) {
						htBounds = new Bounds(new Vector3d(hitInfo.voxelCenter.x, hitInfo.voxelCenter.y - 0.25f, hitInfo.voxelCenter.z), new Vector3(1f, 0.5f, 1f));
					} else if (s > 0) {
						htBounds = new Bounds(new Vector3d(hitInfo.voxelCenter.x, hitInfo.voxelCenter.y + 0.25f, hitInfo.voxelCenter.z), new Vector3(1f, 0.5f, 1f));
					}
				}
				if (htBounds.size == Vector3.zero) {
					htBounds = GetHighlightBounds(ref hitInfo, vd != null && vd.supportsMicroVoxels ? microVoxelSize : 0);
				}
				ht.position = htBounds.center;
				ht.localScale = htBounds.size;
				ht.localRotation = Misc.quaternionZero;

				// Adapt box highlight to voxel contents
				if ((object)hitInfo.chunk != null && hitInfo.voxel.typeIndex > 0) {
					// water?
					int waterLevel = hitInfo.voxel.GetWaterLevel();
					if (waterLevel > 0) {
						float ly = waterLevel / 15f;
						ht.localScale = new Vector3(1, ly, 1);
						Vector3d pos = new Vector3d(hitInfo.voxelCenter.x, hitInfo.voxelCenter.y - 0.5 + ly * 0.5, hitInfo.voxelCenter.z);
						ht.position = pos;
					} else {
						if (vd.gpuInstancing && vd.renderType == RenderType.Custom) {
							// instanced mesh ?
							Bounds bounds = vd.mesh.bounds;
							Quaternion rotation = vd.GetRotation(hitInfo.voxelCenter);
							// User rotation
							float rot = hitInfo.chunk.voxels[hitInfo.voxelIndex].GetTextureRotationDegrees();
							if (rot != 0) {
								rotation *= Quaternion.Euler(0, rot, 0);
							}
							// Custom position
							Vector3d localPos = hitInfo.voxelCenter + rotation * (bounds.center + vd.GetOffset(hitInfo.voxelCenter));
							ht.position = localPos;
							Vector3 size = bounds.size;
							FastVector.Multiply(ref size, ref vd.scale);
							voxelHighlightGO.transform.localScale = size;
							voxelHighlightGO.transform.localRotation = rotation;
						} else if (vd.isVegetation) {
							// grass?
							Vector3d pos = hitInfo.voxelCenter - hitInfo.chunk.position;
							Vector3d aux = pos;
							float random = WorldRand.GetValue(pos.x, pos.z);
							pos.x += random * 0.5 - 0.25;
							aux.x += 1;
							random = WorldRand.GetValue(aux);
							pos.z += random * 0.5 - 0.25;
							float offsetY = random * 0.1f;
							pos.y -= offsetY * 0.5 + 0.5 - vd.scale.y * 0.5;
							ht.position = (hitInfo.chunk.position + pos);
							Vector3 adjustedScale = vd.scale;
							adjustedScale.y -= offsetY;
							voxelHighlightGO.transform.localScale = adjustedScale;
						}
					}
				}
			}
			if (vd != null) {
				ht.localPosition += vd.highlightOffset;
			}

			highlightedVoxels.Add(voxelHighlight);
			voxelHighlight.SetActive(true);
			return true;
		}


		VoxelHighlight GetHighlightFromPool () {
			VoxelHighlight vh;
			while (highlightPool.TryPop(out vh) && vh == null) { }

			if (vh != null) return vh;

			GameObject voxelHighlightGO = Instantiate(Resources.Load<GameObject>("VoxelPlay/Prefabs/VoxelHighlightEdges"));
			voxelHighlightGO.hideFlags = HideFlags.HideInHierarchy;
			voxelHighlightGO.name = VOXEL_HIGHLIGHT_NAME;
			Renderer renderer = voxelHighlightGO.GetComponent<Renderer>();
			if (voxelHighlightMaterial == null) {
				voxelHighlightMaterial = Instantiate(renderer.sharedMaterial); // instantiates material to avoid changing resource
			}
			renderer.sharedMaterial = voxelHighlightMaterial;
			if (!voxelHighlightGO.TryGetComponent(out vh)) {
				vh = voxelHighlightGO.AddComponent<VoxelHighlight>();
			}
			return vh;
		}


		/// <summary>
		/// Creates a preview GameObject that depicts the same appearance as the given voxel definition
		/// </summary>
		/// <param name="position">Position where the preview should be created</param>
		/// <param name="vd">VoxelDefinition to preview</param>
		/// <param name="alpha">Alpha value for transparency (1.0 = opaque, 0.0 = fully transparent)</param>
		/// <returns>GameObject representing the voxel preview, or null for unsupported voxel types (vegetation)</returns>
		public GameObject VoxelPreview (Vector3d position, VoxelDefinition vd, Color tintColor = default) {
			if (vd == null) return null;

			// Resolve connected voxel rules at this position first
			vd = GetResolvedVoxelDefinition(position, vd, rotation: 0);
			if (vd == null) return null;

			// Vegetation (CutoutCross): build crossed-quad preview
			if (vd.isVegetation) {
				return CreateVegetationPreview(position, vd, tintColor);
			}

			// Handle custom voxels by instantiating their prefab
			if (vd.renderType == RenderType.Custom) {
				if (vd.prefab == null) return null;

				GameObject previewObj = Instantiate(vd.prefab);
				previewObj.name = "VoxelPreview " + vd.name;
				previewObj.transform.position = position;
				previewObj.transform.localScale = vd.scale;

				// Apply rotation if specified
				if (vd.rotation != Vector3.zero) {
					previewObj.transform.rotation = Quaternion.Euler(vd.rotation);
				}

				// Apply offset if specified
				if (vd.offset != Vector3.zero) {
					previewObj.transform.position += vd.offset;
				}

				if (tintColor != default) {
					Material mat = previewObj.GetComponentInChildren<Renderer>()?.material;
					if (mat != null) {
						mat.color = tintColor * vd.tintColor;
					}
				}

				return previewObj;
			}

			return CreateCubePreview(position, vd, tintColor);
		}

		/// <summary>
		/// Creates a cube preview for regular voxel types
		/// </summary>
		GameObject CreateCubePreview (Vector3d position, VoxelDefinition vd, Color tintColor = default) {

			CubeSideSettings[] sides = new CubeSideSettings[6];
			for (int i = 0; i < 6; i++) {
				sides[i] = new CubeSideSettings {
					color = Misc.colorWhite
				};
			}

			sides[0].texture = vd.textureTop != null ? vd.textureTop : vd.textureSide;
			sides[1].texture = vd.textureBottom != null ? vd.textureBottom : vd.textureSide;
			sides[2].texture = vd.textureForward != null ? vd.textureForward : vd.textureSide;
			sides[3].texture = vd.textureSide;
			sides[4].texture = vd.textureLeft != null ? vd.textureLeft : vd.textureSide;
			sides[5].texture = vd.textureRight != null ? vd.textureRight : vd.textureSide;

			CubeShadingStyle shadingStyle = CubeShadingStyle.ColorAlpha;
			for (int i = 0; i < 6; i++) {
				if (sides[i].texture != null) {
					shadingStyle = CubeShadingStyle.TexturedAlpha;
					break;
				}
			}

			Material material = CubeTools.GetMaterialForShadingStyle(shadingStyle);
			if (tintColor != default) {
				material.color = tintColor * vd.tintColor;
			} else {
				material.color = vd.tintColor;
			}
			if (shadingStyle != CubeShadingStyle.ColorAlpha) {
				Texture2D packedTexture = CubeTools.PackTextures(sides, 2048);
				packedTexture.filterMode = filterMode;
				if (packedTexture != null) {
					material = Instantiate(material);
					material.mainTexture = packedTexture;
				}
			}

			// Generate the cube mesh
			Mesh cubeMesh = CubeTools.GenerateCubeMesh(sides, Misc.vector3one, Misc.vector3zero, shadingStyle);
			if (cubeMesh == null) return null;

			GameObject cubeObj = new GameObject("VoxelPreview " + vd.name, typeof(MeshFilter), typeof(MeshRenderer));
			cubeObj.transform.position = position;

			MeshFilter meshFilter = cubeObj.GetComponent<MeshFilter>();
			meshFilter.mesh = cubeMesh;

			MeshRenderer meshRenderer = cubeObj.GetComponent<MeshRenderer>();
			meshRenderer.material = material;

			return cubeObj;
		}

		/// <summary>
		/// Creates a crossed-quad mesh preview for vegetation voxels (RenderType.CutoutCross)
		/// </summary>
		GameObject CreateVegetationPreview (Vector3d position, VoxelDefinition vd, Color tintColor = default) {
			// Determine vegetation height (use the average between min/max)
			float height = (vd.vegetationMinHeight + vd.vegetationMaxHeight) * 0.5f;
			if (height <= 0f) height = 1f;

			// Build vertices for two crossed quads using the same base geometry as the mesher
			List<Vector3> vertices = new List<Vector3>(8);
			List<int> indices = new List<int>(12);
			List<Vector2> uv = new List<Vector2>(8);
			List<Vector3> normals = new List<Vector3>(8);

			void AddCrossQuad (Vector3[] faceVertices) {
				int baseIndex = vertices.Count;
				for (int v = 0; v < 4; v++) {
					Vector3 vert = faceVertices[v];
					if (vert.y > 0f) vert.y *= height; // emulate mesher behavior (only top vertices are scaled by height)
					vertices.Add(vert);
					normals.Add(Misc.vector3zero); // vegetation uses zero normals in mesher
												   // UVs 0..1
					uv.Add(v switch { 0 => new Vector2(0, 0), 1 => new Vector2(0, 1), 2 => new Vector2(1, 0), _ => new Vector2(1, 1) });
				}
				indices.Add(baseIndex + 0);
				indices.Add(baseIndex + 1);
				indices.Add(baseIndex + 2);
				indices.Add(baseIndex + 3);
				indices.Add(baseIndex + 2);
				indices.Add(baseIndex + 1);
			}

			AddCrossQuad(MeshingThread.faceVerticesCross1);
			AddCrossQuad(MeshingThread.faceVerticesCross2);

			Mesh mesh = new Mesh();
			mesh.name = "VegetationPreviewMesh";
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uv);
			mesh.SetTriangles(indices, 0);
			mesh.RecalculateBounds();

			GameObject go = new GameObject("VoxelPreview " + vd.name, typeof(MeshFilter), typeof(MeshRenderer));
			go.transform.position = position;
			go.GetComponent<MeshFilter>().sharedMesh = mesh;

			// Use a simple cutout double-sided material and assign the side texture
			Material mat = Resources.Load<Material>("VoxelPlay/Materials/VP Model Texture Alpha Double Sided");
			if (mat != null) {
				mat = Instantiate(mat);
				if (vd.textureSide != null) {
					mat.mainTexture = vd.textureSide;
				}
				mat.color = (tintColor != default) ? tintColor * vd.tintColor : vd.tintColor;
				go.GetComponent<MeshRenderer>().sharedMaterial = mat;
			}

			return go;
		}

	}
}

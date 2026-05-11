using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	public partial class VoxelPlayEnvironment : MonoBehaviour {

		public enum PlacementAnimationMode { None, Scale, Throw, ElasticPop }

		public PlacementAnimationMode placementAnimation = PlacementAnimationMode.Scale;
		[Range(0.05f, 5f)] public float placementAnimDuration = 0.125f;
		[Range(0f, 0.2f)] public float placementAnimThrowArcMagnitude = 0.01f;
		public bool placementAnimSpin;
		[Range(0f, 2f)] public float placementAnimSpinTurns = 1f;
		public bool placementAnimFade;
		public bool placementAnimUseGhostCollider;
		public bool placementAnimElasticPop;
		[Range(0f, 0.4f)] public float placementAnimElasticPopAmount = 0.08f;

		interface IPlacementAnimator {
			void Configure (VoxelPlayEnvironment env, float duration);
			bool enabled { get; }
			bool Schedule (in PlacementRequest req);
			void Tick (float dt);
		}

		abstract class BasePlacementAnimator : IPlacementAnimator {
			protected struct Ghost {
				public Transform transform;
				public MeshFilter meshFilter;
				public MeshRenderer meshRenderer;
				public MaterialPropertyBlock mpb;
				public Renderer[] previewRenderers;
				public GameObject previewGO;
			}

			protected struct PreviewEntry {
				public GameObject go;
				public Renderer[] renderers;
			}

			protected readonly List<Ghost> _ghosts = new List<Ghost>(32);
			protected readonly Stack<int> _pool = new Stack<int>(32);
			protected readonly Dictionary<VoxelDefinition, Stack<PreviewEntry>> _previewPool = new Dictionary<VoxelDefinition, Stack<PreviewEntry>>();
			protected VoxelPlayEnvironment _env;
			protected float _duration = 0.25f;
			protected Transform _root;
			protected Material _ghostMat;
			protected readonly Stack<VoxelPlaceholder> _placeholderPool = new Stack<VoxelPlaceholder>(32);
			protected bool _enableFade;

			public virtual bool enabled => true;
			protected abstract string RootName { get; }
			protected abstract string GhostName { get; }

			public virtual void Configure (VoxelPlayEnvironment env, float duration) {
				_env = env;
				_duration = Mathf.Max(0.01f, duration);
				if (_root == null) {
					GameObject go = new GameObject(RootName);
					go.hideFlags = HideFlags.HideAndDontSave;
					_root = go.transform;
					if (env.worldRoot != null) _root.SetParent(env.worldRoot, false);
				}
				if (_ghostMat == null) {
					Shader sh = Shader.Find("Unlit/Color");
					_ghostMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
				}
				OnConfigure(env);
			}

			protected virtual void OnConfigure (VoxelPlayEnvironment env) { }

			protected bool AllowFade (in PlacementRequest req) {
				VoxelDefinition vd = req.voxelType;
				if (vd == null) return false;
				if (vd.isVegetation) return false;
				if (vd.renderType == RenderType.Custom) return false;
				return true;
			}

			protected Vector3 GetTargetScale (in PlacementRequest req) {
				if (req.isSingleMicroVoxel || req.microVoxelSize > 0) {
					float s = MicroVoxels.SIZE * (req.microVoxelSize > 0 ? req.microVoxelSize : 1);
					return req.slabMode ? new Vector3(s, s * 0.5f, s) : new Vector3(s, s, s);
				}
				return req.slabMode ? new Vector3(1f, 0.5f, 1f) : Vector3.one;
			}

			protected int CreateGhost () {
				GameObject go = new GameObject(GhostName, typeof(MeshFilter), typeof(MeshRenderer));
				go.hideFlags = HideFlags.HideAndDontSave;
				go.transform.SetParent(_root, false);
				var mf = go.GetComponent<MeshFilter>();
				var mr = go.GetComponent<MeshRenderer>();
				mr.sharedMaterial = _ghostMat;
				var mpb = new MaterialPropertyBlock();
				go.SetActive(false);
				int idx = _ghosts.Count;
				_ghosts.Add(new Ghost { transform = go.transform, meshFilter = mf, meshRenderer = mr, mpb = mpb, previewRenderers = null, previewGO = null });
				return idx;
			}

			protected void ClearGhostChildren (ref Ghost g) {
				if (g.transform.childCount > 0) {
					for (int i = g.transform.childCount - 1; i >= 0; i--) {
						Transform child = g.transform.GetChild(i);
						child.SetParent(_root, false);
						child.gameObject.SetActive(false);
					}
				}
				g.previewRenderers = null;
				g.previewGO = null;
			}

			protected Quaternion GetCombinedRotation (in PlacementRequest req) {
				Quaternion rot = req.voxelType.GetRotation(req.position);
				float userRotDeg = Voxel.GetTextureRotationDegrees(req.rotation);
				if (userRotDeg != 0f) rot *= Quaternion.Euler(0f, userRotDeg, 0f);
				return rot;
			}

			protected virtual bool AllowMicroVoxelMesh (in PlacementRequest req) {
				return req.placeMicroVoxels;
			}

			protected void SetupVisual (in PlacementRequest req, ref Ghost g, Quaternion combinedRot) {
				if (req.voxelType.renderType == RenderType.Custom) {
					PreviewEntry entry = PopOrCreatePreview(req.voxelType, req.position, (Color)req.tintColor);
					if (entry.go != null) {
						entry.go.transform.SetParent(g.transform, false);
						entry.go.transform.localPosition = Vector3.zero;
						entry.go.transform.localRotation = Misc.quaternionZero;
						if (g.meshRenderer != null) g.meshRenderer.enabled = false;
						g.previewGO = entry.go;
						g.previewRenderers = entry.renderers;
					}
					g.transform.rotation = combinedRot;
				} else {
					if (g.meshRenderer != null) g.meshRenderer.enabled = true;
					Mesh mesh = null;
					if (AllowMicroVoxelMesh(req)) {
						MicroVoxels mv = req.microVoxels != null ? req.microVoxels : req.voxelType.microVoxels;
						if (mv != null && !mv.isEmpty) {
							mesh = req.voxelType.GetMicroVoxelsMesh(mv, req.rotation, mv.secondaryType, true);
							if (mesh != null && mesh.vertexCount == 0) mesh = null;
						}
					}
					if (mesh == null) {
						mesh = req.voxelType.isVegetation ? _env.BuildVegetationPlacementMesh(req.voxelType, req.position) : _env.GetOrCreatePlacementMesh(req.voxelType, (Color32)req.tintColor);
					}
					Material mat;
					if (_enableFade && AllowFade(req)) {
						mat = _env.matDynamicAlpha;
					} else {
						mat = _env.GetPlacementMaterial(req.voxelType);
					}
					if (g.meshFilter != null) g.meshFilter.sharedMesh = mesh;
					if (g.meshRenderer != null) g.meshRenderer.sharedMaterial = mat;
					g.transform.rotation = combinedRot;
				}
			}

			protected void SetGhostAlpha (ref Ghost g, float alpha) {
				if (g.meshRenderer == null) return;
				float a = Mathf.Clamp01(alpha);
				g.mpb.SetFloat(ShaderParams.GhostAlpha, a);
				g.meshRenderer.SetPropertyBlock(g.mpb);
			}

			protected void ApplyLighting (ref Ghost g, Vector3 samplePos, in PlacementRequest req) {
				if (!_env.effectiveGlobalIllumination) return;
				int packedLight;
				if (req.voxelType.isVegetation && _env.GetVoxelIndex(req.position, out VoxelChunk lc, out int lvi, false) && lc != null && !lc.needsLightmapRebuild) {
					Vector3d posLocal = req.position - lc.position;
					float rnd = WorldRand.GetValue(posLocal.x, posLocal.z);
					float colorVariation = 1f + (rnd - 0.45f) * req.voxelType.colorVariation;
					byte sun = lc.voxels[lvi].light;
					byte torch = lc.voxels[lvi].torchLight;
					int sunVar = Mathf.Clamp((int)(sun * colorVariation), 0, 255);
					packedLight = (torch << 12) + sunVar;
				} else {
					packedLight = _env.GetVoxelLightPacked(samplePos);
				}
				if (g.meshRenderer != null && g.meshRenderer.enabled) { g.mpb.SetInt(ShaderParams.VoxelLight, packedLight); g.meshRenderer.SetPropertyBlock(g.mpb); }
				if (g.previewRenderers != null) {
					for (int r = 0; r < g.previewRenderers.Length; r++) if (g.previewRenderers[r] != null) { g.mpb.SetInt(ShaderParams.VoxelLight, packedLight); g.previewRenderers[r].SetPropertyBlock(g.mpb); }
				}
			}

			protected VoxelPlaceholder PopOrCreatePlaceholder (in PlacementRequest req, Vector3 endPos, Vector3 targetScale) {
				if (!_env.placementAnimUseGhostCollider) return null;
				if (!_env.GetVoxelIndex(req.position, out VoxelChunk pChunk, out int pIndex)) return null;

				VoxelPlaceholder placeholder;
				if (_placeholderPool.Count > 0) {
					placeholder = _placeholderPool.Pop();
				} else {
					GameObject go = new GameObject("VP_PlacementCollider", typeof(BoxCollider), typeof(VoxelPlaceholder));
					go.hideFlags = HideFlags.HideAndDontSave;
					go.transform.SetParent(_root, false);
					placeholder = go.GetComponent<VoxelPlaceholder>();
					var bc = go.GetComponent<BoxCollider>();
					bc.isTrigger = true;
				}

				placeholder.gameObject.layer = _env.layerVoxels;
				placeholder.chunk = pChunk;
				placeholder.voxelIndex = pIndex;
				placeholder.plannedType = req.voxelType;
				placeholder.plannedTint = (Color32)req.tintColor;
				placeholder.plannedRotation = req.rotation;
				placeholder.transform.position = endPos;
				placeholder.bounds = new Bounds(Vector3.zero, targetScale);
				var box = placeholder.GetComponent<BoxCollider>();
				box.size = targetScale;
				placeholder.gameObject.SetActive(true);
				Physics.SyncTransforms();
				return placeholder;
			}

			protected void PushPlaceholder (VoxelPlaceholder placeholder) {
				if (placeholder == null) return;
				placeholder.gameObject.SetActive(false);
				_placeholderPool.Push(placeholder);
			}

			protected void Commit (in PlacementRequest r) {
				_env._bypassPlacementAnim = true;
				if (r.playSound) _env.PlayBuildSound(r.voxelType.buildSound, r.position);
				if (r.isSingleMicroVoxel && r.microVoxelSize <= 1) {
					_env.MicroVoxelPlace(r.originalHitPoint, r.voxelType, r.tintColor, r.rotation);
				} else if (r.microVoxelSize > 1) {
					VoxelHitInfo hitInfo = new VoxelHitInfo();
					hitInfo.point = r.originalHitPoint;
					hitInfo.voxelCenter = r.originalVoxelCenter;
					hitInfo.normal = r.placementNormalWS.sqrMagnitude > 0.0001f ? r.placementNormalWS : (_env._nextPlacementAnimNormalWS.HasValue ? _env._nextPlacementAnimNormalWS.Value : Vector3.up);
					_env.MicroVoxelPlace(ref hitInfo, r.microVoxelSize, r.voxelType, 1f, r.tintColor, r.rotation);
				} else {
					_env.VoxelPlaceFast(r.position, r.voxelType, out _, out _, (Color32)r.tintColor, r.amount, r.rotation, r.refresh, r.placeMicroVoxels, r.microVoxels, r.slabMode);
				}
				_env._bypassPlacementAnim = false;
			}

			protected void ReturnToPool (int ghostIndex, in Ghost g, in PlacementRequest req, bool clearNormal) {
				_env._nextPlacementAnimStartWS = null;
				if (clearNormal) _env._nextPlacementAnimNormalWS = null;
				if (g.previewGO != null) { PushPreview(req.voxelType, g.previewGO, g.previewRenderers); }
				if (g.transform.childCount > 0) {
					for (int c = g.transform.childCount - 1; c >= 0; c--) {
						Transform child = g.transform.GetChild(c);
						child.SetParent(_root, false);
						child.gameObject.SetActive(false);
					}
				}
				g.transform.gameObject.SetActive(false);
				_pool.Push(ghostIndex);
			}

			protected PreviewEntry PopOrCreatePreview (VoxelDefinition vd, Vector3d position, Color tint) {
				if (!_previewPool.TryGetValue(vd, out Stack<PreviewEntry> stack)) { stack = new Stack<PreviewEntry>(2); _previewPool[vd] = stack; }
				if (stack.Count > 0) { PreviewEntry entry = stack.Pop(); if (entry.go != null) { entry.go.SetActive(true); return entry; } }
				GameObject preview = _env.VoxelPreview(position, vd, tint); if (preview == null) return default;
				foreach (var col in preview.GetComponentsInChildren<Collider>()) col.enabled = false;
				foreach (var rb in preview.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
				Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(true);
				return new PreviewEntry { go = preview, renderers = renderers };
			}

			protected void PushPreview (VoxelDefinition vd, GameObject go, Renderer[] renderers) {
				if (go == null) return; go.transform.SetParent(_root, false); go.SetActive(false);
				if (!_previewPool.TryGetValue(vd, out Stack<PreviewEntry> stack)) { stack = new Stack<PreviewEntry>(2); _previewPool[vd] = stack; }
				stack.Push(new PreviewEntry { go = go, renderers = renderers });
			}

			protected Boundsd ComputePlacementBounds (in PlacementRequest req, Vector3 placementPos) {
				if (req.isSingleMicroVoxel || req.microVoxelSize > 0) {
					VoxelHitInfo hitInfo = new VoxelHitInfo {
						voxelCenter = req.originalVoxelCenter,
						normal = req.placementNormalWS.sqrMagnitude > 0.0001f ? req.placementNormalWS : Vector3.up
					};
					int microSize = req.microVoxelSize > 0 ? req.microVoxelSize : 1;
					return _env.GetMicroVoxelBounds(ref hitInfo, microSize);
				}
				Vector3 voxelSize = req.slabMode ? new Vector3(1f, 0.5f, 1f) : req.voxelType.scale;
				return new Boundsd((Vector3d)placementPos, voxelSize);
			}

			protected bool BoundsOverlap (Boundsd a, Boundsd b) {
				return (a.min.x < b.max.x) && (a.max.x > b.min.x) &&
					(a.min.y < b.max.y) && (a.max.y > b.min.y) &&
					(a.min.z < b.max.z) && (a.max.z > b.min.z);
			}

			public abstract bool Schedule (in PlacementRequest req);
			public abstract void Tick (float dt);
		}

		sealed class ThrowPlacementAnimator : BasePlacementAnimator {
			struct Active {
				public int ghostIndex;
				public PlacementRequest req;
				public float tNorm;
				public Vector3 startPos;
				public Vector3 endPos;
				public float linger;
				public bool committed;
				public float arcHeight;
				public Quaternion baseRot;
				public Vector3 spinAxis;
				public Vector3 targetScale;
				public Boundsd bounds;
				public VoxelPlaceholder placeholder;
			}

			readonly List<Active> _active = new List<Active>(32);
			bool _enableSpin;
			float _spinTurns;

			protected override string RootName => "VP_PlacementGhosts_Throw";
			protected override string GhostName => "VP_Ghost_Throw";

			protected override void OnConfigure (VoxelPlayEnvironment env) {
				_enableSpin = env.placementAnimSpin;
				_spinTurns = env.placementAnimSpinTurns;
				_enableFade = env.placementAnimFade;
			}

			public override bool Schedule (in PlacementRequest req) {
				Quaternion combinedRot = GetCombinedRotation(req);
				Vector3 end = (Vector3)req.position + combinedRot * req.voxelType.GetOffset(req.position);
				if (req.voxelType.isVegetation && req.voxelType.offsetRandomVegetation) {
					if (_env.GetVoxelIndex(req.position, out VoxelChunk vChunk, out int _)) {
						Vector3d posLocal = req.position - vChunk.position;
						double rnd = WorldRand.GetValue(posLocal.x, posLocal.z);
						posLocal.x += rnd * 0.5 - 0.25;
						Vector3d aux = posLocal; aux.x += 1; rnd = WorldRand.GetValue(aux);
						posLocal.z += rnd * 0.5 - 0.25;
						float offsetY = (float)rnd * 0.1f;
						posLocal.y -= offsetY + 0.5f - req.voxelType.scale.y * 0.5f;
						Vector3 vegWorld = (Vector3)(vChunk.position + posLocal);
						end = vegWorld + combinedRot * req.voxelType.GetOffset(req.position);
					}
				}

				Boundsd newBounds = ComputePlacementBounds(req, end);
				for (int i = 0; i < _active.Count; i++) {
					if (BoundsOverlap(_active[i].bounds, newBounds)) {
						return false;
					}
				}

				int gi = _pool.Count > 0 ? _pool.Pop() : CreateGhost();
				Ghost g = _ghosts[gi];
				ClearGhostChildren(ref g);

				Vector3 start;
				if (_env.cameraMain != null) {
					Transform cam = _env.cameraMain.transform;
					start = cam.position + cam.forward * 0.5f + cam.up * 0.1f;
				} else if (_env.characterController != null) {
					start = _env.characterController.transform.position + Vector3.up * 1.6f;
				} else {
					start = (Vector3)req.position;
				}

				SetupVisual(req, ref g, combinedRot);
				ApplyLighting(ref g, start, req);
				if (_enableFade && AllowFade(req)) SetGhostAlpha(ref g, 0f);

				g.transform.position = start;
				g.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
				g.transform.gameObject.SetActive(true);

				float dist = Vector3.Distance(start, end);
				float arc = dist * _env.placementAnimThrowArcMagnitude;
				_ghosts[gi] = g;
				Quaternion baseRot = g.transform.rotation;
				Vector3 axis = _env._nextPlacementAnimNormalWS.HasValue ? _env._nextPlacementAnimNormalWS.Value : Vector3.up;
				VoxelPlaceholder ph = PopOrCreatePlaceholder(req, end, GetTargetScale(req));
				_active.Add(new Active { ghostIndex = gi, req = req, tNorm = 0f, startPos = start, endPos = end, committed = false, linger = 0f, arcHeight = arc, baseRot = baseRot, spinAxis = axis, targetScale = GetTargetScale(req), bounds = newBounds, placeholder = ph });
				return true;
			}

			public override void Tick (float dt) {
				if (_active.Count == 0) return;
				for (int i = _active.Count - 1; i >= 0; i--) {
					Active a = _active[i];
					Ghost g = _ghosts[a.ghostIndex];
					if (!a.committed) {
						a.tNorm += dt / _duration;
						float s = Mathf.Clamp01(a.tNorm);
						Vector3 basePos = Vector3.Lerp(a.startPos, a.endPos, s);
						basePos.y += Mathf.Sin(s * Mathf.PI) * a.arcHeight;
						g.transform.position = basePos;
						Vector3 sc = Vector3.Lerp(new Vector3(0.3f, 0.3f, 0.3f), a.targetScale, s);
						g.transform.localScale = sc;
						if (_enableSpin) {
							float ang = 360f * _spinTurns * s;
							g.transform.rotation = Quaternion.AngleAxis(ang, a.spinAxis) * a.baseRot;
						}
						if (a.tNorm >= 1f) {
							Commit(a.req);
							a.committed = true;
							a.linger = 0.08f;
							g.transform.position = a.endPos;
							g.transform.localScale = a.targetScale;
							g.transform.rotation = a.baseRot;
							PushPlaceholder(a.placeholder);
							a.placeholder = null;
						}
					} else {
						a.linger -= dt;
						if (a.linger <= 0f) {
							ReturnToPool(a.ghostIndex, g, a.req, true);
							_active.RemoveAt(i);
							continue;
						}
					}
					if (_enableFade && AllowFade(a.req)) SetGhostAlpha(ref g, a.tNorm);
					_active[i] = a;
				}
			}
		}

		struct PlacementRequest {
			public Vector3d position;
			public VoxelDefinition voxelType;
			public Color tintColor;
			public float amount;
			public int rotation;
			public bool refresh;
			public bool placeMicroVoxels;
			public MicroVoxels microVoxels;
			public bool slabMode;
			public bool playSound;
			public bool isSingleMicroVoxel;
			public int microVoxelSize;
			public Vector3d originalHitPoint;  // Store the original hit point for microvoxel placement
			public Vector3d originalVoxelCenter;  // Store the original voxel center
			public Vector3 placementNormalWS; // Captured world normal at scheduling time (used for multi-size microvoxels)
		}

		IPlacementAnimator _placementAnimator;
		PlacementAnimationMode _placementAnimatorMode;
		bool _bypassPlacementAnim;
		Vector3? _nextPlacementAnimStartWS;
		Vector3? _nextPlacementAnimNormalWS;

		public void SetNextPlacementAnimationPosition (Vector3d worldPosition) {
			_nextPlacementAnimStartWS = (Vector3)worldPosition;
		}

		public void SetNextPlacementAnimationNormal (Vector3 worldNormal) {
			if (worldNormal.sqrMagnitude > 0.0001f) {
				_nextPlacementAnimNormalWS = worldNormal.normalized;
			} else {
				_nextPlacementAnimNormalWS = null;
			}
		}

		public void SetNextPlacementAnimation (Vector3d worldPosition, Vector3 worldNormal) {
			_nextPlacementAnimStartWS = (Vector3)worldPosition;
			if (worldNormal.sqrMagnitude > 0.0001f) {
				_nextPlacementAnimNormalWS = worldNormal.normalized;
			} else {
				_nextPlacementAnimNormalWS = null;
			}
		}

		sealed class ScalePlacementAnimator : BasePlacementAnimator {
			struct Active {
				public int ghostIndex;
				public PlacementRequest req;
				public float tNorm;
				public Vector3 startPos;
				public Vector3 endPos;
				public float linger;
				public bool committed;
				public Quaternion baseRot;
				public Vector3 spinAxis;
				public Vector3 targetScale;
				public Boundsd bounds;
				public VoxelPlaceholder placeholder;
			}

			readonly List<Active> _active = new List<Active>(32);
			bool _enableSpin;
			float _spinTurns;

			protected override string RootName => "VP_PlacementGhosts";
			protected override string GhostName => "VP_Ghost";

			protected override void OnConfigure (VoxelPlayEnvironment env) {
				_enableSpin = env.placementAnimSpin;
				_spinTurns = env.placementAnimSpinTurns;
				_enableFade = env.placementAnimFade;
			}

			protected override bool AllowMicroVoxelMesh (in PlacementRequest req) {
				return req.placeMicroVoxels && !req.isSingleMicroVoxel;
			}

			public override bool Schedule (in PlacementRequest req) {
				Quaternion combinedRot = GetCombinedRotation(req);
				Vector3 end;
				if (req.isSingleMicroVoxel) {
					Vector3d voxelPos = _env.GetVoxelPosition((Vector3d)req.position);
					Vector3d microVoxelOffset = (Vector3d)req.position - voxelPos;
					end = (Vector3)(voxelPos + microVoxelOffset);
				} else {
					end = (Vector3)req.position + combinedRot * req.voxelType.GetOffset(req.position);
				}
				if (req.voxelType.isVegetation && req.voxelType.offsetRandomVegetation) {
					if (_env.GetVoxelIndex(req.position, out VoxelChunk vChunk, out int _)) {
						Vector3d posLocal = req.position - vChunk.position;
						double rnd = WorldRand.GetValue(posLocal.x, posLocal.z);
						posLocal.x += rnd * 0.5 - 0.25;
						Vector3d aux = posLocal; aux.x += 1; rnd = WorldRand.GetValue(aux);
						posLocal.z += rnd * 0.5 - 0.25;
						float offsetY = (float)rnd * 0.1f;
						posLocal.y -= offsetY + 0.5f - req.voxelType.scale.y * 0.5f;
						Vector3 vegWorld = (Vector3)(vChunk.position + posLocal);
						end = vegWorld + combinedRot * req.voxelType.GetOffset(req.position);
					}
				}

				Boundsd newBounds = ComputePlacementBounds(req, end);
				for (int i = 0; i < _active.Count; i++) {
					if (BoundsOverlap(_active[i].bounds, newBounds)) {
						return false;
					}
				}

				int gi = _pool.Count > 0 ? _pool.Pop() : CreateGhost();
				Ghost g = _ghosts[gi];
				ClearGhostChildren(ref g);

				Vector3 start = _env._nextPlacementAnimStartWS.HasValue ? _env._nextPlacementAnimStartWS.Value : (Vector3)req.position;
				if (req.voxelType.isVegetation) {
					start.x = end.x;
					start.z = end.z;
				}

				SetupVisual(req, ref g, combinedRot);
				ApplyLighting(ref g, start, req);
				if (_enableFade && AllowFade(req)) SetGhostAlpha(ref g, 0f);

				g.transform.position = start;
				g.transform.localScale = Vector3.zero;
				g.transform.gameObject.SetActive(true);
				_ghosts[gi] = g;
				Quaternion baseRot = g.transform.rotation;
				Vector3 axis = _env._nextPlacementAnimNormalWS.HasValue ? _env._nextPlacementAnimNormalWS.Value : Vector3.up;
				VoxelPlaceholder ph = PopOrCreatePlaceholder(req, end, GetTargetScale(req));
				_active.Add(new Active { ghostIndex = gi, req = req, tNorm = 0f, startPos = start, endPos = end, committed = false, linger = 0f, baseRot = baseRot, spinAxis = axis, targetScale = GetTargetScale(req), bounds = newBounds, placeholder = ph });
				return true;
			}

			public override void Tick (float dt) {
				if (_active.Count == 0) return;
				for (int i = _active.Count - 1; i >= 0; i--) {
					Active a = _active[i];
					Ghost g = _ghosts[a.ghostIndex];
					if (!a.committed) {
						a.tNorm += dt / _duration;
						float s = Mathf.Clamp01(a.tNorm);
						float sPos = s * s * (3f - 2f * s);
						g.transform.position = Vector3.Lerp(a.startPos, a.endPos, sPos);
						Vector3 currentScale = a.targetScale * sPos;
						g.transform.localScale = currentScale;
						if (_enableSpin) {
							float ang = 360f * _spinTurns * s;
							g.transform.rotation = Quaternion.AngleAxis(ang, a.spinAxis) * a.baseRot;
						}
						if (a.tNorm >= 1f) {
							Commit(a.req);
							a.committed = true;
							a.linger = 0.08f;
							g.transform.position = a.endPos;
							g.transform.localScale = a.targetScale;
							g.transform.rotation = a.baseRot;
							PushPlaceholder(a.placeholder);
							a.placeholder = null;
						}
					} else {
						a.linger -= dt;
						if (a.linger <= 0f) {
							ReturnToPool(a.ghostIndex, g, a.req, true);
							_active.RemoveAt(i);
							continue;
						}
					}
					if (_enableFade && AllowFade(a.req)) SetGhostAlpha(ref g, a.tNorm);
					_active[i] = a;
				}
			}
		}

		sealed class ElasticPopPlacementAnimator : BasePlacementAnimator {
			struct Active {
				public int ghostIndex;
				public PlacementRequest req;
				public float tNorm;
				public Vector3 endPos;
				public float linger;
				public bool committed;
				public Quaternion baseRot;
				public Vector3 targetScale;
				public Boundsd bounds;
				public VoxelPlaceholder placeholder;
			}

			readonly List<Active> _active = new List<Active>(32);
			float _popAmount;

			protected override string RootName => "VP_PlacementGhosts_ElasticPop";
			protected override string GhostName => "VP_Ghost_ElasticPop";

			protected override void OnConfigure (VoxelPlayEnvironment env) {
				_popAmount = env.placementAnimElasticPopAmount;
				_enableFade = env.placementAnimFade;
			}

			public override bool Schedule (in PlacementRequest req) {
				Quaternion combinedRot = GetCombinedRotation(req);
				Vector3 end = (Vector3)req.position + combinedRot * req.voxelType.GetOffset(req.position);
				if (req.voxelType.isVegetation && req.voxelType.offsetRandomVegetation) {
					if (_env.GetVoxelIndex(req.position, out VoxelChunk vChunk, out int _)) {
						Vector3d posLocal = req.position - vChunk.position;
						double rnd = WorldRand.GetValue(posLocal.x, posLocal.z);
						posLocal.x += rnd * 0.5 - 0.25;
						Vector3d aux = posLocal; aux.x += 1; rnd = WorldRand.GetValue(aux);
						posLocal.z += rnd * 0.5 - 0.25;
						float offsetY = (float)rnd * 0.1f;
						posLocal.y -= offsetY + 0.5f - req.voxelType.scale.y * 0.5f;
						Vector3 vegWorld = (Vector3)(vChunk.position + posLocal);
						end = vegWorld + combinedRot * req.voxelType.GetOffset(req.position);
					}
				}

				Boundsd newBounds = ComputePlacementBounds(req, end);
				for (int i = 0; i < _active.Count; i++) {
					if (BoundsOverlap(_active[i].bounds, newBounds)) {
						return false;
					}
				}

				int gi = _pool.Count > 0 ? _pool.Pop() : CreateGhost();
				Ghost g = _ghosts[gi];
				ClearGhostChildren(ref g);

				SetupVisual(req, ref g, combinedRot);
				ApplyLighting(ref g, end, req);
				if (_enableFade && AllowFade(req)) SetGhostAlpha(ref g, 0f);

				g.transform.position = end;
				g.transform.localScale = Vector3.zero;
				g.transform.gameObject.SetActive(true);

				_ghosts[gi] = g;
				Quaternion baseRot = g.transform.rotation;
				VoxelPlaceholder ph = PopOrCreatePlaceholder(req, end, GetTargetScale(req));
				_active.Add(new Active { ghostIndex = gi, req = req, tNorm = 0f, endPos = end, committed = false, linger = 0f, baseRot = baseRot, targetScale = GetTargetScale(req), bounds = newBounds, placeholder = ph });
				return true;
			}

			public override void Tick (float dt) {
				if (_active.Count == 0) return;
				for (int i = _active.Count - 1; i >= 0; i--) {
					Active a = _active[i];
					Ghost g = _ghosts[a.ghostIndex];
					if (!a.committed) {
						a.tNorm += dt / _duration;
						float s = Mathf.Clamp01(a.tNorm);
						float k = EaseOutBack(s, _popAmount);
						g.transform.position = a.endPos;
						g.transform.localScale = a.targetScale * k;
						if (a.tNorm >= 1f) {
							Commit(a.req);
							a.committed = true;
							a.linger = 0.08f;
							g.transform.position = a.endPos;
							g.transform.localScale = a.targetScale;
							g.transform.rotation = a.baseRot;
							PushPlaceholder(a.placeholder);
							a.placeholder = null;
						}
					} else {
						a.linger -= dt;
						if (a.linger <= 0f) {
							ReturnToPool(a.ghostIndex, g, a.req, false);
							_active.RemoveAt(i);
							continue;
						}
					}
					if (_enableFade && AllowFade(a.req)) SetGhostAlpha(ref g, a.tNorm);
					_active[i] = a;
				}
			}

			static float EaseOutBack (float t, float overshoot) {
				float s = overshoot <= 0f ? 1.70158f : 1.70158f + overshoot * 2f;
				t = t - 1f;
				return 1f + (s + 1f) * t * t * t + s * t * t;
			}
		}

		void EnsurePlacementAnimator () {
			if (_placementAnimator == null || _placementAnimatorMode != placementAnimation) {
				switch (placementAnimation) {
					case PlacementAnimationMode.Scale:
						_placementAnimator = new ScalePlacementAnimator();
						break;
					case PlacementAnimationMode.Throw:
						_placementAnimator = new ThrowPlacementAnimator();
						break;
					case PlacementAnimationMode.ElasticPop:
						_placementAnimator = new ElasticPopPlacementAnimator();
						break;
					default:
						_placementAnimator = null;
						break;
				}
				_placementAnimatorMode = placementAnimation;
			}
			_placementAnimator?.Configure(this, placementAnimDuration);
		}

		// Returns a dynamic mesh for the given voxel type and color, creating and caching it on demand
		Mesh GetOrCreatePlacementMesh (VoxelDefinition type, Color32 tintColor) {
			Mesh mesh = null;
			if (type.dynamicMeshes == null) {
				type.dynamicMeshes = new Dictionary<Color, Mesh>();
			} else {
				type.dynamicMeshes.TryGetValue(tintColor, out mesh);
			}
			if (mesh != null) return mesh;

			// Build cube mesh honoring per-face texture indices and tinting
			tempVertices.Clear();
			tempNormals.Clear();
			tempUVs.Clear();
			tempColors.Clear();
			tempIndicesPos = 0;

			AddFace(Cube.faceVerticesBack, Cube.normalsBack, type.textureIndexSide, tintColor);
			AddFace(Cube.faceVerticesForward, Cube.normalsForward, type.textureIndexForward, tintColor);
			AddFace(Cube.faceVerticesLeft, Cube.normalsLeft, type.textureIndexLeft, tintColor);
			AddFace(Cube.faceVerticesRight, Cube.normalsRight, type.textureIndexRight, tintColor);
			AddFace(Cube.faceVerticesTop, Cube.normalsUp, type.textureIndexTop, tintColor);
			AddFace(Cube.faceVerticesBottom, Cube.normalsDown, type.textureIndexBottom, tintColor);

			mesh = new Mesh();
			mesh.SetVertices(tempVertices);
			mesh.SetUVs(0, tempUVs);
			mesh.SetNormals(tempNormals);
			if (enableTinting) mesh.SetColors(tempColors);
			mesh.triangles = tempIndices;
			type.dynamicMeshes[tintColor] = mesh;
			return mesh;
		}

		// Chooses a material consistent with engine rendering for the given voxel type
		Material GetPlacementMaterial (VoxelDefinition type) {
			if (type.overrideMaterial) {
				return type.overrideMaterialNonGeo;
			}
			if (type.renderType == RenderType.Custom) {
				return GetDynamicVoxelMaterialFromCustom(type);
			}
			if (type.renderType == RenderType.CutoutCross) {
				return renderingMaterials[INDICES_BUFFER_CUTXSS].material;
			}
			if (type.renderType == RenderType.Cutout) {
				return matDynamicCutout;
			}
			if (type.renderType == RenderType.Transp6tex || type.renderType == RenderType.Water || type.renderType == RenderType.Cloud || type.renderType == RenderType.Fluid) {
				return matDynamicAlpha;
			}
			return matDynamicOpaque;
		}

		// Builds a vegetation cross mesh using engine UV.z encoding for texture arrays
		Mesh BuildVegetationPlacementMesh (VoxelDefinition type, Vector3d position) {
			// Compute deterministic height like mesher
			float height = WorldRand.Range(type.vegetationMinHeight, type.vegetationMaxHeight, position);

			tempVertices.Clear();
			tempNormals.Clear();
			tempUVs.Clear();
			tempColors.Clear();
			tempIndicesPos = 0;

			void AddCrossQuad (Vector3[] faceVertices) {
				int baseIndex = tempVertices.Count;
				for (int v = 0; v < 4; v++) {
					Vector3 vert = faceVertices[v];
					if (vert.y > 0f) vert.y *= height;
					tempVertices.Add(vert);
					tempNormals.Add(Misc.vector3zero);
				}
				int texIndex = type.textureIndexSide;
				Vector4 uv = new Vector4(0, 0, texIndex, 15f);
				tempUVs.Add(uv);
				uv.y = 1f; tempUVs.Add(uv);
				uv.x = 1f; uv.y = 0f; tempUVs.Add(uv);
				uv.y = 1f; tempUVs.Add(uv);

				if (enableTinting) {
					tempColors.Add(Misc.color32White);
					tempColors.Add(Misc.color32White);
					tempColors.Add(Misc.color32White);
					tempColors.Add(Misc.color32White);
				}

				tempIndices[tempIndicesPos++] = baseIndex + 0;
				tempIndices[tempIndicesPos++] = baseIndex + 1;
				tempIndices[tempIndicesPos++] = baseIndex + 2;
				tempIndices[tempIndicesPos++] = baseIndex + 3;
				tempIndices[tempIndicesPos++] = baseIndex + 2;
				tempIndices[tempIndicesPos++] = baseIndex + 1;
			}

			AddCrossQuad(MeshingThread.faceVerticesCross1);
			AddCrossQuad(MeshingThread.faceVerticesCross2);

			Mesh mesh = new Mesh();
			mesh.SetVertices(tempVertices);
			mesh.SetUVs(0, tempUVs);
			mesh.SetNormals(tempNormals);
			if (enableTinting) mesh.SetColors(tempColors);
			mesh.SetIndices(tempIndices, 0, tempIndicesPos, MeshTopology.Triangles, 0);
			return mesh;
		}
	}
}



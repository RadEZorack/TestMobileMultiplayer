using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {

	[HelpURL("https://kronnect.com/docs/voxel-play/")]
	public partial class VoxelPlayInteractiveObjectsManager : MonoBehaviour {

		static VoxelPlayInteractiveObjectsManager _instance;
		VoxelPlayInteractiveObject[] objs, nearObjs;
		int count, nearCount;
		VoxelPlayEnvironment env;
		int lastPlayerPosX, lastPlayerPosY, lastPlayerPosZ;
		Collider lastCollider;

		public static VoxelPlayInteractiveObjectsManager instance {
			get {
				if (_instance == null) {
					VoxelPlayEnvironment env = VoxelPlayEnvironment.instance;
					if (env != null) {
						_instance = env.GetComponent<VoxelPlayInteractiveObjectsManager> ();
						if (_instance == null) {
							_instance = env.gameObject.AddComponent<VoxelPlayInteractiveObjectsManager> ();
						}
					}

				}
				return _instance;
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void InitSingleton()
		{
			_instance = null;
		}


		public void InteractiveObjectRegister (VoxelPlayInteractiveObject o) {
			o.registrationIndex = AddToDynamicList (o, ref objs, ref count);
		}


		public void InteractiveObjectUnRegister (VoxelPlayInteractiveObject o) {
			objs [o.registrationIndex] = null;
			if (o.registrationIndex == count - 1) {
				count--;
			}
			o.registrationIndex = 0;
		}


		public virtual void OnEnable () {
			objs = new VoxelPlayInteractiveObject[100];
			count = 0;
			nearObjs = new VoxelPlayInteractiveObject[100];
			nearCount = 0;
		}

		public virtual void Start () {
			env = VoxelPlayEnvironment.instance;
		}

		public virtual void LateUpdate () {

			Collider collider = null;
			if (env != null && (UnityEngine.Object)env.characterController != null && env.input != null) {
				// Check object on the crosshair
				collider = env.characterController.crosshairHitInfo.collider;
				if (env.input.GetButtonDown (InputButtonNames.Action)) {
					if (collider != null) {
						if (!collider.TryGetComponent(out VoxelPlayInteractiveObject obj)) {
							obj = collider.GetComponentInChildren<VoxelPlayInteractiveObject>();
						}
						if (obj != null) {
							if (obj.triggerNearbyObjects) {
								for (int k = 0; k < nearCount; k++) {
									obj = nearObjs [k];
									if (obj != null && obj.playerIsNear) {
										nearObjs [k].OnPlayerAction ();
									}
								}
							} else if (obj.playerIsNear) {
								obj.OnPlayerAction ();
							}
						}
					}
				}
			}

			// Check if player has moved since last frame
			Vector3 playerPos = env.currentAnchorPosWS;
			int playerPosX = (int)playerPos.x;
			int playerPosY = (int)playerPos.y;
			int playerPosZ = (int)playerPos.z;
			if (playerPosX == lastPlayerPosX && playerPosY == lastPlayerPosY && playerPosZ == lastPlayerPosZ && collider == lastCollider) {
				return;
			}
			lastCollider = collider;
			lastPlayerPosX = playerPosX;
			lastPlayerPosY = playerPosY;
			lastPlayerPosZ = playerPosZ;

			// Check if player enters/exits the interaction area per object
			for (int k = 0; k < count; k++) {
				VoxelPlayInteractiveObject o = objs [k];
				if (o != null && o.enabled) {
					Vector3 objPos = o.transform.position;
					float interactionDistanceSqr = o.interactionDistance * o.interactionDistance;
					float dist = FastVector.SqrDistance (ref playerPos, ref objPos);
					bool isNear = dist <= interactionDistanceSqr;
					if (o.playerIsNear && !isNear) {
						o.playerIsNear = false;
						// Remove from near list
						nearObjs [o.nearIndex] = null;
						if (o.nearIndex == nearCount - 1) {
							nearCount--;
						}
						o.nearIndex = 0;
						// Call event
						o.OnPlayerGoesAway ();
					} else if (isNear && !o.playerIsNear) {
						o.playerIsNear = true;
						o.nearIndex = AddToDynamicList (o, ref nearObjs, ref nearCount);
						// Call event
						o.OnPlayerApproach ();
					}
				}
			}
		}

		int AddToDynamicList (VoxelPlayInteractiveObject o, ref VoxelPlayInteractiveObject[] objs, ref int count) {
			int index;

			// Next place is free?
			if (count < objs.Length) {
				if (objs [count] == null) {
					index = count;
					objs [count++] = o;
					return index;
				}
			}
			// Look for an empty slot
			for (int k = 0; k < objs.Length; k++) {
				if (objs [k] == null) {
					objs [k] = o;
					return k;
				}
			}
			// Make array bigger
			VoxelPlayInteractiveObject[] newObjs = new VoxelPlayInteractiveObject[count * 2];
			Array.Copy (objs, newObjs, objs.Length);
			objs = newObjs;
			index = count;
			objs [count++] = o;
			return index;
		}
	}


}

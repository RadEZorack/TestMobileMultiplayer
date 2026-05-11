using System;
using System.Collections;
using UnityEngine;


namespace VoxelPlay {

	public class Door : VoxelPlayInteractiveObject {

		public float speed = 50f;
		public AudioClip sound;


		const string DOOR_ROTATION_PROPERTY = "doorRotation";
		const string DOOR_IS_OPEN_PROPERTY = "doorIsOpen";

		[NonSerialized]
		public bool isOpen;

		bool shown;
		WaitForEndOfFrame nextFrame;
		bool rotating;
		float targetRotation;
		float baseRotation;
		float direction;
		float currentAngle;

		public override void OnStart () {
			nextFrame = new WaitForEndOfFrame();
			baseRotation = transform.eulerAngles.y;
			float rotation = env.GetVoxelPropertyFloat(chunk, voxelIndex, DOOR_ROTATION_PROPERTY);
			if (rotation != 0) {
				transform.eulerAngles = new Vector3(0, rotation, 0);
			}
			isOpen = env.GetVoxelPropertyFloat(chunk, voxelIndex, DOOR_IS_OPEN_PROPERTY) > 0;
			currentAngle = transform.eulerAngles.y;
		}

		public override void OnPlayerApproach () {
			if (!shown) {
				if (Application.isMobilePlatform) {
					env.ShowMessage(txt: "<color=green>Press </color><color=yellow>Action</color> button to open/close this door.", allowDuplicatedMessage: true);
				} else {
					env.ShowMessage(txt: "<color=green>Press </color><color=yellow>T</color> to open/close this door.", allowDuplicatedMessage: true);
				}
				shown = true;
			}
		}

		public override void OnPlayerGoesAway () {
		}

		public override void OnPlayerAction () {
			if (speed <= 0)
				return;

			float openRotation = customTag.Equals("left") ? -90 : 90;
			isOpen = !isOpen;
			if (isOpen && sound != null) {
				AudioSource.PlayClipAtPoint(sound, transform.position);
			}
			targetRotation = isOpen ? baseRotation + openRotation : baseRotation;
			direction = targetRotation > currentAngle ? 1 : -1;
			if (!rotating) {
				rotating = true;
				StartCoroutine(RotateDoor());
			}
		}

		IEnumerator RotateDoor () {

			for (; ; ) {
				currentAngle += speed * Time.deltaTime * direction;
				float sign = targetRotation > currentAngle ? 1 : -1;
				bool ends = false;
				if (sign != direction) {
					currentAngle = targetRotation;
					ends = true;
				}
				transform.eulerAngles = new Vector3(0, currentAngle, 0);
				if (ends) {
					if (!isOpen && sound != null) {
						AudioSource.PlayClipAtPoint(sound, transform.position);
					}
					rotating = false;
					break;
				}
				yield return nextFrame;
			}

			if (env != null) {
				env.VoxelSetProperty(chunk, voxelIndex, DOOR_ROTATION_PROPERTY, transform.eulerAngles.y);
				env.VoxelSetProperty(chunk, voxelIndex, DOOR_IS_OPEN_PROPERTY, isOpen ? 1 : 0);
			}

		}

	}

}
// Voxel Play 
// Created by Ramiro Oliva (Kronnect)

// Voxel Play Interactive Object - attach this script to any custom voxel or object that you want to react to player interactions

using System;
using UnityEngine;
using System.Collections;

namespace VoxelPlay {
				
	[HelpURL("https://kronnect.com/docs/voxel-play/")]
	public abstract class VoxelPlayInteractiveObject : MonoBehaviour {

		/// <summary>
		/// User defined tag. Can be useful to differentiate an object in the scene. For example in a double door, the customTag helps differentiate the left and right sides
		/// </summary>
		public string customTag;

		/// <summary>
		/// The interaction distance for this object. When player approaches this object, he can press 'E' to interact with the object. The OnPlayerAction event is called then.
		/// </summary>
		public float interactionDistance = 4f;

		/// <summary>
		/// If interacting with this object will also activate other nearby objects. Useful for compound objects, like double doors
		/// </summary>
		public bool triggerNearbyObjects;

		/// <summary>
		/// Set to true by Voxel Play when player approaches this object (is nearer than interaction distance). Set to false when player exits the interaction area. Evens OnPlayerApproach/OnPlayerGoesAway are fired accordingly.
		/// </summary>
		[NonSerialized]
		public bool playerIsNear;

		/// <summary>
		/// Internal assigned index for this interactive object when it's registered. Do not modify.
		/// </summary>
		[NonSerialized]
		public int registrationIndex;

		/// <summary>
		/// Internal assigned index for this interactive object when player is within interaction distance. Do not modify.
		/// </summary>
		[NonSerialized]
		public int nearIndex;

		[NonSerialized]
		public VoxelChunk chunk;

		[NonSerialized]
		public int voxelIndex;

		protected VoxelPlayEnvironment env;

		public virtual void OnStart() {}
		public virtual void OnPlayerApproach() {}
		public virtual void OnPlayerGoesAway() {}
		public virtual void OnPlayerAction() {}

		public virtual void Start() {
			env = VoxelPlayEnvironment.instance;
			if (env != null) {
				VoxelPlayInteractiveObjectsManager.instance.InteractiveObjectRegister (this);
			}
			VoxelPlaceholder placeholder = GetComponentInParent<VoxelPlaceholder>(true);
			if (placeholder != null) {
				chunk = placeholder.chunk;
				voxelIndex = placeholder.voxelIndex;
			}
			OnStart ();
		}

		public virtual void OnDestroy() {
			if (env != null) {
				VoxelPlayInteractiveObjectsManager.instance.InteractiveObjectUnRegister (this);
			}
			
		}


	}
}
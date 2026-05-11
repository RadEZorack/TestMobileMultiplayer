using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelPlay;

namespace VoxelPlayDemos {
				
	public class DemoTPS : MonoBehaviour {

		VoxelPlayEnvironment env;

		void Start () {
			env = VoxelPlayEnvironment.instance;

			// When Voxel Play is ready, do some stuff...
			env.OnInitialized += OnInitialized;

			VoxelPlayPlayer player = VoxelPlayPlayer.instance as VoxelPlayPlayer;
			// Get notified is player is damaged
			player.OnPlayerGetDamage += OnPlayerGetDamage;
			// Get notified is player is killed
			player.OnPlayerIsKilled += OnPlayerIsKilled;
		}

		void OnInitialized () {
			// Add special instructions after 4 seconds of game running
			Invoke ("SpecialInfo", 4);
		}

		void SpecialInfo () {
			env.ShowMessage ("<color=green>Press <color=yellow>WASD</color> to move, <color=yellow>Mouse Scroll Wheel</color> to zoom in/out, <color=yellow>Right Mouse Button</color> to rotate view, <color=yellow>Space</color> to jump, <color=yellow>C</color> to crouch</color>", 20, true);
		}


		void OnPlayerGetDamage (ref int damage, int remainingLifePoints) {
			Debug.Log ("Player gets " + damage + " damage points (" + remainingLifePoints + " life points left)"); 
		}


		void OnPlayerIsKilled () {
			Debug.Log ("Player is dead!");
		}
	

	}

}
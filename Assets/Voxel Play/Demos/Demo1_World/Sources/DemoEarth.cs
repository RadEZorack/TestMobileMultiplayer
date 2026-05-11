using System.Collections.Generic;
using UnityEngine;
using VoxelPlay;

namespace VoxelPlayDemos {

    public class DemoEarth : MonoBehaviour {

        public GameObject deerPrefab;
        public GameObject bouncingSpherePrefab;
        VoxelPlayEnvironment env;

        void Start () {
            env = VoxelPlayEnvironment.instance;

            // when Voxel Play is ready, do some stuff...
            env.OnInitialized += OnInitialized;

            // when the world has finished generating and rendering
            env.OnWorldLoaded += OnWorldLoaded;

            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player != null) {
                // Get notified if player is damaged
                player.OnPlayerGetDamage += OnPlayerGetDamage;
                // Get notified if player is killed
                player.OnPlayerIsKilled += OnPlayerIsKilled;
            }
        }

        void OnWorldLoaded () {
            // This event triggers when Voxel Play is initialized and the world has finished rendering
            Debug.Log($"World fully loaded {Time.time} s");
        }

        void OnInitialized () {

            Debug.Log("Voxel Play initialized.");

            // Item definitions are stored in Items folder within the world name folder
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player != null) {
                // Add 3 torches to initial player inventory
                player.AddInventoryItem(env.GetItemDefinition("Torch"), 3);
                player.AddInventoryItem(env.GetItemDefinition("Torch Red"), 2);

                // Add a shovel (no need to specify quantity it's 1 unit)
                player.AddInventoryItem(env.GetItemDefinition("Shovel"));

                // Add a sword 
                player.AddInventoryItem(env.GetItemDefinition("Sword"));

                // Add a pickaxe (which enables microvoxels)
                player.AddInventoryItem(env.GetItemDefinition("Pickaxe"));

                // Add a mace (which enables microvoxels of size 3)
                player.AddInventoryItem(env.GetItemDefinition("Mace"));

                // Add a shotgun (which enables microvoxels of size 6 and random probability)
                player.AddInventoryItem(env.GetItemDefinition("Shotgun"));

                // Add 20 grenades
                player.AddInventoryItem(env.GetItemDefinition("Grenade"), 20);
            }

            // Add special instructions after 4 seconds of game running
            Invoke("SpecialKeys", 4);
        }

        void OnPlayerGetDamage (ref int damage, int remainingLifePoints) {
            Debug.Log("Player gets " + damage + " damage points (" + remainingLifePoints + " life points left)");
        }


        void OnPlayerIsKilled () {
            Debug.Log("Player is dead!");
        }


        void SpecialKeys () {
            env.ShowMessage("<color=green>Press <color=yellow>O</color> to throw a ball, <color=yellow>Y</color> to summon a deer, <color=yellow>X</color> to place a brick, <color=yellow>J</color> to levitate a voxel, <color=yellow>R</color> to rotate a voxel or <color=yellow>1</color> to create a path :)</color>", 20, true);
        }

        void Update () {
            // If Voxel Play is not yet initialized OR console is visible, do not react to normal player input
            if (!env.initialized || VoxelPlayUI.instance.IsConsoleVisible)
                return;
            VoxelPlayInputController input = env.input;
            if (input == null) return;
            if (input.GetKeyDown("o")) {
                ThrowBall();
            }
            if (input.GetKeyDown("y")) {
                SummonDeer();
            }
            if (input.GetKeyDown("x")) {
                PlaceBrick();
            }
            if (input.GetKeyDown("j")) {
                LevitateVoxel();
            }
            if (input.GetKeyDown("r")) {
                RotateVoxel();
            }
            if (input.GetKeyDown("1")) {
                CreatePathWithMicroVoxels();
            }
        }


        /// <summary>
        /// Summons a ball that interacts with voxel environment. It can be launched entering in the console "Invoke Demo Ball"
        /// </summary>
        void ThrowBall () {
            GameObject ball = Instantiate(bouncingSpherePrefab);
            ball.transform.position = Camera.main.transform.position + Camera.main.transform.forward;
            ball.GetComponent<Renderer>().material.color = new Color(Random.value * 0.5f + 0.5f, Random.value * 0.5f + 0.5f, Random.value * 0.5f + 0.5f);

            // Throw it! :)
            ball.GetComponent<Rigidbody>().linearVelocity = Camera.main.transform.forward * 10f;
        }

        /// <summary>
        /// Summons a deer prefab
        /// </summary>
        void SummonDeer () {
            VoxelHitInfo hitInfo;
            if (env.RayCast(Camera.main.transform.position, Camera.main.transform.forward, out hitInfo)) {
                // Instantiate deer
                GameObject deer = Instantiate(deerPrefab);
                // Position it on ground
                deer.transform.position = hitInfo.point;
                // Important: instantiate material so different deers can have different colors and smooth lighting; we do it by assigning a random color to provide variation
                deer.GetComponent<MeshRenderer>().material.color = new Color(Random.value * 0.1f + 0.9f, Random.value * 0.1f + 0.9f, 1f);
            }
        }


        /// <summary>
        /// Places a brickWall voxel in front of player. Can be executed in game entering in the console "Invoke Demo PlaceBrick"
        /// </summary>
        void PlaceBrick () {
            // Instead of using Raycast like in the SummonDeer function, will reuse the crosshair data (just another way of doing the same)
            VoxelPlayFirstPersonController fpsController = VoxelPlayFirstPersonController.instance;
            if (fpsController.crosshairOnBlock) {
                Vector3d pos = fpsController.crosshairHitInfo.voxelCenter + fpsController.crosshairHitInfo.normal;
                VoxelDefinition brickWall = env.GetVoxelDefinition("VoxelBrickWall");
                env.VoxelPlace(pos, brickWall);
            }
        }

        /// <summary>
        /// Converts voxel on the crosshair into a dynamic gameobject
        /// </summary>
        void LevitateVoxel () {
            VoxelPlayFirstPersonController fpsController = VoxelPlayFirstPersonController.instance;
            if (fpsController.crosshairOnBlock) {
                VoxelChunk chunk = fpsController.crosshairHitInfo.chunk;
                int voxelIndex = fpsController.crosshairHitInfo.voxelIndex;
                VoxelDefinition type = chunk.voxels[voxelIndex].type;
                if (!type.renderType.supportsDynamic()) {
                    env.ShowError("The voxel type " + type.name + " can't be levitated.");
                    return;
                }
                GameObject obj = env.VoxelGetDynamic(chunk, voxelIndex, true);
                if (obj != null) {
                    Rigidbody rb = obj.GetComponent<Rigidbody>();
                    rb.AddForce(Vector3.up * 500f);
                }
            }
        }


        /// <summary>
        /// Rotates voxel on the crosshair
        /// </summary>
        void RotateVoxel () {
            VoxelPlayFirstPersonController fpsController = VoxelPlayFirstPersonController.instance;
            if (fpsController.crosshairOnBlock) {
                env.VoxelRotate(env.lastHighlightInfo.center, 0f, 15f, 0);
                // You could also call env.VoxelRotateTextures method which switches the textures around the sides. VoxelRotateTextures accepts "turns" of 1, 2, 3, etc. each one meaning a 90º rotation.
            }
        }


        /// <summary>
        /// Creates a path with microvoxels that have the top layer removed
        /// </summary>
        void CreatePathWithMicroVoxels () {
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player == null) return;

            Vector3d pathStart = player.GetTransform().position;
            Vector3d pathEnd = pathStart + new Vector3d(0, 0, 100);

            List<VoxelIndex> pathIndices = new List<VoxelIndex>();
            env.GetVoxelIndicesOnPath(pathStart, pathEnd, pathIndices, createChunkIfNotExists: true);

            if (pathIndices.Count == 0) {
                env.ShowError("Could not create path!");
                return;
            }

            MicroVoxels mv = null;
            env.CreateMicroVoxels(bottomY: 0, topY: MicroVoxels.COUNT_PER_AXIS - 2, ref mv, shared: true);

            VoxelDefinition brickWall = env.GetVoxelDefinition("VoxelBrickWall");
            env.VoxelPlace(pathIndices, brickWall, Color.white, refresh: true, placeMicroVoxels: true, microVoxels: mv);

            env.ShowMessage($"<color=green>Created path with {pathIndices.Count} voxels!</color>", 3);
        }
    }

}
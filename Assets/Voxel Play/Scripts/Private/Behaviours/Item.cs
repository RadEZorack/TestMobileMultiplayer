using System;
using UnityEngine;

namespace VoxelPlay
{

    /// <summary>
    /// This behaviour should be attached to any object in the scene that can be recovered or damaged by the player
    /// </summary>
    public class Item : MonoBehaviour
    {

        /// <summary>
        /// The item type represented by this object.
        /// </summary>
        public ItemDefinition itemDefinition;

        public float quantity = 1f;

        public bool autoRotate = true;

        /// <summary>
        /// If set to true, this item will be added to the chunk items list. If item moves (ie. falls down), it will switch chunk automatically
        /// </summary>
        public bool persistentItem;

        /// <summary>
        /// If true, the object can be destroyed from the scene when picked up
        /// </summary>
        public bool canBeDestroyed;

        /// <summary>
        /// Resistance points left for this item. Used for items that can be damaged on the scene (not voxels).
        /// </summary>
        [NonSerialized]
        public byte resistancePointsLeft;

        /// <summary>
        /// If this object represents an item that can be picked
        /// </summary>
        public bool canPickOnApproach;

        [NonSerialized]
        public float creationTime;

        const float PICK_UP_END_DISTANCE_SQR = 0.81f;
        const float ROTATION_SPEED = 40f;

        [NonSerialized, HideInInspector]
        public Rigidbody rb;

        [NonSerialized]
        public bool pickingUp;

        [NonSerialized]
        public IVoxelPlayPlayer pickingPlayer;

        VoxelPlayEnvironment env;
        Material mat;
        Vector3d lastPosition;

        /// <summary>
        /// Stores the voxel related to this item
        /// </summary>
        public VoxelChunk itemChunk;
        public int itemVoxelIndex = -1;

        void Start ()
        {
            if (rb == null) {
                rb = GetComponent<Rigidbody> ();
            }
            env = VoxelPlayEnvironment.instance;
            if (persistentItem) {
                // Clone material to support voxel lighting
                Renderer renderer = GetComponent<Renderer> ();
                if (renderer != null) {
                    mat = renderer.sharedMaterial;
                    if (mat != null) {
                        mat = Instantiate(mat);
                        renderer.sharedMaterial = mat;
                    }
                }
                ManageItem ();
            }
        }

        void Update ()
        {
            if (this == null) return;

            if (transform.position.y < -11000) {
                // safety check in case the object drops to infinite
                Destroy (gameObject);
                return;
            }

            if (!canPickOnApproach || itemDefinition == null) {
                if (!pickingUp) {
                    return;
                }
            }

            if (autoRotate && rb != null) {
                rb.rotation = Quaternion.Euler (Misc.vector3up * ((Time.time * ROTATION_SPEED) % 360));
            }

            if (persistentItem) {
                ManageItem ();
            }

            if (!pickingUp) {
                if (Time.frameCount % 10 != 0)
                    return;
            }

            // Check if player is near
            Vector3 playerPosition = pickingPlayer != null ? pickingPlayer.GetTransform().position : env.currentAnchorPosWS;
            Vector3 pos = transform.position;

            float dx = playerPosition.x - pos.x;
            float dy = playerPosition.y - pos.y;
            float dz = playerPosition.z - pos.z;

            if (pickingUp) {
                pos.x += dx * 0.25f;
                pos.y += dy * 0.25f;
                pos.z += dz * 0.25f;
                transform.position = pos;
            }

            if (Time.time - creationTime > 1f) {
                float dist = dx * dx + dy * dy + dz * dz;
                if (dist < PICK_UP_END_DISTANCE_SQR) {
                    IVoxelPlayPlayer player = pickingPlayer;
                    if (player == null) {
                        player = VoxelPlayPlayer.instance;
                    }
                    if (player != null) {
                        player.PickUpItem (itemDefinition, quantity);
                    }
                    VoxelPlayUI ui = VoxelPlayUI.instance;
                    if (ui != null) {
                        ui.RefreshInventoryContents ();
                    }
                    if (persistentItem || canBeDestroyed) {
                        Destroy (gameObject);
                    } else {
                        gameObject.SetActive (false);
                    }
                } else {
                    float startRadius = itemDefinition.pickupRadius > 0 ? itemDefinition.pickupRadius : env.defaultPickupRadius;
                    if (dist < startRadius * startRadius) {
                        pickingUp = true;
                    }
                }
            }
        }

        public void PickItem (IVoxelPlayPlayer pickingPlayer)
        {
            if (itemDefinition == null) return;
            if (!itemDefinition.canBePicked) return;
            this.pickingPlayer = pickingPlayer;
            pickingUp = true;
        }


        void ManageItem ()
        {
            if (env == null)
                return;

            Vector3d currentPosition = transform.position;

            if (itemChunk != null && itemChunk.isRendered) {
                if (currentPosition == lastPosition) {
                    return;
                }
                lastPosition = currentPosition;
            }

            // Update lighting
            if (mat != null) {
                float light = env.GetVoxelLightPacked (currentPosition);
                mat.SetFloat (ShaderParams.VoxelLight, light);
            }

            // Update owner chunk
            if (!env.GetVoxelIndex (currentPosition, out VoxelChunk currentChunk, out int currentVoxelIndex, true))
                return;
            if (currentVoxelIndex != itemVoxelIndex || currentChunk != itemChunk) {
                if (itemChunk != null) {
                    itemChunk.RemoveItem (this);
                    env.RegisterChunkChanges (itemChunk);
                }
                currentChunk.AddItem (this);
                env.RegisterChunkChanges (currentChunk);
                itemChunk = currentChunk;
                itemVoxelIndex = currentVoxelIndex;
            }

            if (currentChunk.isRendered && rb != null && !rb.useGravity) {
                rb.useGravity = true;
            }
        }

        void OnDestroy ()
        {
            if (itemChunk != null) {
                itemChunk.RemoveItem (this);
            }
        }


    }

}
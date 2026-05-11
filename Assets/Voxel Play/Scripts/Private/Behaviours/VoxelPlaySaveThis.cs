using System.Collections;
using UnityEngine;

namespace VoxelPlay
{

    public class VoxelPlaySaveThis : MonoBehaviour {

        /// <summary>
        /// Path to the prefab (ie. "Worlds/Earth/Models/Deer").
        /// </summary>
        [Tooltip("The path to the prefab in a Resources folder")]
        public string prefabResourcesPath;

        VoxelPlayEnvironment env;
        Rigidbody rb;

        void Start() {
            if (!TryGetComponent(out rb) || rb.isKinematic) return;

            // If chunk is not rendered and have a rigidbody, wait until ready
            env = VoxelPlayEnvironment.instance;
            if (env == null) return;

            if (!env.GetChunk(transform.position, out VoxelChunk chunk, false) || !chunk.isRendered) {
                rb.isKinematic = true;
                StartCoroutine(WaitForChunk(chunk));
            }
        }

        IEnumerator WaitForChunk(VoxelChunk chunk) {
            while (chunk != null && !chunk.isRendered) {
                if (gameObject == null) yield break;
                yield return null;
            }
            if (rb != null) {
                rb.isKinematic = false;
            }
        }
    }
}

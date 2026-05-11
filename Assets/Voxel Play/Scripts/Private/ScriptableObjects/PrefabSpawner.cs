using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace VoxelPlay {

    [CreateAssetMenu(menuName = "Voxel Play/Detail Generators/Prefab Spawner", fileName = "PrefabSpawner", order = 103)]
    public class PrefabSpawner : VoxelPlayDetailGenerator {

        public float seed = 1;

        [Range(0, 1f)]
        public float spawnProbability = 0.02f;
        [Tooltip("Adds an offset to the placement position. The placement position will be just on the surface of the terrain. This offset could place the prefab above the terrain for example.")]
        public Vector3 spawnPositionOffset;
        public BiomeDefinition[] allowedBiomes;
        [Tooltip("Enable to allow placing prefab on water.")]
        public bool allowSpawnOnWater;
        [Tooltip("Enable to wait for the chunk collider to be present before spawning the prefab.")]
        public bool requireCollider;
        [Tooltip("Enable if the spawned prefab uses NavMesh Agent. This will wait until the chunk navmesh is available before instantiating the prefab.")]
        public bool requireNavMesh;
        public GameObject[] prefabs;

        public bool optimizeMaterial = true;

        VoxelPlayEnvironment env;
        Shader vpShader;

        /// <summary>
        /// Initialization method. Called by Voxel Play at startup.
        /// </summary>
        public override void Init() {
            vpShader = Shader.Find("Voxel Play/Models/Texture/Opaque");
            env = VoxelPlayEnvironment.instance;
            if (requireCollider && !env.enableColliders) {
                Debug.LogWarning($"PrefabSpawner {name} requires colliders but Voxel Play Environment collider option is disabled.");
            }
            if (requireNavMesh && !env.enableNavMesh) {
                Debug.LogWarning($"PrefabSpawner {name} requires NavMesh but Voxel Play Environment NavMesh option is disabled.");
            }
        }


        /// <summary>
        /// Fills the given chunk with detail. Filled voxels won't be replaced by the terrain generator.
        /// Use Voxel.Empty to fill with void.
        /// </summary>
        /// <param name="chunk">Chunk.</param>
        public override void AddDetail(VoxelChunk chunk) {

            if (prefabs == null || prefabs.Length == 0) return;
            Vector3d position = chunk.position;
            Vector3d rndPos = position;
            rndPos.x += seed;
            if (WorldRand.GetValue(rndPos) > spawnProbability) return;

            BiomeDefinition biome = env.GetBiome(position);
            if (allowedBiomes != null) {
                for (int k = 0; k < allowedBiomes.Length; k++) {
                    if (allowedBiomes[k] == biome) {
                        Vector3 spawnPosition = GetSpawnPosition(position);
                        if (!allowSpawnOnWater && env.IsWaterAtPosition(spawnPosition)) return;
                        if (requireCollider || requireNavMesh) {
                            SpawnPrefabAsync(spawnPosition);
                        } else {
                            SpawnPrefab(spawnPosition);
                        }
                        return;
                    }
                }
            }
        }

        Vector3 GetSpawnPosition(Vector3 position) {
            position.x += WorldRand.Range(0, VoxelPlayEnvironment.CHUNK_SIZE) - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            position.z += WorldRand.Range(0, VoxelPlayEnvironment.CHUNK_SIZE) - VoxelPlayEnvironment.CHUNK_HALF_SIZE;
            position.y = env.GetTerrainHeight(position);
            position += spawnPositionOffset;
            return position;
        }

        async void SpawnPrefabAsync(Vector3 position) {
            VoxelChunk chunk = null;
            bool canSpawn = true;
            if (requireCollider) {
                canSpawn = false;
                for (int k = 0; k < 10; k++) {
                    if (env.IsTerrainReadyAtPosition(position, false)) {
                        canSpawn = true;
                        break;
                    }
                    env.GetChunk(position, out chunk, true);
                    env.ChunkRedraw(chunk, refreshLightmap: false, refreshMesh: false, ignoreFrustum: true);
                    await Task.Delay(TimeSpan.FromSeconds(0.5f));
                    if (!env.initialized) return;
                }
            }
            if (requireNavMesh && canSpawn) {
                canSpawn = false;
                for (int k = 0; k < 20; k++) {
                    if (env.ChunkHasNavMeshReady(chunk)) {
                        if (NavMesh.SamplePosition(position, out NavMeshHit navMeshHit, 2f, NavMesh.AllAreas)) {
                            position = navMeshHit.position;
                            canSpawn = true;
                            break;
                        }
                    }
                    env.GetChunk(position, out chunk, true);
                    env.ChunkRedraw(chunk, refreshLightmap: false, refreshMesh: false, ignoreFrustum: true);
                    await Task.Delay(TimeSpan.FromSeconds(0.5f));
                    if (!env.initialized) return;
                }
            }
            if (canSpawn) {
                SpawnPrefab(position);
            }
        }

        void SpawnPrefab(Vector3d position) {

            int prefabIndex = WorldRand.Range(0, prefabs.Length);
            GameObject prefab = prefabs[prefabIndex];
            NavMeshAgent agent = null;
            bool isAgentEnabled = false;
            if (requireNavMesh) {
                agent = prefab.GetComponentInChildren<NavMeshAgent>();
                if (agent != null) {
                    isAgentEnabled = agent.enabled;
                }
                if (isAgentEnabled) {
                    agent.enabled = false;
                }
            }
            GameObject o = Instantiate(prefab);

            if (optimizeMaterial) {
                Renderer r = o.GetComponentInChildren<Renderer>();
                if (r != null) {
                    Material oldMat = r.sharedMaterial;
                    if (oldMat != null && !oldMat.shader.name.Contains("Voxel Play/Models")) {
                        if (vpShader != null) {
                            Material newMat = new Material(vpShader);
                            newMat.mainTexture = oldMat.mainTexture;
                            newMat.color = oldMat.color;
                            r.sharedMaterial = newMat;
                        }
                    }
                }
            }
            o.transform.position = position;

            if (agent != null) {
                if (isAgentEnabled) {
                    agent.enabled = true;
                    NavMeshAgent spawnedAgent = o.GetComponentInChildren<NavMeshAgent>();
                    if (spawnedAgent != null) {
                        spawnedAgent.enabled = true;
                    }
                }
            }

            VoxelPlayBehaviour bh = o.GetComponentInChildren<VoxelPlayBehaviour>();
            if (bh == null) {
                o.AddComponent<VoxelPlayBehaviour>();
            }

        }

    }

}

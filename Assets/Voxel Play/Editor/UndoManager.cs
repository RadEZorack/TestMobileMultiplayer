using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;


namespace VoxelPlay {

    public struct UndoChunkData {
        public Voxel[] voxels;
        public HeightMapInfo[] terrainInfo;
        public bool usesMicroVoxels;
        public Dictionary<int, MicroVoxels> microVoxels;
        public bool modified;
        public int modifiedTimestamp;
    }

    public class UndoSession {
        public Dictionary<VoxelChunk, UndoChunkData> chunks;

        public UndoSession () {
            chunks = new Dictionary<VoxelChunk, UndoChunkData>();
        }

        VoxelChunk lastAddedChunk;

        public void AddChunk (VoxelChunk chunk) {
            if (chunk == lastAddedChunk) return;
            if (chunks.ContainsKey(chunk)) return;
            lastAddedChunk = chunk;
            Voxel[] backupVoxels = new Voxel[chunk.voxels.Length];
            Array.Copy(chunk.voxels, backupVoxels, chunk.voxels.Length);
            HeightMapInfo[] backupTerrainInfo = null;
            if (chunk.terrainInfo != null) {
                backupTerrainInfo = new HeightMapInfo[chunk.terrainInfo.Length];
                Array.Copy(chunk.terrainInfo, backupTerrainInfo, chunk.terrainInfo.Length);
            }
            Dictionary<int, MicroVoxels> backupMicroVoxels = null;
            if (chunk.usesMicroVoxels) {
                backupMicroVoxels = new Dictionary<int, MicroVoxels>(chunk.microVoxels.Count);
                foreach (var kvp in chunk.microVoxels) {
                    MicroVoxels originalMicroVoxel = kvp.Value;
                    MicroVoxels copiedMicroVoxel = originalMicroVoxel.Clone();
                    backupMicroVoxels.Add(kvp.Key, copiedMicroVoxel);
                }
            }
            chunks.Add(chunk, new UndoChunkData {
                voxels = backupVoxels,
                terrainInfo = backupTerrainInfo,
                usesMicroVoxels = chunk.usesMicroVoxels,
                microVoxels = backupMicroVoxels,
                modified = chunk.modified,
                modifiedTimestamp = chunk.modifiedTimestamp
            });
        }
    }


    public class UndoManager : ScriptableObject {

        public static readonly List<UndoSession> undoStack = new List<UndoSession>();
        public int sessionIndex;

        UndoSession currentUndoSession = new UndoSession();
        [NonSerialized]
        public VoxelPlayEnvironment env;

        public void PerformUndo () {

            if (sessionIndex < 0 || sessionIndex >= undoStack.Count) return;

            var undoSession = undoStack[sessionIndex];

            // restore chunks in scene
            var chunkDataList = new List<KeyValuePair<VoxelChunk, UndoChunkData>>(undoSession.chunks);
            for (int i = chunkDataList.Count - 1; i >= 0; i--) {
                var chunkData = chunkDataList[i];
                VoxelChunk chunk = chunkData.Key;
                UndoChunkData undoData = chunkData.Value;
                RestoreChunk(chunk, undoData);
            }
        }

        public void RestoreChunk (VoxelChunk chunk, UndoChunkData undoData) {
            chunk.SetVoxels(undoData.voxels);
            if (chunk.terrainInfo != null && undoData.terrainInfo != null) {
                Array.Copy(undoData.terrainInfo, chunk.terrainInfo, chunk.terrainInfo.Length);
            }
            if (undoData.usesMicroVoxels) {
                chunk.usesMicroVoxels = true;
                if (chunk.microVoxels == null) {
                    chunk.microVoxels = new Dictionary<int, MicroVoxels>(undoData.microVoxels.Count);
                } else {
                    chunk.microVoxels.Clear();
                }
                foreach (var kvp in undoData.microVoxels) {
                    chunk.microVoxels[kvp.Key] = kvp.Value;
                }
            } else {
                chunk.microVoxels = null;
                chunk.usesMicroVoxels = false;
            }
            chunk.modified = undoData.modified;
            chunk.modifiedTimestamp = undoData.modifiedTimestamp;
            env.ChunkRedraw(chunk, includeNeighbours: true, refreshLightmap: true, refreshMesh: true);
        }


        public void StartChangeGroup () {
            currentUndoSession = new UndoSession();
        }

        public void SaveChunk (VoxelChunk chunk) {
            currentUndoSession.AddChunk(chunk);
            env.RegisterChunkChanges(chunk);
        }

        public void EndChangeGroup () {
            if (currentUndoSession.chunks.Count == 0) return;

            if (sessionIndex < undoStack.Count) {
                undoStack[sessionIndex] = currentUndoSession;
                undoStack.RemoveRange(sessionIndex + 1, undoStack.Count - (sessionIndex + 1));
            } else {
                undoStack.Add(currentUndoSession);
            }

            Undo.RecordObject(this, "World Editor Tool");
            sessionIndex++;

            // in order to support redo, we need to store the chunk state after the modifications
            UndoSession redoUndoSession = new UndoSession();
            foreach (var kvp in currentUndoSession.chunks) {
                redoUndoSession.AddChunk(kvp.Key);
            }
            undoStack.Insert(sessionIndex, redoUndoSession);
        }
    }

}

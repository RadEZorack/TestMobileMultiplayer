using System;
using UnityEngine;

namespace VoxelPlay {

    [Serializable]
    public class TerrainGraphEditorSessionState : ScriptableObject {
        public TerrainDefaultGenerator generator;
        public bool hasUnsavedSnapshot;
        public TerrainGraphView.GraphSnapshot snapshot;
        public bool hasGeneratorBackup;
        public bool generatorBackupDirty;
        public string generatorBackupJson;
    }
}

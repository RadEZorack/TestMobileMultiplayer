using UnityEngine;
using UnityEditor;

namespace VoxelPlay {

    public class WorldEditorToolLevelTerrain : WorldEditorToolElevateTerrain {

        public override Texture2D icon => Resources.Load<Texture2D>("VoxelPlay/WorldEditorIcons/toolLevel");
        public override string title => "Level terrain";
        public override string instructions => "Raise terrain to specified altitude.\nHold shift to lower terrain.";
        public override int priority => 2;
        public override WorldEditorToolCategory category => WorldEditorToolCategory.TerrainTool;

        public WorldEditorToolLevelTerrain () : base() {
            clampAltitude = true;
        }

        public override void DrawInspector () {
            base.DrawInspector();
            env.sceneEditorUseCenterVoxelAltitude = EditorGUILayout.Toggle("Use Center Voxel Altitude", env.sceneEditorUseCenterVoxelAltitude);
            if (!env.sceneEditorUseCenterVoxelAltitude) {
                env.sceneEditorAltitude = EditorGUILayout.IntField("Altitude", env.sceneEditorAltitude);
            }

            
        }

    }

}
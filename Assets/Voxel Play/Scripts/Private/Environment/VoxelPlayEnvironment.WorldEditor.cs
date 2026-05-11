using System;
using UnityEngine;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

#if UNITY_EDITOR
        public int worldManagementSelectedTool;
        public int sceneEditorBrushSize = 1;
        public float sceneEditorBrushStrength = 0.5f;
        public Texture2D sceneEditorBrushShape;
        public int sceneEditorAltitude = 32;
        public bool sceneEditorUseCenterVoxelAltitude = true;
        public bool sceneEditorBuildIgnoreWater = true;
        public VoxelDefinition sceneEditorVoxelDefinition;
        [ColorUsage(showAlpha: true, hdr: true)]
        public Color sceneEditorTintColor = Misc.colorWhite;
        public int sceneEditorRiverDepth = 3;
        public VoxelDefinition sceneEditorRiverWaterVoxel, sceneEditorRiverShoreVoxel;
        public bool sceneEditorRiverAddShore = true;
        public VoxelDefinition sceneEditorBuildVoxel;
        public ModelDefinition sceneEditorModelDefinition;
        public int sceneEditorPlacementRotation;
        [Tooltip("Repeats the bottom voxel to ensure no gap with underneath terrain")]
        public bool sceneEditorPlacementFitTerrain;
        public bool sceneEditorModelAsGameObject;
        public Vector3 sceneEditorModelGameObjectScale = Misc.vector3one;
        public bool sceneEditorBrushContinuousMode = true;
        public float sceneEditorBrushSpeed = 0.9f;
        public int sceneEditorBuildMaxLength = 10;
        public int sceneEditorBrushMicroVoxelSize;
        public Vector3Int sceneEditorCaptureSize = new Vector3Int(5, 5, 5);
        public Vector3Int sceneEditorCaptureOffset;
        public string sceneEditorCaptureFileName = "capture";
        public string sceneEditorCaptureVoxelDefinitionName = "voxelDefinition";
        public string sceneEditorMicroVoxelsDefinitionName = "microVoxels";
        public bool sceneEditorCaptureVoxelTintColor = true;
        public bool sceneEditorAutomaticBackup;
        public Vector3 sceneEditorCameraMainPosition;
        public Vector3 sceneEditorCameraSceneViewPosition;
        public int sceneEditorBrushSizeX = 5;
        public int sceneEditorBrushSizeY = 5;
        public int sceneEditorBrushSizeZ = 5;
        public int sceneEditorWallThickness = 1;
        public float sceneEditorBrushHeightOffset;
        public VoxelDefinition sceneEditorRoomFloorVoxel;
        public VoxelDefinition sceneEditorRoomCeilingVoxel;
        public bool sceneEditorEnableEntrances;
        public int sceneEditorRoomEntranceWidth = 2;
        public int sceneEditorRoomEntranceHeight = 2;
        public readonly bool[] sceneEditorRoomWallEntrances = new bool[4]; // Front, Back, Left, Right
        public bool sceneEditorRoomClearInterior = true;
        public bool sceneEditorAddCrenelationOnTop = true;
        public VoxelDefinition sceneEditorCrenelationVoxel;
		public BiomeDefinition sceneEditorBiomeDefinition;
		public bool sceneEditorBiomeIncludeVegetation = true;
		public bool sceneEditorBiomeIncludeTrees = true;
		public int sceneEditorBiomeDepth = 8;
		public VoxelDefinition sceneEditorElevateVoxelSurface;
		public VoxelDefinition sceneEditorElevateVoxelFill;
		public int sceneEditorPaintDepth;
		public VoxelDefinition sceneEditorReplaceSourceVoxel;
		public bool sceneEditorPaintIgnoreVegetation = true;
#endif

    }
}


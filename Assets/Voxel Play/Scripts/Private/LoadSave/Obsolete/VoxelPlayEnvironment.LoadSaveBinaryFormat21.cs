using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        void LoadGameBinaryFileFormat_21 (BinaryReader br, bool singleFile, bool preservePlayerPosition = false, VoxelDefinition fallbackVoxelDefinition = null) {
            // Character controller transform position & rotation
            Vector3 pos = DecodeVector3Binary(br);
            Vector3 characterRotationAngles = DecodeVector3Binary(br);
            Vector3 cameraLocalRotationAngles = DecodeVector3Binary(br);
            if (!preservePlayerPosition) {
                if ((UnityEngine.Object)characterController != null) {
                    characterController.MoveTo(pos);
                    characterController.transform.rotation = Quaternion.Euler(characterRotationAngles);
                    cameraMain.transform.localRotation = Quaternion.Euler(cameraLocalRotationAngles);
                    characterController.UpdateLook();
                }
            }
            stage = br.ReadInt16();
            string regionsIds = br.ReadString();

            // Dynamic loading support
            if (enableDynamicLoad && !singleFile && !string.IsNullOrEmpty(regionsIds)) {
                // Store available regions for dynamic loading
                foreach (var regionId in regionsIds.Split(',')) {
                    if (RegionPartitioner.TryGetRegionIdFromString(regionId, out int id)) {
                        availableRegionIds.Add(id);
                    }
                }
                loadedRegionIds.Clear();

                // Only load the extra data immediately, regions will be loaded on demand
                using (Stream extraStream = GetSaveGameStream("extra")) {
                    if (extraStream != null) {
                        using (BinaryReader brExtra = new BinaryReader(extraStream, Encoding.UTF8)) {
                            LoadGameExtraDataBinaryFormat_21(brExtra);
                        }
                    }
                }
            } else {
                // Normal loading mode - load everything immediately
                if (singleFile) {
                    LoadGameRegionBinaryFormat_21(br);
                    LoadGameExtraDataBinaryFormat_21(br);
                } else {
                    if (!string.IsNullOrEmpty(regionsIds)) {
                        string[] regionIds = regionsIds.Split(',');
                        foreach (var regionId in regionIds) {
                            using (Stream regionStream = GetSaveGameStream(regionId)) {
                                if (regionStream == null) {
                                    ShowError($"Region file {regionId} not found");
                                    continue;
                                }
                                using (BinaryReader brRegion = new BinaryReader(regionStream, Encoding.UTF8)) {
                                    LoadGameRegionBinaryFormat_21(brRegion);
                                }
                            }
                        }
                    }

                    {
                        using (Stream extraStream = GetSaveGameStream("extra")) {
                            if (extraStream != null) {
                                using (BinaryReader brExtra = new BinaryReader(extraStream, Encoding.UTF8)) {
                                    LoadGameExtraDataBinaryFormat_21(brExtra);
                                }
                            }
                        }
                    }
                }
            }
        }

        void LoadGameRegionBinaryFormat_21 (BinaryReader br) {

            InitSaveGameStructs();

            // Read voxel definition table
            int vdCount = br.ReadInt16();
            if (saveVoxelDefinitionsList.Capacity < vdCount) {
                saveVoxelDefinitionsList.Capacity = vdCount;
            }
            for (int k = 0; k < vdCount; k++) {
                VoxelDefinition vd = GetVoxelDefinition(br.ReadString());
                if (VoxelDefinition.IsNull(vd) && fallbackVoxelDefinition != null) {
                    saveVoxelDefinitionsList.Add(fallbackVoxelDefinition);
                } else {
                    saveVoxelDefinitionsList.Add(vd);
                }
            }

            // Read item definition table
            int idCount = br.ReadInt16();
            if (saveItemDefinitionsList.Capacity < idCount) {
                saveItemDefinitionsList.Capacity = idCount;
            }
            for (int k = 0; k < idCount; k++) {
                saveItemDefinitionsList.Add(br.ReadString());
            }

            // Read chunks
            int numChunks = br.ReadInt32();
            VoxelDefinition voxelDefinition = defaultVoxel;
            int prevVdIndex = -1;
            Color32 voxelColor = Misc.color32White;
            for (int c = 0; c < numChunks; c++) {
                // Get chunk position
                Vector3d chunkPosition = DecodeVector3Binary(br).ToVector3d();
                VoxelChunk chunk = GetChunkUnpopulated(chunkPosition);
                byte isAboveSurface = br.ReadByte();
                chunk.isAboveSurface = isAboveSurface == 1;
                byte isPopulated = br.ReadByte();
                chunk.isPopulated = isPopulated == 1;
                chunk.allowTrees = false;
                chunk.modified = true;
                chunk.modifiedTimestamp = br.ReadInt16();
                chunk.voxelSignature = -1;
                chunk.renderState = ChunkRenderState.Pending;
                chunk.usesMicroVoxels = false;
                SetChunkOctreeIsDirty(chunkPosition, false);
                ChunkClearFast(chunk);

                // Read voxels
                int numWords = br.ReadInt16();
                for (int k = 0; k < numWords; k++) {
                    // Voxel definition
                    int vdIndex = br.ReadInt16();
                    if (prevVdIndex != vdIndex) {
                        if (vdIndex >= 0 && vdIndex < vdCount) {
                            voxelDefinition = saveVoxelDefinitionsList[vdIndex];
                            prevVdIndex = vdIndex;
                        }
                    }
                    // RGB
                    voxelColor.r = br.ReadByte();
                    voxelColor.g = br.ReadByte();
                    voxelColor.b = br.ReadByte();
                    // Voxel index
                    int voxelIndex = br.ReadInt16();
                    // Repetitions
                    int repetitions = br.ReadInt16();
                    // Flags (rotation and water level)
                    byte flags = br.ReadByte();

                    if (voxelDefinition == null) {
                        continue;
                    }

                    for (int i = 0; i < repetitions; i++) {
                        chunk.SetVoxel(voxelIndex + i, voxelDefinition, voxelColor);
                        chunk.voxels[voxelIndex + i].SetFlags(flags);
                    }
                }

                // Light sources
                int lightCount = br.ReadInt16();
                VoxelHitInfo hitInfo = new VoxelHitInfo();
                for (int k = 0; k < lightCount; k++) {
                    // Voxel index
                    hitInfo.voxelIndex = br.ReadInt16();
                    // Voxel center
                    hitInfo.voxelCenter = GetVoxelPosition(chunkPosition, hitInfo.voxelIndex);
                    // Normal
                    hitInfo.normal = DecodeVector3Binary(br);
                    hitInfo.chunk = chunk;
                    // Item definition
                    int itemIndex = br.ReadInt16();
                    if (itemIndex < 0 || itemIndex >= idCount)
                        continue;
                    string itemDefinitionName = saveItemDefinitionsList[itemIndex];
                    ItemDefinition itemDefinition = GetItemDefinition(itemDefinitionName);
                    TorchAttach(hitInfo, itemDefinition);
                }

                // Read items
                int itemCount = br.ReadInt16();
                for (int k = 0; k < itemCount; k++) {
                    // Voxel index
                    int itemIndex = br.ReadInt16();
                    if (itemIndex < 0 || itemIndex >= idCount)
                        continue;
                    string itemDefinitionName = saveItemDefinitionsList[itemIndex];
                    Vector3d itemPosition = DecodeVector3Binary(br).ToVector3d();
                    float quantity = br.ReadSingle();
                    ItemSpawn(itemDefinitionName, itemPosition, quantity);
                }

                // Load custom voxel properties
                if (chunk.voxelsProperties == null) {
                    chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
                }
                int voxelsPropertiesCount = br.ReadInt16();
                for (int k = 0; k < voxelsPropertiesCount; k++) {
                    int voxelIndex = br.ReadInt16();
                    int voxelPropertiesCount = br.ReadInt16();
                    if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                        voxelProperties = new FastHashSet<VoxelProperty>();
                        chunk.voxelsProperties[voxelIndex] = voxelProperties;
                    }
                    for (int i = 0; i < voxelPropertiesCount; i++) {
                        int propId = br.ReadInt32();
                        VoxelProperty prop;
                        prop.floatValue = br.ReadSingle();
                        prop.stringValue = br.ReadString();
                        voxelProperties[propId] = prop;
                    }
                }

                // Read microvoxels
                int mvCount = br.ReadInt16();
                if (mvCount > 0) {
                    if (chunk.microVoxels == null) {
                        chunk.microVoxels = new Dictionary<int, MicroVoxels>();
                    }
                    chunk.usesMicroVoxels = true;
                    for (int k = 0; k < mvCount; k++) {
                        int voxelIndex = br.ReadInt16();
                        MicroVoxels mv = new MicroVoxels();
                        mv.ReadFromBinaryReader(br);
                        mv.layout = (MicroVoxelLayout)br.ReadByte();
                        int secondaryTypeIndex = br.ReadInt16();
                        if (secondaryTypeIndex >= 0 && secondaryTypeIndex < vdCount) {
                            mv.secondaryType = saveVoxelDefinitionsList[secondaryTypeIndex];
                        } else {
                            mv.secondaryType = null;
                            mv.layout = MicroVoxelLayout.Default;
                        }
                        if (voxelIndex >= 0) {
                            chunk.microVoxels[voxelIndex] = mv;
                            byte newOpaque = mv.GetOpaqueProportional();
                            chunk.voxels[voxelIndex].opaque = newOpaque;
                        }
                    }
                }
            }

            // Read VoxelPlaySaveThis gameobjects from region file
            int goCount = br.ReadInt16();
            Dictionary<string, string> data = new Dictionary<string, string>();
            for (int k = 0; k < goCount; k++) {
                string prefabPath = br.ReadString();
                string goName = br.ReadString();
                Vector3 goPosition = DecodeVector3Binary(br);
                Vector3 goAngles = DecodeVector3Binary(br);
                Vector3 goScale = DecodeVector3Binary(br);
                data.Clear();
                Int16 dataCount = br.ReadInt16();
                for (int j = 0; j < dataCount; j++) {
                    string key = br.ReadString();
                    string value = br.ReadString();
                    data[key] = value;
                }

                GameObject o = Resources.Load<GameObject>(prefabPath);
                if (o != null) {
                    o = Instantiate(o, goPosition, Quaternion.Euler(goAngles));
                    if (!o.TryGetComponent(out VoxelPlaySaveThis go)) {
                        DestroyImmediate(o);
                        continue;
                    }
                    o.name = goName;
                    o.transform.localScale = goScale;
                    go.SendMessage("OnLoadGame", data, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        void LoadGameExtraDataBinaryFormat_21 (BinaryReader br) {

            // Read number of custom sections
            int sectionsCount = br.ReadInt16();
            for (int k = 0; k < sectionsCount; k++) {
                string sectionName = br.ReadString();
                int length = br.ReadInt32();
                byte[] sectionData = br.ReadBytes(length);
                if (OnLoadCustomGameData != null) {
                    sectionData = SaveGameCustomDataWriter.Decompress(sectionData);
                    OnLoadCustomGameData(sectionName, sectionData);
                }
            }

        }
    }
}


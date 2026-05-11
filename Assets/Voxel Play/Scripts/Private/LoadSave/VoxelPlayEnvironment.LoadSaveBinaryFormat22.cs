using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        void LoadGameBinaryFileFormat_22 (BinaryReader br, bool singleFile, bool preservePlayerPosition = false, VoxelDefinition fallbackVoxelDefinition = null) {
            // Header compatible with 21
            Vector3 pos = DecodeVector3Binary(br);
            Vector3 characterRotationAngles = DecodeVector3Binary(br);
            Vector3 cameraLocalRotationAngles = DecodeVector3Binary(br);
            if (!preservePlayerPosition) {
                if ((UnityEngine.Object)characterController != null) {
                    characterController.MoveTo(pos);
                    // Normalize to yaw-on-character, pitch-on-camera. Strips roll/yaw leaks that would desync WASD direction from the camera.
                    Vector3 fwd = (Quaternion.Euler(characterRotationAngles) * Quaternion.Euler(cameraLocalRotationAngles)) * Vector3.forward;
                    float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
                    characterController.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    if (cameraMain != null) {
                        cameraMain.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    }
                    characterController.UpdateLook();
                }
            }
            stage = br.ReadInt16();
            string regionsIds = br.ReadString();

            if (enableDynamicLoad && !singleFile && !string.IsNullOrEmpty(regionsIds)) {
                foreach (var regionId in regionsIds.Split(',')) {
                    if (RegionPartitioner.TryGetRegionIdFromString(regionId, out int id)) {
                        availableRegionIds.Add(id);
                    }
                }
                loadedRegionIds.Clear();

                using (Stream extraStream = GetSaveGameStream("extra")) {
                    if (extraStream != null) {
                        using (BinaryReader brExtra = new BinaryReader(extraStream, Encoding.UTF8)) {
                            LoadGameExtraDataBinaryFormat_22(brExtra);
                        }
                    }
                }
            } else {
                if (singleFile) {
                    LoadGameRegionBinaryFormat_22(br);
                    LoadGameExtraDataBinaryFormat_22(br);
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
                                    LoadGameRegionBinaryFormat_22(brRegion);
                                }
                            }
                        }
                    }
                    using (Stream extraStream = GetSaveGameStream("extra")) {
                        if (extraStream != null) {
                            using (BinaryReader brExtra = new BinaryReader(extraStream, Encoding.UTF8)) {
                                LoadGameExtraDataBinaryFormat_22(brExtra);
                            }
                        }
                    }
                }
            }
        }

        void LoadGameExtraDataBinaryFormat_22 (BinaryReader br) {
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

        void LoadGameRegionBinaryFormat_22 (BinaryReader br) {
            InitSaveGameStructs();

            // voxel def table
            int vdCount = br.ReadInt16();
            if (saveVoxelDefinitionsList.Capacity < vdCount) saveVoxelDefinitionsList.Capacity = vdCount;
            for (int k = 0; k < vdCount; k++) {
                VoxelDefinition vd = GetVoxelDefinition(br.ReadString());
                if (VoxelDefinition.IsNull(vd) && fallbackVoxelDefinition != null) {
                    saveVoxelDefinitionsList.Add(fallbackVoxelDefinition);
                } else {
                    saveVoxelDefinitionsList.Add(vd);
                }
            }

            // item def table
            int idCount = br.ReadInt16();
            if (saveItemDefinitionsList.Capacity < idCount) saveItemDefinitionsList.Capacity = idCount;
            for (int k = 0; k < idCount; k++) saveItemDefinitionsList.Add(br.ReadString());

            // chunks
            int numChunks = br.ReadInt32();
            VoxelDefinition voxelDefinition = defaultVoxel;
            int prevVdIndex = -1;
            Color32 voxelColor = Misc.color32White;
            for (int c = 0; c < numChunks; c++) {
                Vector3d chunkPosition = DecodeVector3Binary(br).ToVector3d();
                VoxelChunk chunk = GetChunkUnpopulated(chunkPosition);
                chunk.isAboveSurface = br.ReadByte() == 1;
                chunk.isPopulated = br.ReadByte() == 1;
                chunk.allowTrees = false;
                chunk.modified = true;
                chunk.modifiedTimestamp = br.ReadInt16();
                chunk.voxelSignature = -1;
                chunk.renderState = ChunkRenderState.Pending;
                chunk.usesMicroVoxels = false;
                SetChunkOctreeIsDirty(chunkPosition, false);
                ChunkClearFast(chunk);

                // voxels
                int numWords = br.ReadInt16();
                for (int k = 0; k < numWords; k++) {
                    int vdIndex = br.ReadInt16();
                    if (prevVdIndex != vdIndex) {
                        if (vdIndex >= 0 && vdIndex < vdCount) { voxelDefinition = saveVoxelDefinitionsList[vdIndex]; prevVdIndex = vdIndex; }
                    }
                    voxelColor.r = br.ReadByte();
                    voxelColor.g = br.ReadByte();
                    voxelColor.b = br.ReadByte();
                    int voxelIndex = br.ReadInt16();
                    int repetitions = br.ReadInt16();
                    byte flags = br.ReadByte();
                    if (voxelDefinition == null) continue;
                    for (int i = 0; i < repetitions; i++) {
                        chunk.SetVoxel(voxelIndex + i, voxelDefinition, voxelColor);
                        chunk.voxels[voxelIndex + i].SetFlags(flags);
                    }
                }

                // lights
                int lightCount = br.ReadInt16();
                VoxelHitInfo hitInfo = new VoxelHitInfo();
                for (int k = 0; k < lightCount; k++) {
                    hitInfo.voxelIndex = br.ReadInt16();
                    hitInfo.voxelCenter = GetVoxelPosition(chunkPosition, hitInfo.voxelIndex);
                    hitInfo.normal = DecodeVector3Binary(br);
                    hitInfo.chunk = chunk;
                    int itemIndex = br.ReadInt16();
                    if (itemIndex < 0 || itemIndex >= idCount) continue;
                    string itemDefinitionName = saveItemDefinitionsList[itemIndex];
                    ItemDefinition itemDefinition = GetItemDefinition(itemDefinitionName);
                    TorchAttach(hitInfo, itemDefinition);
                }

                // items
                int itemCount = br.ReadInt16();
                for (int k = 0; k < itemCount; k++) {
                    int itemIndex = br.ReadInt16();
                    Vector3d itemPosition = DecodeVector3Binary(br).ToVector3d();
                    float quantity = br.ReadSingle();
                    if (itemIndex < 0 || itemIndex >= idCount) continue;
                    string itemDefinitionName = saveItemDefinitionsList[itemIndex];
                    ItemSpawn(itemDefinitionName, itemPosition, quantity);
                }

                // properties
                if (chunk.voxelsProperties == null) chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
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
                        VoxelProperty prop; prop.floatValue = br.ReadSingle(); prop.stringValue = br.ReadString();
                        voxelProperties[propId] = prop;
                    }
                }

                // microvoxels palette
                int shapeCount = br.ReadInt16();
                List<MicroVoxels> shapes = null;
                if (shapeCount > 0) {
                    shapes = new List<MicroVoxels>(shapeCount);
                    for (int i = 0; i < shapeCount; i++) {
                        MicroVoxels mv = new MicroVoxels();
                        mv.ReadFromBinaryReader(br);
                        mv.layout = (MicroVoxelLayout)br.ReadByte();
                        int secIdx = br.ReadInt16();
                        mv.secondaryType = (secIdx >= 0 && secIdx < vdCount) ? saveVoxelDefinitionsList[secIdx] : null;
                        mv.isShared = true;
                        shapes.Add(mv);
                    }
                }

                // microvoxel instances
                int instCount = br.ReadInt16();
                if (instCount > 0) {
                    if (chunk.microVoxels == null) chunk.microVoxels = new Dictionary<int, MicroVoxels>();
                    chunk.usesMicroVoxels = true;
                    for (int i = 0; i < instCount; i++) {
                        int voxelIndex = br.ReadInt16();
                        int shapeIdx = br.ReadInt16();
                        MicroVoxels mv = (shapeIdx >= 0 && shapeIdx < (shapes?.Count ?? 0)) ? shapes[shapeIdx] : null;
                        if (mv != null) {
                            chunk.microVoxels[voxelIndex] = mv;
                            chunk.voxels[voxelIndex].opaque = mv.GetOpaqueProportional();
                        }
                    }
                }
            }

            // extra objects
            int goCount = br.ReadInt16();
            Dictionary<string, string> data = new Dictionary<string, string>();
            for (int k = 0; k < goCount; k++) {
                string prefabPath = br.ReadString();
                string goName = br.ReadString();
                Vector3 goPosition = DecodeVector3Binary(br);
                Vector3 goAngles = DecodeVector3Binary(br);
                Vector3 goScale = DecodeVector3Binary(br);
                data.Clear();
                short dataCount = br.ReadInt16();
                for (int j = 0; j < dataCount; j++) { string key = br.ReadString(); string value = br.ReadString(); data[key] = value; }
                GameObject o = Resources.Load<GameObject>(prefabPath);
                if (o != null) {
                    o = Instantiate(o, goPosition, Quaternion.Euler(goAngles));
                    if (!o.TryGetComponent(out VoxelPlaySaveThis go)) { DestroyImmediate(o); continue; }
                    o.name = goName; o.transform.localScale = goScale; go.SendMessage("OnLoadGame", data, SendMessageOptions.DontRequireReceiver);
                }
            }
        }


        void SaveGameBinaryFormat (BinaryWriter bw) {
            SaveGameHeaderBinaryFormat(bw, "");
            SaveGameChunksBinaryFormat(bw, GetChunks(ChunkModifiedFilter.OnlyModified));
            SaveGameExtraDataBinaryFormat(bw);
        }

        void SaveGameHeaderBinaryFormat (BinaryWriter bw, string regionIds) {

            // Header
            bw.Write(SAVE_FILE_CURRENT_FORMAT);
            bw.Write((byte)CHUNK_SIZE);
            // Character controller transform position + normalized look angles (yaw on character, pitch on camera). Avoids propagating roll/yaw leaks from the camera transform.
            if ((UnityEngine.Object)characterController != null) {
                EncodeVector3Binary(bw, characterController.transform.position);
                Quaternion camLocal = cameraMain != null ? cameraMain.transform.localRotation : Quaternion.identity;
                Vector3 fwd = (characterController.transform.rotation * camLocal) * Vector3.forward;
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
                EncodeVector3Binary(bw, new Vector3(0f, yaw, 0f));
                if (cameraMain != null) {
                    EncodeVector3Binary(bw, new Vector3(pitch, 0f, 0f));
                } else {
                    EncodeVector3Binary(bw, Misc.vector3zero);
                }
            } else {
                EncodeVector3Binary(bw, Misc.vector3zero);
                EncodeVector3Binary(bw, Misc.vector3zero);
                EncodeVector3Binary(bw, Misc.vector3zero);
            }
            stage++;
            bw.Write((Int16)stage);
            bw.Write(regionIds);
        }

        void SaveGameExtraDataBinaryFormat (BinaryWriter bw) {
            // Custom sections
            SaveGameCustomDataWriter customDataWriter = new SaveGameCustomDataWriter();
            if (OnSaveCustomGameData != null) {
                OnSaveCustomGameData(customDataWriter);
            }
            customDataWriter.Flush(bw);
        }

        void SaveGameChunksBinaryFormat (BinaryWriter bw, List<VoxelChunk> chunks, Bounds regionBounds = default) {

            // Build a table with all voxel definitions used in modified chunks
            int voxelDefinitionsCount = 0;
            int itemDefinitionsCount = 0;
            int numChunks = 0;
            InitSaveGameStructs();

            // Pack used voxel and item definitions
            foreach (var chunk in chunks) {
                numChunks++;
                VoxelDefinition last = null;
                for (int k = 0; k < CHUNK_VOXEL_COUNT; k++) {
                    VoxelDefinition vd = chunk.voxels[k].type;
                    if (vd == null || vd == last || vd.isDynamic || vd.doNotSave)
                        continue;
                    last = vd;
                    if (saveVoxelDefinitionsDict.TryAdd(vd, voxelDefinitionsCount)) {
                        saveVoxelDefinitionsList.Add(vd);
                        voxelDefinitionsCount++;
                    }
                }
                if (chunk.microVoxels != null) {
                    foreach (var mv in chunk.microVoxels) {
                        VoxelDefinition vd = mv.Value.secondaryType;
                        if (vd == null) continue;
                        if (saveVoxelDefinitionsDict.TryAdd(vd, voxelDefinitionsCount)) {
                            saveVoxelDefinitionsList.Add(vd);
                            voxelDefinitionsCount++;
                        }
                    }
                }
                if (chunk.items != null) {
                    ItemDefinition lastItem = null;
                    for (int k = 0; k < chunk.items.count; k++) {
                        Item item = chunk.items.values[k];
                        if (item == null)
                            continue;
                        ItemDefinition id = item.itemDefinition;
                        if (id == null || id == lastItem)
                            continue;
                        lastItem = id;
                        if (!saveItemDefinitionsDict.ContainsKey(id)) {
                            saveItemDefinitionsDict[id] = itemDefinitionsCount++;
                            saveItemDefinitionsList.Add(id.name);
                        }
                    }
                }
                if (chunk.lightSources != null) {
                    ItemDefinition lastItem = null;
                    for (int k = 0; k < chunk.lightSources.Count; k++) {
                        ItemDefinition id = chunk.lightSources[k].itemDefinition;
                        if (id == null || id == lastItem)
                            continue;
                        lastItem = id;
                        if (!saveItemDefinitionsDict.ContainsKey(id)) {
                            saveItemDefinitionsDict[id] = itemDefinitionsCount++;
                            saveItemDefinitionsList.Add(id.name);
                        }
                    }
                }
            }

            // Add voxel definitions table
            int vdCount = saveVoxelDefinitionsList.Count;
            bw.Write((Int16)vdCount);
            for (int k = 0; k < vdCount; k++) {
                bw.Write(saveVoxelDefinitionsList[k].name);
            }

            // Add item definitions table
            int idCount = saveItemDefinitionsList.Count;
            bw.Write((Int16)idCount);
            for (int k = 0; k < idCount; k++) {
                bw.Write(saveItemDefinitionsList[k]);
            }

            // Add modified chunks
            bw.Write(numChunks);
            foreach (var chunk in chunks) {
                ToggleHiddenVoxels(chunk, true);
                WriteChunkData(bw, chunk);
                ToggleHiddenVoxels(chunk, false);
            }

            // Add VoxelPlaySaveThis gameobjects within region bounds (only if bounds are specified)
            if (regionBounds.size != Vector3.zero) {
                VoxelPlaySaveThis[] allGos = Misc.FindObjectsOfType<VoxelPlaySaveThis>();
                List<VoxelPlaySaveThis> regionGos = new List<VoxelPlaySaveThis>();

                // Filter objects within region bounds
                foreach (var go in allGos) {
                    if (regionBounds.Contains(go.transform.position)) {
                        regionGos.Add(go);
                    }
                }

                bw.Write((Int16)regionGos.Count);
                Dictionary<string, string> data = new Dictionary<string, string>();
                for (int k = 0; k < regionGos.Count; k++) {
                    VoxelPlaySaveThis go = regionGos[k];
                    if (string.IsNullOrEmpty(go.prefabResourcesPath)) {
                        go.prefabResourcesPath = "";
                    }
                    bw.Write(go.prefabResourcesPath);
                    bw.Write(go.name);
                    EncodeVector3Binary(bw, go.transform.position);
                    EncodeVector3Binary(bw, go.transform.eulerAngles);
                    EncodeVector3Binary(bw, go.transform.localScale);
                    data.Clear();
                    go.SendMessage("OnSaveGame", data, SendMessageOptions.DontRequireReceiver);
                    Int16 dataCount = (Int16)data.Count;
                    bw.Write(dataCount);
                    foreach (KeyValuePair<string, string> entry in data) {
                        bw.Write(entry.Key);
                        bw.Write(entry.Value);
                    }
                }
            } else {
                // Write 0 gameobjects if no bounds specified (backward compatibility)
                bw.Write((Int16)0);
            }
        }

        void WriteChunkData (BinaryWriter bw, VoxelChunk chunk) {
            // Chunk position
            EncodeVector3Binary(bw, chunk.position.vector3);
            // Is above surface?
            bw.Write(chunk.isAboveSurface ? (byte)1 : (byte)0);
            // Is populated?
            bw.Write(chunk.isPopulated ? (byte)1 : (byte)0);
            // Modified timestamp
            bw.Write((Int16)chunk.modifiedTimestamp);

            int voxelDefinitionIndex = 0;
            VoxelDefinition prevVD = null;

            // Count voxels words
            int k = 0;
            int numWords = 0;
            while (k < chunk.voxels.Length) {
                if (chunk.voxels[k].typeIndex > 0) {
                    VoxelDefinition voxelDefinition = chunk.voxels[k].type;
                    if (voxelDefinition.isDynamic || voxelDefinition.doNotSave) {
                        k++;
                        continue;
                    }
                    if (voxelDefinition != prevVD) {
                        if (!saveVoxelDefinitionsDict.TryGetValue(voxelDefinition, out voxelDefinitionIndex)) {
                            k++;
                            continue;
                        }
                        prevVD = voxelDefinition;
                    }
                    Color32 tintColor = chunk.voxels[k].color;
                    int flags = chunk.voxels[k].GetFlags();
                    k++;
                    while (k < chunk.voxels.Length &&
                           chunk.voxels[k].type == voxelDefinition &&
                           chunk.voxels[k].color.r == tintColor.r &&
                           chunk.voxels[k].color.g == tintColor.g &&
                           chunk.voxels[k].color.b == tintColor.b &&
                           voxelDefinition.renderType != RenderType.Custom &&
                           chunk.voxels[k].GetFlags() == flags) {
                        k++;
                    }
                    numWords++;
                } else {
                    k++;
                }
            }
            bw.Write((Int16)numWords);

            // Write voxels
            k = 0;
            while (k < chunk.voxels.Length) {
                if (chunk.voxels[k].typeIndex > 0) {
                    int voxelIndex = k;
                    VoxelDefinition voxelDefinition = chunk.voxels[k].type;
                    if (voxelDefinition.isDynamic || voxelDefinition.doNotSave) {
                        k++;
                        continue;
                    }
                    if (voxelDefinition != prevVD) {
                        if (!saveVoxelDefinitionsDict.TryGetValue(voxelDefinition, out voxelDefinitionIndex)) {
                            k++;
                            continue;
                        }
                        prevVD = voxelDefinition;
                    }
                    Color32 tintColor = chunk.voxels[k].color;
                    byte flags = chunk.voxels[k].GetFlags();
                    int repetitions = 1;
                    k++;
                    while (k < chunk.voxels.Length &&
                           chunk.voxels[k].type == voxelDefinition &&
                           chunk.voxels[k].color.r == tintColor.r &&
                           chunk.voxels[k].color.g == tintColor.g &&
                           chunk.voxels[k].color.b == tintColor.b &&
                           voxelDefinition.renderType != RenderType.Custom &&
                           chunk.voxels[k].GetFlags() == flags) {
                        repetitions++;
                        k++;
                    }
                    bw.Write((Int16)voxelDefinitionIndex);
                    bw.Write(tintColor.r);
                    bw.Write(tintColor.g);
                    bw.Write(tintColor.b);
                    bw.Write((Int16)voxelIndex);
                    bw.Write((Int16)repetitions);
                    bw.Write(flags);
                } else {
                    k++;
                }
            }

            // Write light sources
            int lightCount = chunk.lightSources != null ? chunk.lightSources.Count : 0;
            bw.Write((Int16)lightCount);
            for (int j = 0; j < lightCount; j++) {
                LightSource lightSource = chunk.lightSources[j];
                int voxelIndex = lightSource.hitInfo.voxelIndex;
                Vector3 normal = lightSource.hitInfo.normal;
                int itemIndex = 0;
                ItemDefinition id = lightSource.itemDefinition;
                if (id != null) {
                    saveItemDefinitionsDict.TryGetValue(id, out itemIndex);
                }
                bw.Write((Int16)voxelIndex);
                EncodeVector3Binary(bw, normal);
                bw.Write((Int16)itemIndex);
            }

            // Write items
            int itemCount = chunk.items != null ? chunk.items.count : 0;
            bw.Write((Int16)itemCount);
            for (int j = 0; j < itemCount; j++) {
                Int16 itemIndex = 0;
                float itemQuantity = 0;
                Vector3 itemPosition = Misc.vector3zero;
                Item item = chunk.items.values[j];
                if (item != null && item.itemDefinition != null) {
                    ItemDefinition id = item.itemDefinition;
                    if (saveItemDefinitionsDict.TryGetValue(id, out int idIndex)) {
                        itemIndex = ((Int16)idIndex);
                        itemPosition = item.transform.position;
                        itemQuantity = item.quantity;
                    }
                }
                bw.Write(itemIndex);
                EncodeVector3Binary(bw, itemPosition);
                bw.Write(itemQuantity);
            }

            // Save custom voxel properties
            if (chunk.voxelsProperties != null) {
                List<KeyValuePair<int, FastHashSet<VoxelProperty>>> voxelsProperties = BufferPool<KeyValuePair<int, FastHashSet<VoxelProperty>>>.Get();
                List<KeyValuePair<int, VoxelProperty>> voxelProperties = BufferPool<KeyValuePair<int, VoxelProperty>>.Get();
                chunk.voxelsProperties.GetValues(voxelsProperties);
                int voxelsPropertiesCount = voxelsProperties.Count;
                bw.Write((Int16)voxelsPropertiesCount);
                for (int j = 0; j < voxelsPropertiesCount; j++) {
                    KeyValuePair<int, FastHashSet<VoxelProperty>> kvp = voxelsProperties[j];
                    bw.Write((Int16)kvp.Key); // voxel index

                    kvp.Value.GetValues(voxelProperties);
                    int voxelPropertiesCount = voxelProperties.Count;

                    bw.Write((Int16)voxelPropertiesCount); // properties count for this voxel
                    for (int i = 0; i < voxelPropertiesCount; i++) {
                        KeyValuePair<int, VoxelProperty> prop = voxelProperties[i];
                        bw.Write((Int32)prop.Key); // property id
                        bw.Write(prop.Value.floatValue); // float value
                        if (prop.Value.stringValue != null) {
                            bw.Write(prop.Value.stringValue); // string value
                        } else {
                            bw.Write("");
                        }
                    }
                }
                BufferPool<KeyValuePair<int, VoxelProperty>>.Release(voxelProperties);
                BufferPool<KeyValuePair<int, FastHashSet<VoxelProperty>>>.Release(voxelsProperties);
            } else {
                bw.Write((Int16)0);
            }

            // Save microvoxels using v22 palette
            if (chunk.microVoxels != null && chunk.microVoxels.Count > 0) {
                // Build palette
                Dictionary<(ulong, int), int> indexOf = new Dictionary<(ulong, int), int>();
                List<MicroVoxels> shapes = new List<MicroVoxels>();
                foreach (var kv in chunk.microVoxels) {
                    MicroVoxels mv = kv.Value;
                    if (mv == null) continue;
                    int secIdx = -1;
                    if (mv.secondaryType != null) {
                        saveVoxelDefinitionsDict.TryGetValue(mv.secondaryType, out secIdx);
                    }
                    var key = (mv.GetGridHashCode(), secIdx);
                    if (!indexOf.ContainsKey(key)) {
                        indexOf[key] = shapes.Count;
                        shapes.Add(mv);
                    }
                }
                // write palette
                bw.Write((Int16)shapes.Count);
                for (int i = 0; i < shapes.Count; i++) {
                    MicroVoxels mv = shapes[i];
                    mv.WriteToBinaryWriter(bw);
                    bw.Write((byte)mv.layout);
                    int secIdx = -1;
                    if (mv.secondaryType != null) {
                        saveVoxelDefinitionsDict.TryGetValue(mv.secondaryType, out secIdx);
                    }
                    bw.Write((Int16)secIdx);
                }
                // write instances
                bw.Write((Int16)chunk.microVoxels.Count);
                foreach (var kv in chunk.microVoxels) {
                    int voxelIndex = kv.Key;
                    MicroVoxels mv = kv.Value;
                    int secIdx = -1;
                    if (mv.secondaryType != null) {
                        saveVoxelDefinitionsDict.TryGetValue(mv.secondaryType, out secIdx);
                    }
                    int shapeIdx = indexOf[(mv.GetGridHashCode(), secIdx)];
                    bw.Write((Int16)voxelIndex);
                    bw.Write((Int16)shapeIdx);
                }
            } else {
                bw.Write((Int16)0);
                bw.Write((Int16)0);
            }
        }
    }
}


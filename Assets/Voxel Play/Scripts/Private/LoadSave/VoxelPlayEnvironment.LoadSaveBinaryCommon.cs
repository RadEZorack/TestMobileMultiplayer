using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Text;

namespace VoxelPlay {

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        List<VoxelDefinition> saveVoxelDefinitionsList;
        Dictionary<VoxelDefinition, int> saveVoxelDefinitionsDict;
        List<string> saveItemDefinitionsList;
        Dictionary<ItemDefinition, int> saveItemDefinitionsDict;
        static byte[] zeroInt = { 0, 0, 0, 0 };
        static byte[] zeroInt16 = { 0, 0 };

        void InitSaveGameStructs () {
            if (saveVoxelDefinitionsList == null) {
                saveVoxelDefinitionsList = new List<VoxelDefinition>(100);
            } else {
                saveVoxelDefinitionsList.Clear();
            }
            if (saveVoxelDefinitionsDict == null) {
                saveVoxelDefinitionsDict = new Dictionary<VoxelDefinition, int>(100);
            } else {
                saveVoxelDefinitionsDict.Clear();
            }
            if (saveItemDefinitionsList == null) {
                saveItemDefinitionsList = new List<string>(100);
            } else {
                saveItemDefinitionsList.Clear();
            }
            if (saveItemDefinitionsDict == null) {
                saveItemDefinitionsDict = new Dictionary<ItemDefinition, int>(100);
            } else {
                saveItemDefinitionsDict.Clear();
            }
        }


        Vector3 DecodeVector3Binary (BinaryReader br) {
            Vector3 v = new Vector3();
            v.x = br.ReadSingle();
            v.y = br.ReadSingle();
            v.z = br.ReadSingle();
            return v;
        }

        void EncodeVector3Binary (BinaryWriter bw, Vector3 v) {
            bw.Write(v.x);
            bw.Write(v.y);
            bw.Write(v.z);
        }


        /// <summary>
        /// Returns a new byte array with enough capacity to hold the contents of a chunk. Use this before calling GetChunkRawData().
        /// </summary>
        /// <returns></returns>
        public byte[] GetChunkRawBuffer () {
            int bufferSize = GetChunkBufferMinimumSize();
            return new byte[bufferSize];
        }

        void WriteByte (byte source, byte[] destination, ref int baseIndex) {
            destination[baseIndex] = source;
            baseIndex++;
        }

        void WriteBytes (byte[] source, byte[] destination, ref int baseIndex) {
            source.CopyTo(destination, baseIndex);
            baseIndex += source.Length;
        }

        void ReadByte (byte[] source, ref int baseIndex, out int value) {
            value = source[baseIndex];
            baseIndex++;
        }


        void ReadInt16 (byte[] source, ref int baseIndex, out int value) {
            value = BitConverter.ToInt16(source, baseIndex);
            baseIndex += 2;
        }

        void ReadInt32 (byte[] source, ref int baseIndex, out int value) {
            value = BitConverter.ToInt32(source, baseIndex);
            baseIndex += 4;
        }

        void ReadFloat (byte[] source, ref int baseIndex, out float value) {
            value = BitConverter.ToSingle(source, baseIndex);
            baseIndex += 4;
        }

        void ReadUlong (byte[] source, ref int baseIndex, out ulong value) {
            value = BitConverter.ToUInt64(source, baseIndex);
            baseIndex += 8;
        }

        void ReadString (byte[] source, ref int baseIndex, out string value) {
            int length = BitConverter.ToInt32(source, baseIndex);
            baseIndex += 4;
            if (length > 0) {
                value = Encoding.ASCII.GetString(source, baseIndex, length);
                baseIndex += length;
            } else {
                value = "";
            }
        }


        int GetChunkRawProperties (VoxelChunk chunk, byte[] contents, int baseIndex) {
            if (chunk.voxelsProperties == null) {
                WriteBytes(zeroInt16, contents, ref baseIndex);
                return baseIndex;
            }

            List<KeyValuePair<int, FastHashSet<VoxelProperty>>> voxelsProperties = BufferPool<KeyValuePair<int, FastHashSet<VoxelProperty>>>.Get();
            List<KeyValuePair<int, VoxelProperty>> voxelProperties = BufferPool<KeyValuePair<int, VoxelProperty>>.Get();
            chunk.voxelsProperties.GetValues(voxelsProperties);
            int voxelsPropertiesCount = chunk.voxelsProperties.Count;
            WriteBytes(BitConverter.GetBytes((Int16)voxelsPropertiesCount), contents, ref baseIndex);
            for (int j = 0; j < voxelsPropertiesCount; j++) {
                KeyValuePair<int, FastHashSet<VoxelProperty>> kvp = voxelsProperties[j];
                WriteBytes(BitConverter.GetBytes((Int16)kvp.Key), contents, ref baseIndex); // voxel index

                kvp.Value.GetValues(voxelProperties);
                int voxelPropertiesCount = voxelProperties.Count;

                WriteBytes(BitConverter.GetBytes((Int16)voxelPropertiesCount), contents, ref baseIndex); // properties count for this voxel
                for (int i = 0; i < voxelPropertiesCount; i++) {
                    KeyValuePair<int, VoxelProperty> prop = voxelProperties[i];
                    WriteBytes(BitConverter.GetBytes(prop.Key), contents, ref baseIndex); // property id
                    WriteBytes(BitConverter.GetBytes(prop.Value.floatValue), contents, ref baseIndex); // float value
                    if (!string.IsNullOrEmpty(prop.Value.stringValue)) {
                        WriteBytes(BitConverter.GetBytes(prop.Value.stringValue.Length), contents, ref baseIndex); // string length
                        WriteBytes(Encoding.ASCII.GetBytes(prop.Value.stringValue), contents, ref baseIndex); // string value
                    } else {
                        WriteBytes(zeroInt, contents, ref baseIndex); // 0-length string
                    }
                }
            }
            BufferPool<KeyValuePair<int, VoxelProperty>>.Release(voxelProperties);
            BufferPool<KeyValuePair<int, FastHashSet<VoxelProperty>>>.Release(voxelsProperties);

            return baseIndex;
        }

        void SetChunkRawProperties (VoxelChunk chunk, byte[] contents, ref int baseIndex) {
            if (chunk.voxelsProperties == null) {
                chunk.voxelsProperties = new FastHashSet<FastHashSet<VoxelProperty>>();
            } else {
                chunk.voxelsProperties.Clear();
            }
            ReadInt16(contents, ref baseIndex, out int voxelsPropertiesCount);
            for (int k = 0; k < voxelsPropertiesCount; k++) {
                ReadInt16(contents, ref baseIndex, out int voxelIndex);
                ReadInt16(contents, ref baseIndex, out int voxelPropertiesCount);
                if (!chunk.voxelsProperties.TryGetValue(voxelIndex, out FastHashSet<VoxelProperty> voxelProperties)) {
                    voxelProperties = new FastHashSet<VoxelProperty>();
                    chunk.voxelsProperties[voxelIndex] = voxelProperties;
                }
                for (int i = 0; i < voxelPropertiesCount; i++) {
                    VoxelProperty prop;
                    ReadInt32(contents, ref baseIndex, out int propId);
                    ReadFloat(contents, ref baseIndex, out float floatValue);
                    prop.floatValue = floatValue;
                    ReadInt32(contents, ref baseIndex, out int stringLength);
                    if (stringLength > 0) {
                        ReadString(contents, ref baseIndex, out string stringValue);
                        prop.stringValue = stringValue;
                    } else {
                        prop.stringValue = "";
                    }
                    voxelProperties[propId] = prop;
                }
            }
        }

        int GetChunkRawMicroVoxels (VoxelChunk chunk, byte[] contents, int baseIndex) {
            if (!chunk.usesMicroVoxels || chunk.microVoxels == null) {
                WriteBytes(zeroInt16, contents, ref baseIndex);
                return baseIndex;
            }
            int microVoxelsCount = chunk.microVoxels.Count;
            WriteBytes(BitConverter.GetBytes((Int16)microVoxelsCount), contents, ref baseIndex); // number of microvoxels
            foreach (var kvp in chunk.microVoxels) {
                WriteBytes(BitConverter.GetBytes((Int16)kvp.Key), contents, ref baseIndex); // voxel index
                ulong[] data = kvp.Value.gridData;
                int dataCount = data.Length;
                WriteByte((byte)dataCount, contents, ref baseIndex); // microvoxels data count
                for (int i = 0; i < dataCount; i++) {
                    WriteBytes(BitConverter.GetBytes(data[i]), contents, ref baseIndex); // microvoxels data
                }
            }
            return baseIndex;
        }

        void SetChunkRawMicroVoxels (VoxelChunk chunk, byte[] contents, ref int baseIndex) {
            ReadInt16(contents, ref baseIndex, out int microVoxelsCount);  // number of microvoxels
            if (microVoxelsCount == 0) return;
            if (chunk.microVoxels == null) {
                chunk.microVoxels = new Dictionary<int, MicroVoxels>();
            } else {
                chunk.microVoxels.Clear();
            }
            for (int k = 0; k < microVoxelsCount; k++) {
                ReadInt16(contents, ref baseIndex, out int voxelIndex); // voxel index
                ReadByte(contents, ref baseIndex, out int dataCount); // microvoxels data count
                MicroVoxels mv = new MicroVoxels();
                for (int i = 0; i < dataCount; i++) {
                    ReadUlong(contents, ref baseIndex, out mv.gridData[i]); // microvoxels data
                }
                chunk.microVoxels[voxelIndex] = mv;
            }
        }


        int GetChunkBufferMinimumSize () {
            return CHUNK_VOXEL_COUNT * (Voxel.memorySize + 2);
        }

        /// <summary>
        /// Returns the voxels of a given chunk in compressed binary form (RLE)
        /// </summary>
        /// <param name="contents">The byte array where to write the data. Use GetChunkRawBuffer() to get a new byte array.</param>
        /// <param name="includeVoxelProperties">If true, custom voxel properties will also be included</param>
        /// <param name="includeMicroVoxels">If true, microVoxels data will also be included</param>
        /// <returns>The actual length of the data inside the contents array buffer</returns>
        public int GetChunkRawData (VoxelChunk chunk, byte[] contents, bool includeVoxelProperties = true, bool includeMicroVoxels = true) {
            if ((object)chunk == null || contents == null) return 0;

            int baseIndex = 0;
            int minimumLength = GetChunkBufferMinimumSize();
            if (contents.Length < minimumLength) {
                Debug.Log("Contents length must be at least of " + minimumLength);
            }
            int i, k = 0, count;
            Voxel voxel = Voxel.Empty;

            for (i = 0; i < CHUNK_VOXEL_COUNT; i++) {
                if (chunk.voxels[i] == voxel) continue;
                count = i - k;
                if (count > 0) {
                    if (count >= 255) {
                        contents[baseIndex++] = 255;
                        contents[baseIndex++] = (byte)(count >> 8);
                    }
                    contents[baseIndex++] = (byte)(count & 0xFF);
                    baseIndex = chunk.voxels[k].WriteRawData(contents, baseIndex);
                }
                k = i;
                voxel = chunk.voxels[i];
            }
            count = i - k;
            if (count > 0) {
                if (count >= 255) {
                    contents[baseIndex++] = 255;
                    contents[baseIndex++] = (byte)(count >> 8);
                }
                contents[baseIndex++] = (byte)(count & 0xFF);
                baseIndex = chunk.voxels[k].WriteRawData(contents, baseIndex);
            }

            if (includeVoxelProperties) {
                baseIndex = GetChunkRawProperties(chunk, contents, baseIndex);
            }
            if (includeMicroVoxels) {
                baseIndex = GetChunkRawMicroVoxels(chunk, contents, baseIndex);
            }

            return baseIndex;
        }


        /// <summary>
        /// Replaces the content of a chunk with new voxel content provided by the contents array. Use GetChunkRawData() to get ray binary data from a chunk.
        /// </summary>
        /// <param name="contents">Contents of the chunk in raw byte format</param>
        /// <param name="length">Length of data in the contents buffer</param>
        /// <param name="validate">Ensures the type of voxels corresponds with any voxel definition.</param>
        /// <param name="includeVoxelProperties">If true, custom voxel properties will also be included</param>
        /// <param name="includeMicroVoxels">If true, microVoxels data will also be included</param>
        public void SetChunkRawData (VoxelChunk chunk, byte[] contents, int length, bool validate = true, bool includeVoxelProperties = true, bool includeMicroVoxels = true) {
            if ((object)chunk == null || contents == null) return;
            int baseIndex = 0;
            Voxel voxel = new Voxel();
            int i = 0;
            while (i < length) {
                int count = contents[i++];
                if (count == 255) {
                    count = (contents[i] << 8) + contents[i + 1];
                    i += 2;
                }
                i = voxel.ReadRawData(contents, i);
                for (int k = 0; k < count; k++) {
                    chunk.voxels[baseIndex++] = voxel;
                }
            }
            if (validate) {
                for (int k = 0; k < CHUNK_VOXEL_COUNT; k++) {
                    if (chunk.voxels[k].typeIndex < 0 || chunk.voxels[k].typeIndex >= voxelDefinitionsCount) {
                        chunk.voxels[k].typeIndex = 0;
                        ShowError("SetChunkRawData: unknown voxel definition at chunk pos=" + chunk.position + " index=" + k + ". Make sure all voxel definitions are loaded/added to Voxel Play during start up in order to ensure proper synchronization.");
                    }
                }
            }

            if (includeVoxelProperties) {
                SetChunkRawProperties(chunk, contents, ref i);
            }
            if (includeMicroVoxels) {
                SetChunkRawMicroVoxels(chunk, contents, ref i);
            }
        }
    }



}

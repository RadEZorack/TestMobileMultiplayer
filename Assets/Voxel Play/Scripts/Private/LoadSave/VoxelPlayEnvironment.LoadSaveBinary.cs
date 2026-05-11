using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;

namespace VoxelPlay {

    public delegate void LoadGameEvent (string tag, byte[] contents);
    public delegate void SaveGameEvent (SaveGameCustomDataWriter writer);

    public partial class VoxelPlayEnvironment : MonoBehaviour {

        public event LoadGameEvent OnLoadCustomGameData;
        public event SaveGameEvent OnSaveCustomGameData;

        const string SAVEGAMEDATA_EXTENSION = ".bytes";
        static readonly StringBuilder _sb = new StringBuilder();

        /// <summary>
        /// True if the current game has been loaded from a savefile.
        /// </summary>
        [NonSerialized]
        public bool saveFileIsLoaded;

        public bool saveFileSupportsDynamicLoad => saveFileIsLoaded && loadedSaveFileVersion >= 20;

        /// <summary>
        /// Version of the currently loaded savegame file. 0 if no save file is loaded.
        /// </summary>
        [NonSerialized]
        public int loadedSaveFileVersion;


        const byte SAVE_FILE_CURRENT_FORMAT = 22;
        bool isLoadingGame;

        // Dynamic loading support
        // Note: availableRegionIds is only populated when using LoadGameBinary with enableDynamicLoad=true
        // It remains null when using LoadGameFromByteArray or LoadGameFromBase64 since they load all data at once
        readonly HashSet<int> availableRegionIds = new HashSet<int>();
        readonly HashSet<int> loadedRegionIds = new HashSet<int>();

        /// <summary>
        /// Attempts to dynamically load a region 
        /// </summary>
        /// <param name="chunkX">Chunk X coordinate</param>
        /// <param name="chunkZ">Chunk Z coordinate</param>
        /// <returns>True if the region has been loaded</returns>
        public bool TryLoadRegionDynamically (int chunkX, int chunkZ) {
            if (!enableDynamicLoad || availableRegionIds == null) {
                return false;
            }

            // Get the region ID for this chunk
            int regionId = RegionPartitioner.GetChunkRegionId(chunkX, chunkZ);

            // Check if region has already been loaded
            if (loadedRegionIds.Contains(regionId)) return false;

            // Check if region is available
            if (!availableRegionIds.Contains(regionId)) return false;

            // Load the region
            string regionIdString = RegionPartitioner.GetRegionStringIdFromChunkXZ(chunkX, chunkZ);
            using (Stream regionStream = GetSaveGameStream(regionIdString)) {
                if (regionStream == null) {
                    LogMessage($"Dynamic load: Region file {regionId} not found");
                    return false;
                }
                    using (BinaryReader brRegion = new BinaryReader(regionStream, Encoding.UTF8)) {
                        if (loadedSaveFileVersion >= 22) {
                            LoadGameRegionBinaryFormat_22(brRegion);
                        } else {
                            LoadGameRegionBinaryFormat_21(brRegion);
                        }
                        loadedRegionIds.Add(regionId);
                        LogMessage($"Dynamic load: Loaded region {regionId}");
                    }
            }
            return true;
        }

        /// <summary>
        /// Clears the loading state. Called when loading a new game or resetting.
        /// </summary>
        void ClearLoadingState () {
            saveFileIsLoaded = false;
            loadedSaveFileVersion = 0;
            availableRegionIds.Clear();
            loadedRegionIds.Clear();
        }

        /// <summary>
        /// Loads the savegame file specified in the "saveFilename" property of Voxel Play Environment.
        /// This method supports dynamic loading when enableDynamicLoad is true, loading regions on demand from files.
        /// </summary>
        /// <param name="preservePlayerPosition">If set to <c>true</c> preserve player position.</param>
        /// <returns>true if the savegame was loaded correctly</returns>
        public bool LoadGameBinary (bool preservePlayerPosition = false, bool clearScene = true) {

            ClearLoadingState();

            if (!CheckGameFilename())
                return false;

            bool captureChunkChangeEventsState = captureChunkChanges;

            bool result = true;
            try {
                using (Stream saveGameStream = GetSaveGameStream()) {
                    if (saveGameStream == null) {
                        return false;
                    }

                    captureChunkChanges = false;

            if (clearScene) {
                DestroyAllVoxels();
            } else {
                // Remove all modified chunks to ensure only loaded chunks are the modified ones
                List<VoxelChunk> tempChunks = BufferPool<VoxelChunk>.Get();
                GetChunks(tempChunks, ChunkModifiedFilter.OnlyModified);
                int count = tempChunks.Count;
                for (int k = 0; k < count; k++) {
                    VoxelChunk chunk = tempChunks[k];
                    if (chunk != null && chunk.modified) {
                        // Restore original contents
                        world.terrainGenerator.PaintChunk(chunk);
                        ChunkRequestRefresh(chunk, true, true);
                        chunk.modified = false;
                        chunk.modifiedTimestamp = 0;
                    }
                }
                BufferPool<VoxelChunk>.Release(tempChunks);
            }
                    // get version
                    isLoadingGame = true;
                    using (BinaryReader br = new BinaryReader(saveGameStream, Encoding.UTF8)) {
                        int version = br.ReadByte();
                        loadedSaveFileVersion = version;
#pragma warning disable 0429
#pragma warning disable 0162
                        if (CHUNK_SIZE != 16 && version <= 9) {
                            throw new ApplicationException("Saved game cannot be loaded. Chunk size does not match!");
                        }
                        if (version >= 10) {
                            int chunkSize = br.ReadByte();
                            if (CHUNK_SIZE != chunkSize) {
                                throw new ApplicationException("Saved game cannot be loaded. Saved chunk size (" + chunkSize + ") does not match current scene chunk size!");
                            }
                        }
#pragma warning restore 0162
#pragma warning restore 0429
                        switch (version) {
                            case 5:
                                LoadGameBinaryFileFormat_5(br, preservePlayerPosition);
                                break;
                            case 6:
                                LoadGameBinaryFileFormat_6(br, preservePlayerPosition);
                                break;
                            case 7:
                                LoadGameBinaryFileFormat_7(br, preservePlayerPosition);
                                break;
                            case 8:
                                LoadGameBinaryFileFormat_8(br, preservePlayerPosition);
                                break;
                            case 9:
                                LoadGameBinaryFileFormat_9(br, preservePlayerPosition);
                                break;
                            case 10:
                                LoadGameBinaryFileFormat_10(br, preservePlayerPosition);
                                break;
                            case 11:
                                LoadGameBinaryFileFormat_11(br, preservePlayerPosition);
                                break;
                            case 12:
                                LoadGameBinaryFileFormat_12(br, preservePlayerPosition);
                                break;
                            case 13:
                                LoadGameBinaryFileFormat_13(br, preservePlayerPosition);
                                break;
                            case 14:
                                LoadGameBinaryFileFormat_14(br, preservePlayerPosition);
                                break;
                            case 15:
                                LoadGameBinaryFileFormat_15(br, preservePlayerPosition);
                                break;
                            case 16:
                                LoadGameBinaryFileFormat_16(br, singleFile: false, preservePlayerPosition);
                                break;
                            case 20:
                                LoadGameBinaryFileFormat_20(br, singleFile: false, preservePlayerPosition);
                                break;
                            case 21:
                                LoadGameBinaryFileFormat_21(br, singleFile: false, preservePlayerPosition);
                                break;
                            case 22:
                                LoadGameBinaryFileFormat_22(br, singleFile: false, preservePlayerPosition);
                                break;
                            default:
                                throw new ApplicationException("LoadGame() does not support this file format.");
                        }
                    }
                }
                isLoadingGame = false;
                saveFileIsLoaded = true;
                if (applicationIsPlaying && initialized && VoxelPlayUI.instance != null) {
                    VoxelPlayUI.instance.ToggleConsoleVisibility(false);
                    ShowMessage("<color=green>Game loaded successfully!</color>");
                }
                if (OnGameLoaded != null) {
                    OnGameLoaded();
                }
            }
            catch (Exception ex) {
                ShowError("<color=red>Load error:</color> <color=orange>" + ex.Message + "</color><color=white>" + ex.StackTrace + "</color>");
                loadedSaveFileVersion = 0;
                result = false;
            }
            finally {
                captureChunkChanges = captureChunkChangeEventsState;
            }

            isLoadingGame = false;
            shouldCheckChunksInFrustum = true;
            return result;
        }

        string GetFullFilename (string suffix = null) {
#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(world);
            path = Path.GetDirectoryName(path) + "/SavedGames";
#else
            string path = Application.persistentDataPath + "/VoxelPlay";
#endif
            Directory.CreateDirectory(path);
            path += "/" + saveFilename;
            if (!string.IsNullOrEmpty(suffix)) {
                path += "_" + suffix;
            }
            path += SAVEGAMEDATA_EXTENSION;
            return path;
        }


        Stream GetSaveGameStream (string suffix = null) {

#if UNITY_EDITOR
            // In Editor, always load saved game from Resources/Worlds/<name of world>/SavedGames folder
            string path = AssetDatabase.GetAssetPath(world);
            path = Path.GetDirectoryName(path) + "/SavedGames/" + saveFilename;
            if (!string.IsNullOrEmpty(suffix)) {
                path += "_" + suffix;
            }
            path += SAVEGAMEDATA_EXTENSION;
            if (File.Exists(path)) {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192);
            }
            return null;

#else
												// In Build, try to load the saved game from application data path. If there's none, try to load a default saved game from Resources.
			string path = Application.persistentDataPath + "/VoxelPlay/" + saveFilename;
            if (!string.IsNullOrEmpty(suffix)) {
                path += "_" + suffix;
            }
            path += SAVEGAMEDATA_EXTENSION;
												if (File.Exists(path)) {
			return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192);

												} else {
                string resource = "Worlds/" + world.name + "/SavedGames/" + saveFilename;
                if (!string.IsNullOrEmpty(suffix)) {
                    resource += "_" + suffix;
                }
                TextAsset ta = Resources.Load<TextAsset>(resource);
                if (ta != null) {
                    return new MemoryStream(ta.bytes, false);
                } else {
                    return null;
                }
												}
#endif
        }


        bool CheckGameFilename () {
            if (string.IsNullOrEmpty(saveFilename)) {
                ShowMessage("<color=orange>Set a file name for the game to load/save first.</color>");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Saves the game world to a binary file.
        /// If incremental is true, only modified chunks will be saved. And only modified regions are updated.
        /// If makeBackup is true, a backup of the file will be created.
        /// </summary>
        public bool SaveGameBinary (bool incremental = false, bool makeBackup = false) {
            if (!CheckGameFilename())
                return false;

			// Incremental saves are only valid when a save file is currently loaded
			// and its version matches the latest save file format. Otherwise, force full save.
			if (incremental) {
				if (!(saveFileIsLoaded && loadedSaveFileVersion == SAVE_FILE_CURRENT_FORMAT)) {
					incremental = false;
				}
			}

            bool success = true;
            try {
                VoxelCancelDynamicAll();
                RegionPartitioner regionPartitioner = new RegionPartitioner(GetChunks(ChunkModifiedFilter.OnlyModified));

                // save region files
                _sb.Clear();
                foreach (var region in regionPartitioner.GetRegions()) {
                    string regionId = region.id;
                    if (_sb.Length > 0) {
                        _sb.Append(',');
                    }
                    _sb.Append(regionId);

                    string filename = GetFullFilename(regionId);
                    if (incremental && File.Exists(filename) && !region.IsModifiedSinceLastSave()) continue;

                    if (makeBackup) {
                        MakeBackup(filename);
                    }
                    using (FileStream fs = new FileStream(filename, FileMode.Create)) {
                        using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8)) {
                            SaveGameChunksBinaryFormat(bw, region.chunks, region.GetBounds());
                        }
                    }
                }

                // Merge previously known region IDs to preserve regions saved in earlier sessions
                if (availableRegionIds.Count > 0) {
                    HashSet<string> currentRegionIds = new HashSet<string>();
                    foreach (var region in regionPartitioner.GetRegions()) {
                        currentRegionIds.Add(region.id);
                    }
                    foreach (int prevRegionId in availableRegionIds) {
                        string prevRegionStringId = RegionPartitioner.GetStringIdFromRegionId(prevRegionId);
                        if (!currentRegionIds.Contains(prevRegionStringId)) {
                            string regionFilename = GetFullFilename(prevRegionStringId);
                            if (File.Exists(regionFilename)) {
                                if (_sb.Length > 0) {
                                    _sb.Append(',');
                                }
                                _sb.Append(prevRegionStringId);
                            }
                        }
                    }
                }

                string regionsIds = _sb.ToString();

                // Update availableRegionIds with newly saved regions
                foreach (var region in regionPartitioner.GetRegions()) {
                    if (RegionPartitioner.TryGetRegionIdFromString(region.id, out int id)) {
                        availableRegionIds.Add(id);
                    }
                }

                {
                    // save header file
                    string filename = GetFullFilename();
                    if (makeBackup) {
                        MakeBackup(filename);
                    }
                    using (FileStream fs = new FileStream(filename, FileMode.Create)) {
                        using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8)) {
                            SaveGameHeaderBinaryFormat(bw, regionsIds);
                        }
                    }
                }

                {
                    // save extra data
                    string filename = GetFullFilename("extra");
                    if (makeBackup) {
                        MakeBackup(filename);
                    }
                    using (FileStream fs = new FileStream(filename, FileMode.Create)) {
                        using (BinaryWriter bw = new BinaryWriter(fs, Encoding.UTF8)) {
                            SaveGameExtraDataBinaryFormat(bw);
                        }
                    }
                }

                if (Application.isPlaying) {
                    ShowMessage("<color=green>Game saved successfully!</color>");
                }
            }
            catch (Exception ex) {
                ShowError("<color=red>Error:</color> <color=orange>" + ex.Message + "</color>");
                success = false;
            }
            return success;
        }

        void MakeBackup (string filename) {
            if (!File.Exists(filename))
                return;
            string backupFolder = Path.Combine(Path.GetDirectoryName(filename), "Backup");
            Directory.CreateDirectory(backupFolder);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmm");
            string backupFilename = Path.Combine(backupFolder, timestamp + "_" + Path.GetFileNameWithoutExtension(filename) + Path.GetExtension(filename));
            if (File.Exists(backupFilename)) {
                File.Delete(backupFilename);
            }
            File.Copy(filename, backupFilename);
        }

        /// <summary>
        /// Returns the world encoded in a string
        /// </summary>
        /// <returns>The game to text.</returns>
        public byte[] SaveGameToByteArray () {
            VoxelCancelDynamicAll();
            using (MemoryStream ms = new MemoryStream()) {
                using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8)) {
                    SaveGameBinaryFormat(bw);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Returns the world encoded in base 64 format
        /// </summary>
        public string SaveGameToBase64 () {
            return Convert.ToBase64String(SaveGameToByteArray());
        }

        /// <summary>
        /// Loads game world from a base64 string. This method loads all data at once and does not support dynamic loading.
        /// </summary>
        /// <returns>True if saveGameData was loaded successfully.</returns>
        /// <param name="preservePlayerPosition">If set to <c>true</c> preserve player position.</param>
        /// <param name="clearScene">If set to <c>true</c> existing chunks will be cleared before loading the game. If set to false, only chunks from the saved game will be replaced.</param>
        public bool LoadGameFromBase64 (string saveGameDataBase64string, bool preservePlayerPosition, bool clearScene = true) {
            byte[] saveGameData = System.Convert.FromBase64String(saveGameDataBase64string);
            return LoadGameFromByteArray(saveGameData, preservePlayerPosition, clearScene);
        }

        /// <summary>
        /// Loads game world from a byte array. This method loads all data at once and does not support dynamic loading.
        /// </summary>
        /// <returns>True if saveGameData was loaded successfully.</returns>
        /// <param name="preservePlayerPosition">If set to <c>true</c> preserve player position.</param>
        /// <param name="clearScene">If set to <c>true</c> existing chunks will be cleared before loading the game. If set to false, only chunks from the saved game will be replaced.</param>
        public bool LoadGameFromByteArray (byte[] saveGameData, bool preservePlayerPosition, bool clearScene = true) {
            if (saveGameData == null) {
                return false;
            }
            using (MemoryStream stream = new MemoryStream(saveGameData)) {
                return LoadGameFromStream(stream, preservePlayerPosition, clearScene);
            }
        }

        public bool LoadGameFromStream (Stream saveGameStream, bool preservePlayerPosition, bool clearScene = true) {
            ClearLoadingState();
            
            bool captureChunkChangeEventsState = captureChunkChanges;
            captureChunkChanges = false;

            if (clearScene) {
                DestroyAllVoxels();
            } else {
                // Remove all modified chunks to ensure only loaded chunks are the modified ones
                List<VoxelChunk> tempChunks = BufferPool<VoxelChunk>.Get();
                GetChunks(tempChunks, ChunkModifiedFilter.OnlyModified);
                int count = tempChunks.Count;
                for (int k = 0; k < count; k++) {
                    VoxelChunk chunk = tempChunks[k];
                    if (chunk != null && chunk.modified) {
                        // Restore original contents
                        world.terrainGenerator.PaintChunk(chunk);
                        ChunkRequestRefresh(chunk, true, true);
                        chunk.modified = false;
                        chunk.modifiedTimestamp = 0;
                    }
                }
                BufferPool<VoxelChunk>.Release(tempChunks);
            }


            bool result;
            try {
                if (saveGameStream == null) {
                    return false;
                }

                // get version
                isLoadingGame = true;
                using (BinaryReader br = new BinaryReader(saveGameStream, Encoding.UTF8)) {
                    byte version = br.ReadByte();
                    loadedSaveFileVersion = version;
#pragma warning disable 0429
#pragma warning disable 0162
                    if (CHUNK_SIZE != 16 && version <= 9) {
                        throw new ApplicationException("Saved game cannot be loaded. Chunk size does not match!");
                    }
                    if (version >= 10) {
                        int chunkSize = br.ReadByte();
                        if (CHUNK_SIZE != chunkSize) {
                            throw new ApplicationException("Saved game cannot be loaded. Saved chunk size (" + chunkSize + ") does not match current scene chunk size!");
                        }
                    }
#pragma warning restore 0162
#pragma warning restore 0429
                    switch (version) {
                        case 5:
                            LoadGameBinaryFileFormat_5(br, preservePlayerPosition);
                            break;
                        case 6:
                            LoadGameBinaryFileFormat_6(br, preservePlayerPosition);
                            break;
                        case 7:
                            LoadGameBinaryFileFormat_7(br, preservePlayerPosition);
                            break;
                        case 8:
                            LoadGameBinaryFileFormat_8(br, preservePlayerPosition);
                            break;
                        case 9:
                            LoadGameBinaryFileFormat_9(br, preservePlayerPosition);
                            break;
                        case 10:
                            LoadGameBinaryFileFormat_10(br, preservePlayerPosition);
                            break;
                        case 11:
                            LoadGameBinaryFileFormat_11(br, preservePlayerPosition);
                            break;
                        case 12:
                            LoadGameBinaryFileFormat_12(br, preservePlayerPosition);
                            break;
                        case 13:
                            LoadGameBinaryFileFormat_13(br, preservePlayerPosition);
                            break;
                        case 14:
                            LoadGameBinaryFileFormat_14(br, preservePlayerPosition);
                            break;
                        case 15:
                            LoadGameBinaryFileFormat_15(br, preservePlayerPosition);
                            break;
                        case 16:
                            LoadGameBinaryFileFormat_16(br, singleFile: true, preservePlayerPosition);
                            break;
                        case 20:
                            LoadGameBinaryFileFormat_20(br, singleFile: true, preservePlayerPosition);
                            break;
                        case 21:
                            LoadGameBinaryFileFormat_21(br, singleFile: true, preservePlayerPosition);
                            break;
                        case 22:
                            LoadGameBinaryFileFormat_22(br, singleFile: true, preservePlayerPosition);
                            break;
                        default:
                            throw new ApplicationException("LoadGameFromArray() does not support this file format.");
                    }
                }
                isLoadingGame = false;
                saveFileIsLoaded = true;
                if (OnGameLoaded != null) {
                    OnGameLoaded();
                }
                result = true;
            }
            catch (Exception ex) {
                Debug.LogError("Voxel Play: " + ex.Message);
                loadedSaveFileVersion = 0;
                result = false;
            }
            finally {
                captureChunkChanges = captureChunkChangeEventsState;
            }

            isLoadingGame = false;
            shouldCheckChunksInFrustum = true;
            return result;

        }

    }



}

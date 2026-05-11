using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoxelPlay {


    public partial class VoxelPlayUIDefault : VoxelPlayUI {

        /// <summary>
        /// Returns true if the console is visible
        /// </summary>
        public override bool IsConsoleVisible {
            get {
                if (console != null) {
                    return console.activeSelf;
                }
                return false;
            }
        }

        /// <summary>
        /// Returns true if the inventory is visible
        /// </summary>
        public override bool IsInventoryVisible {
            get {
                if (inventoryPlaceholder != null) {
                    return inventoryPlaceholder.activeSelf;
                }
                return false;
            }
        }

        [SerializeField]
        int _inventoryRows = 10;

        public virtual int inventoryRows {
            get { return _inventoryRows; }
            set {
                if (_inventoryRows != value) {
                    _inventoryRows = Mathf.Clamp(value, 1, 10);
                }
            }
        }

        [SerializeField]
        int _inventoryColumns = 3;

        public virtual int inventoryColumns {
            get { return _inventoryColumns; }
            set {
                if (_inventoryColumns != value) {
                    _inventoryColumns = Mathf.Clamp(value, 1, 10);
                }
            }
        }

        [SerializeField]
        bool _showSelectedItemName = true;
        public virtual bool showSelectedItemName {
            get { return _showSelectedItemName; }
            set {
                if (_showSelectedItemName != value) {
                    _showSelectedItemName = value;
                    ToggleSelectedItemName();
                }
            }
        }


        [SerializeField]
        bool _showItemQuantity = true;
        public virtual bool showItemQuantity {
            get { return _showItemQuantity; }
            set {
                if (_showItemQuantity != value) {
                    _showItemQuantity = value;
                    RefreshInventoryContents();
                }
            }
        }

        [NonSerialized]
        public VoxelPlayEnvironment env;

        [NonSerialized]
        public bool inventoryUIShouldBeRebuilt;


        static readonly char[] SEPARATOR_SPACE = { ' ' };
        readonly string KEY_CODES = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        StringBuilder sb, sbDebug;
        GameObject console, status, debug;
        RawImage selectedItem;
        GameObject selectedItemPlaceholder;
        Text consoleText, debugText, statusText, selectedItemName, selectedItemNameShadow, selectedItemQuantityShadow, selectedItemQuantity, inventoryTitleText, fpsText, fpsShadow, initText;
        GameObject inventoryPlaceholder, inventoryItemTemplate, inventoryTitle, initPanel;
        Transform initProgress;
        RectTransform rtCanvas;
        string lastCommand;

        InputField inputField;
        bool firstTimeConsole;
        readonly char[] forbiddenCharacters = { '<', '>' };
        List<GameObject> inventoryItems;
        List<RawImage> inventoryItemsImages;
        int inventoryCurrentPage;
        Image statusBackground;

        float fpsUpdateInterval = 0.5f;

        // FPS accumulated over the interval
        float fpsAccum;

        // Frames drawn over the interval
        int fpsFrames;

        // Left time for current interval
        float fpsTimeleft;

        // Can be used to enter certain data in console with command /ID and use that value in code
        public static int debugId;

        public override void InitUI () {
            firstTimeConsole = true;
            inventoryCurrentPage = 0;
            lastCommand = "";
            CheckReferences();
            fpsTimeleft = fpsUpdateInterval;
            fpsFrames = 1000;
        }

        void CheckReferences () {
            if (env == null) {
                env = VoxelPlayEnvironment.instance;
            }

            sb = new StringBuilder(1000);
            sbDebug = new StringBuilder(1000);

            CheckEventSystem();
            rtCanvas = GetComponent<RectTransform>();
            selectedItemPlaceholder = transform.Find("ItemPlaceholder").gameObject;
            selectedItem = selectedItemPlaceholder.transform.Find("ItemImage").GetComponent<RawImage>();
            selectedItemName = selectedItemPlaceholder.transform.Find("ItemName").GetComponent<Text>();
            selectedItemNameShadow = selectedItemPlaceholder.transform.Find("ItemNameShadow").GetComponent<Text>();
            selectedItemQuantity = selectedItemPlaceholder.transform.Find("QuantityShadow/QuantityText").GetComponent<Text>();
            selectedItemQuantityShadow = selectedItemPlaceholder.transform.Find("QuantityShadow").GetComponent<Text>();
            fpsShadow = transform.Find("FPSShadow").GetComponent<Text>();
            fpsText = fpsShadow.transform.Find("FPSText").GetComponent<Text>();
            fpsShadow.gameObject.SetActive(env.showFPS);
            console = transform.Find("Console").gameObject;
            console.GetComponent<Image>().color = env.consoleBackgroundColor;
            consoleText = transform.Find("Console/Scroll View/Viewport/ConsoleText").GetComponent<Text>();
            status = transform.Find("Status").gameObject;
            statusBackground = status.GetComponent<Image>();
            statusBackground.color = env.statusBarBackgroundColor;
            statusText = transform.Find("Status/StatusText").GetComponent<Text>();
            debug = transform.Find("Debug").gameObject;
            debug.GetComponent<Image>().color = env.consoleBackgroundColor;
            debugText = transform.Find("Debug/Scroll View/Viewport/DebugText").GetComponent<Text>();
            inputField = transform.Find("Status/InputField").GetComponent<InputField>();
            inputField.onEndEdit.AddListener(delegate {
                UserConsoleCommandHandler();
            });
            inventoryPlaceholder = transform.Find("InventoryPlaceholder").gameObject;
            inventoryItemTemplate = inventoryPlaceholder.transform.Find("ItemButtonTemplate").gameObject;
            inventoryTitle = inventoryPlaceholder.transform.Find("Title").gameObject;
            inventoryTitleText = inventoryPlaceholder.transform.Find("Title/Text").GetComponent<Text>();
            inventoryUIShouldBeRebuilt = true;
            initPanel = transform.Find("InitPanel").gameObject;
            initProgress = initPanel.transform.Find("Box/Progress").transform;
            initText = initPanel.transform.Find("StatusText").GetComponent<Text>();
        }

        void OnDestroy () {
            if (inputField != null) {
                inputField.onEndEdit.RemoveAllListeners();
            }
        }

        void LateUpdate () {
            LateUpdateImpl();
        }

        protected virtual void LateUpdateImpl () {
            if (env == null) return;
            VoxelPlayInputController input = env.input;
            if (input == null || inventoryPlaceholder == null)
                return;
            if (input.anyKey) {
                if (env.enableConsole && input.GetButtonDown(InputButtonNames.Console)) {
                    ToggleConsoleVisibility(!console.activeSelf);
                } else if (env.enableDebugWindow && input.GetButtonDown(InputButtonNames.DebugWindow)) {
                    ToggleDebugWindow(!debug.activeSelf);
                } else if (input.GetButtonDown(InputButtonNames.Escape)) {
                    if (IsConsoleVisible) {
                        ToggleConsoleVisibility(false);
                    }
                    if (IsInventoryVisible) {
                        ToggleInventoryVisibility(false);
                    }
				} else if (env.enableInventory && input.GetButtonDown(InputButtonNames.Inventory)) {
                    if (!inventoryPlaceholder.activeSelf) {
                        ToggleInventoryVisibility(true);
					} else if (input.GetButton(InputButtonNames.LeftShift)) {
                        InventoryPreviousPage();
                    } else {
                        InventoryNextPage();
                    }
				} else if (input.GetKeyDown("up") && IsConsoleVisible) {
                    inputField.text = lastCommand;
                    inputField.MoveTextEnd(false);
				} else if (input.GetKeyDown("f8")) {
                    ToggleFPS();
                } else if (inventoryPlaceholder.activeSelf) {
                    for (int k = 0; k < KEY_CODES.Length; k++) {
                        if (input.GetKeyDown(KEY_CODES.Substring(k, 1).ToLower())) {
                            SelectItemFromVisibleInventorySlot(k);
                            ToggleInventoryVisibility(false);
                        }
                    }
                }
            }

            if (debug.activeSelf) {
                UpdateDebugInfo();
            }

            if (fpsText.enabled) {
                UpdateFPSCounter();
            }

        }


        void CheckEventSystem () {

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
			EventSystem eventSystem = EventSystem.current;
			if (eventSystem == null) {
				GameObject go = new GameObject("EventSystem");
				eventSystem = go.AddComponent<EventSystem>();
				go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
			} else {
				if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null) {
					eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
				}
				StandaloneInputModule legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
				if (legacyModule != null) {
					if (Application.isPlaying) {
						Destroy(legacyModule);
					} else {
						DestroyImmediate(legacyModule);
					}
				}
			}
			return;
#else
            EventSystem eventSystem = Misc.FindObjectOfType<EventSystem>();
            if (eventSystem == null) {
                GameObject prefab = Resources.Load<GameObject>("VoxelPlay/Prefabs/EventSystem");
                if (prefab != null) {
                    GameObject go = Instantiate(prefab) as GameObject;
                    go.name = "EventSystem";
                }
            }
#endif
        }

        void EnableCursor (bool state) {
            if (env.initialized) {
                VoxelPlayFirstPersonController controller = VoxelPlayFirstPersonController.instance;
                if (controller != null) {
                    controller.mouseLook.SetCursorLock(!state);
                    controller.enabled = !state;
                } else {
                    VoxelPlayThirdPersonController thirdPersonController = VoxelPlayThirdPersonController.instance;
                    if (thirdPersonController != null && !thirdPersonController.useThirdPartyController) {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = state;
                    }
                }
            }
        }

        #region Console

        void PrintKeySheet () {
            if (sb.Length > 0) {
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.AppendLine("<color=orange>** KEY LIST **</color>");
            AppendValue("W/A/S/D");
            sb.AppendLine(" : Move player (front/left/back/right)");
            AppendValue("F");
            sb.AppendLine(" : Toggle Flight Mode");
            AppendValue("Q/E");
            sb.AppendLine(" : Fly up / down");
            AppendValue("C");
            sb.AppendLine(" : Toggles crouching");
            AppendValue("Left Shift");
            sb.AppendLine(" : Hold while move to run / fly faster");
            AppendValue("T");
            sb.AppendLine(" : Interacts with an object");
            AppendValue("G");
            sb.AppendLine(" : Throws currently selected item");
            AppendValue("L");
            sb.AppendLine(" : Toggles character light");
            AppendValue("B");
            sb.AppendLine(" : Activate Build mode");
            AppendValue("V");
            sb.AppendLine(" : Toggles microvoxels mode");
            AppendValue("M");
            sb.AppendLine(" : Enables and cycles microvoxel size");
            AppendValue("H");
            sb.AppendLine(" : Toggles slab mode");
            AppendValue("R");
            sb.AppendLine(" : Rotates build item");
            AppendValue("Mouse Move");
            sb.AppendLine(" : Look around");
            AppendValue("Mouse Left Button");
            sb.AppendLine(" : Fire / hit blocks");
            AppendValue("Mouse Right Button");
            sb.AppendLine(" : Build blocks");
            AppendValue("Tab");
            sb.AppendLine(" : Show inventory and browse items (Tab / Shift-Tab)");
            AppendValue("Esc");
            sb.AppendLine(" : Closes all windows (inventory, console)");
            AppendValue("F1");
            sb.AppendLine(" : Show / hide console");
            AppendValue("F2");
            sb.AppendLine(" : Show / hide debug window");
            AppendValue("Control + F3");
            sb.Append(" : Load Game / ");
            AppendValue("Control + F4");
            sb.AppendLine(" : Quick save");
            AppendValue("F8");
            sb.Append(" : Toggle FPS");
            consoleText.text = sb.ToString();
        }

        void PrintCommands () {
            if (sb.Length > 0) {
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.AppendLine("<color=orange>** COMMAND LIST **</color>");
            AppendValue("/help");
            sb.AppendLine(" : Show this list of commands");
            AppendValue("/keys");
            sb.AppendLine(" : Show available keys and actions");
            AppendValue("/clear");
            sb.AppendLine(" : Clear the console");
            AppendValue("/invoke GameObject MethodName");
            sb.AppendLine(" : Call method 'MethodName' on target GameObject");
            AppendValue("/save [filename]");
            sb.AppendLine(" : Save current game to 'filename' in incremental mode (only changes in this session, only filename, no extension)");
            AppendValue("/saveall [filename]");
            sb.AppendLine(" : Save current game to 'filename' (saves everything that's not default content, only filename, no extension)");
            AppendValue("/load [filename]");
            sb.AppendLine(" : Load a previously saved game");
            AppendValue("/build");
            sb.AppendLine(" : Enable/disable build mode (hotkey: <color=yellow>B</color>)");
            AppendValue("/teleport x y z");
            sb.AppendLine(" : Instantly teleport player to x y z location");
            AppendValue("/stuck");
            sb.AppendLine(" : Moves player on top of ground");
            AppendValue("/inventory rows columns");
            sb.AppendLine(" : Changes inventory panel size");
            AppendValue("/viewDistance dist");
            sb.AppendLine(" : Sets visible chunk distance (2-20)");
            AppendValue("/redraw");
            sb.AppendLine(" : Repaints all chunks");
            AppendValue("/flood on/off");
            sb.AppendLine(" : Toggles water flood");
            AppendValue("/time hh:mm");
            sb.AppendLine(" : Sets time of day in 23:59 hour format");
            AppendValue("/fps");
            sb.AppendLine(" : Toggles FPS display on/off");
            AppendValue("/debug");
            sb.AppendLine(" : Shows debug info about the last voxel hit");
            sb.Append("Press <color=yellow>F1</color> again or <color=yellow>ESC</color> to return to game.");

            consoleText.text = sb.ToString();
        }

        void AppendValue (object o) {
            sb.Append("<color=yellow>");
            sb.Append(o);
            sb.Append("</color>");
        }


        /// <summary>
        /// Shows/hides the console
        /// </summary>
        /// <param name="state">If set to <c>true</c> state.</param>
        public override void ToggleConsoleVisibility (bool state) {
            if (env == null) {
                CheckReferences();
                if (env == null) return;
            }
            if (!env.applicationIsPlaying)
                return;

            if (IsInventoryVisible) ToggleInventoryVisibility(false);

            if (statusText == null) {
                CheckReferences();
                if (statusText == null)
                    return;
            }

            if (firstTimeConsole) {
                firstTimeConsole = false;
                AdjustWindowPositions();
                AddConsoleText("<color=green>Enter <color=yellow>/help</color> for a list of commands.</color>");
            }
            status.SetActive(state);
            console.SetActive(state);
            consoleText.fontSize = statusText.fontSize;

            EnableCursor(state);

            if (state) {
                ToggleInventoryVisibility(false);
                statusText.text = "";
                FocusInputField();
            }

            env.input.enabled = !state;
        }

        /// <summary>
        /// Adds a custom text to the console
        /// </summary>
        public override void AddConsoleText (string text) {
            if (sb == null || consoleText == null || !env.enableStatusBar)
                return;
            if (sb.Length > 0) {
                sb.AppendLine();
            }
            if (sb.Length > 12000) {
                sb.Length = 0;
            }
            sb.Append(text);
            consoleText.text = sb.ToString();
        }

        /// <summary>
        /// Adds a custom message to the status bar and to the console.
        /// </summary>
        public override void AddMessage (string text, float displayTime = 4f, bool flash = true, bool openConsole = false) {
            if (!Application.isPlaying || env == null || !env.enableStatusBar)
                return;

            if (statusText == null) {
                CheckReferences();
                if (statusText == null)
                    return;
            }

            if (text != statusText.text) {
                AddConsoleText(text);

                // If console is not shown, only show this message
                if (!console.activeSelf) {
                    if (openConsole) {
                        ToggleConsoleVisibility(true);
                    } else {
                        statusText.text = text;
                        status.SetActive(true);
                        CancelInvoke(nameof(HideStatusText));
                        Invoke(nameof(HideStatusText), displayTime);
                        if (flash && gameObject.activeInHierarchy) {
                            StartCoroutine(FlashStatusText());
                        }
                    }
                }

                ConsoleNewMessage(text);
            }
        }

        IEnumerator FlashStatusText () {
            if (statusBackground == null)
                yield break;
            float startTime = Time.time;
            float elapsed;
            Color startColor = new Color(0, 1.1f, 1.1f, env.statusBarBackgroundColor.a);
            do {
                elapsed = Time.time - startTime;
                if (elapsed >= 1f)
                    elapsed = 1f;
                if (statusBackground == null)
                    yield break;
                statusBackground.color = Color.Lerp(startColor, env.statusBarBackgroundColor, elapsed);
                yield return new WaitForEndOfFrame();
            } while (elapsed < 1f);
        }


        /// <summary>
        /// Hides the status bar
        /// </summary>
        public override void HideStatusText () {
            if (statusText != null) {
                statusText.text = "";
            }
            if (console != null && console.activeSelf) {
                return;
            }
            if (status != null) {
                status.SetActive(false);
            }
        }

        void UserConsoleCommandHandler () {
            if (inputField == null)
                return;
            string text = inputField.text;
            bool sanitize = false;
            for (int k = 0; k < forbiddenCharacters.Length; k++) {
                if (text.IndexOf(forbiddenCharacters[k]) >= 0) {
                    sanitize = true;
                    break;
                }
            }
            if (sanitize) {
                string[] temp = text.Split(forbiddenCharacters, StringSplitOptions.RemoveEmptyEntries);
                text = String.Join("", temp);
            }

            if (!string.IsNullOrEmpty(text)) {
                lastCommand = text;
                if (!ProcessConsoleCommand(text)) {
                    env.ShowMessage(text);
                }
                ConsoleNewCommand(inputField.text);
                if (inputField != null) {
                    inputField.text = "";
                    FocusInputField(); // avoids losing focus
                }
            }
        }

        void FocusInputField () {
            if (inputField == null)
                return;
            inputField.ActivateInputField();
            inputField.Select();
        }

        void AdjustWindowPositions () {
            float startY = 4;
            RectTransform rt = status.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY);
            startY = rt.anchoredPosition.y + rt.sizeDelta.y + 4;
            rt = console.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, startY);
        }


        bool ProcessConsoleCommand (string command) {
            string upperCommand = command.ToUpper();
            if (upperCommand.IndexOf("/ID") >= 0) {
                ProcessInternalDebug(upperCommand);
            }
            if (upperCommand.IndexOf("/CLEAR") >= 0) {
                sb.Length = 0;
                consoleText.text = "";
                return true;
            }
            if (upperCommand.IndexOf("/KEYS") >= 0) {
                PrintKeySheet();
                return true;
            }
            if (upperCommand.IndexOf("/HELP") >= 0) {
                PrintCommands();
                return true;
            }
            if (upperCommand.IndexOf("/INVOKE") >= 0) {
                ProcessInvokeCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/LOAD") >= 0) {
                ProcessLoadCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/SAVE") >= 0) {
                ProcessSaveCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/BUILD") >= 0) {
                ToggleConsoleVisibility(false);
                env.SetBuildMode(!env.buildMode);
                return true;
            }
            if (upperCommand.IndexOf("/TELEPORT") >= 0) {
                ToggleConsoleVisibility(false);
                ProcessTeleportCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/INVENTORY") >= 0) {
                ProcessInventoryResizeCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/VIEWDISTANCE ") >= 0) {
                ProcessViewDistanceCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/DEBUG") >= 0) {
                ToggleDebugWindow(!debug.activeSelf);
                return true;
            }
            if (upperCommand.IndexOf("/FLOOD") >= 0) {
                ProcessFloodCommand(command);
                return true;
            }
            if (upperCommand.IndexOf("/STUCK") >= 0) {  
                VoxelPlayFirstPersonController firstPersonController = VoxelPlayFirstPersonController.instance;
                if (firstPersonController != null)
                    firstPersonController.Unstuck(true);
                return true;
            }
            if (upperCommand.IndexOf("/REDRAW") >= 0) {
                ProcessRefresh();
                return true;
            }
            if (upperCommand.IndexOf("/TIME ") >= 0) {
                ProcessTimeCommand(command);
            }
            if (upperCommand.IndexOf("/FPS") >= 0) {
                ToggleFPS();
            }
            return false;
        }

        void ProcessInvokeCommand (string command) {
            string[] args = command.Split(SEPARATOR_SPACE, System.StringSplitOptions.RemoveEmptyEntries);
            if (args.Length >= 3) {
                string goName = args[1];
                string cmdParams = args[2];
                GameObject go = GameObject.Find(goName);
                if (go == null) {
                    AddMessage("GameObject '" + goName + "' not found.");
                } else {
                    go.SendMessage(cmdParams, SendMessageOptions.DontRequireReceiver);
                    ToggleConsoleVisibility(false);
                }
            }
        }

        void ProcessSaveCommand (string command) {
            string[] args = command.Split(SEPARATOR_SPACE, System.StringSplitOptions.RemoveEmptyEntries);
            if (args.Length >= 2) {
                string saveFilename = args[1];
                if (!string.IsNullOrEmpty(saveFilename)) {
                    env.saveFilename = args[1];
                }
            }
            env.SaveGameBinary(command.ToUpper().Contains("/SAVEALL") ? false : true);
        }

        void ProcessLoadCommand (string command) {
            string[] args = command.Split(SEPARATOR_SPACE, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length >= 2) {
                string saveFilename = args[1];
                if (!string.IsNullOrEmpty(saveFilename)) {
                    env.saveFilename = args[1];
                }
            }
            // use invoke to ensure all pending UI events are processed before destroying UI, console, etc. and avoid errors with EventSystem, etc.
            Invoke(nameof(LoadGame), 0.1f);
        }

        void ProcessInternalDebug (string command) {
            string[] args = command.Split(SEPARATOR_SPACE, System.StringSplitOptions.RemoveEmptyEntries);
            if (args.Length >= 2) {
                int.TryParse(args[1], out debugId);
            }
        }

        void LoadGame () {
            if (!env.LoadGameBinary(false)) {
                AddMessage("<color=red>Load error:</color><color=orange> Game '<color=white>" + env.saveFilename + "</color>' could not be loaded.</color>");
            }
        }

        void ProcessFloodCommand (string command) {
            string[] args = command.Split(SEPARATOR_SPACE, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length >= 2) {
                string mode = args[1].ToUpper();
                env.enableWaterFlood = "ON".Equals(mode);
            }
            AddMessage("<color=green>Flood is <color=yellow>" + (env.enableWaterFlood ? "ON" : "OFF") + "</color></color>");
        }

        void ProcessRefresh () {
            env.ChunkRedrawAll();
        }


        void ProcessTeleportCommand (string command) {
            try {
                string[] args = command.Split(SEPARATOR_SPACE, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length >= 4) {
                    float x = float.Parse(args[1]);
                    float y = float.Parse(args[2]);
                    float z = float.Parse(args[3]);
                    env.characterController.MoveTo(new Vector3(x + 0.5f, y, z + 0.5f));
                    ToggleConsoleVisibility(false);
                }
            }
            catch {
                AddInvalidCommandError();
            }
        }

        void ProcessInventoryResizeCommand (string command) {
            try {
                string[] args = command.Split(SEPARATOR_SPACE, System.StringSplitOptions.RemoveEmptyEntries);
                if (args.Length >= 3) {
                    int rows = int.Parse(args[1]);
                    int columns = int.Parse(args[2]);
                    if (rows > 0 && columns > 0) {
                        inventoryRows = rows;
                        inventoryColumns = columns;
                        inventoryUIShouldBeRebuilt = true;
                        ToggleInventoryVisibility(true);
                    }
                }
            }
            catch {
                AddInvalidCommandError();
            }
        }


        void ProcessViewDistanceCommand (string command) {
            try {
                string[] args = command.Split(SEPARATOR_SPACE, System.StringSplitOptions.RemoveEmptyEntries);
                if (args.Length >= 2) {
                    int distance = int.Parse(args[1]);
                    if (distance >= 2 && distance <= 20) {
                        env.visibleChunksDistance = distance;
                    }
                }
            }
            catch {
                AddInvalidCommandError();
            }
        }

        void ProcessTimeCommand (string command) {
            try {
                string[] args = command.Split(SEPARATOR_SPACE, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length >= 2) {
                    env.SetTimeOfDay(args[1]);
                }
            }
            catch {
                AddInvalidCommandError();
            }
        }


        void AddInvalidCommandError () {
            AddMessage("<color=orange>Invalid command.</color>");
        }

        #endregion

        #region Inventory related

        /// <summary>
        /// Show/hide inventory
        /// </summary>
        /// <param name="state">If set to <c>true</c> visible.</param>
        public override void ToggleInventoryVisibility (bool state) {
            if (!state) {
                inventoryPlaceholder.SetActive(false);
            } else {
                if (IsConsoleVisible) ToggleConsoleVisibility(false);
                CheckInventoryUI();
                RefreshInventoryContents();
                inventoryPlaceholder.SetActive(true);
            }
            ToggleSelectedItemName();
            AdjustWindowPositions();
        }

        /// <summary>
        /// Advances to next inventory page
        /// </summary>
        public override void InventoryNextPage () {
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player == null)
                return;
            int itemsPerPage = _inventoryRows * _inventoryColumns;
            if ((inventoryCurrentPage + 1) * itemsPerPage < player.items.Count) {
                inventoryCurrentPage++;
                RefreshInventoryContents();
            } else {
                inventoryCurrentPage = 0;
                ToggleInventoryVisibility(false);
            }
        }

        /// <summary>
        /// Shows previous inventory page
        /// </summary>
        public override void InventoryPreviousPage () {
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player == null)
                return;
            if (inventoryCurrentPage > 0) {
                inventoryCurrentPage--;
            } else {
                int itemsPerPage = _inventoryRows * _inventoryColumns;
                inventoryCurrentPage = player.items.Count / itemsPerPage;
            }
            RefreshInventoryContents();
        }

        /// <summary>
        /// Builds the inventory UI elements
        /// </summary>
        void CheckInventoryUI () {

            const float itemSize = 48;
            const float padding = 3;

            bool landscape = _inventoryColumns > _inventoryRows;
            while (_inventoryColumns * _inventoryRows > 36) {
                if (landscape) _inventoryRows--; else _inventoryColumns--;
            }

            float panelWidth = padding + _inventoryColumns * (itemSize + padding);
            float panelHeight;
            bool refit;
            do {
                refit = false;
                panelHeight = padding + _inventoryRows * (itemSize + padding);
                if (_inventoryRows > 3 && panelHeight * rtCanvas.localScale.y > Screen.height * 0.9f) {
                    refit = true;
                    inventoryUIShouldBeRebuilt = true;
                    _inventoryRows--;
                }
            } while (refit);

            if (!inventoryUIShouldBeRebuilt) {
                return;
            }
            Transform root = inventoryPlaceholder.transform.Find("Root");
            if (root != null) {
                DestroyImmediate(root.gameObject);
            }
            GameObject rootGO = new GameObject("Root");
            root = rootGO.transform;
            root.SetParent(inventoryPlaceholder.transform, false);


            if (inventoryItems == null)
                inventoryItems = new List<GameObject>();
            else
                inventoryItems.Clear();

            if (inventoryItemsImages == null)
                inventoryItemsImages = new List<RawImage>();
            else
                inventoryItemsImages.Clear();

            inventoryPlaceholder.GetComponent<RectTransform>().sizeDelta = new Vector2(panelWidth, panelHeight);
            int i = 0;
            for (int c = 0; c < _inventoryColumns; c++) {
                float x = padding + c * (itemSize + padding);
                for (int r = 0; r < _inventoryRows; r++) {
                    float y = padding + r * (itemSize + padding);
                    GameObject itemButton = Instantiate(inventoryItemTemplate);
                    inventoryItems.Add(itemButton);
                    itemButton.transform.SetParent(root, false);
                    RectTransform rt = itemButton.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(x, panelHeight * 0.5f - y);
                    itemButton.SetActive(true);
                    string keyCode = i < KEY_CODES.Length ? KEY_CODES.Substring(i, 1) : "";
                    Text t = itemButton.transform.Find("KeyCodeShadow/KeyCodeText").GetComponent<Text>();
                    t.text = keyCode;
                    inventoryItemsImages.Add(itemButton.GetComponent<RawImage>());
                    int aux = i; // dummy assignation so the lambda expression takes the appropiate value and not always the last item
                    itemButton.GetComponent<Button>().onClick.AddListener(delegate () {
                        InventoryImageClick(aux);
                    });
                    i++;
                }
            }
        }

        void InventoryImageClick (int inventoryImageIndex) {
            int itemsPerPage = _inventoryRows * _inventoryColumns;
            int itemIndex = inventoryCurrentPage * itemsPerPage + inventoryImageIndex;
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player != null)
                player.selectedItemIndex = itemIndex;
            ToggleInventoryVisibility(false);
        }

        /// <summary>
        /// Refreshs the inventory contents.
        /// </summary>
        public override void RefreshInventoryContents () {
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (inventoryItemsImages == null || env == null || player == null)
                return;
            int itemsPerPage = _inventoryRows * _inventoryColumns;
            int selectedItemIndex = player.selectedItemIndex;
            List<InventoryItem> playerItems = player.items;
            int playerItemsCount = playerItems != null ? playerItems.Count : 0;
            if (inventoryCurrentPage * itemsPerPage > playerItemsCount) {
                inventoryCurrentPage = 0;
            }
            int inventoryItemsImagesCount = inventoryItemsImages.Count;
            for (int k = 0; k < itemsPerPage; k++) {
                int itemIndex = inventoryCurrentPage * itemsPerPage + k;
                if (k >= inventoryItemsImagesCount)
                    continue;
                RawImage img = inventoryItemsImages[k];
                if (img == null)
                    continue;
                Text quantityShadow = img.transform.Find("QuantityShadow").GetComponent<Text>();
                Text quantityText = img.transform.Find("QuantityShadow/QuantityText").GetComponent<Text>();
                GameObject keyHint = img.transform.Find("KeyCodeShadow").gameObject;
                img.gameObject.SetActive(true);
                if (itemIndex < playerItemsCount) {
                    InventoryItem inventoryItem = playerItems[itemIndex];
                    if (inventoryItem.item != null) {
                        img.color = inventoryItem.item.color;
                        img.texture = inventoryItem.item.icon;
                    } else {
                        img.texture = null;
                    }
                    float quantity = inventoryItem.quantity;
                    // show quantity if greater than 1
                    if (quantity <= 0 || env.buildMode || !_showItemQuantity) {
                        quantityText.enabled = false;
                        quantityShadow.enabled = false;
                    } else {
                        string quantityStr = string.Format("{0:0.##}", quantity);
                        quantityText.text = quantityStr;
                        quantityShadow.text = quantityStr;
                        quantityText.enabled = true;
                        quantityShadow.enabled = true;
                    }
                    // Mark selected item
                    img.transform.Find("SelectedBorder").gameObject.SetActive(k + itemsPerPage * inventoryCurrentPage == selectedItemIndex);
                    keyHint.SetActive(true);
                } else {
                    img.texture = Texture2D.whiteTexture;
                    img.color = new Color(0, 0, 0, 0.25f);
                    quantityText.enabled = false;
                    quantityShadow.enabled = false;
                    // Hide selected border
                    img.transform.Find("SelectedBorder").gameObject.SetActive(false);
                    keyHint.SetActive(false);
                }
            }

            if (inventoryTitle != null) {
                if (playerItemsCount == 0) {
                    inventoryTitle.SetActive(true);
                    inventoryTitleText.text = "Empty.";
                } else if (playerItemsCount > itemsPerPage) {
                    inventoryTitle.SetActive(true);
                    int totalPages = (playerItemsCount - 1) / itemsPerPage + 1;
                    if (totalPages < 0)
                        totalPages = 1;
                    inventoryTitleText.text = "Page " + (inventoryCurrentPage + 1) + "/" + totalPages;
                } else {
                    inventoryTitle.SetActive(false);
                }
            }

        }


        void SelectItemFromVisibleInventorySlot (int itemIndex) {
            int itemsPerPage = _inventoryRows * _inventoryColumns;
            int selectedItemIndex = inventoryCurrentPage * itemsPerPage + itemIndex;
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (player != null)
                player.selectedItemIndex = selectedItemIndex;
        }


        /// <summary>
        /// Updates selected item representation on screen
        /// </summary>
        public override void ShowSelectedItem (InventoryItem inventoryItem) {
            IVoxelPlayPlayer player = VoxelPlayPlayer.instance;
            if (selectedItemPlaceholder == null || env == null || !env.enableInventory || player == null)
                return;
            ItemDefinition item = inventoryItem.item;
            selectedItem.texture = item.icon;
            Color tintedColor = item.color * player.selectedItemTintColor;
            selectedItem.color = tintedColor;
            string txt = item.title;
            if (string.IsNullOrEmpty(txt) && item.voxelType != null) {
                txt = item.voxelType.name;
            }
            selectedItemName.text = txt;
            selectedItemNameShadow.text = txt;
            selectedItemPlaceholder.SetActive(true);
            string quantity = inventoryItem.quantity.ToString();
            bool quantityVisible = showItemQuantity && !env.buildMode;
            selectedItemQuantityShadow.enabled = quantityVisible;
            selectedItemQuantityShadow.text = quantity;
            selectedItemQuantity.enabled = quantityVisible;
            selectedItemQuantity.text = quantity;
            RefreshInventoryContents();
            ToggleSelectedItemName();
        }

        void ToggleSelectedItemName () {
            bool showItemName = _showSelectedItemName;
            selectedItemName.enabled = showItemName;
            selectedItemNameShadow.enabled = showItemName;
        }

        /// <summary>
        /// Hides selected item graphic
        /// </summary>
        public override void HideSelectedItem () {
            if (selectedItemPlaceholder == null)
                return;
            selectedItemPlaceholder.SetActive(false);
            RefreshInventoryContents();
        }

        #endregion


        #region Initialization Panel

        public override void ToggleInitializationPanel (bool visible, string text = "", float progress = 0) {
            if (!Application.isPlaying)
                return;

            if (initProgress == null) {
                CheckReferences();
            }
            if (progress > 1)
                progress = 1f;
            initProgress.localScale = new Vector3(progress, 1, 1);
            if (visible) {
                initText.text = text;
            }
            initPanel.SetActive(visible);
        }

        #endregion

        #region Debug Window

        public override void ToggleDebugWindow (bool visible) {
            debug.SetActive(visible);
        }

        void UpdateDebugInfo () {

            sbDebug.Length = 0;

            if (env.playerGameObject != null) {
                Vector3 pos = env.playerGameObject.transform.position;
                sbDebug.Append("Player Position: X=");
                AppendValueDebug(pos.x.ToString("F2"));

                sbDebug.Append(", Y=");
                AppendValueDebug(pos.y.ToString("F2"));

                sbDebug.Append(", Z=");
                AppendValueDebug(pos.z.ToString("F2"));

                sbDebug.Append(", Angle=");
                float angle = env.playerGameObject.transform.eulerAngles.y;
                AppendValueDebug(angle.ToString("0"));
            }

            VoxelChunk currentChunk = env.GetCurrentChunk();
            if (currentChunk != null) {

                sbDebug.AppendLine();

                sbDebug.Append("Current Chunk: Id=");
                AppendValueDebug(currentChunk.poolIndex);

                sbDebug.Append(", X=");
                AppendValueDebug(currentChunk.position.x);

                sbDebug.Append(", Y=");
                AppendValueDebug(currentChunk.position.y);

                sbDebug.Append(", Z=");
                AppendValueDebug(currentChunk.position.z);
            }
            VoxelChunk hitChunk = env.lastHitInfo.chunk;
            if (hitChunk != null) {
                int voxelIndex = env.lastHitInfo.voxelIndex;

                sbDebug.AppendLine();

                sbDebug.Append("Last Chunk Hit: Id=");
                AppendValueDebug(hitChunk.poolIndex);

                sbDebug.Append(", X=");
                AppendValueDebug(hitChunk.position.x);

                sbDebug.Append(", Y=");
                AppendValueDebug(hitChunk.position.y);

                sbDebug.Append(", Z=");
                AppendValueDebug(hitChunk.position.z);

                sbDebug.Append(", AboveTerrain=");
                AppendValueDebug(hitChunk.isAboveSurface);

                if (hitChunk.modified) {
                    sbDebug.Append(" (modified)");
                }

                int px, py, pz;
                env.GetVoxelChunkCoordinates(voxelIndex, out px, out py, out pz);

                sbDebug.AppendLine();

                sbDebug.Append("Last Voxel Hit: X=");
                AppendValueDebug(px);

                sbDebug.Append(", Y=");
                AppendValueDebug(py);

                sbDebug.Append(", Z=");
                AppendValueDebug(pz);

                sbDebug.Append(", Index=");
                AppendValueDebug(env.lastHitInfo.voxelIndex);

                sbDebug.Append(", Torch/Sun=");
                AppendValueDebug(env.lastHitInfo.voxel.torchLight);
                sbDebug.Append("/");
                AppendValueDebug(env.lastHitInfo.voxel.light);

                sbDebug.Append(", Light Front=");
                Vector3d infrontPos = env.lastHighlightInfo.voxelCenter + env.lastHighlightInfo.normal;
                AppendValueDebug(env.GetVoxel(infrontPos).torchLight);
                sbDebug.Append("/");
                AppendValueDebug(env.GetVoxel(infrontPos).light);

                if (env.lastHitInfo.voxel.typeIndex != 0) {
                    sbDebug.AppendLine();
                    sbDebug.Append("     Voxel Type=");
                    AppendValueDebug(env.lastHitInfo.voxel.type.name);

                    sbDebug.Append(", Pos: X=");
                    Vector3d v = env.GetVoxelPosition(hitChunk.position, px, py, pz);
                    AppendValueDebug((int)v.x);

                    sbDebug.Append(", Y=");
                    AppendValueDebug((int)v.y);

                    sbDebug.Append(", Z=");
                    AppendValueDebug((int)v.z);
                }


            }
            debugText.text = sbDebug.ToString();
        }

        void AppendValueDebug (object o) {
            sbDebug.Append("<color=yellow>");
            sbDebug.Append(o);
            sbDebug.Append("</color>");
        }

        #endregion

        #region FPS

        void ToggleFPS () {
            fpsShadow.gameObject.SetActive(!fpsShadow.gameObject.activeSelf);
        }

        void UpdateFPSCounter () {
            fpsTimeleft -= Time.deltaTime;
            fpsAccum += Time.timeScale / Time.deltaTime;
            ++fpsFrames;
            if (fpsTimeleft <= 0.0) {
                if (fpsText != null && fpsShadow != null) {
                    int fps = (int)(fpsAccum / fpsFrames);
                    fpsText.text = fps.ToString();
                    fpsShadow.text = fpsText.text;
                    if (fps < 10)
                        fpsText.color = Color.red;
                    else if (fps < 30)
                        fpsText.color = Color.yellow;
                    else
                        fpsText.color = Color.green;
                }
                fpsTimeleft = fpsUpdateInterval;
                fpsAccum = 0.0f;
                fpsFrames = 0;
            }
        }


        #endregion


    }



}

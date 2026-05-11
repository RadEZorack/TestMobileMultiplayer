using UnityEngine;


namespace VoxelPlay {

    public class KeyboardMouseController : VoxelPlayInputController {
        protected override bool Initialize() {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return false;
#else
            return true;
#endif
        }

        [Header("Keymap")]
        public KeyCode keyJump = KeyCode.Space;
        public KeyCode keyUp = KeyCode.E;
        public KeyCode keyDown = KeyCode.Q;
        public KeyCode keyBuild = KeyCode.B;
        public KeyCode keyFly = KeyCode.F;
        public KeyCode keyCrouch = KeyCode.C;
        public KeyCode keyInventory = KeyCode.Tab;
        public KeyCode keyLight = KeyCode.L;
        public KeyCode keyThrowItem = KeyCode.G;
        public KeyCode keyAction = KeyCode.T;
        public KeyCode keySeeThroughUp = KeyCode.Q;
        public KeyCode keySeeThroughDown = KeyCode.E;
        public KeyCode keyEscape = KeyCode.Escape;
        public KeyCode keyConsole = KeyCode.F1;
        public KeyCode keyDebugWindow = KeyCode.F2;
        public KeyCode keyThrust = KeyCode.X;
        public KeyCode keyRotate = KeyCode.R;
        public KeyCode keyMicroVoxels = KeyCode.V;
        public KeyCode keyToggleMicroVoxelSize = KeyCode.M;
        public KeyCode keyToggleSlabMode = KeyCode.H;
        public KeyCode keyCustom1 = KeyCode.Alpha1;
        public KeyCode keyCustom2 = KeyCode.Alpha2;
        public KeyCode keyCustom3 = KeyCode.Alpha3;
        public KeyCode keyCustom4 = KeyCode.Alpha4;
        public KeyCode keyCustom5 = KeyCode.Alpha5;
        public KeyCode keyCustom6 = KeyCode.Alpha6;
        public KeyCode keyCustom7 = KeyCode.Alpha7;
        public KeyCode keyCustom8 = KeyCode.Alpha8;
        public KeyCode keyCustom9 = KeyCode.Alpha9;
		public KeyCode keyCustom0 = KeyCode.Alpha0;


        protected override void UpdateInputState() {

            screenPos = Input.mousePosition;

            mouseX = Input.GetAxis("Mouse X");
            mouseY = Input.GetAxis("Mouse Y");
            mouseScrollWheel = Input.GetAxis("Mouse ScrollWheel");
            horizontalAxis = Input.GetAxis("Horizontal");
            verticalAxis = Input.GetAxis("Vertical");
            anyAxisButtonPressed = Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;

            // Left mouse button
            if (Input.GetMouseButtonDown(0)) {
                buttons[(int)InputButtonNames.Button1].pressStartTime = Time.time;
                buttons[(int)InputButtonNames.Button1].pressState = InputButtonPressState.Down;
            } else if (Input.GetMouseButtonUp(0)) {
                buttons[(int)InputButtonNames.Button1].pressState = InputButtonPressState.Up;
            } else if (Input.GetMouseButton(0)) {
                buttons[(int)InputButtonNames.Button1].pressState = InputButtonPressState.Pressed;
            }
            // Right mouse button
            if (Input.GetMouseButtonDown(1)) {
                buttons[(int)InputButtonNames.Button2].pressStartTime = Time.time;
                buttons[(int)InputButtonNames.Button2].pressState = InputButtonPressState.Down;
            } else if (Input.GetMouseButtonUp(1)) {
                buttons[(int)InputButtonNames.Button2].pressState = InputButtonPressState.Up;
            } else if (Input.GetMouseButton(1)) {
                buttons[(int)InputButtonNames.Button2].pressState = InputButtonPressState.Pressed;
            }
            // Middle mouse button
            if (Input.GetMouseButtonDown(2)) {
                buttons[(int)InputButtonNames.MiddleButton].pressStartTime = Time.time;
                buttons[(int)InputButtonNames.MiddleButton].pressState = InputButtonPressState.Down;
            } else if (Input.GetMouseButtonUp(2)) {
                buttons[(int)InputButtonNames.MiddleButton].pressState = InputButtonPressState.Up;
            } else if (Input.GetMouseButton(2)) {
                buttons[(int)InputButtonNames.MiddleButton].pressState = InputButtonPressState.Pressed;
            }
            // Jump key
            ReadKeyState(InputButtonNames.Jump, keyJump);
            ReadKeyState(InputButtonNames.Up, keyUp);
            ReadKeyState(InputButtonNames.Down, keyDown);
            ReadKeyState(InputButtonNames.LeftControl, KeyCode.LeftControl);
            ReadKeyState(InputButtonNames.LeftShift, KeyCode.LeftShift);
            ReadKeyState(InputButtonNames.LeftAlt, KeyCode.LeftAlt);
            ReadKeyState(InputButtonNames.Build, keyBuild);
            ReadKeyState(InputButtonNames.Fly, keyFly);
            ReadKeyState(InputButtonNames.Crouch, keyCrouch);
            ReadKeyState(InputButtonNames.Inventory, keyInventory);
            ReadKeyState(InputButtonNames.Light, keyLight);
            ReadKeyState(InputButtonNames.ThrowItem, keyThrowItem);
            ReadKeyState(InputButtonNames.Action, keyAction);
            ReadKeyState(InputButtonNames.SeeThroughUp, keySeeThroughUp);
            ReadKeyState(InputButtonNames.SeeThroughDown, keySeeThroughDown);
            ReadKeyState(InputButtonNames.Escape, keyEscape);
            ReadKeyState(InputButtonNames.Console, keyConsole);
            ReadKeyState(InputButtonNames.DebugWindow, keyDebugWindow);
            ReadKeyState(InputButtonNames.Thrust, keyThrust);
            ReadKeyState(InputButtonNames.Rotate, keyRotate);
            ReadKeyState(InputButtonNames.MicroVoxels, keyMicroVoxels);
            ReadKeyState(InputButtonNames.ToggleMicroVoxelSize, keyToggleMicroVoxelSize);
            ReadKeyState(InputButtonNames.ToggleSlabMode, keyToggleSlabMode);
            ReadKeyState(InputButtonNames.Custom1, keyCustom1);
            ReadKeyState(InputButtonNames.Custom2, keyCustom2);
            ReadKeyState(InputButtonNames.Custom3, keyCustom3);
            ReadKeyState(InputButtonNames.Custom4, keyCustom4);
            ReadKeyState(InputButtonNames.Custom5, keyCustom5);
            ReadKeyState(InputButtonNames.Custom6, keyCustom6);
            ReadKeyState(InputButtonNames.Custom7, keyCustom7);
            ReadKeyState(InputButtonNames.Custom8, keyCustom8);
            ReadKeyState(InputButtonNames.Custom9, keyCustom9);
			ReadKeyState(InputButtonNames.Custom0, keyCustom0);
        }

		public override bool GetKeyDown (string buttonName) {
#if ENABLE_LEGACY_INPUT_MANAGER || (!ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER)
			switch(buttonName) {
				case "a": return Input.GetKeyDown(KeyCode.A);
				case "b": return Input.GetKeyDown(KeyCode.B);
				case "c": return Input.GetKeyDown(KeyCode.C);
				case "d": return Input.GetKeyDown(KeyCode.D);
				case "e": return Input.GetKeyDown(KeyCode.E);
				case "f": return Input.GetKeyDown(KeyCode.F);
				case "g": return Input.GetKeyDown(KeyCode.G);
				case "h": return Input.GetKeyDown(KeyCode.H);
				case "i": return Input.GetKeyDown(KeyCode.I);
				case "j": return Input.GetKeyDown(KeyCode.J);
				case "k": return Input.GetKeyDown(KeyCode.K);
				case "l": return Input.GetKeyDown(KeyCode.L);
				case "m": return Input.GetKeyDown(KeyCode.M);
				case "n": return Input.GetKeyDown(KeyCode.N);
				case "o": return Input.GetKeyDown(KeyCode.O);
				case "p": return Input.GetKeyDown(KeyCode.P);
				case "q": return Input.GetKeyDown(KeyCode.Q);
				case "r": return Input.GetKeyDown(KeyCode.R);
				case "s": return Input.GetKeyDown(KeyCode.S);
				case "t": return Input.GetKeyDown(KeyCode.T);
				case "u": return Input.GetKeyDown(KeyCode.U);
				case "v": return Input.GetKeyDown(KeyCode.V);
				case "w": return Input.GetKeyDown(KeyCode.W);
				case "x": return Input.GetKeyDown(KeyCode.X);
				case "y": return Input.GetKeyDown(KeyCode.Y);
				case "z": return Input.GetKeyDown(KeyCode.Z);
				case "0": return Input.GetKeyDown(KeyCode.Alpha0);
				case "1": return Input.GetKeyDown(KeyCode.Alpha1);
				case "2": return Input.GetKeyDown(KeyCode.Alpha2);
				case "3": return Input.GetKeyDown(KeyCode.Alpha3);
				case "4": return Input.GetKeyDown(KeyCode.Alpha4);
				case "5": return Input.GetKeyDown(KeyCode.Alpha5);
				case "6": return Input.GetKeyDown(KeyCode.Alpha6);
				case "7": return Input.GetKeyDown(KeyCode.Alpha7);
				case "8": return Input.GetKeyDown(KeyCode.Alpha8);
				case "9": return Input.GetKeyDown(KeyCode.Alpha9);
				case "enter": return Input.GetKeyDown(KeyCode.Return);
				case "escape": return Input.GetKeyDown(KeyCode.Escape);
				case "f1": return Input.GetKeyDown(KeyCode.F1);
				case "f2": return Input.GetKeyDown(KeyCode.F2);
				case "f3": return Input.GetKeyDown(KeyCode.F3);
				case "f4": return Input.GetKeyDown(KeyCode.F4);
				case "f5": return Input.GetKeyDown(KeyCode.F5);
				case "f6": return Input.GetKeyDown(KeyCode.F6);
				case "f7": return Input.GetKeyDown(KeyCode.F7);
				case "f8": return Input.GetKeyDown(KeyCode.F8);
				case "f9": return Input.GetKeyDown(KeyCode.F9);
				case "f10": return Input.GetKeyDown(KeyCode.F10);
				case "f11": return Input.GetKeyDown(KeyCode.F11);
				case "f12": return Input.GetKeyDown(KeyCode.F12);
				case "tab": return Input.GetKeyDown(KeyCode.Tab);
				case "up": return Input.GetKeyDown(KeyCode.UpArrow);
				case "down": return Input.GetKeyDown(KeyCode.DownArrow);
				case "left": return Input.GetKeyDown(KeyCode.LeftArrow);
				case "right": return Input.GetKeyDown(KeyCode.RightArrow);
			}
			return Input.GetButtonDown(buttonName);
#else
			return false;
#endif
		}

    }



}

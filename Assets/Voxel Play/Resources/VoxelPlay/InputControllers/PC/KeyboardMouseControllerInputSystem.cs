#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace VoxelPlay {
	public class KeyboardMouseControllerInputSystem : VoxelPlayInputController {
		Keyboard keyboard;
		Mouse mouse;
		Gamepad gamepad;

		[Header("Sensitivity")]
		public float mouseSensitivity = 0.1f; // Match default legacy Input Manager
		public float scrollSensitivity = 0.1f;

		protected override bool Initialize() {
#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
			return false;
#else
			keyboard = Keyboard.current;
			mouse = Mouse.current;
			gamepad = Gamepad.current;
			return true;
#endif
		}

		protected override void UpdateInputState() {
			if (keyboard == null) return;

			// Mouse position and deltas
			if (mouse != null) {
				Vector2 pos = mouse.position.ReadValue();
				screenPos.x = pos.x;
				screenPos.y = pos.y;
				screenPos.z = 0;
				Vector2 delta = mouse.delta.ReadValue();
				mouseX = delta.x * mouseSensitivity;
				mouseY = delta.y * mouseSensitivity;
				Vector2 scroll = mouse.scroll.ReadValue();
				mouseScrollWheel = scroll.y * scrollSensitivity;
				// Buttons: Left/Right/Middle
				WriteButtonState(InputButtonNames.Button1, mouse.leftButton);
				WriteButtonState(InputButtonNames.Button2, mouse.rightButton);
				WriteButtonState(InputButtonNames.MiddleButton, mouse.middleButton);
			}

			// Axes from keyboard and gamepad
			float horiz = 0f;
			float vert = 0f;
			if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horiz -= 1f;
			if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horiz += 1f;
			if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vert += 1f;
			if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vert -= 1f;
			if (gamepad != null) {
				Vector2 ls = gamepad.leftStick.ReadValue();
				horiz += ls.x;
				vert += ls.y;
			}
			horizontalAxis = Mathf.Clamp(horiz, -1f, 1f);
			verticalAxis = Mathf.Clamp(vert, -1f, 1f);
			anyAxisButtonPressed = horizontalAxis != 0f || verticalAxis != 0f;

			// Movement keys Up/Down (fly)
			ReadKeyState(InputButtonNames.Up, keyboard.eKey);
			ReadKeyState(InputButtonNames.Down, keyboard.qKey);

			// Modifiers
			ReadKeyState(InputButtonNames.LeftControl, keyboard.leftCtrlKey);
			ReadKeyState(InputButtonNames.LeftShift, keyboard.leftShiftKey);
			ReadKeyState(InputButtonNames.LeftAlt, keyboard.leftAltKey);

			// Gameplay mapped keys
			ReadKeyState(InputButtonNames.Jump, keyboard.spaceKey, gamepad != null ? gamepad.buttonSouth : null);
			ReadKeyState(InputButtonNames.Build, keyboard.bKey);
			ReadKeyState(InputButtonNames.Fly, keyboard.fKey);
			ReadKeyState(InputButtonNames.Crouch, keyboard.cKey);
			ReadKeyState(InputButtonNames.Inventory, keyboard.tabKey);
			ReadKeyState(InputButtonNames.Light, keyboard.lKey);
			ReadKeyState(InputButtonNames.ThrowItem, keyboard.gKey);
			ReadKeyState(InputButtonNames.Action, keyboard.tKey);
			ReadKeyState(InputButtonNames.SeeThroughUp, keyboard.qKey);
			ReadKeyState(InputButtonNames.SeeThroughDown, keyboard.eKey);
			ReadKeyState(InputButtonNames.Escape, keyboard.escapeKey);
			ReadKeyState(InputButtonNames.DebugWindow, keyboard.f2Key);
			ReadKeyState(InputButtonNames.Console, keyboard.f1Key);
			ReadKeyState(InputButtonNames.Thrust, keyboard.xKey);
			ReadKeyState(InputButtonNames.Rotate, keyboard.rKey);
			ReadKeyState(InputButtonNames.MicroVoxels, keyboard.vKey);
			ReadKeyState(InputButtonNames.ToggleMicroVoxelSize, keyboard.mKey);
			ReadKeyState(InputButtonNames.ToggleSlabMode, keyboard.hKey);

			// Custom keys 1..0
			ReadKeyState(InputButtonNames.Custom1, keyboard.digit1Key);
			ReadKeyState(InputButtonNames.Custom2, keyboard.digit2Key);
			ReadKeyState(InputButtonNames.Custom3, keyboard.digit3Key);
			ReadKeyState(InputButtonNames.Custom4, keyboard.digit4Key);
			ReadKeyState(InputButtonNames.Custom5, keyboard.digit5Key);
			ReadKeyState(InputButtonNames.Custom6, keyboard.digit6Key);
			ReadKeyState(InputButtonNames.Custom7, keyboard.digit7Key);
			ReadKeyState(InputButtonNames.Custom8, keyboard.digit8Key);
			ReadKeyState(InputButtonNames.Custom9, keyboard.digit9Key);
			ReadKeyState(InputButtonNames.Custom0, keyboard.digit0Key);

			// Focus
			focused = true;
		}

		protected override void UpdateImpl() {
			for (int k = 0; k < buttons.Length; k++) {
				buttons[k].pressState = InputButtonPressState.Idle;
			}
			if (!enabled) {
				mouseScrollWheel = mouseX = mouseY = horizontalAxis = verticalAxis = 0f;
			}
			UpdateInputState();
			anyKey = false;
			for (int k = 0; k < buttons.Length; k++) {
				if (buttons[k].pressState != InputButtonPressState.Idle) {
					anyKey = true;
					break;
				}
			}
		}

		void WriteButtonState(InputButtonNames button, ButtonControl control) {
			if (control == null) return;
			int idx = (int)button;
			if (control.wasPressedThisFrame) {
				buttons[idx].pressStartTime = Time.time;
				buttons[idx].pressState = InputButtonPressState.Down;
			} else if (control.wasReleasedThisFrame) {
				buttons[idx].pressState = InputButtonPressState.Up;
			} else if (control.isPressed) {
				buttons[idx].pressState = InputButtonPressState.Pressed;
			}
		}

		void ReadKeyState(InputButtonNames button, KeyControl key) {
			if (key == null) return;
			int idx = (int)button;
			if (key.wasPressedThisFrame) {
				buttons[idx].pressStartTime = Time.time;
				buttons[idx].pressState = InputButtonPressState.Down;
			} else if (key.wasReleasedThisFrame) {
				buttons[idx].pressState = InputButtonPressState.Up;
			} else if (key.isPressed) {
				buttons[idx].pressState = InputButtonPressState.Pressed;
			}
		}

		void ReadKeyState(InputButtonNames button, KeyControl key, ButtonControl gamepadButton) {
			int idx = (int)button;
			bool down = (key != null && key.wasPressedThisFrame) || (gamepadButton != null && gamepadButton.wasPressedThisFrame);
			bool up = (key != null && key.wasReleasedThisFrame) || (gamepadButton != null && gamepadButton.wasReleasedThisFrame);
			bool pressed = (key != null && key.isPressed) || (gamepadButton != null && gamepadButton.isPressed);
			if (down) {
				buttons[idx].pressStartTime = Time.time;
				buttons[idx].pressState = InputButtonPressState.Down;
			} else if (up) {
				buttons[idx].pressState = InputButtonPressState.Up;
			} else if (pressed) {
				buttons[idx].pressState = InputButtonPressState.Pressed;
			}
		}

		public override bool GetKeyDown (string key) {
			switch(key) {
				case "a": return keyboard.aKey.wasPressedThisFrame;
				case "b": return keyboard.bKey.wasPressedThisFrame;
				case "c": return keyboard.cKey.wasPressedThisFrame;
				case "d": return keyboard.dKey.wasPressedThisFrame;
				case "e": return keyboard.eKey.wasPressedThisFrame;
				case "f": return keyboard.fKey.wasPressedThisFrame;
				case "g": return keyboard.gKey.wasPressedThisFrame;
				case "h": return keyboard.hKey.wasPressedThisFrame;
				case "i": return keyboard.iKey.wasPressedThisFrame;
				case "j": return keyboard.jKey.wasPressedThisFrame;
				case "k": return keyboard.kKey.wasPressedThisFrame;
				case "l": return keyboard.lKey.wasPressedThisFrame;
				case "m": return keyboard.mKey.wasPressedThisFrame;
				case "n": return keyboard.nKey.wasPressedThisFrame;
				case "o": return keyboard.oKey.wasPressedThisFrame;
				case "p": return keyboard.pKey.wasPressedThisFrame;
				case "q": return keyboard.qKey.wasPressedThisFrame;
				case "r": return keyboard.rKey.wasPressedThisFrame;
				case "s": return keyboard.sKey.wasPressedThisFrame;
				case "t": return keyboard.tKey.wasPressedThisFrame;
				case "u": return keyboard.uKey.wasPressedThisFrame;
				case "v": return keyboard.vKey.wasPressedThisFrame;
				case "w": return keyboard.wKey.wasPressedThisFrame;
				case "x": return keyboard.xKey.wasPressedThisFrame;
				case "y": return keyboard.yKey.wasPressedThisFrame;
				case "z": return keyboard.zKey.wasPressedThisFrame;
				case "0": return keyboard.digit0Key.wasPressedThisFrame;
				case "1": return keyboard.digit1Key.wasPressedThisFrame;
				case "2": return keyboard.digit2Key.wasPressedThisFrame;
				case "3": return keyboard.digit3Key.wasPressedThisFrame;
				case "4": return keyboard.digit4Key.wasPressedThisFrame;
				case "5": return keyboard.digit5Key.wasPressedThisFrame;
				case "6": return keyboard.digit6Key.wasPressedThisFrame;
				case "7": return keyboard.digit7Key.wasPressedThisFrame;
				case "8": return keyboard.digit8Key.wasPressedThisFrame;
				case "9": return keyboard.digit9Key.wasPressedThisFrame;
				case "enter": return keyboard.enterKey.wasPressedThisFrame;
				case "escape": return keyboard.escapeKey.wasPressedThisFrame;
				case "f1": return keyboard.f1Key.wasPressedThisFrame;
				case "f2": return keyboard.f2Key.wasPressedThisFrame;
				case "f3": return keyboard.f3Key.wasPressedThisFrame;
				case "f4": return keyboard.f4Key.wasPressedThisFrame;
				case "f5": return keyboard.f5Key.wasPressedThisFrame;
				case "f6": return keyboard.f6Key.wasPressedThisFrame;
				case "f7": return keyboard.f7Key.wasPressedThisFrame;
				case "f8": return keyboard.f8Key.wasPressedThisFrame;
				case "f9": return keyboard.f9Key.wasPressedThisFrame;
				case "f10": return keyboard.f10Key.wasPressedThisFrame;
				case "f11": return keyboard.f11Key.wasPressedThisFrame;
				case "f12": return keyboard.f12Key.wasPressedThisFrame;
				case "tab": return keyboard.tabKey.wasPressedThisFrame;
				case "up": return keyboard.upArrowKey.wasPressedThisFrame;
				case "down": return keyboard.downArrowKey.wasPressedThisFrame;
				case "left": return keyboard.leftArrowKey.wasPressedThisFrame;
				case "right": return keyboard.rightArrowKey.wasPressedThisFrame;
			}
			return false;
		}
	}
}
#endif



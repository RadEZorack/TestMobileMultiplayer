#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace VoxelPlay {
	public class DualTouchControllerInputSystem : VoxelPlayInputController {
		public float dragThreshold = 1f;
		public float rotationSpeed = 0.1f;
		public float alpha = 0.7f;
		public float fadeInSpeed = 2f;

		RectTransform buttonInventoryRT, buttonCrouchRT, buttonJumpRT, buttonBuildRT, buttonActionRT;
		CanvasGroup canvasGroup;
		float startTime;
		bool leftTouched;
		float pressTime;
		Vector2 leftTouchPos;
		bool dragged;

		protected override bool Initialize () {
#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
			return false;
#else
			Transform t = transform.Find("ButtonBuild");
			if (t != null) buttonBuildRT = t.GetComponent<RectTransform>();
			t = transform.Find("ButtonJump");
			if (t != null) buttonJumpRT = t.GetComponent<RectTransform>();
			t = transform.Find("ButtonCrouch");
			if (t != null) buttonCrouchRT = t.GetComponent<RectTransform>();
			t = transform.Find("ButtonInventory");
			if (t != null) buttonInventoryRT = t.GetComponent<RectTransform>();
			t = transform.Find("ButtonAction");
			if (t != null) buttonActionRT = t.GetComponent<RectTransform>();

			canvasGroup = GetComponent<CanvasGroup>();
			if (canvasGroup != null) {
				canvasGroup.alpha = 0;
			}
			startTime = Time.time;
			EnhancedTouchSupport.Enable();
			return true;
#endif
		}

		protected override void UpdateInputState () {
			// Fade in UI
			if (canvasGroup != null && canvasGroup.alpha < alpha) {
				float t = (Time.time - startTime) / fadeInSpeed;
				if (t > alpha) t = alpha;
				canvasGroup.alpha = t;
			}

			// Screen position (last pointer)
			var mouse = Mouse.current;
			if (mouse != null) {
				Vector2 pos = mouse.position.ReadValue();
				screenPos.x = pos.x; screenPos.y = pos.y; screenPos.z = 0;
			}
			focused = true;

			leftTouched = false;
			var touches = ETouch.activeTouches;
			int touchCount = touches.Count;
			for (int i = 0; i < touchCount; i++) {
				ManageTouch(touches[i]);
			}
			if (!leftTouched) {
				horizontalAxis = verticalAxis = 0f;
				anyAxisButtonPressed = false;
			}
		}

		protected override void UpdateImpl () {
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

		void ManageTouch (ETouch t) {
			switch (t.phase) {
				case ETouchPhase.Began:
					if (PointOver(buttonActionRT, t.screenPosition)) { SetDown(InputButtonNames.Action); return; }
					if (PointOver(buttonBuildRT, t.screenPosition)) { SetDown(InputButtonNames.Button2); return; }
					if (PointOver(buttonJumpRT, t.screenPosition)) { SetDown(InputButtonNames.Jump); return; }
					if (PointOver(buttonCrouchRT, t.screenPosition)) { SetDown(InputButtonNames.Crouch); return; }
					if (PointOver(buttonInventoryRT, t.screenPosition)) { SetDown(InputButtonNames.Inventory); return; }
					if (t.screenPosition.x < Screen.width * 0.5f) {
						leftTouched = true;
						leftTouchPos = t.screenPosition;
						return;
					}
					pressTime = Time.time;
					dragged = false;
					break;
				case ETouchPhase.Moved:
					if (t.screenPosition.x < Screen.width * 0.5f) {
						leftTouched = true;
						horizontalAxis = t.screenPosition.x - leftTouchPos.x;
						verticalAxis = t.screenPosition.y - leftTouchPos.y;
						anyAxisButtonPressed = true;
						return;
					}
					pressTime = Time.time;
					float dx = t.delta.x;
					if (dx > 0) { dx -= dragThreshold; if (dx < 0) dx = 0; }
					else if (dx < 0) { dx += dragThreshold; if (dx > 0) dx = 0; }
					dx *= rotationSpeed * 3000f / Screen.width;
					mouseX = mouseX * 0.75f + dx * 0.25f;
					float dy = t.delta.y;
					if (dy > 0) { dy -= dragThreshold; if (dy < 0) dy = 0; }
					else if (dy < 0) { dy += dragThreshold; if (dy > 0) dy = 0; }
					dy *= rotationSpeed * 1500f / Screen.height;
					mouseY = mouseY * 0.75f + dy * 0.25f;
					buttons[(int)InputButtonNames.Button1].pressState = InputButtonPressState.Pressed;
					dragged = true;
					break;
				case ETouchPhase.Ended:
					mouseX = mouseY = 0;
					if (PointOver(buttonActionRT, t.screenPosition)) { SetUp(InputButtonNames.Action); return; }
					if (PointOver(buttonBuildRT, t.screenPosition)) { SetUp(InputButtonNames.Button2); return; }
					if (PointOver(buttonJumpRT, t.screenPosition)) { SetUp(InputButtonNames.Jump); return; }
					if (PointOver(buttonCrouchRT, t.screenPosition)) { SetUp(InputButtonNames.Crouch); return; }
					if (PointOver(buttonInventoryRT, t.screenPosition)) { SetUp(InputButtonNames.Inventory); return; }
					if (!dragged && Time.time - pressTime < 0.3f) {
						SetDown(InputButtonNames.Button1);
					} else {
						SetUp(InputButtonNames.Button1);
					}
					break;
				case ETouchPhase.Stationary:
					if (t.screenPosition.x < Screen.width * 0.5f) {
						leftTouched = true;
						horizontalAxis = t.screenPosition.x - leftTouchPos.x;
						verticalAxis = t.screenPosition.y - leftTouchPos.y;
						anyAxisButtonPressed = true;
						return;
					}
					if (!dragged && Time.time - pressTime > 0.5f) {
						mouseX = mouseY = 0;
						SetDown(InputButtonNames.Button1);
					}
					break;
			}
		}

		bool PointOver (RectTransform rt, Vector2 screen) {
			if (rt == null) return false;
			return RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null);
		}

		void SetDown (InputButtonNames b) {
			int i = (int)b;
			buttons[i].pressStartTime = Time.time;
			buttons[i].pressState = InputButtonPressState.Down;
		}

		void SetUp (InputButtonNames b) {
			int i = (int)b;
			buttons[i].pressState = InputButtonPressState.Up;
		}
	}
}
#endif



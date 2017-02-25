using UnityEngine;
#if (CROSS_PLATFORM_INPUT)
using UnityStandardAssets.CrossPlatformInput;
#endif

namespace GreenByteSoftware.UNetController {
	public class DefaultInputManager : MonoBehaviour, IPLayerInputs {

		public ControllerInputDataObject data;

		private Vector2 inputs;
		private bool sprint;
		private bool jump;
		private bool crouch;
		private float x;
		private float y;

		void Update () {
			#if (CROSS_PLATFORM_INPUT)
			inputs.x = CrossPlatformInputManager.GetAxisRaw ("Horizontal");
			inputs.y = CrossPlatformInputManager.GetAxisRaw ("Vertical");
			#else
			inputs.x = Input.GetAxisRaw ("Horizontal");
			inputs.y = Input.GetAxisRaw ("Vertical");
			#endif
			#if (CROSS_PLATFORM_INPUT)
			if (CrossPlatformInputManager.GetButton ("Jump"))
				jump = true;
			#else
			if (Input.GetKey (KeyCode.Space))
			jump = true;
			#endif
			else
				jump = false;

			#if (CROSS_PLATFORM_INPUT)
			if (CrossPlatformInputManager.GetButton ("Sprint"))
				sprint = true;
			#else
			if (Input.GetKey (KeyCode.LeftShift))
			sprint = true;
			#endif
			else
				sprint = false;

			#if (CROSS_PLATFORM_INPUT)
			if (CrossPlatformInputManager.GetButton ("Crouch"))
				crouch = !crouch;
			#else
			if (Input.GetKeyDown (KeyCode.C))
			crouch = !crouch;
			#endif

			//Change if using different camera control
			if (CameraControl.singleton != null && (CameraControl.singleton.firstPerson || CameraControl.singleton.aiming || !inputs.x.AlmostEquals(0, 0.01f) || !inputs.y.AlmostEquals(0, 0.01f))) {
				if (CameraControl.singleton.firstPerson || CameraControl.singleton.aiming) {
					x = CameraControl.singleton.x;
					y = CameraControl.singleton.y;
				} else {
					x = Mathf.Lerp(x, CameraControl.singleton.x, Time.deltaTime * data.rotInterp);
					y = Mathf.Lerp(y, CameraControl.singleton.y, Time.deltaTime * data.rotInterp);
				}
			} else if (CameraControl.singleton == null) {
				#if (CROSS_PLATFORM_INPUT)
				y -= CrossPlatformInputManager.GetAxisRaw ("Mouse Y") * data.rotateSensitivity;
				x += CrossPlatformInputManager.GetAxisRaw ("Mouse X") * data.rotateSensitivity;
				#else
				y -= Input.GetAxisRaw ("Mouse Y") * data.rotateSensitivity;
				x += Input.GetAxisRaw ("Mouse X") * data.rotateSensitivity;
				#endif
			}
		}

		public float GetMouseX () {
			return x;
		}

		public float GetMouseY () {
			return y;
		}

		public float GetMoveX () {
			return inputs.x;
		}

		public float GetMoveY () {
			return inputs.y;
		}

		public bool GetJump () {
			return jump;
		}

		public bool GetCrouch () {
			return crouch;
		}

		public bool GetSprint () {
			return sprint;
		}

	}
}
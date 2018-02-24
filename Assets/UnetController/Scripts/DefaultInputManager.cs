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
			if (CrossPlatformInputManager.GetButtonDown ("Crouch"))
				crouch = !crouch;
			#else
			if (Input.GetKeyDown (KeyCode.C))
			crouch = !crouch;
			#endif

			if (!sprint && !crouch)
				inputs *= data.walkAxisMultiplier;

			//Change if using different camera control
			if (CameraControl.singleton != null && (CameraControl.singleton.firstPerson || CameraControl.singleton.aiming || !inputs.x.AlmostEquals(0, 0.01f) || !inputs.y.AlmostEquals(0, 0.01f))) {
				if (CameraControl.singleton.firstPerson || CameraControl.singleton.aiming) {
					x = CameraControl.singleton.x;
					y = CameraControl.singleton.y;
				} else {
					float xv = CameraControl.singleton.x;
					if (inputs.y > 0 && inputs.x > 0)
						xv += 45f;
					else if (inputs.y > 0 && inputs.x < 0)
						xv -= 45f;
					else if (inputs.y < 0 && inputs.x == 0)
						xv += 180f;
					else if (inputs.y < 0 && inputs.x > 0)
						xv += 135f;
					else if (inputs.y < 0 && inputs.x < 0)
						xv += 215f;
					else if (inputs.y == 0 && inputs.x > 0)
						xv += 90f;
					else if (inputs.y == 0 && inputs.x < 0)
						xv -= 90f;
					
					x = Mathf.LerpAngle(x, xv, Time.deltaTime * data.rotInterp);
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

		public float GetMoveX() { return GetMoveX (false); }
		public float GetMoveX (bool forceFPS) {
			if (forceFPS || CameraControl.singleton.firstPerson || CameraControl.singleton.aiming)
				return inputs.x;
			else
				return 0;
		}

		public float GetMoveY() { return GetMoveY (false); }
		public float GetMoveY (bool forceFPS) {
			if (forceFPS || CameraControl.singleton.firstPerson || CameraControl.singleton.aiming)
				return inputs.y;
			else
				return inputs.magnitude;
		}

		public Keys GetKeys () {
			Keys val = (Keys)0;
			if (jump)
				val |= Keys.JUMP;
			if (crouch)
				val |= Keys.CROUCH;
			return val;
		}
	}
}
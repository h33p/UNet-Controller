//Script taken from my game, this integrates with various features which do not exist in the current state of the controller (like being inside vehicles, aiming and being in ragdoll state), which can be integrated into any project
//Contains code written by a lot of experimentation. It is really hard to explain how clamping angles works.
//Make sure it is executed after the Controller script

using UnityEngine;
using System.Collections;
#if (CROSS_PLATFORM_INPUT)
using UnityStandardAssets.CrossPlatformInput;
#endif

namespace GreenByteSoftware.UNetController {

	public class CameraControl : MonoBehaviour {

		public Transform target;
		public Transform targetFPS;
		public Transform moveBackVehicleTarget;

		public static CameraControl singleton;

		public ControllerInputDataObject data;

		public bool pause = false;
		public bool ragdollState = false;
		public bool clampedMode = false;
		public bool aiming = false;

		public float moveThreshold = 0.8f;
		public float noMoveTime = 1f;
		public float moveBackSpeed = 3f;
		public float lastMoveTime = 0f;
		public bool moveBackInVehicle = true;
		public bool moveBackInVehicleFP = true;
		public bool moveBackNormal = false;
		[Range (-45f, 45f)]
		public float moveBackVehicleYOffsetTP = 15f;

		public float distance = 2.0f;
		public float distanceNormal = 2.0f;
		public float distanceAim = 0.5f;
		public float minDistance = 0.3f;
		public float distanceNormalVehicle = 4f;
		public float distanceAimVehicle = 1.5f;
		public float distanceFirstPerson = 0f;
		public bool firstPerson = false;
		public bool inVehicle = false;


		public float xMinLimitFPV = -40f;
		public float xMaxLimitFPV = 40f;

		[Range (1f, 180f)]
		public float fovNormal = 60f;
		[Range (1f, 180f)]
		public float fovFirstPerson = 80f;

		public float x = 0.0f;
		public float y = 0.0f;

		public int ping = 0;

		public float rotY;
		public float lastRotY;
		public float yDifference = 0f;
		public int lastmode = 0;
		public int lastmode2 = 0;

		public LayerMask layerMask;

		void Awake () {
			Init ();
		}

		void OnEnable () {
			Init ();
		}

		//Initialization
		void Init () {
			//Sets the static access to this component instance
			if (singleton == null) {
				singleton = this;
				var angles = transform.eulerAngles;
				x = angles.y;
				y = angles.x;
				x = transform.root.rotation.eulerAngles.y;
				lastMoveTime = Time.time;
			} else if (singleton != this) {
				Debug.LogError ("Only one instance of CameraControl can be enabled at the same time!");
				this.enabled = false;
			}
		}

		void OnDisable () {
			if (singleton == this)
				singleton = null;
		}

		//Should be called when local player is spawned
		public static void SetTarget (Transform newTarget, Transform newFPSTarget) {
			if (singleton != null) {
				singleton.target = newTarget;
				singleton.targetFPS = newFPSTarget;
			} else
				Debug.LogWarning ("SetTarget called when no instance exists");
		}
			
		//Sets everything up once the character is spawned
		void Update(){
			if(aiming){
				if(inVehicle)
					distance = Mathf.Lerp(distance, distanceAimVehicle, 5 * Time.deltaTime);
				else
					distance = Mathf.Lerp(distance, distanceAim, 5 * Time.deltaTime);
			}
			else if(!aiming){
				if(inVehicle)
					distance = Mathf.Lerp(distance, distanceNormalVehicle, 5 * Time.deltaTime);
				else
					distance = Mathf.Lerp(distance, distanceNormal, 5 * Time.deltaTime);
			}

			#if (CROSS_PLATFORM_INPUT)
			if (CrossPlatformInputManager.GetButtonDown ("CameraViewChange") && !pause)
			#else
			if (Input.GetKeyDown (KeyCode.V) && !pause)
			#endif
				firstPerson = !firstPerson;

			if(target != null)
				return;

		}
		//Moves the camera
		void LateUpdate () {
			if (target) {
				#if (CROSS_PLATFORM_INPUT)
				if (CrossPlatformInputManager.GetAxisRaw ("Mouse X") > moveThreshold || -CrossPlatformInputManager.GetAxisRaw ("Mouse X") > moveThreshold || CrossPlatformInputManager.GetAxisRaw ("Mouse Y") > moveThreshold || -CrossPlatformInputManager.GetAxisRaw ("Mouse Y") > moveThreshold) {
				#else
				if (Input.GetAxisRaw ("Mouse X") > moveThreshold || -Input.GetAxisRaw ("Mouse X") > moveThreshold || Input.GetAxisRaw ("Mouse Y") > moveThreshold || -Input.GetAxisRaw ("Mouse Y") > moveThreshold) {
				#endif

					lastMoveTime = Time.time;
				}

				if (!firstPerson && Time.time - noMoveTime > lastMoveTime && moveBackNormal && !inVehicle) {
					x = Mathf.LerpAngle (x, target.transform.rotation.eulerAngles.y, moveBackSpeed * Time.deltaTime);
					y = Mathf.LerpAngle (y, target.transform.rotation.eulerAngles.x, moveBackSpeed * Time.deltaTime);
				} if (!firstPerson && Time.time - noMoveTime > lastMoveTime && moveBackInVehicle && inVehicle) {
					x = Mathf.LerpAngle (x, moveBackVehicleTarget.transform.rotation.eulerAngles.y, moveBackSpeed * Time.deltaTime);
					y = Mathf.LerpAngle (y, moveBackVehicleTarget.transform.rotation.eulerAngles.x + moveBackVehicleYOffsetTP, moveBackSpeed * Time.deltaTime);
				}

				if (!pause) {
					#if (CROSS_PLATFORM_INPUT)
					x += CrossPlatformInputManager.GetAxisRaw ("Mouse X") * data.rotateSensitivity;
					y -= CrossPlatformInputManager.GetAxisRaw ("Mouse Y") * data.rotateSensitivity;
					#else
					x += Input.GetAxisRaw ("Mouse X") * xSpeed;
					y -= Input.GetAxisRaw ("Mouse Y") * ySpeed;
					#endif
				}

				lastRotY = rotY;
				rotY = target.transform.root.eulerAngles.y;
				if (lastRotY > rotY && Mathf.Abs (lastRotY - rotY) < Mathf.Abs (lastRotY - 360f - rotY)) {
					
					lastmode2 = 0;
				} else if (lastRotY < rotY && Mathf.Abs (rotY - lastRotY) < Mathf.Abs (rotY - 360f - lastRotY)) {
					
					lastmode2 = 0;
				} else {
					lastmode2 = 1;
				}
					

				if (firstPerson) {

					if (Time.time - noMoveTime > lastMoveTime && moveBackInVehicleFP && inVehicle) {
						x = Mathf.LerpAngle (x, target.transform.rotation.eulerAngles.y, moveBackSpeed * Time.deltaTime);
						y = Mathf.LerpAngle (y, target.transform.rotation.eulerAngles.x, moveBackSpeed * Time.deltaTime);
					}

					GetComponent<Camera> ().fieldOfView = fovFirstPerson;

					y = ClampAngle(y, data.camMinY + FixAngle(target.transform.rotation.eulerAngles.x), data.camMaxY + FixAngle(target.transform.rotation.eulerAngles.x));

					if (ragdollState) {
						y = ClampAngle (y, target.transform.rotation.eulerAngles.x, target.transform.rotation.eulerAngles.x);
						x = ClampAngle (x, target.transform.rotation.eulerAngles.y, target.transform.rotation.eulerAngles.y);
					}

					if (clampedMode)
						x = ClampAngle(x, rotY + xMinLimitFPV, rotY + xMaxLimitFPV, true);
				} else {
					GetComponent<Camera> ().fieldOfView = fovNormal;
					y = ClampAngle(y, data.camMinY, data.camMaxY);
				}

				#if (CROSS_PLATFORM_INPUT)
				if(CrossPlatformInputManager.GetButtonUp("Fire2")) {
				#else
				if(Input.GetKeyUp("Fire2")) {
				#endif
					x = transform.root.rotation.eulerAngles.y;
				}

				Quaternion rotation;

				if(!firstPerson)
					rotation = Quaternion.Euler(y, x, 0);
				else
					rotation = Quaternion.Euler(y, x, target.rotation.eulerAngles.z);
				Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

				if (!firstPerson) {
					RaycastHit hit;
					if (Physics.Linecast (target.position, position, out hit, layerMask)) {
						if (hit.distance >= minDistance)
							position = hit.point;
						else
							position = target.position + (position - target.position).normalized * minDistance;
					}

					position -= (position - target.position).normalized * Camera.main.nearClipPlane;
				}

				transform.rotation = rotation;
				if (!firstPerson)
					transform.position = position;
				else
					transform.position = targetFPS.position;
			}
		}

		float ClampAngle (float angle, float min, float max, bool useMode = false) {
			if (min < -360f) {
				angle += 360f;
				return Mathf.Clamp (angle, min + 360f, max + 360f);
				#pragma warning disable 0162 //Fails to detect that the value only is false by default
				if(useMode)
					lastmode = 1;
				#pragma warning restore 0162
			}
			if (max > 360f) {
				if (useMode) {
					if (lastmode == 0 && lastmode2 == 0) {
						angle -= 360f;
					}
					lastmode = 2;
				}

				if (angle > max - 360f && angle - 360f > min - 360f) {
					angle -= 360f;
				}

				return Mathf.Clamp (angle, min - 360f, max - 360f);
			}
			if (useMode) {
				if (lastmode == 1)
					angle -= 360f;
				else if (lastmode == 2 && lastmode2 == 0)
					angle += 360f;

				lastmode = 0;
			}
			return Mathf.Clamp (angle, min, max);
		}

		static float FixAngle (float angle) {
			if (angle < -180f)
				angle += 360f;
			if (angle > 180f)
				angle -= 360f;
			return angle;
		}
	}
}
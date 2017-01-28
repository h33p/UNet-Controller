//#define SIMULATE
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.Events;

namespace GreenByteSoftware.UNetController {

	[System.Serializable]
	public struct Inputs
	{

		public Vector2 inputs;
		public float x;
		public bool jump;
		public bool sprint;
		public int timestamp;

	}

	[System.Serializable]
	public struct Results
	{
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 speed;
		public bool isGrounded;
		public bool jumped;
		public float groundPoint;
		public float groundPointTime;
		public int timestamp;

		public Results (Vector3 pos, Quaternion rot, Vector3 spe, bool ground, bool jump, float gp, float gpt, int tick) {
			position = pos;
			rotation = rot;
			speed = spe;
			isGrounded = ground;
			jumped = jump;
			groundPoint = gp;
			groundPointTime = gpt;
			timestamp = tick;
		}
	}

	[NetworkSettings (channel=1)]
	public class Controller : NetworkBehaviour {

		private CharacterController controller;
		private Transform myTransform;

		private Vector3 speed;
		[Tooltip("Maximum speed when sprinting in X and Z directions.")]
		public Vector3 maxSpeedSprint = new Vector3 (3f, 0f, 7f);
		[Tooltip("Maximum speed when not sprinting in X and Z directions.")]
		public Vector3 maxSpeedNormal = new Vector3 (1f, 0f, 1.5f);

		[Tooltip("Normal acceleration when moving forward.")]
		public float accelerationForward = 6f;
		[Tooltip("Acceleration when moving backwards.")]
		public float accelerationBack = 3f;
		[Tooltip("Decceleration when the opposite key to the moving direction is pressed.")]
		public float accelerationStop = 8f;
		[Tooltip("Decceleration while not pressing anything.")]
		public float decceleration = 2f;
		[Tooltip("Normal acceleration to the sides of the player.")]
		public float accelerationSides = 4f;
		[Tooltip("Upwards speed to be set while jumping.")]
		public float speedJump = 3f;
		[Tooltip("Acceleration while strafing.")]
		public float accelerationStrafe = 6f;
		[Tooltip("Curve which multiplies strafing acceleration based on the angle difference (in degrees per second).")]
		public AnimationCurve strafeAngleCurve;
		[Tooltip("Strafing acceleration to speed multiplier.")]
		public AnimationCurve strafeToSpeedCurve;
			
		[Tooltip("The speed at point 1 in the strafe to speed curve.")]
		public float strafeToSpeedCurveScale = 18f;
		//Private variables used to optimize for mobile's instruction set
		private float strafeToSpeedCurveScaleMul;
		private float _strafeToSpeedCurveScale;

		[Tooltip("The closest multiple of the value the player position is set to.")]
		[Range(0,1)]
		public float snapSize = 0.02f;
		private float snapInvert;

		Vector3 interpPos;

		public float rotateSensitivity = 3f;

		[Tooltip("Period in seconds how often the network events happen. Note, not the actual value is used, the closest multiple of FixedUpdates is calculated and it is used instead.")]
		[Range (0.01f, 1f)]
		public float sendRate = 0.1f;

		private int _sendUpdates;

		public int sendUpdates {
			get { return _sendUpdates; }
		}

		private int currentFixedUpdates = 0;

		private int currentTick = 0;

		[Tooltip("Maximum number of inputs to store. The higher the number, the bigger the latency can be to have smooth reconceliation. However, if the latency is big, this can result in big performance overhead.")]
		[Range (1, 300)]
		public int inputsToStore = 10;

		private List<Inputs> clientInputs;
		private Inputs curInput;
		private Inputs curInputServer;

		private List<Results> clientResults;
		private Results serverResults;
		private Results tempResults;
		private Results lastResults;

		[Tooltip("Number of server results to buffer. This stores the minimum nuber of server results, keeps them sorted and once another update comes, takes the first result and uses in reconciliation. Good for latency differences.")]
		[Range (1, 20)]
		public int serverResultsBuffer = 3;
		[Tooltip("Same as above, but on the server side, using player inputs.")]
		[Range (1, 20)]
		public int clientInputsBuffer = 3;

		private List<Results> serverResultList;

		public Transform cam;
		public Transform camTarget;

		public enum MoveType
		{
			UpdateOnce = 1,
			UpdateOnceAndLerp = 2,
			UpdateOnceAndSLerp = 3
		};

		[Tooltip("Movement type to use. UpdateOnceAndLerp works the best.")]
		public MoveType movementType;

		private Vector3 posStart;
		private Vector3 posEnd;
		#pragma warning disable 0414 //Currently this variable is not in use anywhere, but we can not remove it so just suppress the warnings
		private float posEndG;
		#pragma warning restore 0414
		private Vector3 posEndO;
		private Quaternion rotStart;
		private Quaternion rotEnd;
		#pragma warning disable 0414
		private Quaternion rotEndO;

		private float groundPointTime;
		#pragma warning restore 0414
		private float startTime;

		private bool reconciliate = false;
		[Tooltip("In development feature to handle hitting and jumping of the ground while mid-tick.")]
		public bool handleMidTickJump = false;

		public override float GetNetworkSendInterval () {
			return sendRate;
		}

		void Start () {

			myTransform = transform;

			if (snapSize > 0)
				snapInvert = 1f / snapSize;

			clientInputs = new List<Inputs>();
			clientResults = new List<Results>();
			serverResultList = new List<Results>();

			controller = GetComponent<CharacterController> ();
			curInput = new Inputs ();
			curInput.x = myTransform.rotation.eulerAngles.y;
			curInput.inputs = new Vector2 ();

			posStart = myTransform.position;
			rotStart = myTransform.rotation;

			cam = Camera.main.transform;

			posEnd = myTransform.position;
			rotEnd = myTransform.rotation;

			_sendUpdates = Mathf.RoundToInt (sendRate / Time.fixedDeltaTime);

			if (isServer)
				curInput.timestamp = -1000;
		}

		[Command]
		void CmdSendInputs (Inputs inp) {
			#if (SIMULATE)
			StartCoroutine (SendInputs (inp));
		}

		IEnumerator SendInputs (Inputs inp) {
			yield return new WaitForSeconds (Random.Range (0.21f, 0.28f));
			#endif

			if (!isLocalPlayer) {

				if (clientInputs.Count > clientInputsBuffer)
					clientInputs.RemoveAt (0);

				if (!ClientInputsContainTimestamp (inp.timestamp))
					clientInputs.Add (inp);

				curInputServer = SortClientInputsAndReturnFirst ();

				if (inp.timestamp > curInput.timestamp)
					curInputServer = inp;
			}
		}

		[ClientRpc]
		void RpcSendResults (Results res) {

			if (isServer)
				return;
			#if (SIMULATE)
			StartCoroutine (SendResults (res));
		}

		IEnumerator SendResults (Results res) {
			yield return new WaitForSeconds (Random.Range (0.21f, 0.38f));
			#endif

			if (isLocalPlayer) {

				foreach (Results t in clientResults) {
					if (t.timestamp == res.timestamp)
						Debug_UI.UpdateUI (posEnd, res.position, t.position, currentTick, res.timestamp);
				}

				if (serverResultList.Count > serverResultsBuffer)
					serverResultList.RemoveAt (0);

				if (!ServerResultsContainTimestamp (res.timestamp))
					serverResultList.Add (res);

				serverResults = SortServerResultsAndReturnFirst ();

				if (serverResultList.Count >= serverResultsBuffer)
					reconciliate = true;

			} else {
				currentTick++;

				if (!isServer)
					serverResults = res;

				if (currentTick > 2) {
					serverResults = res;
					posStart = posEnd;
					rotStart = rotEnd;
					if (Time.fixedTime - 2f > startTime)
						startTime = Time.fixedTime;
					else
						startTime = Time.fixedTime - ((Time.fixedTime - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1) * (Time.fixedDeltaTime * _sendUpdates);
					posEnd = posEndO;
					rotEnd = rotEndO;
					groundPointTime = serverResults.groundPointTime;
					posEndG = serverResults.groundPoint;
					posEndO = serverResults.position;
					rotEndO = serverResults.rotation;
				} else {
					startTime = Time.fixedTime;
					serverResults = res;
					posStart = serverResults.position;
					rotStart = serverResults.rotation;
					posEnd = posStart;
					rotEnd = rotStart;
					groundPointTime = serverResults.groundPointTime;
					posEndG = posEndO.y;
					posEndO = posStart;
					rotEndO = rotStart;

				}
			}
		}

		Results SortServerResultsAndReturnFirst () {

			Results tempRes;

			for (int x = 0; x < serverResultList.Count; x++) {
				for (int y = 0; y < serverResultList.Count - 1; y++) {
					if (serverResultList [y].timestamp > serverResultList [y + 1].timestamp) {
						tempRes = serverResultList [y + 1];
						serverResultList [y + 1] = serverResultList [y];
						serverResultList [y] = tempRes;
					}
				}
			}

			if (serverResultList.Count > serverResultsBuffer)
				serverResultList.RemoveAt (0);


			return serverResultList [0];
		}

		bool ServerResultsContainTimestamp (int timeStamp) {
			for (int i = 0; i < serverResultList.Count; i++) {
				if (serverResultList [i].timestamp == timeStamp)
					return true;
			}

			return false;
		}

		Inputs SortClientInputsAndReturnFirst () {

			Inputs tempInp;

			for (int x = 0; x < clientInputs.Count; x++) {
				for (int y = 0; y < clientInputs.Count - 1; y++) {
					if (clientInputs [y].timestamp > clientInputs [y + 1].timestamp) {
						tempInp = clientInputs [y + 1];
						clientInputs [y + 1] = clientInputs [y];
						clientInputs [y] = tempInp;
					}
				}
			}

			if (clientInputs.Count > clientInputsBuffer)
				clientInputs.RemoveAt (0);


			return clientInputs [0];
		}

		bool ClientInputsContainTimestamp (int timeStamp) {
			for (int i = 0; i < clientInputs.Count; i++) {
				if (clientInputs [i].timestamp == timeStamp)
					return true;
			}

			return false;
		}

		void Reconciliate () {

			for (int i = 0; i < clientResults.Count; i++) {
				if (clientResults [i].timestamp == serverResults.timestamp) {
					clientResults.RemoveRange (0, i);
					for (int o = 0; o < clientInputs.Count; o++) {
						if (clientInputs [o].timestamp == serverResults.timestamp) {
							clientInputs.RemoveRange (0, o);
							break;
						}
					}
					break;
				}
			}

			tempResults = serverResults;

			controller.enabled = true;

			for (int i = 1; i < clientInputs.Count - 1; i++) {
				tempResults = MoveCharacter (tempResults, clientInputs [i], Time.fixedDeltaTime * sendUpdates, maxSpeedNormal);
			}

			groundPointTime = tempResults.groundPointTime;
			posEnd = tempResults.position;
			rotEnd = tempResults.rotation;
			posEndG = tempResults.groundPoint;

		}

		void Update () {
			if (isLocalPlayer) {
				curInput.inputs.x = Input.GetAxisRaw ("Horizontal");
				curInput.inputs.y = Input.GetAxisRaw ("Vertical");

				if (Input.GetKey (KeyCode.Space))
					curInput.jump = true;
				else
					curInput.jump = false;

				if (Input.GetKey (KeyCode.LeftShift))
					curInput.sprint = true;
				else
					curInput.sprint = false;

				curInput.x += Input.GetAxisRaw ("Mouse X") * rotateSensitivity;

				if (curInput.x > 360f)
					curInput.x -= 360f;
				else if (curInput.x < 0f)
					curInput.x += 360f;

			}
		}

		void FixedUpdate () {

			if (strafeToSpeedCurveScale != _strafeToSpeedCurveScale) {
				_strafeToSpeedCurveScale = strafeToSpeedCurveScale;
				strafeToSpeedCurveScaleMul = 1f / strafeToSpeedCurveScale;
			}

			if (isLocalPlayer || isServer)
				currentFixedUpdates++;

			if (isLocalPlayer && currentFixedUpdates >= sendUpdates) {
				currentTick++;

				CmdSendInputs (curInput);

				if (!isServer) {
					clientResults.Add (lastResults);
				}

				if (clientInputs.Count >= inputsToStore)
					clientInputs.RemoveAt (0);

				clientInputs.Add (curInput);
				curInput.timestamp = currentTick;

				posStart = myTransform.position;
				rotStart = myTransform.rotation;
				startTime = Time.fixedTime;

				if (reconciliate) {
					Reconciliate ();
					lastResults = tempResults;
					reconciliate = false;
				}
					
				controller.enabled = true;
				lastResults = MoveCharacter (lastResults, clientInputs [clientInputs.Count - 1], Time.fixedDeltaTime * _sendUpdates, maxSpeedNormal);
				speed = lastResults.speed;
				controller.enabled = false;
				posEnd = lastResults.position;
				groundPointTime = lastResults.groundPointTime;
				posEndG = lastResults.groundPoint;
				rotEnd = lastResults.rotation;
			}

			if (isServer && currentFixedUpdates >= sendUpdates) {

				if (isLocalPlayer) {
					serverResults.position = myTransform.position;
					serverResults.rotation = myTransform.rotation;
					serverResults.speed = speed;
					serverResults.timestamp = curInput.timestamp;

					RpcSendResults (serverResults);
				}

				if (!isLocalPlayer) {
					currentFixedUpdates = 0;
					//if (clientInputs.Count == 0)
					//	clientInputs.Add (curInputServer);
					//clientInputs[clientInputs.Count - 1] = curInputServer;
					curInput = curInputServer;

					posStart = myTransform.position;
					rotStart = myTransform.rotation;
					startTime = Time.fixedTime;
					controller.enabled = true;
					serverResults = MoveCharacter (serverResults, curInput, Time.fixedDeltaTime * _sendUpdates, maxSpeedNormal);
					speed = serverResults.speed;
					groundPointTime = serverResults.groundPointTime;
					posEnd = serverResults.position;
					rotEnd = serverResults.rotation;
					posEndG = serverResults.groundPoint;
					controller.enabled = false;

					RpcSendResults (serverResults);
				}

			}

			if (isLocalPlayer && currentFixedUpdates >= sendUpdates)
				currentFixedUpdates = 0;
		}

		void LateUpdate () {
			if (movementType == MoveType.UpdateOnceAndLerp) {
				if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
					interpPos = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					//if ((Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= groundPointTime)
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates * groundPointTime));
					//else
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime + (groundPointTime * Time.fixedDeltaTime * _sendUpdates)) / (Time.fixedDeltaTime * _sendUpdates * (1f - groundPointTime)));
					myTransform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					myTransform.position = interpPos;
				} else if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {

					myTransform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				} else {
					myTransform.position = Vector3.Lerp (posEnd, posEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
					myTransform.rotation = Quaternion.Lerp (rotEnd, rotEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
				}
			} else if (movementType == MoveType.UpdateOnceAndSLerp) {
				if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
					interpPos = Vector3.Slerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					//if ((Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= groundPointTime)
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates * groundPointTime));
					//else
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime + (groundPointTime * Time.fixedDeltaTime * _sendUpdates)) / (Time.fixedDeltaTime * _sendUpdates * (1f - groundPointTime)));
					myTransform.rotation = Quaternion.Slerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					myTransform.position = interpPos;
				} else if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {

					myTransform.rotation = Quaternion.Slerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				} else {
					myTransform.position = Vector3.Slerp (posEnd, posEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
					myTransform.rotation = Quaternion.Slerp (rotEnd, rotEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
				}
			} else {
				myTransform.position = posEnd;
				myTransform.rotation = rotEnd;
			}

			if (isLocalPlayer) {
				cam.rotation = Quaternion.Euler (new Vector3 (0f, curInput.x, 0f));
				cam.position = camTarget.position;
			}
		}


		//Actual movement code. Mostly isolated, except transform
		Results MoveCharacter (Results inpRes, Inputs inp, float deltaMultiplier, Vector3 maxSpeed) {

			Vector3 pos = myTransform.position;
			Quaternion rot = myTransform.rotation;

			myTransform.position = inpRes.position;
			myTransform.rotation = inpRes.rotation;

			BaseMovement (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed);

			AirStrafe (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed);

			myTransform.rotation = Quaternion.Euler (new Vector3 (0, inp.x, 0));

			float tY = myTransform.position.y;

			controller.Move (myTransform.TransformDirection(inpRes.speed) * deltaMultiplier);

			float gpt = 1f;
			float gp = myTransform.position.y;

			//WIP, broken, Handles hitting ground while spacebar is pressed. It determines how much time was left to move based on at which height the player hit the ground. Some math involved.
			if (handleMidTickJump && !inpRes.isGrounded && tY - gp >= 0 && inp.jump && (controller.isGrounded || Physics.Raycast (myTransform.position + controller.center, Vector3.down, (controller.height / 2) + (controller.skinWidth * 1.5f)))) {
				float oSpeed = inpRes.speed.y;
				gpt = (tY - gp) / (-oSpeed);
				inpRes.speed.y = speedJump + ((Physics.gravity.y / 2) * Mathf.Abs((1f - gpt) * deltaMultiplier));
				Debug.Log (inpRes.speed.y + " " + gpt);
				controller.Move (myTransform.TransformDirection (0, inpRes.speed.y * deltaMultiplier, 0));
				inpRes.isGrounded = true;
				Debug.DrawLine (new Vector3( myTransform.position.x, gp, myTransform.position.z), myTransform.position, Color.blue, deltaMultiplier);
				inpRes.jumped = true;
			}

			if (snapSize > 0f)
				myTransform.position = new Vector3 (Mathf.Round (myTransform.position.x * snapInvert) * snapSize, Mathf.Round (myTransform.position.y * snapInvert) * snapSize, Mathf.Round (myTransform.position.z * snapInvert) * snapSize);

			inpRes = new Results (myTransform.position, myTransform.rotation, inpRes.speed, controller.isGrounded, inpRes.jumped, gp, gpt, inp.timestamp);

			myTransform.position = pos;
			myTransform.rotation = rot;

			return inpRes;
		}

		public void AirStrafe(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed) {
			if (inpRes.isGrounded)
				return;

			float tAccel = strafeAngleCurve.Evaluate(Mathf.Abs (inpRes.rotation.eulerAngles.y - inp.x) / deltaMultiplier);
			bool rDir = (inpRes.rotation.eulerAngles.y - inp.x) > 0;

			if (inp.inputs.x > 0f && inp.inputs.y == 0 && !rDir)
				inpRes.speed.z += tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x < 0f && inp.inputs.y == 0 && rDir)
				inpRes.speed.z += tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x > 0f && inp.inputs.y == 0 && rDir)
				inpRes.speed.z -= tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x < 0f && inp.inputs.y == 0 && !rDir)
				inpRes.speed.z -= tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y > 0f && inp.inputs.x == 0 && !rDir)
				inpRes.speed.x -= tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y < 0f && inp.inputs.x == 0 && rDir)
				inpRes.speed.x -= tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y > 0f && inp.inputs.x == 0 && rDir)
				inpRes.speed.x += tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y < 0f && inp.inputs.x == 0 && !rDir)
				inpRes.speed.x += tAccel * strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
		}

		public void BaseMovement(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed) {
			if (inp.sprint)
				maxSpeed = maxSpeedSprint;

			inpRes.jumped = false;

			if (inpRes.isGrounded && inp.jump) {
				inpRes.speed.y = speedJump;
				inpRes.jumped = true;
			} else if (!inpRes.isGrounded)
				inpRes.speed.y += Physics.gravity.y * deltaMultiplier;
			else
				inpRes.speed.y = Physics.gravity.y * deltaMultiplier;

			if (inpRes.isGrounded) {
				if (inpRes.speed.x >= 0f && inp.inputs.x > 0f && inpRes.speed.x < maxSpeed.x) {
					inpRes.speed.x += accelerationSides * deltaMultiplier;
					if (inpRes.speed.x > maxSpeed.x)
						inpRes.speed.x = maxSpeed.x;
				} else if (inpRes.speed.x >= 0f && (inp.inputs.x < 0f || inpRes.speed.x > maxSpeed.x))
					inpRes.speed.x -= accelerationStop * deltaMultiplier;
				else if (inpRes.speed.x <= 0f && inp.inputs.x < 0f && inpRes.speed.x > -maxSpeed.x) {
					inpRes.speed.x -= accelerationSides * deltaMultiplier;
					if (inpRes.speed.x < -maxSpeed.x)
						inpRes.speed.x = -maxSpeed.x;
				} else if (inpRes.speed.x <= 0f && (inp.inputs.x > 0f || inpRes.speed.x < -maxSpeed.x))
					inpRes.speed.x += accelerationStop * deltaMultiplier;
				else if (inpRes.speed.x > 0f) {
					inpRes.speed.x -= decceleration * deltaMultiplier;
					if (inpRes.speed.x < 0f)
						inpRes.speed.x = 0f;
				} else if (inpRes.speed.x < 0f) {
					inpRes.speed.x += decceleration * deltaMultiplier;
					if (inpRes.speed.x > 0f)
						inpRes.speed.x = 0f;
				} else
					inpRes.speed.x = 0;

				if (inpRes.speed.z >= 0f && inp.inputs.y > 0f && inpRes.speed.z < maxSpeed.z) {
					inpRes.speed.z += accelerationSides * deltaMultiplier;
					if (inpRes.speed.z > maxSpeed.z)
						inpRes.speed.z = maxSpeed.z;
				} else if (inpRes.speed.z >= 0f && (inp.inputs.y < 0f || inpRes.speed.z > maxSpeed.z))
					inpRes.speed.z -= accelerationStop * deltaMultiplier;
				else if (inpRes.speed.z <= 0f && inp.inputs.y < 0f && inpRes.speed.z > -maxSpeed.z) {
					inpRes.speed.z -= accelerationSides * deltaMultiplier;
					if (inpRes.speed.z < -maxSpeed.z)
						inpRes.speed.z = -maxSpeed.z;
				} else if (inpRes.speed.z <= 0f && (inp.inputs.y > 0f || inpRes.speed.z < -maxSpeed.z))
					inpRes.speed.z += accelerationStop * deltaMultiplier;
				else if (inpRes.speed.z > 0f) {
					inpRes.speed.z -= decceleration * deltaMultiplier;
					if (inpRes.speed.z < 0f)
						inpRes.speed.z = 0f;
				} else if (inpRes.speed.z < 0f) {
					inpRes.speed.z += decceleration * deltaMultiplier;
					if (inpRes.speed.z > 0f)
						inpRes.speed.z = 0f;
				} else
					inpRes.speed.z = 0;
			}
		}
	}
}
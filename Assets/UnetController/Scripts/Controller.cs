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

		public ControllerDataObject data;

		[System.NonSerialized]
		public CharacterController controller;
		[System.NonSerialized]
		public Transform myTransform;

		private Vector3 speed;
		//Private variables used to optimize for mobile's instruction set
		private float strafeToSpeedCurveScaleMul;
		private float _strafeToSpeedCurveScale;

		private float snapInvert;

		Vector3 interpPos;

		private int _sendUpdates;

		public int sendUpdates {
			get { return _sendUpdates; }
		}

		private int currentFixedUpdates = 0;

		private int currentTick = 0;

		[System.NonSerialized]
		public List<Inputs> clientInputs;
		private Inputs curInput;
		private Inputs curInputServer;

		[System.NonSerialized]
		public List<Results> clientResults;
		private Results serverResults;
		private Results tempResults;
		private Results lastResults;

		private List<Results> serverResultList;

		public Transform cam;
		public Transform camTarget;


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

		public override float GetNetworkSendInterval () {
			if (data != null)
				return data.sendRate;
			else
				return 0.1f;
		}

		void Start () {

			if (data == null) {
				Debug.LogError ("No controller data attached! Will not continue.");
				this.enabled = false;
				return;
			}

			myTransform = transform;

			if (data.snapSize > 0)
				snapInvert = 1f / data.snapSize;

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

			_sendUpdates = Mathf.RoundToInt (data.sendRate / Time.fixedDeltaTime);

			if (isServer)
				curInput.timestamp = -1000;

			LagCompensationManager.RegisterController (this);
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

				if (clientInputs.Count > data.clientInputsBuffer)
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

				if (serverResultList.Count > data.serverResultsBuffer)
					serverResultList.RemoveAt (0);

				if (!ServerResultsContainTimestamp (res.timestamp))
					serverResultList.Add (res);

				serverResults = SortServerResultsAndReturnFirst ();

				if (serverResultList.Count >= data.serverResultsBuffer)
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

			if (serverResultList.Count > data.serverResultsBuffer)
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

			if (clientInputs.Count > data.clientInputsBuffer)
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
				tempResults = MoveCharacter (tempResults, clientInputs [i], Time.fixedDeltaTime * sendUpdates, data.maxSpeedNormal);
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

				curInput.x += Input.GetAxisRaw ("Mouse X") * data.rotateSensitivity;

				if (curInput.x > 360f)
					curInput.x -= 360f;
				else if (curInput.x < 0f)
					curInput.x += 360f;

			}
		}

		void FixedUpdate () {

			if (data.strafeToSpeedCurveScale != _strafeToSpeedCurveScale) {
				_strafeToSpeedCurveScale = data.strafeToSpeedCurveScale;
				strafeToSpeedCurveScaleMul = 1f / data.strafeToSpeedCurveScale;
			}

			if (isLocalPlayer || isServer)
				currentFixedUpdates++;

			if (isLocalPlayer && currentFixedUpdates >= sendUpdates) {
				currentTick++;

				CmdSendInputs (curInput);

				if (!isServer) {
					clientResults.Add (lastResults);
				}

				if (clientInputs.Count >= data.inputsToStore)
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
				lastResults = MoveCharacter (lastResults, clientInputs [clientInputs.Count - 1], Time.fixedDeltaTime * _sendUpdates, data.maxSpeedNormal);
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
					serverResults = MoveCharacter (serverResults, curInput, Time.fixedDeltaTime * _sendUpdates, data.maxSpeedNormal);
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
			if (data.movementType == MoveType.UpdateOnceAndLerp) {
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
			} else if (data.movementType == MoveType.UpdateOnceAndSLerp) {
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
			if (data.handleMidTickJump && !inpRes.isGrounded && tY - gp >= 0 && inp.jump && (controller.isGrounded || Physics.Raycast (myTransform.position + controller.center, Vector3.down, (controller.height / 2) + (controller.skinWidth * 1.5f)))) {
				float oSpeed = inpRes.speed.y;
				gpt = (tY - gp) / (-oSpeed);
				inpRes.speed.y = data.speedJump + ((Physics.gravity.y / 2) * Mathf.Abs((1f - gpt) * deltaMultiplier));
				Debug.Log (inpRes.speed.y + " " + gpt);
				controller.Move (myTransform.TransformDirection (0, inpRes.speed.y * deltaMultiplier, 0));
				inpRes.isGrounded = true;
				Debug.DrawLine (new Vector3( myTransform.position.x, gp, myTransform.position.z), myTransform.position, Color.blue, deltaMultiplier);
				inpRes.jumped = true;
			}

			if (data.snapSize > 0f)
				myTransform.position = new Vector3 (Mathf.Round (myTransform.position.x * snapInvert) * data.snapSize, Mathf.Round (myTransform.position.y * snapInvert) * data.snapSize, Mathf.Round (myTransform.position.z * snapInvert) * data.snapSize);

			inpRes = new Results (myTransform.position, myTransform.rotation, inpRes.speed, controller.isGrounded, inpRes.jumped, gp, gpt, inp.timestamp);

			myTransform.position = pos;
			myTransform.rotation = rot;

			return inpRes;
		}

		public void AirStrafe(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed) {
			if (inpRes.isGrounded)
				return;

			float tAccel = data.strafeAngleCurve.Evaluate(Mathf.Abs (inpRes.rotation.eulerAngles.y - inp.x) / deltaMultiplier);
			bool rDir = (inpRes.rotation.eulerAngles.y - inp.x) > 0;

			if (inp.inputs.x > 0f && inp.inputs.y == 0 && !rDir)
				inpRes.speed.z += tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x < 0f && inp.inputs.y == 0 && rDir)
				inpRes.speed.z += tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x > 0f && inp.inputs.y == 0 && rDir)
				inpRes.speed.z -= tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.x < 0f && inp.inputs.y == 0 && !rDir)
				inpRes.speed.z -= tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.z) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y > 0f && inp.inputs.x == 0 && !rDir)
				inpRes.speed.x -= tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y < 0f && inp.inputs.x == 0 && rDir)
				inpRes.speed.x -= tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y > 0f && inp.inputs.x == 0 && rDir)
				inpRes.speed.x += tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
			else if (inp.inputs.y < 0f && inp.inputs.x == 0 && !rDir)
				inpRes.speed.x += tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(inpRes.speed.x) * strafeToSpeedCurveScaleMul);
		}

		public void BaseMovement(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed) {
			if (inp.sprint)
				maxSpeed = data.maxSpeedSprint;

			inpRes.jumped = false;

			if (inpRes.isGrounded && inp.jump) {
				inpRes.speed.y = data.speedJump;
				inpRes.jumped = true;
			} else if (!inpRes.isGrounded)
				inpRes.speed.y += Physics.gravity.y * deltaMultiplier;
			else
				inpRes.speed.y = Physics.gravity.y * deltaMultiplier;

			if (inpRes.isGrounded) {
				if (inpRes.speed.x >= 0f && inp.inputs.x > 0f && inpRes.speed.x < maxSpeed.x) {
					inpRes.speed.x += data.accelerationSides * deltaMultiplier;
					if (inpRes.speed.x > maxSpeed.x)
						inpRes.speed.x = maxSpeed.x;
				} else if (inpRes.speed.x >= 0f && (inp.inputs.x < 0f || inpRes.speed.x > maxSpeed.x))
					inpRes.speed.x -= data.accelerationStop * deltaMultiplier;
				else if (inpRes.speed.x <= 0f && inp.inputs.x < 0f && inpRes.speed.x > -maxSpeed.x) {
					inpRes.speed.x -= data.accelerationSides * deltaMultiplier;
					if (inpRes.speed.x < -maxSpeed.x)
						inpRes.speed.x = -maxSpeed.x;
				} else if (inpRes.speed.x <= 0f && (inp.inputs.x > 0f || inpRes.speed.x < -maxSpeed.x))
					inpRes.speed.x += data.accelerationStop * deltaMultiplier;
				else if (inpRes.speed.x > 0f) {
					inpRes.speed.x -= data.decceleration * deltaMultiplier;
					if (inpRes.speed.x < 0f)
						inpRes.speed.x = 0f;
				} else if (inpRes.speed.x < 0f) {
					inpRes.speed.x += data.decceleration * deltaMultiplier;
					if (inpRes.speed.x > 0f)
						inpRes.speed.x = 0f;
				} else
					inpRes.speed.x = 0;

				if (inpRes.speed.z >= 0f && inp.inputs.y > 0f && inpRes.speed.z < maxSpeed.z) {
					inpRes.speed.z += data.accelerationSides * deltaMultiplier;
					if (inpRes.speed.z > maxSpeed.z)
						inpRes.speed.z = maxSpeed.z;
				} else if (inpRes.speed.z >= 0f && (inp.inputs.y < 0f || inpRes.speed.z > maxSpeed.z))
					inpRes.speed.z -= data.accelerationStop * deltaMultiplier;
				else if (inpRes.speed.z <= 0f && inp.inputs.y < 0f && inpRes.speed.z > -maxSpeed.z) {
					inpRes.speed.z -= data.accelerationSides * deltaMultiplier;
					if (inpRes.speed.z < -maxSpeed.z)
						inpRes.speed.z = -maxSpeed.z;
				} else if (inpRes.speed.z <= 0f && (inp.inputs.y > 0f || inpRes.speed.z < -maxSpeed.z))
					inpRes.speed.z += data.accelerationStop * deltaMultiplier;
				else if (inpRes.speed.z > 0f) {
					inpRes.speed.z -= data.decceleration * deltaMultiplier;
					if (inpRes.speed.z < 0f)
						inpRes.speed.z = 0f;
				} else if (inpRes.speed.z < 0f) {
					inpRes.speed.z += data.decceleration * deltaMultiplier;
					if (inpRes.speed.z > 0f)
						inpRes.speed.z = 0f;
				} else
					inpRes.speed.z = 0;
			}
		}
	}
}
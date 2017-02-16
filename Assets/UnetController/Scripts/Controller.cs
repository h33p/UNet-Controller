//#define SIMULATE
//#define CLIENT_TRUST

using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using UnityEngine.Events;

namespace GreenByteSoftware.UNetController {

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Inputs
	{

		public Vector2 inputs;
		public float x;
		public float y;
		public bool jump;
		public bool crouch;
		public bool sprint;
		public int timestamp;

	}

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Results
	{
		public Vector3 position;
		public Quaternion rotation;
		public float camX;
		public Vector3 speed;
		public bool isGrounded;
		public bool jumped;
		public bool crouch;
		public float groundPoint;
		public float groundPointTime;
		public int timestamp;

		public Results (Vector3 pos, Quaternion rot, float cam, Vector3 spe, bool ground, bool jump, bool crch, float gp, float gpt, int tick) {
			position = pos;
			rotation = rot;
			camX = cam;
			speed = spe;
			isGrounded = ground;
			jumped = jump;
			crouch = crch;
			groundPoint = gp;
			groundPointTime = gpt;
			timestamp = tick;
		}

		public string ToString () {
			return "" + position + "\n"
			+ rotation + "\n"
			+ camX + "\n"
			+ speed + "\n"
			+ isGrounded + "\n"
			+ jumped + "\n"
			+ crouch + "\n"
			+ groundPoint + "\n"
			+ groundPointTime + "\n"
			+ timestamp + "\n";
		}
	}

	[System.Serializable]
	public class TickUpdateEvent : UnityEvent<Results>{}

	[System.Serializable]
	public class TickUpdateAllEvent : UnityEvent<Inputs, Results>{}

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
		private int currentTFixedUpdates = 0;

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
		private float headStartRot = 0f;
		private float headEndRot = 0f;

		private float groundPointTime;
		#pragma warning restore 0414
		private float startTime;

		private bool reconciliate = false;

		private int lastTick = -1;
		private bool receivedFirstTime;

		public TickUpdateEvent onTickUpdate;

		public TickUpdateAllEvent onTickUpdateDebug;

		public override float GetNetworkSendInterval () {
			if (data != null)
				return data.sendRate;
			else
				return 0.1f;
		}

		private float _crouchSwitchMul = -1;

		public float crouchSwitchMul {
			get {
				if (_crouchSwitchMul >= 0)
					return _crouchSwitchMul;
				else if (data != null) {
					_crouchSwitchMul = 1 / (data.controllerCrouchSwitch / (Time.fixedDeltaTime * sendUpdates));
					return _crouchSwitchMul;
				}
				return 0;
			}
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
		#if (CLIENT_TRUST)
		void CmdSendInputs (Inputs inp, Results res) {
		#else
		void CmdSendInputs (Inputs inp) {
		#endif
			#if (SIMULATE)
			#if (CLIENT_TRUST)
			StartCoroutine (SendInputs (inp, res));
			#else
			StartCoroutine (SendInputs (inp));
			#endif
		}
		#if (CLIENT_TRUST)
		IEnumerator SendInputs (Inputs inp, Results res) {
		#else
		IEnumerator SendInputs (Inputs inp) {
		#endif
			yield return new WaitForSeconds (UnityEngine.Random.Range (0.21f, 0.28f));
			#endif

			if (!isLocalPlayer) {

				if (clientInputs.Count > data.clientInputsBuffer)
					clientInputs.RemoveAt (0);

				if (!ClientInputsContainTimestamp (inp.timestamp))
					clientInputs.Add (inp);

				#if (CLIENT_TRUST)
				tempResults = res;
				#endif

				currentTFixedUpdates += sendUpdates;

				if (data.debug && lastTick + 1 != inp.timestamp && lastTick != -1) {
					Debug.Log ("Missing tick " + lastTick + 1);
				}
				lastTick = inp.timestamp;
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
			yield return new WaitForSeconds (UnityEngine.Random.Range (0.21f, 0.38f));
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

				if (!isServer) {
					serverResults = res;
					onTickUpdate.Invoke (res);
				}

				if (currentTick > 2) {
					serverResults = res;
					posStart = posEnd;
					rotStart = rotEnd;
					headStartRot = headEndRot;
					headEndRot = res.camX;
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
					headStartRot = headEndRot;
					headEndRot = res.camX;
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

		//Function which replays the old inputs if prediction errors occur
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

		//Input gathering
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

				if (Input.GetKeyDown (KeyCode.C))
					curInput.crouch = !curInput.crouch;

				curInput.y -= Input.GetAxisRaw ("Mouse Y") * data.rotateSensitivity;
				curInput.x += Input.GetAxisRaw ("Mouse X") * data.rotateSensitivity;

				curInput.y = Mathf.Clamp (curInput.y, data.camMinY, data.camMaxY);

				if (curInput.x > 360f)
					curInput.x -= 360f;
				else if (curInput.x < 0f)
					curInput.x += 360f;

			}
		}

		//This is where the ticks happen
		void FixedUpdate () {

			if (data.strafeToSpeedCurveScale != _strafeToSpeedCurveScale) {
				_strafeToSpeedCurveScale = data.strafeToSpeedCurveScale;
				strafeToSpeedCurveScaleMul = 1f / data.strafeToSpeedCurveScale;
			}

			if (isLocalPlayer || isServer) {
				currentFixedUpdates++;
			}

			if (isLocalPlayer && currentFixedUpdates >= sendUpdates) {
				currentTick++;

				if (!isServer) {
					onTickUpdate.Invoke (lastResults);
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

				#if (CLIENT_TRUST)
				CmdSendInputs (clientInputs [clientInputs.Count - 1], lastResults);
				#else
				CmdSendInputs (clientInputs [clientInputs.Count - 1]);
				#endif
				if (data.debug)
					onTickUpdateDebug.Invoke(clientInputs [clientInputs.Count - 1], lastResults);

				speed = lastResults.speed;
				controller.enabled = false;
				posEnd = lastResults.position;
				groundPointTime = lastResults.groundPointTime;
				posEndG = lastResults.groundPoint;
				rotEnd = lastResults.rotation;
			}

			if (isServer && currentFixedUpdates >= sendUpdates && (currentTFixedUpdates >= sendUpdates || isLocalPlayer)) {

				if (isLocalPlayer) {
					onTickUpdate.Invoke (lastResults);
					RpcSendResults (lastResults);
				}

				if (!isLocalPlayer && clientInputs.Count > 0) {
					currentFixedUpdates -= sendUpdates;
					currentTFixedUpdates -= sendUpdates;
					//if (clientInputs.Count == 0)
					//	clientInputs.Add (curInputServer);
					//clientInputs[clientInputs.Count - 1] = curInputServer;
					curInput = SortClientInputsAndReturnFirst ();

					posStart = myTransform.position;
					rotStart = myTransform.rotation;
					startTime = Time.fixedTime;
					controller.enabled = true;
					serverResults = MoveCharacter (serverResults, curInput, Time.fixedDeltaTime * _sendUpdates, data.maxSpeedNormal);
					#if (CLIENT_TRUST)
					if (serverResults.timestamp == tempResults.timestamp && Vector3.SqrMagnitude(serverResults.position-tempResults.position) <= data.clientPositionToleration * data.clientPositionToleration && Vector3.SqrMagnitude(serverResults.speed-tempResults.speed) <= data.clientSpeedToleration * data.clientSpeedToleration && ((serverResults.isGrounded == tempResults.isGrounded) || !data.clientGroundedMatch) && ((serverResults.crouch == tempResults.crouch) || !data.clientCrouchMatch))
						serverResults = tempResults;
					#endif
					speed = serverResults.speed;
					groundPointTime = serverResults.groundPointTime;
					posEnd = serverResults.position;
					rotEnd = serverResults.rotation;
					posEndG = serverResults.groundPoint;
					controller.enabled = false;

					onTickUpdate.Invoke (serverResults);
					if (data.debug)
						onTickUpdateDebug.Invoke(curInput, serverResults);
					RpcSendResults (serverResults);
				}

			}

			if (isLocalPlayer && currentFixedUpdates >= sendUpdates)
				currentFixedUpdates = 0;
		}

		//This is where all the interpolation happens
		void LateUpdate () {
			if (data.movementType == MoveType.UpdateOnceAndLerp) {
				if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
					interpPos = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					if ((Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= groundPointTime)
						interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates * groundPointTime));
					else
						interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime + (groundPointTime * Time.fixedDeltaTime * _sendUpdates)) / (Time.fixedDeltaTime * _sendUpdates * (1f - groundPointTime)));

					myTransform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					if (!isLocalPlayer)
						camTarget.rotation = Quaternion.Lerp (Quaternion.Euler(headStartRot, rotStart.eulerAngles.y, rotStart.eulerAngles.z), Quaternion.Euler(headEndRot, rotEnd.eulerAngles.y, rotEnd.eulerAngles.z), (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					else
						myTransform.rotation = Quaternion.Euler (myTransform.rotation.eulerAngles.x, curInput.x, myTransform.rotation.eulerAngles.z);
					myTransform.position = interpPos;
				} else if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {

					myTransform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
					if (!isLocalPlayer)
						camTarget.rotation = Quaternion.Lerp (Quaternion.Euler(headStartRot, rotStart.eulerAngles.y, rotStart.eulerAngles.z), Quaternion.Euler(headEndRot, rotEnd.eulerAngles.y, rotEnd.eulerAngles.z), (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				} else {
					myTransform.position = Vector3.Lerp (posEnd, posEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
					myTransform.rotation = Quaternion.Lerp (rotEnd, rotEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
					if (!isLocalPlayer)
						camTarget.rotation = Quaternion.Lerp (Quaternion.Euler(headStartRot, rotEnd.eulerAngles.y, rotEnd.eulerAngles.z), Quaternion.Euler(headEndRot, rotEndO.eulerAngles.y, rotEndO.eulerAngles.z), (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				}
			} else {
				myTransform.position = posEnd;
				myTransform.rotation = rotEnd;
			}

			if (isLocalPlayer) {
				cam.rotation = Quaternion.Euler (new Vector3 (curInput.y, curInput.x, 0f));
				camTarget.rotation = Quaternion.Euler (new Vector3 (curInput.y, curInput.x, 0f));
				cam.position = camTarget.position;
			}
		}


		//Actual movement code. Mostly isolated, except transform
		Results MoveCharacter (Results inpRes, Inputs inp, float deltaMultiplier, Vector3 maxSpeed) {

			Vector3 pos = myTransform.position;
			Quaternion rot = myTransform.rotation;

			myTransform.position = inpRes.position;
			myTransform.rotation = inpRes.rotation;

			Vector3 tempSpeed = myTransform.InverseTransformDirection (inpRes.speed);

			myTransform.rotation = Quaternion.Euler (new Vector3 (0, inp.x, 0));

			Vector3 localSpeed = myTransform.InverseTransformDirection (inpRes.speed);
			Vector3 localSpeed2 = Vector3.Lerp (myTransform.InverseTransformDirection (inpRes.speed), tempSpeed, data.velocityTransferCurve.Evaluate (Mathf.Abs (inpRes.rotation.eulerAngles.y - inp.x) / (deltaMultiplier * data.velocityTransferDivisor)));

			if (!inpRes.isGrounded)
				AirStrafe (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed, ref localSpeed2);
			else
				localSpeed = localSpeed2;

			BaseMovement (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed);

			float tY = myTransform.position.y;

			inpRes.speed = transform.TransformDirection (localSpeed);
			controller.Move (inpRes.speed * deltaMultiplier);

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

			inpRes = new Results (myTransform.position, myTransform.rotation, inp.y, (transform.position - inpRes.position) / deltaMultiplier, controller.isGrounded, inpRes.jumped, inpRes.crouch, gp, gpt, inp.timestamp);

			myTransform.position = pos;
			myTransform.rotation = rot;

			return inpRes;
		}

		public void AirStrafe(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed, ref Vector3 localSpeed2) {
			if (inpRes.isGrounded)
				return;

			float tAccel = data.strafeAngleCurve.Evaluate(Mathf.Abs (inpRes.rotation.eulerAngles.y - inp.x) / deltaMultiplier);
			bool rDir = (inpRes.rotation.eulerAngles.y - inp.x) > 0;

			if (((inp.inputs.x > 0f && !rDir) || (inp.inputs.x < 0f && rDir)) && inp.inputs.y == 0) {
				if (localSpeed.z >= 0) {
					localSpeed.z = localSpeed2.z + tAccel * data.strafeToSpeedCurve.Evaluate (Mathf.Abs (localSpeed.z) * strafeToSpeedCurveScaleMul);
					localSpeed.x = localSpeed2.x;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.z = localSpeed.z + tAccel * data.strafeToSpeedCurve.Evaluate (Mathf.Abs (localSpeed.z) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.x < 0f && !rDir) || inp.inputs.x > 0f && rDir) && inp.inputs.y == 0) {
				if (localSpeed.z <= 0) {
					localSpeed.z = localSpeed2.z - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
					localSpeed.x = localSpeed2.x;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.z = localSpeed.z - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.y > 0f && !rDir) || (inp.inputs.y < 0f && rDir)) && inp.inputs.x == 0) {
				if (localSpeed.x <= 0) {
					localSpeed.x = localSpeed2.x - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
					localSpeed.z = localSpeed2.z;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.x = localSpeed.x - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.y > 0f && rDir) || (inp.inputs.y < 0f && !rDir)) && inp.inputs.x == 0) {
				if (localSpeed.x >= 0) {
					localSpeed.x = localSpeed2.x + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
					localSpeed.z = localSpeed2.z;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.x = localSpeed.x + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
			}
		}

		public void BaseMovement(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed) {
			if (inp.sprint)
				maxSpeed = data.maxSpeedSprint;
			if (inp.crouch) {
				maxSpeed = data.maxSpeedCrouch;
				if (!inpRes.crouch) {

					inpRes.crouch = true;

				}
				controller.height = Mathf.Clamp (controller.height - crouchSwitchMul, data.controllerHeightCrouch, data.controllerHeightNormal);
				controller.center = new Vector3 (0, controller.height * data.controllerCentreMultiplier, 0);
			} else {
				if (inpRes.crouch) {
					inpRes.crouch = false;

					Collider[] hits;

					hits = Physics.OverlapCapsule (inpRes.position + new Vector3(0f, data.controllerHeightCrouch, 0f), inpRes.position + new Vector3(0f, data.controllerHeightNormal, 0f), controller.radius);

					for (int i = 0; i < hits.Length; i++)
						if (hits [i].transform.root != myTransform.root) {
							inpRes.crouch = true;
							inp.crouch = true;
							maxSpeed = data.maxSpeedCrouch;
							break;
						}
				}
				if (!inpRes.crouch) {
					controller.height = Mathf.Clamp (controller.height + crouchSwitchMul, data.controllerHeightCrouch, data.controllerHeightNormal);
					controller.center = new Vector3 (0, controller.height * data.controllerCentreMultiplier, 0);
				} else {
					controller.height = data.controllerHeightCrouch;
					controller.center = new Vector3(0, controller.height * data.controllerCentreMultiplier, 0);
				}
			}
			inpRes.jumped = false;

			if (inpRes.isGrounded && inp.jump && !inpRes.crouch) {
				localSpeed.y = data.speedJump;
				inpRes.jumped = true;
			} else if (!inpRes.isGrounded)
				localSpeed.y += Physics.gravity.y * deltaMultiplier;
			else
				localSpeed.y = Physics.gravity.y * deltaMultiplier;

			if (inpRes.isGrounded) {
				if (localSpeed.x >= 0f && inp.inputs.x > 0f) {
					localSpeed.x += data.accelerationSides * deltaMultiplier;
					if (localSpeed.x > maxSpeed.x)
						localSpeed.x = maxSpeed.x;
				} else if (localSpeed.x >= 0f && (inp.inputs.x < 0f || localSpeed.x > maxSpeed.x))
					localSpeed.x -= data.accelerationStop * deltaMultiplier;
				else if (localSpeed.x <= 0f && inp.inputs.x < 0f) {
					localSpeed.x -= data.accelerationSides * deltaMultiplier;
					if (localSpeed.x < -maxSpeed.x)
						localSpeed.x = -maxSpeed.x;
				} else if (localSpeed.x <= 0f && (inp.inputs.x > 0f || localSpeed.x < -maxSpeed.x))
					localSpeed.x += data.accelerationStop * deltaMultiplier;
				else if (localSpeed.x > 0f) {
					localSpeed.x -= data.decceleration * deltaMultiplier;
					if (localSpeed.x < 0f)
						localSpeed.x = 0f;
				} else if (localSpeed.x < 0f) {
					localSpeed.x += data.decceleration * deltaMultiplier;
					if (localSpeed.x > 0f)
						localSpeed.x = 0f;
				} else
					localSpeed.x = 0;

				if (localSpeed.z >= 0f && inp.inputs.y > 0f) {
					localSpeed.z += data.accelerationSides * deltaMultiplier;
					if (localSpeed.z > maxSpeed.z)
						localSpeed.z = maxSpeed.z;
				} else if (localSpeed.z >= 0f && (inp.inputs.y < 0f || localSpeed.z > maxSpeed.z))
					localSpeed.z -= data.accelerationStop * deltaMultiplier;
				else if (localSpeed.z <= 0f && inp.inputs.y < 0f) {
					localSpeed.z -= data.accelerationSides * deltaMultiplier;
					if (localSpeed.z < -maxSpeed.z)
						localSpeed.z = -maxSpeed.z;
				} else if (localSpeed.z <= 0f && (inp.inputs.y > 0f || localSpeed.z < -maxSpeed.z))
					localSpeed.z += data.accelerationStop * deltaMultiplier;
				else if (localSpeed.z > 0f) {
					localSpeed.z -= data.decceleration * deltaMultiplier;
					if (localSpeed.z < 0f)
						localSpeed.z = 0f;
				} else if (localSpeed.z < 0f) {
					localSpeed.z += data.decceleration * deltaMultiplier;
					if (localSpeed.z > 0f)
						localSpeed.z = 0f;
				} else
					localSpeed.z = 0;
			}
		}
	}
}
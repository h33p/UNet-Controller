//#define SIMULATE
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;

[NetworkSettings (channel=1)]
public class Controller : NetworkBehaviour {

	private CharacterController controller;

	private Vector3 speed;
	public Vector3 maxSpeedSprint = new Vector3 (3f, 0f, 7f);
	public Vector3 maxSpeedNormal = new Vector3 (1f, 0f, 1.5f);

	public float accelerationForward = 6f;
	public float accelerationBack = 3f;
	public float accelerationStop = 8f;
	public float decceleration = 2f;
	public float accelerationSides = 4f;
	public float speedJump = 3f;

	[Range(0,1)]
	public float snapSize = 0.02f;
	private float snapInvert;

	Vector3 interpPos;

	public float rotateSensitivity = 3f;

	[Range (0.01f, 1f)]
	public float sendRate = 0.1f;

	private int _sendUpdates;

	public int sendUpdates {
		get { return _sendUpdates; }
	}

	private int currentFixedUpdates = 0;

	private int currentTick = 0;

	[Range (1, 30)]
	public int inputsToStore = 10;

	[System.Serializable]
	public struct Inputs
	{

		public Vector2 inputs;
		public float x;
		public bool jump;
		public bool sprint;
		public int timestamp;

	}

	private List<Inputs> clientInputs;
	private Inputs curInput;
	private Inputs curInputServer;


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

	private List<Results> clientResults;
	private Results serverResults;
	private Results tempResults;
	private Results lastResults;

	[Range (1, 20)]
	public int serverResultsBuffer = 3;
	[Range (1, 20)]
	public int clientInputsBuffer = 3;

	private List<Results> serverResultList;

	public Transform cam;
	public Transform camTarget;

	public enum MoveType
	{
		EveryFixedUpdate = 0,
		UpdateOnce = 1,
		UpdateOnceAndLerp = 2,
		UpdateOnceAndSLerp = 3,
		UpdateOnceAndCLerp = 4
	};

	public MoveType movementType;

	private Vector3 posStart;
	private Vector3 posEnd;
	private float posEndG;
	private Vector3 posEndO;
	private Quaternion rotStart;
	private Quaternion rotEnd;
	private Quaternion rotEndO;

	private float groundPointTime;
	private float startTime;

	public bool reconciliate = false;
	public bool handleMidTickJump = false;

	void Start () {

		if (snapSize > 0)
			snapInvert = 1f / snapSize;

		clientInputs = new List<Inputs>();
		clientResults = new List<Results>();
		serverResultList = new List<Results>();

		controller = GetComponent<CharacterController> ();
		curInput = new Inputs ();
		curInput.x = transform.rotation.eulerAngles.y;
		curInput.inputs = new Vector2 ();

		posStart = transform.position;
		rotStart = transform.rotation;

		cam = Camera.main.transform;

		posEnd = transform.position;
		rotEnd = transform.rotation;

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

		//if (Vector3.Distance (serverResults.position, clientResults [0].position) < 0.05f)
		//	return;
		/*if (Vector3.Distance (serverResults.position, clientResults [0].position) > 1f) {
			transform.position = serverResults.position;
			transform.rotation = serverResults.rotation;
			posStart = transform.position;
			posEnd = transform.position;
			rotStart = transform.rotation;
			rotEnd = transform.rotation;

			speed = serverResults.speed;
			return;
		}*/


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

			posStart = transform.position;
			rotStart = transform.rotation;
			startTime = Time.fixedTime;

			if (reconciliate) {
				Reconciliate ();
				lastResults = tempResults;
				reconciliate = false;
			}

			if (movementType != MoveType.EveryFixedUpdate) {
				controller.enabled = true;
				lastResults = MoveCharacter (lastResults, clientInputs [clientInputs.Count - 1], Time.fixedDeltaTime * _sendUpdates, maxSpeedNormal);
				speed = lastResults.speed;
				controller.enabled = false;
				posEnd = lastResults.position;
				groundPointTime = lastResults.groundPointTime;
				posEndG = lastResults.groundPoint;
				rotEnd = lastResults.rotation;
			}
		}

		if (isServer && currentFixedUpdates >= sendUpdates) {

			if (movementType == MoveType.EveryFixedUpdate || isLocalPlayer) {
				serverResults.position = transform.position;
				serverResults.rotation = transform.rotation;
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

				if (movementType != MoveType.EveryFixedUpdate) {
					posStart = transform.position;
					rotStart = transform.rotation;
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

		}

		if (isLocalPlayer && currentFixedUpdates >= sendUpdates)
			currentFixedUpdates = 0;

		if (clientInputs.Count >= 1 && movementType == MoveType.EveryFixedUpdate) {
			serverResults = MoveCharacter (serverResults, clientInputs [0], Time.fixedDeltaTime, maxSpeedNormal);
			speed = serverResults.speed;
			groundPointTime = serverResults.groundPointTime;
			posEndG = serverResults.groundPoint;
		}
	}

	void LateUpdate () {
		if (movementType == MoveType.UpdateOnceAndLerp) {
			if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
				interpPos = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				//if ((Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= groundPointTime)
				//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates * groundPointTime));
				//else
				//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime + (groundPointTime * Time.fixedDeltaTime * _sendUpdates)) / (Time.fixedDeltaTime * _sendUpdates * (1f - groundPointTime)));
				transform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
				transform.position = interpPos;
			} else if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
				
				transform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
			} else {
				transform.position = Vector3.Lerp (posEnd, posEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
				transform.rotation = Quaternion.Lerp (rotEnd, rotEndO, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1f);
			}
		}

		if (isLocalPlayer) {
			cam.rotation = Quaternion.Euler (new Vector3 (0f, curInput.x, 0f));
			cam.position = camTarget.position;
		}
	}

	//Actual movement code. Mostly isolated, except transform
	Results MoveCharacter (Results inpRes, Inputs inp, float deltaMultiplier, Vector3 maxSpeed) {

		Vector3 pos = transform.position;
		Quaternion rot = transform.rotation;

		transform.position = inpRes.position;
		transform.rotation = inpRes.rotation;

		if (inp.sprint)
			maxSpeed = maxSpeedSprint;

		bool jumped = false;

		if (inpRes.isGrounded && inp.jump) {
			inpRes.speed.y = speedJump;
			jumped = true;
		} else if (!inpRes.isGrounded)
			inpRes.speed.y += Physics.gravity.y * deltaMultiplier;
		else
			inpRes.speed.y = Physics.gravity.y * deltaMultiplier;

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

		transform.rotation = Quaternion.Euler (new Vector3 (0, inp.x, 0));

		float tY = transform.position.y;

		controller.Move (transform.TransformDirection(inpRes.speed) * deltaMultiplier);

		float gpt = 1f;
		float gp = transform.position.y;

		//WIP, broken, Handles hitting ground while spacebar is pressed. It determines how much time was left to move based on at which height the player hit the ground. Some math involved.
		if (handleMidTickJump && !inpRes.isGrounded && tY - gp >= 0 && inp.jump && (controller.isGrounded || Physics.Raycast (transform.position + controller.center, Vector3.down, (controller.height / 2) + (controller.skinWidth * 1.5f)))) {
			float oSpeed = inpRes.speed.y;
			gpt = (tY - gp) / (-oSpeed);
			inpRes.speed.y = speedJump + ((Physics.gravity.y / 2) * Mathf.Abs((1f - gpt) * deltaMultiplier));
			Debug.Log (inpRes.speed.y + " " + gpt);
			controller.Move (transform.TransformDirection (0, inpRes.speed.y * deltaMultiplier, 0));
			inpRes.isGrounded = true;
			Debug.DrawLine (new Vector3( transform.position.x, gp, transform.position.z), transform.position, Color.blue, deltaMultiplier);
			jumped = true;
		}

		if (snapSize > 0f)
			transform.position = new Vector3 (Mathf.Round (transform.position.x * snapInvert) * snapSize, Mathf.Round (transform.position.y * snapInvert) * snapSize, Mathf.Round (transform.position.z * snapInvert) * snapSize);

		inpRes = new Results (transform.position, transform.rotation, inpRes.speed, controller.isGrounded, jumped, gp, gpt, inp.timestamp);

		transform.position = pos;
		transform.rotation = rot;

		return inpRes;
	}
}

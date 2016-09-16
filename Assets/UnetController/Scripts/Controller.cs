//#define SIMULATE
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;

[NetworkSettings (channel=1)]
public class Controller : NetworkBehaviour {

	private CharacterController controller;

	public Vector3 speed;
	public Vector3 maxSpeedSprint = new Vector3 (3f, 0f, 7f);
	public Vector3 maxSpeedNormal = new Vector3 (1f, 0f, 1.5f);

	public float accelerationForward = 6f;
	public float accelerationBack = 3f;
	public float accelerationStop = 8f;
	public float decceleration = 2f;
	public float accelerationSides = 4f;
	public float speedJump = 3f;

	public float rotateSensitivity = 3f;

	[Range (0.01f, 1f)]
	public float sendRate = 0.1f;

	private int _sendUpdates;

	public int sendUpdates {
		get { return _sendUpdates; }
	}

	public int currentFixedUpdates = 0;

	public int currentTick = 0;

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

	public List<Inputs> clientInputs;
	public Inputs curInput;
	public Inputs curInputServer;


	[System.Serializable]
	public struct Results
	{
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 speed;
		public int timestamp;
	}

	public List<Results> clientResults;
	public Results serverResults;
	public Results tempResults;

	[Range (1, 20)]
	public int serverResultsToReconciliate = 4;
	[Range (1, 20)]
	public int serverResultsToKeep = 10;

	public List<Results> serverResultList;

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

	public Vector3 posStart;
	public Vector3 posEnd;
	public Vector3 posEndO;
	public Quaternion rotStart;
	public Quaternion rotEnd;
	public Quaternion rotEndO;

	public float startTime;

	public bool reconciliate = false;


	public bool jumped = false;

	void Start () {
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
		yield return new WaitForSeconds (Random.Range (0.03f, 0.04f));
		//yield return new WaitForSeconds (0.02f);
		Debug.Log ("ranning");
		#endif
		if (inp.timestamp > curInput.timestamp)
			curInputServer = inp;
	}

	[ClientRpc]
	void RpcSendResults (Results res) {

		if (isServer)
			return;
		#if (SIMULATE)
		StartCoroutine (SendResults (res));
	}

	IEnumerator SendResults (Results res) {
		yield return new WaitForSeconds (Random.Range (0.03f, 0.04f));
		//yield return new WaitForSeconds (0.02f);
		#endif

		//Debug.Log (res.position);
		//Debug.Log (isLocalPlayer);


		if (isLocalPlayer) {

			if (serverResultList.Count > serverResultsToReconciliate)
				serverResultList.RemoveAt (0);

			if (!ServerResultsContainTimestamp (res.timestamp))
				serverResultList.Add (res);

			serverResults = SortServerResultsAndReturnFirst ();

			if (serverResultList.Count >= serverResultsToReconciliate)
				reconciliate = true;

		} else {
			currentTick++;


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
				posEndO = serverResults.position;
				rotEndO = serverResults.rotation;
			} else {
				startTime = Time.fixedTime;
				serverResults = res;
				posStart = serverResults.position;
				rotStart = serverResults.rotation;
				posEnd = posStart;
				rotEnd = rotStart;
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

		if (serverResultList.Count > serverResultsToKeep)
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

		if (Vector3.Distance (serverResults.position, clientResults [0].position) < 0.05f)
			return;
		else if (Vector3.Distance (serverResults.position, clientResults [0].position) > 1f) {
			transform.position = serverResults.position;
			transform.rotation = serverResults.rotation;
			speed = serverResults.speed;
			return;
		}

		transform.position = serverResults.position;
		transform.rotation = serverResults.rotation;
		speed = serverResults.speed;

		controller.enabled = true;
		for (int i = 1; i < clientInputs.Count - 1; i++) {
			speed = MoveCharacter (clientInputs [i].inputs, clientInputs [i].jump, clientInputs [i].sprint, speed, Time.fixedDeltaTime * sendUpdates, maxSpeedNormal, clientInputs [i].x);
		}

		posEnd = transform.position;
		rotEnd = transform.rotation;

	}

	void Update () {
		if (isLocalPlayer) {
			curInput.inputs.x = Input.GetAxisRaw ("Horizontal");
			curInput.inputs.y = Input.GetAxisRaw ("Vertical");

			if (Input.GetKeyDown (KeyCode.Space))
				curInput.jump = true;

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
				tempResults = new Results ();
				tempResults.timestamp = curInput.timestamp - 1;
				tempResults.position = transform.position;
				tempResults.rotation = transform.rotation;
				tempResults.speed = speed;
				clientResults.Add (tempResults);
			}

			if (clientInputs.Count >= inputsToStore)
				clientInputs.RemoveAt (0);

			clientInputs.Add (curInput);
			curInput.timestamp = currentTick;
			jumped = false;

			posStart = transform.position;
			rotStart = transform.rotation;
			startTime = Time.fixedTime;

			if (reconciliate) {
				Reconciliate ();
				reconciliate = false;
			}

			if (movementType != MoveType.EveryFixedUpdate) {
				controller.enabled = true;
				transform.position = posEnd;
				transform.rotation = rotEnd;
				speed = MoveCharacter (clientInputs [clientInputs.Count - 1].inputs, (clientInputs [clientInputs.Count - 1].jump && !jumped), clientInputs [clientInputs.Count - 1].sprint, speed, Time.fixedDeltaTime * _sendUpdates, maxSpeedNormal, clientInputs [clientInputs.Count - 1].x);
				controller.enabled = false;
				if (clientInputs [clientInputs.Count - 1].jump) {
					if (!jumped)
						curInput.jump = false;
					jumped = true;
				}
				posEnd = transform.position;
				rotEnd = transform.rotation;
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
				if (clientInputs.Count == 0)
					clientInputs.Add (curInputServer);
				clientInputs[clientInputs.Count - 1] = curInputServer;
				curInput = curInputServer;
				jumped = false;

				if (movementType != MoveType.EveryFixedUpdate) {
					posStart = transform.position;
					rotStart = transform.rotation;
					startTime = Time.fixedTime;
					controller.enabled = true;
					transform.position = posEnd;
					transform.rotation = rotEnd;
					speed = MoveCharacter (clientInputs [clientInputs.Count - 1].inputs, (clientInputs [clientInputs.Count - 1].jump && !jumped), clientInputs [clientInputs.Count - 1].sprint, speed, Time.fixedDeltaTime * _sendUpdates, maxSpeedNormal, clientInputs [clientInputs.Count - 1].x);
					controller.enabled = false;
					if (clientInputs [clientInputs.Count - 1].jump) {
						if (!jumped)
							curInput.jump = false;
						jumped = true;
					}
					posEnd = transform.position;
					rotEnd = transform.rotation;

					serverResults.position = transform.position;
					serverResults.rotation = transform.rotation;
					serverResults.speed = speed;
					serverResults.timestamp = curInput.timestamp;

					RpcSendResults (serverResults);
				}
			}

		}

		if (isLocalPlayer && currentFixedUpdates >= sendUpdates)
			currentFixedUpdates = 0;

		if (clientInputs.Count >= 1 && movementType == MoveType.EveryFixedUpdate) {
			speed = MoveCharacter (clientInputs [clientInputs.Count - 1].inputs, (clientInputs [clientInputs.Count - 1].jump && !jumped), clientInputs [clientInputs.Count - 1].sprint, speed, Time.fixedDeltaTime, maxSpeedNormal, clientInputs [clientInputs.Count - 1].x);
			if (clientInputs [clientInputs.Count - 1].jump) {
				if (!jumped)
					curInput.jump = false;
				jumped = true;
			}
		}
	}

	void LateUpdate () {
		if (movementType == MoveType.UpdateOnceAndLerp) {
			if (isLocalPlayer || isServer || (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
				transform.position = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates));
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

	Vector3 MoveCharacter (Vector2 inp, bool jump, bool sprint, Vector3 curSpeed, float deltaMultiplier, Vector3 maxSpeed, float targetRotation) {

		if (sprint)
			maxSpeed = maxSpeedSprint;

		if (jump &&  (controller.isGrounded || Physics.Raycast (transform.position, Vector3.down, (controller.height / 2) + (controller.skinWidth * 1.5f))))
			curSpeed.y = speedJump;
		else if (!controller.isGrounded)
			curSpeed.y += Physics.gravity.y * deltaMultiplier;
		else
			curSpeed.y = Physics.gravity.y * deltaMultiplier;

		if (curSpeed.x >= 0f && inp.x > 0f && curSpeed.x < maxSpeed.x) {
			curSpeed.x += accelerationSides * deltaMultiplier;
			if (curSpeed.x > maxSpeed.x)
				curSpeed.x = maxSpeed.x;
		} else if (curSpeed.x >= 0f && (inp.x < 0f || curSpeed.x > maxSpeed.x))
			curSpeed.x -= accelerationStop * deltaMultiplier;
		else if (curSpeed.x <= 0f && inp.x < 0f && curSpeed.x > -maxSpeed.x) {
			curSpeed.x -= accelerationSides * deltaMultiplier;
			if (curSpeed.x < -maxSpeed.x)
				curSpeed.x = -maxSpeed.x;
		} else if (curSpeed.x <= 0f && (inp.x > 0f || curSpeed.x < -maxSpeed.x))
			curSpeed.x += accelerationStop * deltaMultiplier;
		else if (curSpeed.x > 0f) {
			curSpeed.x -= decceleration * deltaMultiplier;
			if (curSpeed.x < 0f)
				curSpeed.x = 0f;
		} else if (curSpeed.x < 0f) {
			curSpeed.x += decceleration * deltaMultiplier;
			if (curSpeed.x > 0f)
				curSpeed.x = 0f;
		} else
			curSpeed.x = 0;

		if (curSpeed.z >= 0f && inp.y > 0f && curSpeed.z < maxSpeed.z) {
			curSpeed.z += accelerationSides * deltaMultiplier;
			if (curSpeed.z > maxSpeed.z)
				curSpeed.z = maxSpeed.z;
		} else if (curSpeed.z >= 0f && (inp.y < 0f || curSpeed.z > maxSpeed.z))
			curSpeed.z -= accelerationStop * deltaMultiplier;
		else if (curSpeed.z <= 0f && inp.y < 0f && curSpeed.z > -maxSpeed.z) {
			curSpeed.z -= accelerationSides * deltaMultiplier;
			if (curSpeed.z < -maxSpeed.z)
				curSpeed.z = -maxSpeed.z;
		} else if (curSpeed.z <= 0f && (inp.y > 0f || curSpeed.z < -maxSpeed.z))
			curSpeed.z += accelerationStop * deltaMultiplier;
		else if (curSpeed.z > 0f) {
			curSpeed.z -= decceleration * deltaMultiplier;
			if (curSpeed.z < 0f)
				curSpeed.z = 0f;
		} else if (curSpeed.z < 0f) {
			curSpeed.z += decceleration * deltaMultiplier;
			if (curSpeed.z > 0f)
				curSpeed.z = 0f;
		} else
			curSpeed.z = 0;

		transform.rotation = Quaternion.Euler (new Vector3 (0, targetRotation, 0));

		controller.Move (transform.TransformDirection(curSpeed) * deltaMultiplier);



		return curSpeed;
	}
}

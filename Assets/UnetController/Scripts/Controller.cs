//#define SIMULATE

//Should we use command checksum? Due to the use of network writer we produce quite a bit of garbage
//#define CMD_CHECKSUM

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace GreenByteSoftware.UNetController {

	#region classes

	//Input management
	public interface IPLayerInputs {
		float GetMouseX ();
		float GetMouseY ();
		float GetMoveX (bool forceFPS);
		float GetMoveY (bool forceFPS);
		float GetMoveX ();
		float GetMoveY ();
		bool GetJump ();
		bool GetCrouch ();
	}

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Inputs
	{

		public Vector2 inputs;
		public float x;
		public float y;
		public bool jump;
		public bool crouch;
		public uint timestamp;
        public uint servertick;
#if (CMD_CHECKSUM)
		public byte checksum;
#endif

	}

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Results
	{
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 groundNormal;
		public float camX;
		public Vector3 speed;
		public bool isGrounded;
		public bool jumped;
		public bool crouch;
		public float groundPoint;
		public float groundPointTime;
		public Vector3 aiTarget;
		public bool aiEnabled;
		public bool controlledOutside;
		public bool ragdoll;
		public uint ragdollTime;
		public uint timestamp;

		public Results (Vector3 pos, Quaternion rot, Vector3 gndNormal, float cam, Vector3 spe, bool ground, bool jump, bool crch, float gp, float gpt, Vector3 target, bool enabled, bool contrOutside, bool rgdl, uint rgdlTime, uint tick) {
			position = pos;
			rotation = rot;
			groundNormal = gndNormal;
			camX = cam;
			speed = spe;
			isGrounded = ground;
			jumped = jump;
			crouch = crch;
			groundPoint = gp;
			groundPointTime = gpt;
			aiTarget = target;
			aiEnabled = enabled;
			controlledOutside = contrOutside;
			ragdoll = rgdl;
			ragdollTime = rgdlTime;
			timestamp = tick;
		}

		public override string ToString () {
			return "" + position + "\n"
				+ rotation + "\n"
				+ camX + "\n"
				+ speed + "\n"
				+ isGrounded + "\n"
				+ jumped + "\n"
				+ crouch + "\n"
				+ groundPoint + "\n"
				+ groundPointTime + "\n"
				+ aiTarget + "\n"
				+ aiEnabled + "\n"
				+ controlledOutside + "\n"
				+ timestamp + "\n";
		}
	}

	[System.Serializable]
	public class InputSend : MessageBase {
		public byte[] inp;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct InputsBytes {
		[FieldOffset(0)] public Inputs inp;
		[FieldOffset(0)] public byte[] bytes;
	}

	public delegate void TickUpdateNotifyDelegate(bool inLagCompensation);
	public delegate void TickUpdateDelegate(Results res, bool inLagCompensation);
	public delegate void TickUpdateAllDelegate(Inputs inp, Results res, bool inLagCompensation);

	#endregion

	//The Controller
	[NetworkSettings (channel=1)]
	public class Controller : NetworkBehaviour {

		private const int MAX_INPUTS_MESSAGE = 24;

		private int inputsSize = Marshal.SizeOf(typeof(Inputs));

		public ControllerDataObject data;
		public ControllerInputDataObject dataInp;
		public MonoBehaviour inputsInterfaceClass;
		private IPLayerInputs _inputsInterface;

		//Returns inputs interface
		public IPLayerInputs inputsInterface {
			get {
				if (_inputsInterface == null && inputsInterfaceClass != null)
					_inputsInterface = inputsInterfaceClass as IPLayerInputs;
				if (_inputsInterface == null)
					inputsInterfaceClass = null;
				return _inputsInterface;
			}
		}

		//Caches CharacterController
		private CharacterController _controller;
		public CharacterController controller {
			get {
				if (_controller == null)
					_controller = GetComponent<CharacterController> ();
				return _controller;
			}
		}

		//Caches transform
		private Transform _transform;
		public Transform myTransform {
			get {
				if (_transform == null)
					_transform = transform;
				return _transform;
			}
		}

		//Private variables used to optimize for mobile's instruction set
		private float strafeToSpeedCurveScaleMul;
		private float _strafeToSpeedCurveScale;

		private float snapInvert;

		Vector3 interpPos;
		Quaternion interpRot;

		private int _sendUpdates;

		public int lSendUpdates {
			get { return _sendUpdates; }
		}

		public int sendUpdates {
			get { return GameManager.sendUpdates; }
		}

		private int currentFixedUpdates = 0;
		private int currentTFixedUpdates = 0;

		private uint currentTick = 0;
		private uint lerpTicks = 0;

		[System.NonSerialized]
		public Inputs[] clientInputs;
		private Inputs curInput;
		private Inputs curInputServer;

		[System.NonSerialized]
		public List<Results> clientResults;
		private Results serverResults;
		private Results tempResults;
		private Results lastResults;

		private List<Results> sendResultsArray = new List<Results> (5);
		private Results sendResults;
		private Results sentResults;

		private List<Results> serverResultList;

		public Transform head;
		public Transform camTarget;
		public Transform camTargetFPS;

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

		private bool worldUpdated = false;

		private uint lastTick = 0;
		private bool receivedFirstTime;

		public TickUpdateNotifyDelegate tickUpdateNotify;
    	public TickUpdateDelegate tickUpdate;
		public TickUpdateAllDelegate tickUpdateDebug;

		private float commandTime = 0f;
		private float simulationTime = 0f;

		[System.NonSerialized]
		public int gmIndex = -1;

		[System.NonSerialized]
		public int cachedID = -103;

		//AI part
		[System.NonSerialized]
		public bool aiEnabled;
		//A value to check by scripts enabling AI so they can identify themselves
		[System.NonSerialized]
		public short aiEnablerCode;
		//2 Targets because scripts might not be fast enough to set a new value. Only one (the second one) can be used as well, just set the aiTargetReached to 1 instead of 0 when setting the target. 
		[System.NonSerialized]
		public Vector3 aiTarget1;
		[System.NonSerialized]
		public Vector3 aiTarget2;
		[System.NonSerialized]
		public short aiTargetReached;

		public bool playbackMode = false;
		public float playbackSpeed = 1f;

		private InputSend inpSend;

		private NetworkWriter inputWriter;

		//Returns network client instance
		private NetworkClient _myClient;
		public NetworkClient myClient {
			get {
				if (_myClient == null && isLocalPlayer)
					_myClient = NetworkManager.singleton.client;
				return _myClient;
			}
		}
			
		const short inputMessage = 101;

		//Sets the send interval used by UNET
		public override float GetNetworkSendInterval () {
			if (GameManager.settings != null)
				return GameManager.settings.sendRate;
			else
				return 0.1f;
		}

		private float _crouchSwitchMul = -1;

		//Retrieves crouch switch time multiplier. Multiplication is faster on ARM than division
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

		private uint _ragdollTime = 0;
		private float _ragdollTimeFloat = 0f;
		public uint ragdollTime {
			get {
				if (data.ragdollStopTimeout != _ragdollTimeFloat) {
					_ragdollTimeFloat = data.ragdollStopTimeout;
					_ragdollTime = (uint)Mathf.RoundToInt(_ragdollTimeFloat / GameManager.settings.sendRate);
				}
				return _ragdollTime;
			}
		}

		//Once this becomes a local player, set the camera target to it
		public override void OnStartLocalPlayer () {
			CameraControl.SetTarget (camTarget, camTargetFPS);
		}

		public void SetControl (bool controlMode) {
			if (isLocalPlayer) {
				if (!controlMode && lastResults.controlledOutside) {
					SetPosition (myTransform.position);
					SetRotation (myTransform.rotation);
				}
				lastResults.controlledOutside = controlMode;
			} else if (isServer) {
				if (!controlMode && serverResults.controlledOutside) {
					SetPosition (myTransform.position);
					SetRotation (myTransform.rotation);
				}
				serverResults.controlledOutside = controlMode;
			}
		}

		public void SetRagdoll (bool controlMode) {
			if (isLocalPlayer) {
				if (!controlMode && lastResults.ragdoll && !lastResults.controlledOutside) {
					SetPosition (myTransform.position);
					SetRotation (myTransform.rotation);
				}
				lastResults.ragdoll = controlMode;
			} else if (isServer) {
				if (!controlMode && serverResults.ragdoll && !serverResults.controlledOutside) {
					SetPosition (myTransform.position);
					SetRotation (myTransform.rotation);
				}
				serverResults.ragdoll = controlMode;
			}
		}

		//Returns a copy of the results on the local environment
		public Results GetResults () {
			if (isLocalPlayer) {
				return lastResults;
			}
			return serverResults;
		}

		//Sets the velocity on the last result
		public void SetVelocity (Vector3 vel) {
			if (isLocalPlayer) {
				lastResults.speed = vel;
			} else if (isServer) {
				serverResults.speed = vel;
			}
		}

		//Sets the position on the last result
		public void SetPosition (Vector3 pos) {
			if (isLocalPlayer) {
				lastResults.position = pos;
				interpPos = pos;
			} else if (isServer) {
				serverResults.position = pos;
				interpPos = pos;
			}
		}

		//Sets the rotation
		public void SetRotation (Quaternion rot) {
			if (isLocalPlayer) {
				lastResults.rotation = rot;
				interpRot = rot;
			} else if (isServer) {
				serverResults.rotation = rot;
				interpRot = rot;
			}
		}

		public uint GetTimestamp() {
			return currentTick;
		}

		//Initialization
		void Start () {
			gameObject.name = Extensions.GenerateGUID ();

			if (data == null || dataInp == null) {
				Debug.LogError ("No controller data attached! Will not continue.");
				this.enabled = false;
				return;
			}

			if (data.snapSize > 0)
				snapInvert = 1f / data.snapSize;

			commandTime = GameManager.curtime;
			simulationTime = GameManager.curtime;

			clientInputs = new Inputs[data.inputsToStore];
			serverResultList = new List<Results>();

			curInput = new Inputs ();
			curInput.x = myTransform.rotation.eulerAngles.y;
			curInput.inputs = new Vector2 ();

			posStart = myTransform.position;
			rotStart = myTransform.rotation;
			interpPos = posStart;
			interpRot = rotStart;

			posEnd = myTransform.position;
			rotEnd = myTransform.rotation;

			_sendUpdates = GameManager.sendUpdates;

			if (isServer) {
				curInput.timestamp = 0;
				NetworkServer.RegisterHandler (inputMessage, GameManager.OnSendInputs);
			}

			SetPosition (myTransform.position);
			SetRotation (myTransform.rotation);

			if (!playbackMode)
				GameManager.RegisterController(this);
			else if (GetComponent<RecordableObject>() != null)
				GetComponent<RecordableObject>().RecordCountHook(ref tickUpdateNotify);
		}

		public void OnDestroy () {
			GameManager.UnregisterController (this);
		}

		NetworkWriter cSumWriter;
		byte[] sumBytes;

		public byte GetCommandChecksum(Inputs inp) {

#if (CMD_CHECKSUM)
			cSumWriter = new NetworkWriter();

			int checkSum = 0;

			cSumWriter.Write(inp.inputs);
			cSumWriter.Write(inp.x);
			cSumWriter.Write(inp.y);
			cSumWriter.Write(inp.jump);
			cSumWriter.Write(inp.crouch);
			cSumWriter.Write(inp.timestamp);
			cSumWriter.Write(inp.servertick);

			sumBytes = cSumWriter.AsArray();

			for (int i = 0; i < sumBytes.Length; i++)
				checkSum += sumBytes[i];

			return (byte)(checkSum & 0xFF);
#else
			return 0;
#endif
		}

		//Creates the bitmask by comparing 2 different inputs
		uint GetInputsBitMask(Inputs inp1, Inputs inp2) {
			uint mask = 0;
			if (inp2.timestamp % GameManager.settings.maxDeltaTicks == 0)
				return 0xFFFFFFFF;

			if (inp1.inputs != inp2.inputs) mask |= 1 << 0;
			if (inp1.x != inp2.x) mask |= 1 << 1;
			if (inp1.y != inp2.y) mask |= 1 << 2;
			if (inp1.jump != inp2.jump) mask |= 1 << 3;
			if (inp1.crouch != inp2.crouch) mask |= 1 << 4;
			if (inp1.timestamp + 1 != inp2.timestamp) mask |= 1 << 5;
			if (inp1.servertick + 1 != inp2.servertick) mask |= 1 << 6;
			return mask;
		}

		public Inputs ReadInputs(NetworkReader reader, Inputs inp) {
			uint mask = reader.ReadPackedUInt32();

			if ((mask & (1 << 0)) != 0)
				inp.inputs = reader.ReadVector2();
			if ((mask & (1 << 1)) != 0)
				inp.x = reader.ReadSingle();
			if ((mask & (1 << 2)) != 0)
				inp.y = reader.ReadSingle();
			if ((mask & (1 << 3)) != 0)
				inp.jump = reader.ReadBoolean();
			if ((mask & (1 << 4)) != 0)
				inp.crouch = reader.ReadBoolean();

			//We are expecting timestamp to be one tick higher,
			//so that is why this flag is set on the client to be checked for difference between command values rather than equality
			if ((mask & (1 << 5)) != 0)
				inp.timestamp = reader.ReadPackedUInt32();
			else
				inp.timestamp++;
			if ((mask & (1 << 6)) != 0)
				inp.servertick = reader.ReadPackedUInt32();
			else
				inp.servertick++;
#if (CMD_CHECKSUM)
			inp.checksum = reader.ReadByte();
#endif

			return inp;
		}

		public void WriteInputs(ref NetworkWriter writer, Inputs inp, Inputs prevInp) {
			uint mask = GetInputsBitMask(prevInp, inp);

			writer.WritePackedUInt32(mask);

			if ((mask & (1 << 0)) != 0)
				writer.Write(inp.inputs);
			if ((mask & (1 << 1)) != 0)
				writer.Write(inp.x);
			if ((mask & (1 << 2)) != 0)
				writer.Write(inp.y);
			if ((mask & (1 << 3)) != 0)
				writer.Write(inp.jump);
			if ((mask & (1 << 4)) != 0)
				writer.Write(inp.crouch);
			if ((mask & (1 << 5)) != 0)
				writer.WritePackedUInt32(inp.timestamp);
			if ((mask & (1 << 6)) != 0)
				writer.WritePackedUInt32(inp.servertick);
#if (CMD_CHECKSUM)
			writer.Write(inp.checksum);
#endif
		}

		private Inputs prevInput;

		//This is called on the client to send the current inputs
		void SendInputs (ref List<Inputs> inp) {

			if (!isLocalPlayer || isServer)
				return;

#if (CMD_CHECKSUM)
			inp.checksum = GetCommandChecksum(inp);
#endif

			if (inputWriter == null)
				inputWriter = new NetworkWriter ();

			inputWriter.SeekZero();
			inputWriter.StartMessage(inputMessage);
			int sz = inp.Count;
			inputWriter.WritePackedUInt32((uint)sz);
			for (int i = 0; i < sz; i++) {
				WriteInputs(ref inputWriter, inp[i], prevInput);
				prevInput = inp[i];
			}
			inputWriter.FinishMessage();

			myClient.SendWriter(inputWriter, GetNetworkChannel());

			inp.Clear();
		}

		//We need to check the data for validity and decide on what to do later
		//0 - invalid checksum, network error
		//1 - valid command, good to go
		//-1 - invalid command data, probably a hacked client or mistake in code
		public int IsCommandValid (ref Inputs cmd) {

#if (CMD_CHECKSUM)
			byte checkSum = GetCommandChecksum(cmd);

			if (checkSum != cmd.checksum)
				return 0;
#endif

			if (cmd.x > 360 || cmd.x < 0 || cmd.y > 360 || cmd.y < 0)
				return -1;

			//Check for tampered servertick, it should only go up or stay on the current servertick
			if (!isLocalPlayer) {
				uint highestTick = GetHighestServerTick();
				//Here we could do 2 things, first - return -1 so the server can deal with this incident, or just silently adjust the server tick, which we chose to do this time
				if (cmd.servertick < highestTick)
					cmd.servertick = highestTick;
			}

			return 1;
		}

		private Inputs[] inps;
		private Inputs lSendInp;

		//This is called on the server to handle inputs
		public void OnSendInputs (NetworkMessage msg) {

			NetworkReader mRead = msg.reader;

			uint sz = mRead.ReadPackedUInt32();

			if (sz > MAX_INPUTS_MESSAGE)
				sz = MAX_INPUTS_MESSAGE;

			if (inps == null)
				inps = new Inputs[MAX_INPUTS_MESSAGE];

			for (int i = 0; i < sz && i < MAX_INPUTS_MESSAGE; i++) {
				inps[i] = ReadInputs(mRead, lSendInp);
				lSendInp = inps[i];
			}

#if (SIMULATE)
			StartCoroutine (SendInputsC (inp, sz));
		}
		IEnumerator SendInputsC (Inputs[] inps, int sz) {
			yield return new WaitForSeconds (UnityEngine.Random.Range (0.21f, 0.28f));
#endif

			if (!isLocalPlayer)
				ProcessCommandsServer(ref inps, sz);
		}

		//Results part
		public override void OnDeserialize (NetworkReader reader, bool initialState) {

			if (isServer)
				return;

			//Initial state, everything is provided
			if (initialState) {
				sendResults.position = reader.ReadVector3 ();
				sendResults.rotation = reader.ReadQuaternion ();
				sendResults.groundNormal = reader.ReadVector3 ();
				sendResults.camX = reader.ReadSingle ();
				sendResults.speed = reader.ReadVector3 ();
				sendResults.isGrounded = reader.ReadBoolean ();
				sendResults.jumped = reader.ReadBoolean ();
				sendResults.crouch = reader.ReadBoolean ();
				sendResults.groundPoint = reader.ReadSingle ();
				sendResults.groundPointTime = reader.ReadSingle ();
				sendResults.aiTarget = reader.ReadVector3 ();
				sendResults.aiEnabled = reader.ReadBoolean ();
				sendResults.controlledOutside = reader.ReadBoolean ();
				sendResults.ragdoll = reader.ReadBoolean ();
				sendResults.ragdollTime = reader.ReadPackedUInt32 ();
				sendResults.timestamp = reader.ReadPackedUInt32 ();
				OnSendResults (sendResults);
			} else {
				//We need to check the mask to see if the value has been updated
				int count = (int)reader.ReadPackedUInt32 ();

				for (int i = 0; i < count; i++) {

					uint mask = reader.ReadPackedUInt32 ();

					if ((mask & (1 << 0)) != 0)
						sendResults.position = reader.ReadVector3 ();
					if ((mask & (1 << 1)) != 0)
						sendResults.rotation = reader.ReadQuaternion ();
					if ((mask & (1 << 2)) != 0)
						sendResults.groundNormal = reader.ReadVector3 ();
					if ((mask & (1 << 3)) != 0)
						sendResults.camX = reader.ReadSingle ();
					if ((mask & (1 << 4)) != 0)
						sendResults.speed = reader.ReadVector3 ();
					if ((mask & (1 << 5)) != 0)
						sendResults.isGrounded = reader.ReadBoolean ();
					if ((mask & (1 << 6)) != 0)
						sendResults.jumped = reader.ReadBoolean ();
					if ((mask & (1 << 7)) != 0)
						sendResults.crouch = reader.ReadBoolean ();
					if ((mask & (1 << 8)) != 0)
						sendResults.groundPoint = reader.ReadSingle ();
					if ((mask & (1 << 9)) != 0)
						sendResults.groundPointTime = reader.ReadSingle ();
					if ((mask & (1 << 10)) != 0)
						sendResults.aiTarget = reader.ReadVector3 ();
					if ((mask & (1 << 11)) != 0)
						sendResults.aiEnabled = reader.ReadBoolean ();
					if ((mask & (1 << 12)) != 0)
						sendResults.controlledOutside = reader.ReadBoolean ();
					if ((mask & (1 << 13)) != 0)
						sendResults.ragdoll = reader.ReadBoolean ();
					if ((mask & (1 << 14)) != 0)
						sendResults.ragdollTime = reader.ReadPackedUInt32 ();
					if ((mask & (1 << 15)) != 0)
						sendResults.timestamp = reader.ReadPackedUInt32();
					else
						sendResults.timestamp++;
					OnSendResults (sendResults);
				}
			}
			
		}

		//Creates the bitmask by comparing 2 different results
		//Uses some expectancy checks in timestamps for better compression
		uint GetResultsBitMask (Results res1, Results res2) {
			uint mask = 0;
			if(res1.position != res2.position) mask |= 1 << 0;
			if(res1.rotation != res2.rotation) mask |= 1 << 1;
			if(res1.groundNormal != res2.groundNormal) mask |= 1 << 2;
			if(res1.camX != res2.camX) mask |= 1 << 3;
			if(res1.speed != res2.speed) mask |= 1 << 4;
			if(res1.isGrounded != res2.isGrounded) mask |= 1 << 5;
			if(res1.jumped != res2.jumped) mask |= 1 << 6;
			if(res1.crouch != res2.crouch) mask |= 1 << 7;
			if(res1.groundPoint != res2.groundPoint) mask |= 1 << 8;
			if(res1.groundPointTime != res2.groundPointTime) mask |= 1 << 9;
			if(res1.aiTarget != res2.aiTarget) mask |= 1 << 10;
			if(res1.aiEnabled != res2.aiEnabled) mask |= 1 << 11;
			if(res1.controlledOutside != res2.controlledOutside) mask |= 1 << 12;
			if(res1.ragdoll != res2.ragdoll) mask |= 1 << 13;
			if(res1.ragdollTime != res2.ragdollTime) mask |= 1 << 14;
			if(res1.timestamp != res2.timestamp + 1 || res1.timestamp % GameManager.settings.maxDeltaTicks == 0) mask |= 1 << 15;
			return mask;
		}

		//Called on the server when serializing the results
		public override bool OnSerialize (NetworkWriter writer, bool forceAll) {

			if (forceAll) {
				writer.Write(sendResults.position);
				writer.Write(sendResults.rotation);
				writer.Write(sendResults.groundNormal);
				writer.Write(sendResults.camX);
				writer.Write(sendResults.speed);
				writer.Write(sendResults.isGrounded);
				writer.Write(sendResults.jumped);
				writer.Write(sendResults.crouch);
				writer.Write(sendResults.groundPoint);
				writer.Write(sendResults.groundPointTime);
				writer.Write(sendResults.aiTarget);
				writer.Write(sendResults.aiEnabled);
				writer.Write(sendResults.controlledOutside);
				writer.Write(sendResults.ragdoll);
				writer.WritePackedUInt32(sendResults.ragdollTime);
				writer.WritePackedUInt32(sendResults.timestamp);

				sentResults = sendResults;
				return true;
			} else {

				writer.WritePackedUInt32 ((uint)sendResultsArray.Count);

				while (sendResultsArray.Count > 0) {

					sendResults = sendResultsArray [0];
					sendResultsArray.RemoveAt (0);

					uint mask = GetResultsBitMask (sendResults, sentResults);
					writer.WritePackedUInt32 (mask);

					//This is some bitmask magic. We check if that bit is 1 or 0, and depending on that, we decide if we should write the data.
					if ((mask & (1 << 0)) != 0)
						writer.Write (sendResults.position);
					if ((mask & (1 << 1)) != 0)
						writer.Write (sendResults.rotation);
					if ((mask & (1 << 2)) != 0)
						writer.Write (sendResults.groundNormal);
					if ((mask & (1 << 3)) != 0)
						writer.Write (sendResults.camX);
					if ((mask & (1 << 4)) != 0)
						writer.Write (sendResults.speed);
					if ((mask & (1 << 5)) != 0)
						writer.Write (sendResults.isGrounded);
					if ((mask & (1 << 6)) != 0)
						writer.Write (sendResults.jumped);
					if ((mask & (1 << 7)) != 0)
						writer.Write (sendResults.crouch);
					if ((mask & (1 << 8)) != 0)
						writer.Write (sendResults.groundPoint);
					if ((mask & (1 << 9)) != 0)
						writer.Write (sendResults.groundPointTime);
					if ((mask & (1 << 10)) != 0)
						writer.Write (sendResults.aiTarget);
					if ((mask & (1 << 11)) != 0)
						writer.Write (sendResults.aiEnabled);
					if ((mask & (1 << 12)) != 0)
						writer.Write (sendResults.controlledOutside);
					if ((mask & (1 << 13)) != 0)
						writer.Write (sendResults.ragdoll);
					if ((mask & (1 << 14)) != 0)
						writer.WritePackedUInt32 (sendResults.ragdollTime);
					if ((mask & (1 << 15)) != 0)
						writer.WritePackedUInt32 (sendResults.timestamp);

					sentResults = sendResults;
				}
				return true;
			}
		}

		//Called on the clients after receiving the results
		void OnSendResults (Results res) {

			if (isServer)
				return;

#if (SIMULATE)
			StartCoroutine (SendResultsC (res));
		}

		IEnumerator SendResultsC (Results res) {
			yield return new WaitForSeconds (UnityEngine.Random.Range (0.21f, 0.38f));
#endif

			if (isLocalPlayer) {

				//foreach (Results t in clientResults) {
				//	if (t.timestamp == res.timestamp)
						Debug_UI.UpdateUI (posEnd, res.position, res.position, currentTick, res.timestamp);
				//}

				/*if (serverResultList.Count > data.serverResultsBuffer)
					serverResultList.RemoveAt (0);

				if (!ServerResultsContainTimestamp (res.timestamp))
					serverResultList.Add (res);

				serverResults = SortServerResultsAndReturnFirst ();*/

				serverResults = res;

				worldUpdated = true;

			} else {
				currentTick++;

				serverResults = res;
				GameManager.PlayerTick (this, serverResults);
				if (tickUpdate != null) tickUpdate(res, false);

				if (currentTick > 2 && currentTick % GameManager.singleton.networkSettings.maxDeltaTicks != 0) {
					serverResults = res;
					posStart = interpPos;
					rotStart = interpRot;
					headStartRot = headEndRot;
					headEndRot = res.camX;
					//if (Time.fixedTime - 2f > startTime)
					lerpTicks++;
					startTime = GameManager.curtime;
					//else
					//	startTime = Time.fixedTime - ((Time.fixedTime - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1) * (Time.fixedDeltaTime * _sendUpdates);
					posEnd = posEndO;
					rotEnd = rotEndO;
					groundPointTime = serverResults.groundPointTime;
					posEndG = serverResults.groundPoint;
					posEndO = serverResults.position;
					rotEndO = serverResults.rotation;
				} else {
					lerpTicks++;
					startTime = GameManager.curtime;
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

		//Sort server results using very inefficent bubble-sort. The list size is usually small, though.
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

		bool ServerResultsContainTimestamp (uint timeStamp) {
			for (int i = 0; i < serverResultList.Count; i++) {
				if (serverResultList [i].timestamp == timeStamp)
					return true;
			}

			return false;
		}

		uint GetHighestServerTick() {
			uint highestTick = 0;
			for (int i = 0; i < clientInputs.Length; i++)
				if (clientInputs[i].servertick > highestTick)
					highestTick = clientInputs[i].servertick;
			return highestTick;
		}

		uint GetHighestServerTimestamp() {
			uint highestTick = 0;
			for (int i = 0; i < clientInputs.Length; i++)
				if (clientInputs[i].timestamp > highestTick)
					highestTick = clientInputs[i].timestamp;
			return highestTick;
		}

		//Input gathering
		public void UpdateInputs () {
			if (isLocalPlayer) {
				if (inputsInterface == null)
					throw (new UnityException ("inputsInterface is not set!"));
				
				curInput.inputs.x = inputsInterface.GetMoveX ();
				curInput.inputs.y = inputsInterface.GetMoveY ();

				curInput.x = inputsInterface.GetMouseX ().ClampAngle();
				curInput.y = inputsInterface.GetMouseY ().ClampAngle();

				curInput.jump = inputsInterface.GetJump ();

				curInput.crouch = inputsInterface.GetCrouch ();
			}
		}

		private List<Inputs> iSend;

		private float curTimeDelta = 0f;

		Results RunCommand(Results inpRes, Inputs inp) {

			if (aiEnabled) {
				inpRes.aiEnabled = true;
				if (aiTargetReached == 0)
					inpRes.aiTarget = aiTarget1;
				else if (aiTargetReached == 1)
					inpRes.aiTarget = aiTarget2;
				else
					inpRes.aiEnabled = false;
			} else
				inpRes.aiEnabled = false;

			inpRes = MoveCharacter(inpRes, inp, Time.fixedDeltaTime * sendUpdates, data.maxSpeed);

			if (lastResults.aiEnabled && Vector2.Distance(new Vector2(lastResults.position.x, lastResults.position.z), new Vector2(lastResults.aiTarget.x, lastResults.aiTarget.z)) <= data.aiTargetDistanceXZ && Mathf.Abs(lastResults.position.y - lastResults.aiTarget.y) <= data.aiTargetDistanceY)
				aiTargetReached++;

			//Notify the game manager
			GameManager.PlayerTick(this, lastResults); //clientInputs [clientInputs.Count - 1]);

			return inpRes;
		}

		//Server's part of processing all user's commands. TODO: add time synchronization when running multiple ticks at once
		[ServerCallback]
		void ProcessCommandsServer(ref Inputs[] commands, uint size) {

			controller.enabled = true;

			posStart = interpPos;
			rotStart = interpRot;
			lerpTicks++;
			startTime = GameManager.curtime;

			for (int i = 0; i < size; i++) {
				int valid = IsCommandValid(ref commands[i]);

				//If valid is -1, you are free to ban the player

				if (valid != 1)
					continue;

				curInputServer = commands[i];

				LagCompensation.StartLagCompensation(GameManager.players[gmIndex], ref curInputServer);
				serverResults = RunCommand(serverResults, curInputServer);
				LagCompensation.EndLagCompensation(GameManager.players[gmIndex]);
				sendResultsArray.Add(serverResults);

				currentTFixedUpdates += sendUpdates;

				if (data.debug && lastTick + 1 != curInputServer.timestamp && lastTick != 0)
					Debug.Log("Missing tick " + lastTick + 1);

				lastTick = curInputServer.timestamp;
				simulationTime = GameManager.curtime;
				commandTime = GameManager.curtime;

				SetDirtyBit(1);
			}

			posEnd = serverResults.position;
			groundPointTime = serverResults.groundPointTime;
			posEndG = serverResults.groundPoint;
			rotEnd = serverResults.rotation;

			if (tickUpdate != null) tickUpdate(serverResults, false);
			if (tickUpdateNotify != null) tickUpdateNotify(false);
			if (data.debug && tickUpdateDebug != null)
				tickUpdateDebug(curInput, serverResults, false);

			controller.enabled = false;
		}

		//Prediction is simple, start from the last acknowledged server result, and continue forward up until the latest tick
		void PerformPrediction() {
			if (!isLocalPlayer)
				return;

			if (worldUpdated)
				tempResults = serverResults;

			worldUpdated = false;

			controller.enabled = true;

			for (uint i = tempResults.timestamp + 1; i < currentTick; i++)
				tempResults = RunCommand(tempResults, clientInputs[i % clientInputs.Length]);

			lastResults = tempResults;

			controller.enabled = false;
		}

		//This is where the ticks happen
		public void Tick () {

			//If playing back from recorded file, we do not need to do any calculations
			if (playbackMode)
				return;

			//Update the value if it is different
			if (data.strafeToSpeedCurveScale != _strafeToSpeedCurveScale) {
				_strafeToSpeedCurveScale = data.strafeToSpeedCurveScale;
				strafeToSpeedCurveScaleMul = 1f / data.strafeToSpeedCurveScale;
			}

			//Increment the fixed update counter
			if (isLocalPlayer || isServer) {
				curTimeDelta += Time.deltaTime;
				int fixedUpdateC = (int)(curTimeDelta / Time.fixedDeltaTime);
				currentFixedUpdates += fixedUpdateC;
				curTimeDelta -= Time.fixedDeltaTime * fixedUpdateC;
			}

			int ticksToRun = currentFixedUpdates / sendUpdates;
			currentFixedUpdates -= ticksToRun * sendUpdates;

			//Local player, generate all the commands needed for the server and send them out
			if (isLocalPlayer) {
				bool sendPacket = true;

				if (iSend == null)
					iSend = new List<Inputs>();

				for (int i = 0; i < ticksToRun; i++) {
					curInput.timestamp = currentTick++;
					curInput.servertick = GameManager.tick;
					iSend.Add(curInput);
					clientInputs[curInput.timestamp % clientInputs.Length] = curInput;
				}

				if (iSend.Count <= 0 || isServer)
					sendPacket = false;

				posStart = interpPos;
				rotStart = interpRot;
				lerpTicks++;
				startTime = GameManager.curtime;

				PerformPrediction();

				posEnd = lastResults.position;
				groundPointTime = lastResults.groundPointTime;
				posEndG = lastResults.groundPoint;
				rotEnd = lastResults.rotation;

				if (tickUpdate != null) tickUpdate(lastResults, false);
				if (tickUpdateNotify != null) tickUpdateNotify(false);
				if (data.debug && tickUpdateDebug != null)
					tickUpdateDebug(curInput, lastResults, false);

				//Send the inputs
				if (sendPacket)
					SendInputs(ref iSend);
			}

			if (isServer) {
				//If local player, then we just need to send the last results over, since the prediction is done
				//otherwise, we check the client if his simulation time is too low, then we can assume the client is timing out
				//but since we need to simulate it, we have to repeat last commands again
				if (isLocalPlayer) {
					sendResultsArray.Add(lastResults);
					//The dirty bit must be set to invoke serialization
					SetDirtyBit(1);
				} else if (GameManager.curtime - commandTime > data.maxLagTime && ticksToRun > 0) {

					controller.enabled = true;

					for (int i = 0; i < ticksToRun; i++) {
						curInputServer.timestamp++;
						curInputServer.servertick++;
						LagCompensation.StartLagCompensation(GameManager.players[gmIndex], ref curInputServer);
						serverResults = RunCommand(serverResults, curInputServer);
						sendResultsArray.Add(serverResults);
						simulationTime = GameManager.curtime;
						LagCompensation.EndLagCompensation(GameManager.players[gmIndex]);
					}

					controller.enabled = false;

					if (tickUpdate != null) tickUpdate(serverResults, false);
					if (tickUpdateNotify != null) tickUpdateNotify(false);
					if (data.debug && tickUpdateDebug != null)
						tickUpdateDebug(curInput, serverResults, false);

					SetDirtyBit(1);
				}
			}
		}

		//Function to set last and next results in the playback mode
		public void PlaybackSetResults (Results fRes, Results sRes, int nSendUpdates, float speed) {

			if (speed == -1f) {

				//We would idealy want to silently update animations and have an ability to silently restore them back to the original state, but currently we don't handle it yet, thus we just return

				if (tickUpdate != null) tickUpdate(sRes, true);
				if (tickUpdateNotify != null) tickUpdateNotify(true);

				return;
			}

			if (!playbackMode)
				return;

			lerpTicks++;
			startTime = Time.fixedTime;
			playbackSpeed = speed;

			_sendUpdates = nSendUpdates;

			controller.enabled = false;
			posEnd = sRes.position;
			groundPointTime = sRes.groundPointTime;
			if (tickUpdate != null) tickUpdate (sRes, false);
			if (tickUpdateNotify != null) tickUpdateNotify (false);
		}

		//This is where all the interpolation happens
		public void PreRender () {

			//If controlled outside, or in playback mode then we stop because in these cases the player should be controlled outside the following code.
			if (serverResults.controlledOutside || lastResults.controlledOutside || serverResults.ragdoll || lastResults.ragdoll || playbackMode)
				return;

			if (data.movementType == MoveType.UpdateOnceAndLerp) {
				if (isLocalPlayer || isServer || (GameManager.curtime - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= 1f) {
					interpPos = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates / (lerpTicks != 0 ? lerpTicks : 1f)));
					//if ((Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates) <= groundPointTime)
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates * groundPointTime));
					//else
					//	interpPos.y = Mathf.Lerp (posStart.y, posEndG, (Time.time - startTime + (groundPointTime * Time.fixedDeltaTime * _sendUpdates)) / (Time.fixedDeltaTime * _sendUpdates * (1f - groundPointTime)));

					interpRot = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates / (lerpTicks != 0 ? lerpTicks : 1f)));
					if (isLocalPlayer)
						interpRot = Quaternion.Euler (myTransform.rotation.eulerAngles.x, lastResults.aiEnabled ? Quaternion.LookRotation (lastResults.aiTarget - myTransform.position).eulerAngles.y : curInput.x, myTransform.rotation.eulerAngles.z);
					myTransform.position = interpPos;
					myTransform.rotation = interpRot;
				} else {
					myTransform.position = Vector3.Lerp (posEnd, posEndO, (GameManager.curtime - startTime) / (Time.fixedDeltaTime * _sendUpdates / (lerpTicks != 0 ? lerpTicks : 1f)) - 1f);
					myTransform.rotation = Quaternion.Lerp (rotEnd, rotEndO, (GameManager.curtime - startTime) / (Time.fixedDeltaTime * _sendUpdates / (lerpTicks != 0 ? lerpTicks : 1f)) - 1f);
				}
			} else {
				myTransform.position = posEnd;
				myTransform.rotation = rotEnd;
			}
			lerpTicks = 0;
		}

		//Data not to be messed with. Needs to be outside the function due to OnControllerColliderHit
		Vector3 hitNormal;

		//Actual movement code. Mostly isolated, except transform
		Results MoveCharacter (Results inpRes, Inputs inp, float deltaMultiplier, Vector3 maxSpeed) {

			//If controlled outside, return results with the current transform position.
			if (inpRes.controlledOutside) {
				inpRes.speed = myTransform.position - inpRes.position;
				return new Results (myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.isGrounded, inpRes.jumped, inpRes.crouch, 0, 0, inpRes.aiTarget, inpRes.aiEnabled, inpRes.controlledOutside, inpRes.ragdoll, inpRes.ragdollTime, inp.timestamp);
			}

			//Calculates if ragdoll should be disabled
			if (inpRes.ragdoll) {
				inpRes.speed = myTransform.position - inpRes.position;
				if (inpRes.speed.magnitude >= data.ragdollStopVelocity)
					inpRes.ragdollTime = inp.timestamp;
				if (inp.timestamp - inpRes.ragdollTime >= ragdollTime)
					inpRes.ragdoll = false;
				return new Results (myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.isGrounded, inpRes.jumped, inpRes.crouch, 0, 0, inpRes.aiTarget, inpRes.aiEnabled, inpRes.controlledOutside, inpRes.ragdoll, inpRes.ragdollTime, inp.timestamp);
			}

			//Clamp camera angles
			inp.y = Mathf.Clamp (curInput.y, dataInp.camMinY, dataInp.camMaxY);

			if (inp.x > 360f)
				inp.x -= 360f;
			else if (inp.x < 0f)
				inp.x += 360f;

			//Save current position and rotation to restore after the move
			Vector3 pos = myTransform.position;
			Quaternion rot = myTransform.rotation;

			//Set the position and rotation to the last results ones
			myTransform.position = inpRes.position;
			myTransform.rotation = inpRes.rotation;

			Vector3 tempSpeed = myTransform.InverseTransformDirection (inpRes.speed);

			if (inpRes.aiEnabled)
				InputsAI (ref inpRes, ref inp, ref deltaMultiplier);
			
			myTransform.rotation = Quaternion.Euler (new Vector3 (0, inp.x, 0));

			//Character sliding of surfaces
			if (!inpRes.isGrounded) {
				//inpRes.speed.x += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.x * (inpRes.speed.y > 0 ? 0 : -inpRes.speed.y) * (1f - data.slideFriction);
				inpRes.speed.x += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.x * (1f - data.slideFriction);
				//inpRes.speed.z += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.z * (inpRes.speed.y > 0 ? 0 : -inpRes.speed.y) * (1f - data.slideFriction);
				inpRes.speed.z += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.z * (1f - data.slideFriction);
			}

			Vector3 localSpeed = myTransform.InverseTransformDirection (inpRes.speed);
			Vector3 localSpeed2 = Vector3.Lerp (myTransform.InverseTransformDirection (inpRes.speed), tempSpeed, data.velocityTransferCurve.Evaluate (Mathf.Abs (inpRes.rotation.eulerAngles.y - inp.x) / (deltaMultiplier * data.velocityTransferDivisor)));
			
			if (!inpRes.isGrounded && data.strafing)
				AirStrafe (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed, ref localSpeed2);
			else
				localSpeed = localSpeed2;

			BaseMovement (ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed);

			float tY = myTransform.position.y;

			//Convert the local coordinates to the world ones
			inpRes.speed = transform.TransformDirection (localSpeed);
			hitNormal = new Vector3 (0, 0, 0);

			//Set the speed to the curve values. Allowing to limit the speed
			inpRes.speed.x = data.finalSpeedCurve.Evaluate (inpRes.speed.x);
			inpRes.speed.y = data.finalSpeedCurve.Evaluate (inpRes.speed.y);
			inpRes.speed.z = data.finalSpeedCurve.Evaluate (inpRes.speed.z);

			//Move the controller
			controller.Move (inpRes.speed * deltaMultiplier);
			//This code continues after OnControllerColliderHit gets called (if it does)

			if (Vector3.Angle (Vector3.up, hitNormal) <= data.slopeLimit)
				inpRes.isGrounded = true;
			else
				inpRes.isGrounded = false;

			//float speed = inpRes.speed.y;
			inpRes.speed = (transform.position - inpRes.position) / deltaMultiplier;

			//if (inpRes.speed.y > 0)
			//	inpRes.speed.y = Mathf.Min (inpRes.speed.y, Mathf.Max(0, speed));
			//else
			//	inpRes.speed.y = Mathf.Max (inpRes.speed.y, Mathf.Min(0, speed));
			//inpRes.speed.y = speed;

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

			//If snapping is enabled, then do it
			if (data.snapSize > 0f)
				myTransform.position = new Vector3 (Mathf.Round (myTransform.position.x * snapInvert) * data.snapSize, Mathf.Round (myTransform.position.y * snapInvert) * data.snapSize, Mathf.Round (myTransform.position.z * snapInvert) * data.snapSize);

			//If grounded set the speed to the gravity
			if (inpRes.isGrounded)
				localSpeed.y = Physics.gravity.y * Mathf.Clamp(deltaMultiplier, 1f, 1f);

			if (inpRes.speed.magnitude > data.ragdollStartVelocity) {
				inpRes.ragdoll = true;
				inpRes.ragdollTime = inp.timestamp;
			}

			//Generate the return value
			inpRes = new Results (myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.isGrounded, inpRes.jumped, inpRes.crouch, gp, gpt, inpRes.aiTarget, inpRes.aiEnabled, inpRes.controlledOutside, inpRes.ragdoll, inpRes.ragdollTime, inp.timestamp);

			//Set back the position and rotation
			myTransform.position = pos;
			myTransform.rotation = rot;

			return inpRes;
		}

		//The part which determines if the controller was hit or not
		void OnControllerColliderHit (ControllerColliderHit hit) {
			hitNormal = hit.normal;
		}

		public void InputsAI(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier) {
			//float rotation = inpRes.rotation.eulerAngles.y;
			//Just sets the look angle to be towards the AI target
			float targetRotation = Quaternion.LookRotation (inpRes.aiTarget - inpRes.position).eulerAngles.y;
			inp.x = targetRotation;
			inp.inputs.y = 1;
		}

		//Handles strafing in air
		public void AirStrafe(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed, ref Vector3 localSpeed2) {
			if (inpRes.isGrounded)
				return;

			float tAccel = data.strafeAngleCurve.Evaluate(Mathf.Abs (inpRes.rotation.eulerAngles.y.ClampAngle() - inp.x.ClampAngle()) / deltaMultiplier);
			bool rDir = (inpRes.rotation.eulerAngles.y.ClampAngle() - inp.x.ClampAngle()) > 0;

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

		//The movement part
		public void BaseMovement(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed) {

			//Gets the target maximum speed
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

			if (!inp.jump)
				inpRes.jumped = false;

			if (inpRes.isGrounded && inp.jump && !inpRes.crouch && !inpRes.jumped) {
				localSpeed.y = data.speedJump;
				if (!data.allowBunnyhopping)
					inpRes.jumped = true;
			} else if (!inpRes.isGrounded)
				localSpeed.y += Physics.gravity.y * deltaMultiplier;
			else
				localSpeed.y = -1f;

			if (inpRes.isGrounded) {

				if (Mathf.Sign (localSpeed.z * inp.inputs.y) == 1 && inp.inputs.y != 0 && Mathf.Abs(localSpeed.z) <= maxSpeed.z * Mathf.Abs (inp.inputs.y)) {
					localSpeed.z = Mathf.Clamp (localSpeed.z + (inp.inputs.y > 0 ? data.accelerationForward : -data.accelerationBack) * deltaMultiplier,
						-maxSpeed.z * Mathf.Abs (inp.inputs.y),
						maxSpeed.z * Mathf.Abs (inp.inputs.y));
				} else if (inp.inputs.y == 0 || Mathf.Abs(localSpeed.z) > maxSpeed.z * Mathf.Abs (inp.inputs.y)) {
					localSpeed.z = Mathf.Clamp (localSpeed.z + (data.decceleration * -Mathf.Sign (localSpeed.z)) * deltaMultiplier,
						localSpeed.z >= 0 ? 0 : -maxSpeed.z,
						localSpeed.z <= 0 ? 0 : maxSpeed.z);
				} else {
					localSpeed.z = Mathf.Clamp (localSpeed.z + data.accelerationStop * inp.inputs.y * deltaMultiplier,
						localSpeed.z >= 0 ? -data.accelerationBack * deltaMultiplier: -maxSpeed.z,
						localSpeed.z <= 0 ? data.accelerationForward * deltaMultiplier: maxSpeed.z);
				}

				if (Mathf.Sign (localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0 && Mathf.Abs(localSpeed.x) <= maxSpeed.x * Mathf.Abs (inp.inputs.x)) {
					localSpeed.x = Mathf.Clamp (localSpeed.x + Mathf.Sign(inp.inputs.x) * data.accelerationSides * deltaMultiplier,
						-maxSpeed.x * ((Mathf.Sign (localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0) ? Mathf.Abs (inp.inputs.x) : 1f - Mathf.Abs (inp.inputs.x)),
						maxSpeed.x * ((Mathf.Sign (localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0) ? Mathf.Abs (inp.inputs.x) : 1f - Mathf.Abs (inp.inputs.x)));
				} else if (inp.inputs.x == 0 || Mathf.Abs(localSpeed.x) > maxSpeed.x * Mathf.Abs (inp.inputs.x)) {
					localSpeed.x = Mathf.Clamp (localSpeed.x + (data.decceleration * -Mathf.Sign (localSpeed.x)) * deltaMultiplier,
						localSpeed.x >= 0 ? 0 : -maxSpeed.x,
						localSpeed.x <= 0 ? 0 : maxSpeed.x);
				} else {
					localSpeed.x = Mathf.Clamp (localSpeed.x + data.accelerationStop * inp.inputs.x * deltaMultiplier,
						localSpeed.x >= 0 ? -data.accelerationBack * deltaMultiplier: -maxSpeed.x,
						localSpeed.x <= 0 ? data.accelerationForward * deltaMultiplier: maxSpeed.x);
				}
			}
		}
	}
}

//#define SIMULATE

//Should we use command checksum? Due to the use of network writer we produce quite a bit of garbage
//#define CMD_CHECKSUM
//#define LONG_PREDMASK

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace GreenByteSoftware.UNetController {

#region classes

	[System.Flags]
	public enum Keys : uint {
		JUMP = (1 << 0),
		CROUCH = (1 << 1)
	}

	[System.Flags]
	public enum Flags : uint {
		IS_GROUNDED = (1 << 0),
		JUMPED = (1 << 1),
		CROUCHED = (1 << 2),
		AI_ENABLED = (1 << 3),
		CONTROLLED_OUTSIDE = (1 << 4),
		RAGDOLL = (1 << 5)
	}

	public struct CFlags {
		uint val;

		static public implicit operator bool(CFlags value) {
			return value.val != 0;
		}

		static public implicit operator uint(CFlags value) {
			return (uint)value.val;
		}

		public override bool Equals(object obj) {
			return val.Equals(obj);
		}

		public override int GetHashCode() {
			return val.GetHashCode();
		}

		static public bool operator ==(CFlags v1, CFlags v2) {
			return v1.val == v2.val;
		}

		static public bool operator !=(CFlags v1, CFlags v2) {
			return v1.val != v2.val;
		}

		public CFlags(uint nVal) {
			val = nVal;
		}

		static public implicit operator CFlags(uint value) {
			return new CFlags(value);
		}

		static public implicit operator CFlags(Flags value) {
			return new CFlags((uint)value);
		}

		static public implicit operator CFlags(Keys value) {
			return new CFlags((uint)value);
		}

		static public CFlags operator |(CFlags v1, CFlags flags) {
			v1.val |= (uint)flags;
			return v1;
		}

		static public CFlags operator &(CFlags v1, CFlags flags) {
			v1.val &= (uint)flags;
			return v1;
		}

		static public bool operator true(CFlags v1) {
			return v1.val != 0;
		}

		static public bool operator false(CFlags v1) {
			return v1.val == 0;
		}

        public void Set(Flags flags, bool doSet) {
            if (doSet)
                val |= (uint)flags;
            else
                val &= ~(uint)flags;
        }

        public bool IsSet(Flags mask) {
            return (val & (uint)mask) != 0;
        }

        public void Set(Keys flags, bool doSet) {
            if (doSet)
                val |= (uint)flags;
            else
                val &= ~(uint)flags;
        }

        public bool IsSet(Keys mask) {
            return (val & (uint)mask) != 0;
        }

        public void Set(uint flags, bool doSet) {
            if (doSet)
                val |= flags;
            else
                val &= ~flags;
        }

        public bool IsSet(uint mask) {
            return (val & mask) != 0;
        }
	}

	//Input management
	public interface IPLayerInputs {
		float GetMouseX();
		float GetMouseY();
		float GetMoveX(bool forceFPS);
		float GetMoveY(bool forceFPS);
		float GetMoveX();
		float GetMoveY();
		Keys GetKeys();
	}

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Inputs {
		public Vector2 inputs;
		public float x;
		public float y;
		public CFlags keys;
		public uint timestamp;
		public uint servertick;
#if (CMD_CHECKSUM)
		public byte checksum;
#endif
	}

	//Be sure to edit the binary serializable class in the extensions script accordingly
	[System.Serializable]
	public struct Results {
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 groundNormal;
		public float camX;
		public Vector3 speed;
		public CFlags flags;
		public float groundPoint;
		public float groundPointTime;
		public uint ragdollTime;
		public uint timestamp;

		public Results(Vector3 pos, Quaternion rot, Vector3 gndNormal, float cam, Vector3 spe, CFlags fl, float gp, float gpt, uint rgdlTime, uint tick) {
			position = pos;
			rotation = rot;
			groundNormal = gndNormal;
			camX = cam;
			speed = spe;
			flags = fl;
			groundPoint = gp;
			groundPointTime = gpt;
			ragdollTime = rgdlTime;
			timestamp = tick;
		}

		public override string ToString() {
			return "" + position + "\n"
				+ rotation + "\n"
				+ camX + "\n"
				+ speed + "\n"
				+ flags + "\n"
				+ groundPoint + "\n"
				+ groundPointTime + "\n"
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

	//Required functions for character
    public abstract class BaseController : NetworkBehaviour
	{
		protected abstract void OnStart();
		protected abstract void InitializeResults(ref Results results);
		protected abstract void OnBeingDestroyed();
		public abstract int IsCommandValid(ref Inputs inp);
		protected abstract void InputUpdate(ref Inputs inputs);
		protected abstract void RunCommand(ref Results results, Inputs inp);
		protected abstract void RunPreMove(ref Results results, ref Inputs inp);
		protected abstract void RunPostMove(ref Results results, ref Inputs inp);
		protected abstract void ProcessInterpolation(ref Results res1, ref Results res2, float lerpTime);
		protected abstract void ProcessTeleportation(ref Results res);
	}

	//The Controller
	[NetworkSettings (channel=1)]
    public class Controller : BaseController {

		private const int MAX_INPUTS_MESSAGE = 24;

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

		protected int _sendUpdates;

		public int lSendUpdates {
			get { return _sendUpdates; }
		}

		private int currentFixedUpdates = 0;

		private uint currentTick = 0;

		public PredVar_uint startTick;

		private int windowError = 0;
		private uint updateCount = 0;

		[System.NonSerialized]
		public Inputs[] clientInputs;
		private Inputs curInput;

		protected Results serverResults;
		private Results tempResults;
		protected Results lastResults;
		private Results[] histResults = new Results[3];

		private Results sendResults;
		private Results sentResults;

		public Transform head;
		public Transform camTarget;
		public Transform camTargetFPS;

		private float startTime;

		private bool worldUpdated = false;

		private uint lastTick = 0;
		private bool receivedFirstTime;

		public RestorePredictedVars restorePredictedVars;
		public GetVarsMask getVarMask;
		public ReadValue readVarValues;
		public WriteValue writeVarValues;

		private int _curPredIndex = 0;
		public int curPredIndex { get { return _curPredIndex; } }

		public TickUpdateNotifyDelegate tickUpdateNotify;
    	public TickUpdateDelegate tickUpdate;
		public TickUpdateAllDelegate tickUpdateDebug;

		private float commandTime = 0f;
		private float simulationTime = 0f;

		[System.NonSerialized]
		public int gmIndex = -1;

		[System.NonSerialized]
		public int cachedID = -103;

		[System.NonSerialized]
		public bool playbackMode = false;
		[System.NonSerialized]
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
                    _crouchSwitchMul = 1 / (data.controllerCrouchSwitch / (Time.fixedDeltaTime * GameManager.sendUpdates));
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
			if (!controlMode && lastResults.flags & Flags.CONTROLLED_OUTSIDE) {
				SetPosition (myTransform.position);
				SetRotation (myTransform.rotation);
			}
            lastResults.flags.Set(Flags.CONTROLLED_OUTSIDE, controlMode);
		}

		public void SetRagdoll (bool controlMode) {
			if (!controlMode && lastResults.flags & Flags.RAGDOLL && !(lastResults.flags & Flags.CONTROLLED_OUTSIDE)) {
				SetPosition (myTransform.position);
				SetRotation (myTransform.rotation);
			}
			lastResults.flags.Set(Flags.RAGDOLL, controlMode);
		}

		//Returns a copy of the results on the local environment
		public Results GetResults () {
            return lastResults;
		}

		//Sets the velocity on the last result
		public void SetVelocity (Vector3 vel) {
			lastResults.speed = vel;
		}

		//Sets the position on the last result
		public void SetPosition (Vector3 pos) {
			lastResults.position = pos;
		}

		//Sets the rotation
		public void SetRotation (Quaternion rot) {
			lastResults.rotation = rot;
		}

		public uint GetTimestamp() {
			return currentTick;
		}

		//Initialization
		protected override void OnStart () {
			gameObject.name = Extensions.GenerateGUID ();

			if (data == null || dataInp == null) {
				Debug.LogError ("No controller data attached! Will not continue.");
				this.enabled = false;
				return;
			}

			commandTime = GameManager.curtime;
			simulationTime = GameManager.curtime;

			//Disable the warning for now
			if (simulationTime == -1f)
				simulationTime = -1f;

			clientInputs = new Inputs[data.inputsToStore];

			curInput = new Inputs ();
			curInput.x = myTransform.rotation.eulerAngles.y;
			curInput.inputs = new Vector2 ();

			_sendUpdates = GameManager.sendUpdates;

			if (isServer) {
				curInput.timestamp = 0;
				NetworkServer.RegisterHandler (inputMessage, GameManager.OnSendInputs);
			}

			if (!playbackMode)
				GameManager.RegisterController(this);
			else if (GetComponent<RecordableObject>() != null)
				GetComponent<RecordableObject>().RecordCountHook(ref tickUpdateNotify);
		}

		protected override void InitializeResults(ref Results results) {
			results.position = myTransform.position;
			results.rotation = myTransform.rotation;
		}

		//A tiny bit of reflection during the initialization, this is all for convenience
		private void Awake() {

			System.Reflection.BindingFlags bindingFlags = System.Reflection.BindingFlags.Public |
							System.Reflection.BindingFlags.NonPublic |
							System.Reflection.BindingFlags.Instance |
							System.Reflection.BindingFlags.Static;

			foreach (System.Reflection.FieldInfo field in this.GetType().GetFields(bindingFlags)) {
				var val = field.GetValue(this);
				IPredVarBase predVar = val as IPredVarBase;
				if (predVar != null) {
					predVar.Initialize(this);
					_curPredIndex++;
				}
			}
		}

		void Start() {
			OnStart();

			if (isLocalPlayer)
                InitializeResults(ref serverResults);
			InitializeResults(ref lastResults);
			InitializeResults(ref tempResults);
		}

		protected override void OnBeingDestroyed() {
			GameManager.UnregisterController(this);
		}

		public void OnDestroy () {
			OnBeingDestroyed();
		}

		NetworkWriter cSumWriter;
		byte[] sumBytes;

		protected void GetCommandChecksum(Inputs inp, NetworkWriter writer) {
			writer.Write(inp.inputs);
			writer.Write(inp.x);
			writer.Write(inp.y);
			writer.WritePackedUInt32(inp.keys);
			writer.Write(inp.timestamp);
			writer.Write(inp.servertick);
		}

		public byte GetCommandChecksum(Inputs inp) {
#if (CMD_CHECKSUM)
			if (cSumWriter == null)
				cSumWriter = new NetworkWriter();

			GetCommandChecksum(inp, cSumWriter);

			sumBytes = cSumWriter.AsArray();

			int checkSum = 0;

			for (int i = 0; i < sumBytes.Length; i++)
				checkSum += sumBytes[i];

			return (byte)(checkSum & 0xFF);
#else
			return 0;
#endif
		}

		//Creates the bitmask by comparing 2 different inputs
		protected uint GetInputsBitMask(Inputs inp1, Inputs inp2) {
			uint mask = 0;
			if (inp2.timestamp % GameManager.settings.maxDeltaTicks == 0)
				return 0xFFFFFFFF;

			if (inp1.inputs != inp2.inputs) mask |= 1 << 0;
			if (inp1.x != inp2.x) mask |= 1 << 1;
			if (inp1.y != inp2.y) mask |= 1 << 2;
			if (inp1.keys != inp2.keys) mask |= 1 << 3;
			if (inp1.timestamp + 1 != inp2.timestamp) mask |= 1 << 4;
			if (inp1.servertick + 1 != inp2.servertick) mask |= 1 << 5;
			return mask;
		}

		protected Inputs ReadInputs(NetworkReader reader, Inputs inp, uint mask) {
			if ((mask & (1 << 0)) != 0)
				inp.inputs = reader.ReadVector2();
			if ((mask & (1 << 1)) != 0)
				inp.x = reader.ReadSingle();
			if ((mask & (1 << 2)) != 0)
				inp.y = reader.ReadSingle();
			if ((mask & (1 << 3)) != 0)
				inp.keys = (Keys)reader.ReadPackedUInt32();

			//We are expecting timestamp to be one tick higher,
			//so that is why this flag is set on the client to be checked for difference between command values rather than equality
			if ((mask & (1 << 4)) != 0)
				inp.timestamp = reader.ReadPackedUInt32();
			else
				inp.timestamp++;
			if ((mask & (1 << 5)) != 0)
				inp.servertick = reader.ReadPackedUInt32();
			else
				inp.servertick++;
#if (CMD_CHECKSUM)
			inp.checksum = reader.ReadByte();
#endif
			return inp;
		}

		public Inputs ReadInputs(NetworkReader reader, Inputs inp) {
			uint mask = reader.ReadPackedUInt32();
			return ReadInputs(reader, inp, mask);
		}

		protected void WriteInputs(ref NetworkWriter writer, Inputs inp, uint mask) {

			if ((mask & (1 << 0)) != 0)
				writer.Write(inp.inputs);
			if ((mask & (1 << 1)) != 0)
				writer.Write(inp.x);
			if ((mask & (1 << 2)) != 0)
				writer.Write(inp.y);
			if ((mask & (1 << 3)) != 0)
				writer.WritePackedUInt32((uint)inp.keys);
			if ((mask & (1 << 4)) != 0)
				writer.WritePackedUInt32(inp.timestamp);
			if ((mask & (1 << 5)) != 0)
				writer.WritePackedUInt32(inp.servertick);
#if (CMD_CHECKSUM)
			writer.Write(inp.checksum);
#endif
		}

		public void WriteInputs(ref NetworkWriter writer, Inputs inp, Inputs prevInp) {
			uint mask = GetInputsBitMask(prevInp, inp);

			writer.WritePackedUInt32(mask);

			WriteInputs(ref writer, inp, mask);
		}

		private Inputs prevInput;
		private Inputs cInp;

		//This is called on the client to send the current inputs
		void SendInputs (ref List<Inputs> inp) {

			if (!isLocalPlayer || isServer)
				return;

			if (inputWriter == null)
				inputWriter = new NetworkWriter ();

			inputWriter.SeekZero();
			inputWriter.StartMessage(inputMessage);
			int sz = inp.Count;
			inputWriter.WritePackedUInt32((uint)sz);
			for (int i = 0; i < sz; i++) {
				cInp = inp[i];
#if (CMD_CHECKSUM)
				cInp.checksum = GetCommandChecksum(cInp);
#endif
				WriteInputs(ref inputWriter, cInp, prevInput);
				prevInput = cInp;
			}
			inputWriter.FinishMessage();

			myClient.SendWriter(inputWriter, GetNetworkChannel());

			inp.Clear();
		}

		//We need to check the data for validity and decide on what to do later
		//0 - invalid checksum, network error
		//1 - valid command, good to go
		//-1 - invalid command data, probably a hacked client or mistake in code
		public override int IsCommandValid (ref Inputs cmd) {

#if (CMD_CHECKSUM)
			byte checkSum = GetCommandChecksum(cmd);

			if (checkSum != cmd.checksum)
				return 0;
#endif
			uint cmdTick = (startTick + cmd.timestamp);
			if (GameManager.tick > cmdTick)
				windowError = (int)(GameManager.tick - cmdTick);
			else
				windowError = (int)(cmdTick - GameManager.tick);

			if (windowError > GameManager.settings.slidingWindowSize)
				return 0;

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

		public void ReadResults (NetworkReader reader, ref Results curRes, uint mask) {
			if ((mask & (1 << 0)) != 0)
				curRes.position = reader.ReadVector3();
			if ((mask & (1 << 1)) != 0)
				curRes.rotation = reader.ReadQuaternion();
			if ((mask & (1 << 2)) != 0)
				curRes.groundNormal = reader.ReadVector3();
			if ((mask & (1 << 3)) != 0)
				curRes.camX = reader.ReadSingle();
			if ((mask & (1 << 4)) != 0)
				curRes.speed = reader.ReadVector3();
			if ((mask & (1 << 5)) != 0)
				curRes.flags = reader.ReadPackedUInt32();
			if ((mask & (1 << 6)) != 0)
				curRes.groundPoint = reader.ReadSingle();
			if ((mask & (1 << 7)) != 0)
				curRes.groundPointTime = reader.ReadSingle();
			if ((mask & (1 << 8)) != 0)
				curRes.ragdollTime = reader.ReadPackedUInt32();
			if ((mask & (1 << 9)) != 0)
				curRes.timestamp = reader.ReadPackedUInt32();
			else
				curRes.timestamp++;
		}

		//Results part
		public override void OnDeserialize (NetworkReader reader, bool initialState) {

			if (isServer)
				return;

			uint mask = reader.ReadPackedUInt32 ();
			ReadResults(reader, ref sendResults, mask);
			OnSendResults (sendResults);

			var predVarMask =
#if (LONG_PREDMASK)
				reader.ReadPackedUInt64();
#else
				reader.ReadPackedUInt32();
#endif

			bool setValues = false;

			if (!isLocalPlayer)
				setValues = true;

			if (readVarValues != null) readVarValues(reader, predVarMask, setValues, initialState);
		}

		//Creates the bitmask by comparing 2 different results
		//Uses some expectancy checks in timestamps for better compression
		protected uint GetResultsBitMask (Results prev, Results cur) {
			uint mask = 0;
			if(cur.position != prev.position) mask |= 1 << 0;
			if(cur.rotation != prev.rotation) mask |= 1 << 1;
			if(cur.groundNormal != prev.groundNormal) mask |= 1 << 2;
			if(cur.camX != prev.camX) mask |= 1 << 3;
			if(cur.speed != prev.speed) mask |= 1 << 4;
			if(cur.flags != prev.flags) mask |= 1 << 5;
			if(cur.groundPoint != prev.groundPoint) mask |= 1 << 6;
			if(cur.groundPointTime != prev.groundPointTime) mask |= 1 << 7;
			if(cur.ragdollTime != prev.ragdollTime) mask |= 1 << 8;
			if(cur.timestamp != prev.timestamp + 1 || cur.timestamp % GameManager.settings.maxDeltaTicks == 0) mask |= 1 << 9;
			return mask;
		}

		public void WriteResults(NetworkWriter writer, ref Results curRes, uint mask) {
			//This is some bitmask magic. We check if that bit is 1 or 0, and depending on that, we decide if we should write the data.
			if ((mask & (1 << 0)) != 0)
				writer.Write(curRes.position);
			if ((mask & (1 << 1)) != 0)
				writer.Write(curRes.rotation);
			if ((mask & (1 << 2)) != 0)
				writer.Write(curRes.groundNormal);
			if ((mask & (1 << 3)) != 0)
				writer.Write(curRes.camX);
			if ((mask & (1 << 4)) != 0)
				writer.Write(curRes.speed);
			if ((mask & (1 << 5)) != 0)
				writer.WritePackedUInt32(curRes.flags);
			if ((mask & (1 << 6)) != 0)
				writer.Write(curRes.groundPoint);
			if ((mask & (1 << 7)) != 0)
				writer.Write(curRes.groundPointTime);
			if ((mask & (1 << 8)) != 0)
				writer.WritePackedUInt32(curRes.ragdollTime);
			if ((mask & (1 << 9)) != 0)
				writer.WritePackedUInt32(curRes.timestamp);
		}

		//Called on the server when serializing the results
		public override bool OnSerialize (NetworkWriter writer, bool initialState) {

            uint mask = initialState ? ~0u : GetResultsBitMask (sentResults, sendResults);
			writer.WritePackedUInt32 (mask);
			WriteResults(writer, ref sendResults, mask);
			sentResults = sendResults;

#if (LONG_PREDMASK)
			ulong predVarMask = 0ul;
#else
			uint predVarMask = 0u;
#endif
			if (getVarMask != null) getVarMask(ref predVarMask);
#if (LONG_PREDMASK)
			writer.WritePackedUInt64(predVarMask);
#else
			writer.WritePackedUInt32(predVarMask);
#endif
            if (writeVarValues != null) writeVarValues(writer, initialState);

			return true;
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
                Debug_UI.UpdateUI(res.position, res.position, res.position, currentTick, res.timestamp);
				serverResults = res;
				worldUpdated = true;
			} else {
				currentTick++;
				updateCount++;

                lastResults = res;
                GameManager.PlayerTick (this, lastResults);
				if (tickUpdate != null) tickUpdate(res, false);

                histResults[updateCount % 3] = lastResults;

				if (currentTick > 2 && currentTick % GameManager.singleton.networkSettings.maxDeltaTicks != 0) {
					//if (Time.fixedTime - 2f > startTime)
					startTime = Mathf.Min(Time.time, Time.fixedTime);
					//else
					//	startTime = Time.fixedTime - ((Time.fixedTime - startTime) / (Time.fixedDeltaTime * _sendUpdates) - 1) * (Time.fixedDeltaTime * _sendUpdates);
				} else
					startTime = Mathf.Min(Time.time, Time.fixedTime);
			}
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

		protected override void InputUpdate (ref Inputs inp) {
			if (inputsInterface == null)
				throw (new UnityException("inputsInterface is not set!"));

			inp.inputs.x = inputsInterface.GetMoveX();
			inp.inputs.y = inputsInterface.GetMoveY();

			inp.x = inputsInterface.GetMouseX().ClampAngle();
			inp.y = inputsInterface.GetMouseY().ClampAngle();

			inp.keys = inputsInterface.GetKeys();
		}

		//Input gathering
		public void UpdateInputs () {
			if (isLocalPlayer)
				InputUpdate(ref curInput);
		}

		private List<Inputs> iSend;

		private float curTimeDelta = 0f;

		protected override void RunPreMove(ref Results results, ref Inputs inp) {}
		protected override void RunPostMove(ref Results results, ref Inputs inp) {}
		protected override void RunCommand(ref Results results, Inputs inp) {}

		//Server's part of processing all user's commands. TODO: add time synchronization when running multiple ticks at once
		[ServerCallback]
		void ProcessCommandsServer(ref Inputs[] commands, uint size) {

			controller.enabled = true;

			if (startTick == 0u)
				startTick.value = GameManager.tick;

			for (int i = 0; i < size; i++) {
				int valid = IsCommandValid(ref commands[i]);

				//If valid is -1, you are free to ban the player

				if (valid != 1)
					continue;

				curInput = commands[i];

				LagCompensation.StartLagCompensation(GameManager.players[gmIndex], ref curInput);
				RunCommand(ref lastResults, curInput);
                LagCompensation.EndLagCompensation(GameManager.players[gmIndex]);

				if (data.debug && lastTick + 1 != curInput.timestamp && lastTick != 0)
					Debug.Log("Missing tick " + lastTick + 1);

				lastTick = curInput.timestamp;
				simulationTime = GameManager.curtime;
				commandTime = GameManager.curtime;

				SetDirtyBit(1);
			}

			if (size > 0) {
				updateCount++;
				histResults[updateCount % 3] = lastResults;
				sendResults = lastResults;
				startTime = Mathf.Min(Time.time, Time.fixedTime);
			}

            if (tickUpdate != null) tickUpdate(lastResults, false);
			if (tickUpdateNotify != null) tickUpdateNotify(false);
			if (data.debug && tickUpdateDebug != null)
                tickUpdateDebug(curInput, lastResults, false);

			controller.enabled = false;
		}

		//Prediction is simple, start from the last acknowledged server result, and continue forward up until the latest tick
		void PerformPrediction() {
			if (!isLocalPlayer)
				return;

			if (worldUpdated) {
				tempResults = serverResults;
				if (restorePredictedVars != null) restorePredictedVars();
			}

			worldUpdated = false;

			controller.enabled = true;

			uint lastTimestamp = tempResults.timestamp;

			for (uint i = tempResults.timestamp + 1; i < currentTick; i++) {
				if (clientInputs[i % clientInputs.Length].timestamp <= lastTimestamp)
					break;
				RunCommand(ref tempResults, clientInputs[i % clientInputs.Length]);
				lastTimestamp = clientInputs[i % clientInputs.Length].timestamp;
			}

			lastResults = tempResults;

			controller.enabled = false;
		}

		//This is where the ticks happen
		public void Tick () {

			//If playing back from recorded file, we do not need to do any calculations
			if (playbackMode)
				return;

			//Increment the fixed update counter
			if (isLocalPlayer || isServer) {
				curTimeDelta += Time.deltaTime;
				int fixedUpdateC = (int)(curTimeDelta / Time.fixedDeltaTime);
				currentFixedUpdates += fixedUpdateC;
				curTimeDelta -= Time.fixedDeltaTime * fixedUpdateC;
			}

			int ticksToRun = currentFixedUpdates / _sendUpdates;
			currentFixedUpdates -= ticksToRun * _sendUpdates;

			if (isLocalPlayer && isServer && startTick == 0u)
				startTick.value = GameManager.tick;

			if (isLocalPlayer && startTick != 0u && currentFixedUpdates > 0) {
				uint cmdTick = (startTick + currentTick);
				if (GameManager.tick > cmdTick)
					windowError = -(int)(GameManager.tick - cmdTick - ticksToRun);
				else
					windowError = (int)(cmdTick + ticksToRun - GameManager.tick);

				if (windowError > GameManager.settings.maxSlidingWindowInaccuracy) {
					ticksToRun -= GameManager.settings.maxSlidingWindowInaccuracy;
					currentFixedUpdates -= windowError * _sendUpdates;
				} else if (windowError < -GameManager.settings.maxSlidingWindowInaccuracy) {
					ticksToRun += GameManager.settings.maxSlidingWindowInaccuracy;
					currentFixedUpdates -= windowError * _sendUpdates;
				}
			}

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

				if(ticksToRun > 0) {
					updateCount++;
					histResults[updateCount % 3] = lastResults;
					startTime = Mathf.Min(Time.time, Time.fixedTime);
				}

				PerformPrediction();

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
                    sendResults = lastResults;
					//The dirty bit must be set to invoke serialization
					SetDirtyBit(1);
				} else if (GameManager.curtime - commandTime > data.maxLagTime && ticksToRun > 0) {

					controller.enabled = true;

					for (int i = 0; i < ticksToRun; i++) {
						curInput.timestamp++;
						curInput.servertick++;
						LagCompensation.StartLagCompensation(GameManager.players[gmIndex], ref curInput);
                        RunCommand(ref lastResults, curInput);
                        sendResults = lastResults;
						simulationTime = GameManager.curtime;
						LagCompensation.EndLagCompensation(GameManager.players[gmIndex]);
					}

					controller.enabled = false;

                    if (tickUpdate != null) tickUpdate(lastResults, false);
					if (tickUpdateNotify != null) tickUpdateNotify(false);
					if (data.debug && tickUpdateDebug != null)
                        tickUpdateDebug(curInput, lastResults, false);

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

			startTime = Time.fixedTime;
			playbackSpeed = speed;

			_sendUpdates = nSendUpdates;

			controller.enabled = false;
			if (tickUpdate != null) tickUpdate (sRes, false);
			if (tickUpdateNotify != null) tickUpdateNotify (false);
		}

		Vector3 startPos = new Vector3(0, 0, 0);
		Vector3 endPos = new Vector3(0, 0, 0);

		protected override void ProcessInterpolation(ref Results res1, ref Results res2, float lerpTime) {

			startPos = res1.position;
			endPos = res2.position;

			if(data.handleMidTickJump) {
				if (lerpTime < res2.groundPointTime) {
					endPos = Vector3.Lerp(startPos, endPos, res2.groundPointTime);
					endPos.z = res2.groundPoint;
					lerpTime /= res2.groundPointTime;
				} else {
					startPos = Vector3.Lerp(startPos, endPos, 1f - res2.groundPointTime);
					startPos.z = res2.groundPoint;
					lerpTime /= (1f - res2.groundPointTime);
				}
			}

			myTransform.position = Vector3.Lerp (startPos, endPos, lerpTime);
			myTransform.rotation = Quaternion.Lerp (res1.rotation, res2.rotation, lerpTime);
		}

		protected override void ProcessTeleportation(ref Results res) {
			myTransform.position = res.position;
			myTransform.rotation = res.rotation;
		}

		//This is where all the interpolation happens
		public void PreRender () {

			//If controlled outside, or in playback mode then we stop because in these cases the player should be controlled outside the following code.
			if ((histResults[updateCount % 3].flags) & (Flags.CONTROLLED_OUTSIDE | Flags.RAGDOLL) || playbackMode)
				return;

			int interpTicks = data.interpTicks;
			if(isLocalPlayer && interpTicks > 1)
				interpTicks = 1;

			if(interpTicks > 0 && updateCount > interpTicks + 2 && Vector3.Distance(histResults[updateCount % 3].position, histResults[(updateCount - 1) % 3].position) < data.minTeleportationDistance) {
				float lerpTime = (Time.time - startTime) / (Time.fixedDeltaTime * _sendUpdates);

                if(lerpTime >= interpTicks - 1)
					ProcessInterpolation(ref histResults[(updateCount - 1) % 3], ref histResults[updateCount % 3], lerpTime - (interpTicks - 1f));
                else
					ProcessInterpolation(ref histResults[(updateCount - 2) % 3], ref histResults[(updateCount - 1) % 3], lerpTime);
                
			} else
				ProcessTeleportation(ref histResults[updateCount % 3]);

		}
	}
}

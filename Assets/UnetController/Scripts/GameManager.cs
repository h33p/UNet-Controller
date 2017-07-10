using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {

	[System.Serializable]
	public class PlayerData {
		public Controller controller;
		public List<Results> ticks; //currently leaks, unused. Physics calls will not work.
		public uint startTick;
		public uint endTick;
		public bool destroyed;

		public PlayerData (Controller contr, uint sTick) {
			controller = contr;
			startTick = sTick;
			ticks = new List<Results> ();
		}
	}

	[System.Serializable]
	public class ObjectData {
		public RecordableObject component;
		public List<RecordData> ticks;
		public uint goIndex;
		public bool destroyed;
		public uint startTick;
		public uint endTick;

		public ObjectData (RecordableObject comp, uint goI, uint sTick) {
			component = comp;
			goIndex = goI;
			startTick = sTick;
		}
	}

	public class GameManager : MonoBehaviour{

		public const uint DEMO_VERSION = 2;

		public static List<Controller> controllers = new List<Controller> ();
		
		public static List<PlayerData> players = new List<PlayerData> ();

		static Dictionary<int, Controller> playersConnID = new Dictionary<int, Controller> ();

		public static List<ObjectData> objects = new List<ObjectData> ();

		public static uint tick = 0;
		public static int sendUpdates = -1;
		static float sendDiv;
		//public static uint maxTicksSaved = 30;

		static bool recording = false;
		static uint recordStartTick = 0;
		public static bool isRecording { get { return recording; } }
		static string recordName = "";
		static FileStream file;

		public static ControllerDataObject data;

		public static NetworkSettingsObject settings;
		public NetworkSettingsObject networkSettings;

		void Awake () {
			settings = networkSettings;
			sendUpdates = Mathf.Max(1, Mathf.RoundToInt (settings.sendRate / Time.fixedDeltaTime));
			sendDiv = 1f / (float)sendUpdates;
		}

		void Update () {
			if (!Extensions.AlmostEquals(sendUpdates * sendDiv, 1f, 0.01f)) {
				sendUpdates = Mathf.Max (1, Mathf.RoundToInt (settings.sendRate / Time.fixedDeltaTime));
				sendDiv = 1f / (float)sendUpdates;
			}
		}

		void FixedUpdate () {
			for (int i = 0; i < players.Count; i++) {
				if (!players [i].destroyed)
					players [i].controller.Tick ();
			}
		}

		void LateUpdate () {
			for (int i = 0; i < players.Count; i++) {
				if (!players [i].destroyed)
					players [i].controller.PreRender ();
			}
		}

		public static void SetGlobalState (uint setTick) {
			foreach (PlayerData c in players) {
				if (tick >= c.startTick && tick < c.endTick) {
					c.controller.myTransform.position = c.ticks[(int)(tick - c.startTick)].position;
					c.controller.myTransform.rotation = c.ticks[(int)(tick - c.startTick)].rotation;
				}
			}
		}

		public static bool Raycast(uint tick, Transform rootTransform, Vector3 origin, bool rootDirection, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = -5, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
			SetGlobalState (tick);
			if (rootTransform == null)
				return Physics.Raycast (origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
			else if (rootDirection)
				return Physics.Raycast (rootTransform.TransformPoint(origin), rootTransform.TransformDirection(direction), out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
			else
				return Physics.Raycast (rootTransform.TransformPoint(origin), direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
		}

		public static bool Linecast (uint tick, Vector3 start, Vector3 end, out RaycastHit hitInfo, int layerMask = -5, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
			Vector3 direction = end - start;
			return Raycast (tick, null, start, false, direction, out hitInfo, direction.magnitude, layerMask, queryTriggerInteraction);
		}

		public static void StartRecord (string fileName) {
			if (recording)
				return;

			recordName = fileName;
			if (!File.Exists (fileName))
				file = File.Open (recordName, FileMode.Create);
			else
				return;

			recording = true;
			recordStartTick = tick;

			foreach (ObjectData obj in objects) {
				obj.ticks = new List<RecordData> ();
			}
		}

		public static void EndRecord () {
			if (!recording)
				return;

			NetworkWriter writer = new NetworkWriter ();

			writer.WritePackedUInt32 (DEMO_VERSION);

			writer.Write (settings.sendRate);
			writer.WritePackedUInt32 ((uint)objects.Count);

			foreach (ObjectData obj in objects) {
				uint startTick = obj.startTick < recordStartTick ? 0 : obj.startTick - recordStartTick;
				//Debug.Log (startTick);
				//Debug.Log (player.destroyed ? player.endTick - recordStartTick : startTick + (uint)player.ticksRecord.Count);
				writer.WritePackedUInt32 (obj.goIndex);
				writer.WritePackedUInt32 (startTick);
				writer.WritePackedUInt32 (obj.destroyed ? obj.endTick - recordStartTick : startTick + (uint) obj.ticks.Count);
				//Used in the future for object's init properties.
				writer.WritePackedUInt32 (0);
				writer.WritePackedUInt32 ((uint)obj.ticks.Count);
				foreach (RecordData res in obj.ticks) {
					writer.Write (res.bytes.Length);
					writer.Write (res.bytes, res.bytes.Length);
					writer.WritePackedUInt32 (res.timestamp);
				}
				obj.ticks = null;
			}
			recording = false;
			file.Write (writer.ToArray (), 0, writer.ToArray().Length);
			file.Close ();
		}

		public static List<ObjectData> GetRecording (string fileName, ref uint tickCount, ref float tickTime, ref uint version, GameObject playerPrefab) {

			NetworkReader reader = new NetworkReader (File.ReadAllBytes (fileName));

			List<ObjectData> ret = new List<ObjectData> ();

			version = reader.ReadPackedUInt32 ();

			tickTime = reader.ReadSingle ();
			uint cnt = reader.ReadPackedUInt32 ();

			for (int i = 0; i < cnt; i++) {
				uint goIndex = reader.ReadPackedUInt32 ();
				GameObject obj = GameObject.Instantiate (settings.recordGameObjects[(int)goIndex]);

				if (obj.GetComponent<RecordableObject> () != null) {
					obj.GetComponent<RecordableObject> ().SetPlayback ();
					ret.Add (new ObjectData (obj.GetComponent<RecordableObject> (), goIndex, reader.ReadPackedUInt32 ()));
					ret [i].endTick = reader.ReadPackedUInt32 ();
					if (tickCount < ret [i].endTick)
						tickCount = ret [i].endTick;
					//The placeholder for init properties buffer length
					reader.ReadPackedUInt32 ();
					uint cnt2 = reader.ReadPackedUInt32 ();
					ret [i].ticks = new List<RecordData> ();
					for (int o = 0; o < cnt2; o++) {
						int s = reader.ReadInt32 ();
						byte[] bytes = reader.ReadBytes (s);
						uint timestamp = reader.ReadPackedUInt32 ();
						ret [i].ticks.Add (new RecordData (bytes, timestamp));

					}
				} else {
					Debug.LogError ("No RecordableObject component found on: " + obj);
					Destroy (obj);
				}
			}

			return ret;
		}

		public static void PlayerTick (Controller contr, Results res) {
			//some way is needed to clear the unused ticks to prevent leak so disabled completely
			//players [contr.gmIndex].ticks.Add (res);
			if (contr.gmIndex == -1)
				RegisterController (contr);

			tick = tick > players [contr.gmIndex].startTick + res.timestamp ? tick : players [contr.gmIndex].startTick + res.timestamp;
		}

		public static void ObjectTick (RecordableObject obj, RecordData res) {
			if (obj.gmIndex == -1)
				RegisterObject (obj);

			if (recording) {
				if (objects [obj.gmIndex].ticks == null)
					objects [obj.gmIndex].ticks = new List<RecordData> ();
				objects [obj.gmIndex].ticks.Add (res);
			}
		}

		public static void RegisterObject (RecordableObject obj) {
			if (objects == null)
				objects = new List<ObjectData> ();

			if (obj.playbackMode)
				return;

			if (obj.spawnIndex >= 0)
				objects.Add (new ObjectData (obj, (uint)obj.spawnIndex, tick));
			else {
				Debug.LogError ("Spawn Index is not set properly on: " + obj.gameObject);
				return;
			}

			obj.gmIndex = objects.Count - 1;
			if (recording)
				objects [obj.gmIndex].ticks = new List<RecordData> ();
		}

		public static void RegisterController (Controller controller) {
			if (NetworkServer.active && playersConnID == null)
				playersConnID = new Dictionary<int, Controller> ();
			if (NetworkServer.active)
				playersConnID.Add (controller.connectionToClient.connectionId, controller);
			data = controller.data;
			if (players == null)
				players = new List<PlayerData> ();

			if (controller.playbackMode)
				return;

			players.Add (new PlayerData(controller, tick));
			controller.gmIndex = players.Count - 1;
			players [controller.gmIndex].startTick = tick;
		}

		public static void UnregisterController (int connectionId) {
			if (playersConnID != null) {
				Controller temp;
				if (playersConnID.TryGetValue (connectionId, out temp)) {
					players [temp.gmIndex].endTick = tick;
					players [temp.gmIndex].destroyed = true;
				}
				if (NetworkServer.active)
					playersConnID.Remove (connectionId);
			}
		}

		public static void OnSendInputs (NetworkMessage msg) {
			Controller temp;
			if (playersConnID.TryGetValue (msg.conn.connectionId, out temp))
				temp.OnSendInputs (msg);
		}
	}
}

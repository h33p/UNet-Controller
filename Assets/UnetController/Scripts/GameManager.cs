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

		public RecordData restoreData;
		public bool needsRestore = false;

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

	public class GameManager : MonoBehaviour {

		public const uint DEMO_VERSION = 4;

		public static List<Controller> controllers = new List<Controller> ();
		
		public static List<PlayerData> players = new List<PlayerData> ();

		static Dictionary<int, Controller> playersConnID = new Dictionary<int, Controller> ();

		public static List<ObjectData> objects = new List<ObjectData> ();

		public static uint tick = 0;
		public static float curtime = 0f;
		public static float frametime = 0f;
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

		public static GameManager singleton;

		void Awake () {
			singleton = this;
			settings = networkSettings;
			sendUpdates = Mathf.Max(1, Mathf.RoundToInt (settings.sendRate / Time.fixedDeltaTime));
			sendDiv = 1f / (float)sendUpdates;
		}

		void DispatchTicks() {
			bool deleteMode = false;

			if (!NetworkServer.active && !NetworkClient.active)
				deleteMode = true;

			if (!deleteMode) {
				for (int i = 0; i < players.Count; i++)
					if (!players[i].destroyed)
						players[i].controller.Tick();
			} else {
				for (int i = 0; i < players.Count; i++)
					if (!players[i].destroyed)
						GameObject.Destroy(players[i].controller.gameObject);
			}
		}

		void Update () {
			curtime = Time.fixedTime;
			frametime = Time.deltaTime;

			//Can happen on script reload
			if (settings == null)
				settings = networkSettings;

			if (!Extensions.AlmostEquals(sendUpdates * sendDiv, 1f, 0.01f)) {
				sendUpdates = Mathf.Max (1, Mathf.RoundToInt (settings.sendRate / Time.fixedDeltaTime));
				sendDiv = 1f / (float)sendUpdates;
			}

			if (!settings.useFixedUpdate)
				DispatchTicks();

			//TODO: store a local player reference and do without looping the list
			for (int i = 0; i < players.Count; i++) {
				if (!players[i].destroyed)
					players[i].controller.UpdateInputs();
			}
		}

		void FixedUpdate () {
			curtime = Time.fixedTime;

			if (settings == null)
				settings = networkSettings;

			if (settings.useFixedUpdate)
				DispatchTicks();
		}

		void LateUpdate () {
			curtime = Time.time;

			for (int i = 0; i < players.Count; i++) {
				if (!players [i].destroyed)
					players [i].controller.PreRender ();
			}
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

			//tick = tick > players [contr.gmIndex].startTick + res.timestamp ? tick : players [contr.gmIndex].startTick + res.timestamp;
		}

		public static void ObjectTick (RecordableObject obj, RecordData res) {
			if (obj.gmIndex == -1)
				RegisterObject (obj);

			if (objects [obj.gmIndex].ticks == null)
				objects [obj.gmIndex].ticks = new List<RecordData> ();
			objects [obj.gmIndex].ticks.Add (res);

			//If not recording, only keep a set amount of history data
			if (!recording) {
				while (objects[obj.gmIndex].ticks.Count > 0 && (float)(res.timestamp - objects[obj.gmIndex].ticks[0].timestamp) * sendDiv * Time.fixedDeltaTime > settings.lagCompensationAmount)
					objects[obj.gmIndex].ticks.RemoveAt(0);
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
			objects [obj.gmIndex].ticks = new List<RecordData> ();
		}

		public static void UnregisterObject(RecordableObject obj) {
			if (objects == null || obj.gmIndex < 0 || objects.Count <= obj.gmIndex)
				return;

			objects[obj.gmIndex].destroyed = true;
		}

		public static void RegisterController (Controller controller) {

			if (NetworkServer.active && playersConnID == null)
				playersConnID = new Dictionary<int, Controller> ();
			if (NetworkServer.active) {
				playersConnID.Add(controller.connectionToClient.connectionId, controller);
				controller.cachedID = controller.connectionToClient.connectionId;
			}
			data = controller.data;
			if (players == null)
				players = new List<PlayerData> ();

			if (controller.playbackMode)
				return;

			//Disallow duplicates, should probably throw an error
			if (controller.gmIndex != -1 && players.Count > controller.gmIndex && players[controller.gmIndex].controller == controller)
				return;

			players.Add (new PlayerData(controller, tick));
			controller.gmIndex = players.Count - 1;
			players [controller.gmIndex].startTick = tick;

			Debug.Assert(controller == players[controller.gmIndex].controller, "Controllers don't match!");
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
				else
					playersConnID.Clear();
			}
		}

		public static void UnregisterController(Controller controller) {

			if (controller.gmIndex == -1)
				return;

			players[controller.gmIndex].endTick = tick;
			players[controller.gmIndex].destroyed = true;

			if (playersConnID != null) {
				if (NetworkServer.active)
					playersConnID.Remove(controller.cachedID);
				else
					playersConnID.Clear();
			}
		}

		public static void OnSendInputs (NetworkMessage msg) {
			Controller temp;
			if (playersConnID.TryGetValue (msg.conn.connectionId, out temp))
				temp.OnSendInputs (msg);
		}
	}
}

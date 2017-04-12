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
		public List<Results> ticksRecord;

		public PlayerData (Controller contr, uint sTick) {
			controller = contr;
			startTick = sTick;
			ticks = new List<Results> ();
		}
	}

	[System.Serializable]
	public class ObjectData {
		public RecordableObject component;
		public List<SmallResults> ticks;
		public uint startTick;
		public uint endTick;

		public ObjectData (RecordableObject comp, uint sTick) {
			component = comp;
			startTick = sTick;
		}
	}

	public class GameManager : MonoBehaviour{

		public static List<Controller> controllers = new List<Controller> ();
		
		public static List<PlayerData> players = new List<PlayerData> ();

		public static uint tick = 0;
		//public static uint maxTicksSaved = 30;

		static bool recording = false;
		static uint recordStartTick = 0;
		public static bool isRecording { get { return recording; } }
		static string recordName = "";
		static FileStream file;

		public static ControllerDataObject data;

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

			foreach (PlayerData player in players) {
				player.ticksRecord = new List<Results> ();
			}
		}

		public static void EndRecord () {
			if (!recording)
				return;

			NetworkWriter writer = new NetworkWriter ();

			//Fix this thing
			if (players.Count > 0)
				writer.Write (data.sendRate);
			else
				writer.Write (0.1f);

			writer.WritePackedUInt32 ((uint)players.Count);

			foreach (PlayerData player in players) {
				player.startTick = player.startTick < recordStartTick ? 0 : player.startTick - recordStartTick;
				writer.WritePackedUInt32 (player.startTick);
				writer.WritePackedUInt32 (player.destroyed ? player.endTick - recordStartTick : player.startTick + (uint) player.ticksRecord.Count);
				writer.WritePackedUInt32 ((uint)player.ticksRecord.Count);
				foreach (Results res in player.ticksRecord) {
					writer.Write(res.position);
					writer.Write(res.rotation);
					writer.Write(res.groundNormal);
					writer.Write(res.camX);
					writer.Write(res.speed);
					writer.Write(res.isGrounded);
					writer.Write(res.jumped);
					writer.Write(res.crouch);
					writer.Write(res.groundPoint);
					writer.Write(res.groundPointTime);
					writer.Write(res.aiTarget);
					writer.Write(res.aiEnabled);
					writer.Write(res.controlledOutside);
					writer.WritePackedUInt32(res.timestamp);
				}
				player.ticksRecord = null;
			}
			recording = false;
			file.Write (writer.ToArray (), 0, writer.ToArray().Length);
			file.Close ();
		}

		public static List<PlayerData> GetRecording (string fileName, ref uint tickCount, ref float tickTime, GameObject playerPrefab) {

			NetworkReader reader = new NetworkReader (File.ReadAllBytes (fileName));

			List<PlayerData> ret = new List<PlayerData> ();

			tickTime = reader.ReadSingle ();
			uint cnt = reader.ReadPackedUInt32 ();

			for (int i = 0; i < cnt; i++) {
				GameObject obj = GameObject.Instantiate (playerPrefab);
				if (obj.GetComponent<Controller> () == null)
					obj.AddComponent <Controller> ();
				obj.GetComponent<Controller> ().playbackMode = true;
				ret.Add (new PlayerData (obj.GetComponent<Controller> (), reader.ReadPackedUInt32 ()));
				ret [i].endTick = reader.ReadPackedUInt32 ();
				if (tickCount < ret [i].endTick)
					tickCount = ret [i].endTick;
				uint cnt2 = reader.ReadPackedUInt32 ();
				ret [i].ticksRecord = new List<Results> ();
				for (int o = 0; o < cnt2; o++)
					ret [i].ticksRecord.Add (new Results (reader.ReadVector3 (), reader.ReadQuaternion (), reader.ReadVector3 (), reader.ReadSingle (),
						reader.ReadVector3 (), reader.ReadBoolean (), reader.ReadBoolean (), reader.ReadBoolean (), reader.ReadSingle (),
						reader.ReadSingle (), reader.ReadVector3 (), reader.ReadBoolean (), reader.ReadBoolean (), reader.ReadPackedUInt32 ()));
			}

			return ret;
		}

		public static void PlayerTick (Controller contr, Results res) {
			//some way is needed to clear the unused ticks to prevent leak so disabled completely
			//players [contr.gmIndex].ticks.Add (res);
			if (contr.gmIndex == -1)
				RegisterController (contr);

			tick = tick > players [contr.gmIndex].startTick + res.timestamp ? tick : players [contr.gmIndex].startTick + res.timestamp;
			if (recording) {
				if (players [contr.gmIndex].ticksRecord == null)
					players [contr.gmIndex].ticksRecord = new List<Results> ();
				players [contr.gmIndex].ticksRecord.Add (res);
			}
		}

		public static void RegisterController (Controller controller) {
			data = controller.data;
			if (players == null)
				players = new List<PlayerData> ();

			if (controller.playbackMode)
				return;

			players.Add (new PlayerData(controller, tick));
			controller.gmIndex = players.Count - 1;
			players [controller.gmIndex].startTick = tick;
			if (recording)
				players [controller.gmIndex].ticksRecord = new List<Results> ();
		}

		public static void UnregisterController (Controller controller) {
			players [controller.gmIndex].endTick = tick;
			players [controller.gmIndex].destroyed = true;
		}
	}
}

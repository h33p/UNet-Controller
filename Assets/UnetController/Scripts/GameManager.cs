using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	public class PlayerData {
		public Controller controller;
		public List<Results> ticks;
		public uint startTick;
		public uint endTick;
		public List<Results> ticksRecord;

		public PlayerData (Controller contr, uint sTick) {
			controller = contr;
			startTick = sTick;
		}
	}

	public class GameManager : MonoBehaviour{

		public static List<Controller> controllers = new List<Controller> ();
		
		public static List<PlayerData> players = new List<PlayerData> ();

		public static uint tick;

		static bool recording = false;
		static string recordName = "";

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

		public static void StartRecord () {
			if (recording)
				return;
			
			foreach (PlayerData player in players) {
				player.ticksRecord = new List<Results> ();
			}
		}

		public static void EndRecord () {
			if (!recording)
				return;

			foreach (PlayerData player in players) {
				player.ticksRecord = null;
			}
		}

		public static void PlayerTick (Controller contr, Results res, Inputs inp) {
			if (recording)
				players [contr.gmIndex].ticks.Add (res);
		}

		public static void RegisterController (Controller controller) {
			if (players == null)
				players = new List<PlayerData> ();
			
			players.Add (new PlayerData(controller, tick));
			controller.gmIndex = players.Count - 1;
		}

		public static void UnregisterController (Controller controller) {
				players[controller.gmIndex].endTick = tick;
		}
	}
}

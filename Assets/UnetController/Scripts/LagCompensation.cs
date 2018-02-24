using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public class LagCompensation : MonoBehaviour {

		private static bool _isDoingCompensation = false;
		public static bool isDoingCompensation {
			get {
				return _isDoingCompensation;
			}
		}

		private static Controller _currentPlayer;
		public static Controller currentPlayer {
			get {
				return _currentPlayer;
			}
		}

		public static void BacktrackObject(ObjectData obj, uint tick) {
			if (tick == 0 && obj.needsRestore) {
				obj.needsRestore = false;
				obj.component.PlayTick(obj.restoreData, obj.restoreData, GameManager.sendUpdates, -1f, GameManager.DEMO_VERSION);
			} else if (tick == 0)
				return;
			obj.needsRestore = true;

			if (obj.component.recordInterface != null)
				obj.component.recordInterface.Tick(ref obj.restoreData);

			int sz = obj.ticks.Count;

			RecordData restoreData = new RecordData();
			bool foundClosest = false;
			uint closestTickDiff = 99999999;

			for (int i = sz - 1; i >= 0; i--) {
				RecordData curData = obj.ticks[i];
				uint tDiff = 0;
				if (curData.timestamp > tick)
					tDiff = curData.timestamp - tick;
				else
					tDiff = tick - curData.timestamp;

				if (tDiff < closestTickDiff) {
					closestTickDiff = tDiff;
					restoreData = curData;
					foundClosest = true;
				} else
					break;
			}

			//Debug.Log(closestTickDiff);

			Debug.Assert(foundClosest || obj.ticks.Count == 0, "Supposed to be able to find closest tick, but were not.");

			obj.component.PlayTick(restoreData, restoreData, GameManager.sendUpdates, -1f, GameManager.DEMO_VERSION);
		}

		public static void StartLagCompensation(PlayerData pl, ref Inputs cmd) {
			Debug.Assert(!_isDoingCompensation, "StartLagCompensation called during lag compensation!");
			_isDoingCompensation = true;
			_currentPlayer = pl.controller;

			uint targetTick = cmd.servertick;

			//Proper handling needs to be done, but, if the player has interpolation on, then we need to move everything back a bit more, due to the interpolation
			if (targetTick > 0 && pl.controller.data.movementType != MoveType.UpdateOnce)
				targetTick--;

			foreach (ObjectData obj in GameManager.objects)
				if (!obj.destroyed && obj.component.lagCompensate && obj.component.gameObject != pl.controller.gameObject)
					BacktrackObject(obj, targetTick);
		}

		public static void EndLagCompensation(PlayerData pl) {

			_isDoingCompensation = false;
			_currentPlayer = null;

			foreach (ObjectData obj in GameManager.objects)
				if (!obj.destroyed && obj.component.lagCompensate && obj.component.gameObject != pl.controller.gameObject)
					BacktrackObject(obj, 0);
		}

		public static void DebugLagCompensationSpawn(GameObject gobj) {
			foreach (ObjectData obj in GameManager.objects)
				if (!obj.destroyed && obj.component.lagCompensate)
					Destroy(GameObject.Instantiate(gobj, obj.component.transform.position, obj.component.transform.rotation), 5);
		}
	}
}

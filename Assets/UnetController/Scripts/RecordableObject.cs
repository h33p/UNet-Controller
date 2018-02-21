using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {

	//A structure which holds all the information, interfaces are trusted to handle it correctly.
	[System.Serializable]
	public struct RecordData
	{
		public byte[] bytes;
		public uint timestamp;

		public RecordData (byte[] byteArr, uint tick) {
			bytes = byteArr;
			timestamp = tick;
		}
	}

	//An interface for a single tick data handling.
	public interface IRecordHandler {
		void SetData (RecordData dataStart, RecordData dataEnd, int sendUpdates, float playbackSpeed, uint version);
		void Tick (ref RecordData results);
		void Init ();
	}

	public class RecordableObject : MonoBehaviour {

		public bool lagCompensate = true;

		public bool playbackMode = false;
		public float playbackSpeed = 1f;

		public bool recordCountHook = false;

		private int curSendUpdates;
		public int gmIndex;

		public bool interpolateSingle = true;

		private float startTime;

		private RecordData data;

		public int startTick;
		public int endTick;

		public int spawnIndex = -1;

		public MonoBehaviour recordInterfaceClass;
		private IRecordHandler _recordInterface;

		public IRecordHandler recordInterface {
			get {
				if (_recordInterface == null && recordInterfaceClass != null)
					_recordInterface = recordInterfaceClass as IRecordHandler;
				if (_recordInterface == null)
					recordInterfaceClass = null;
				return _recordInterface;
			}
		}

		public void PlayTick (RecordData startRes, RecordData endRes, int sendUpdates, float speed, uint version) {
			if (recordInterface != null)
				recordInterface.SetData (startRes, endRes, sendUpdates, speed, version);
		}

		public void SetPlayback () {
			playbackMode = true;
			if (recordInterface != null)
				recordInterface.Init ();
		}

		void Start () {
			if (!playbackMode)
				GameManager.RegisterObject (this);
		}

		void OnDestroy () {
			if (!playbackMode)
				GameManager.UnregisterObject(this);
		}

		public void RecordCountHook (ref TickUpdateNotifyDelegate hook) {
			recordCountHook = true;
			hook += this.Tick;
		}

		public void Tick (bool inLagCompensation) {

			if (inLagCompensation)
				return;

			//If not playing back the recording tell the interface to prepare the data and then GameManager to do it's job.
			if (!playbackMode && (GameManager.isRecording || (lagCompensate && SyncedGlobals.sIsServer))) {
				//data.position = transform.position;
				//data.rotation = transform.rotation;

				if (recordInterface != null)
					recordInterface.Tick (ref data);

				GameManager.ObjectTick (this, data);
				data.timestamp = data.timestamp + 1;
			}
		}

		void FixedUpdate () {
			if (GameManager.sendUpdates == -1)
				return;

			if (!recordCountHook) {
				curSendUpdates++;
				if (curSendUpdates >= GameManager.sendUpdates) {
					curSendUpdates = 0;
					Tick (false);
				}
			}
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {
	//The base implementation for handling the recorded data. Only handles position and rotation. Inherit from this to add extra functionality.
	public class BaseRecordingHandler : MonoBehaviour, IRecordHandler {

		//Used in interpolation
		protected float playbackSpeed = 1f;
		protected float targetTime = 0.1f;
		protected float targetTimeMul;
		protected float startTime = 0f;
		protected int sendUpdates = 0;

		//Should we play back the ticks
		protected bool playBack;

		//Starting data
		private Vector3 posStart;
		private Vector3 posEnd;

		//Ending data
		private Quaternion rotStart;
		private Quaternion rotEnd;

		//Network readers and a writer to be re-used in the inherited classes
		protected NetworkReader readerStart;
		protected NetworkReader readerEnd;
		protected NetworkWriter writer;

		//Interpolation part if playing back
		protected virtual void LateUpdate () {
			if (playBack) {
				transform.position = Vector3.Lerp (posStart, posEnd, (Time.time - startTime) * targetTimeMul);
				transform.rotation = Quaternion.Lerp (rotStart, rotEnd, (Time.time - startTime) * targetTimeMul);
			}
		}

		//Set things up
		public virtual void Init () {

		}

		//Read the start and end bytes to positions and rotations
		public virtual void SetData (RecordData dataStart, RecordData dataEnd, int sUpdates, float tTime, uint version) {
			readerStart = new NetworkReader (dataStart.bytes);
			posStart = readerStart.ReadVector3 ();
			rotStart = readerStart.ReadQuaternion ();

			readerEnd = new NetworkReader (dataEnd.bytes);
			posEnd = readerEnd.ReadVector3 ();
			rotEnd = readerEnd.ReadQuaternion ();

			if (tTime == -1f) {
				transform.position = posEnd;
				transform.rotation = rotEnd;
			} else {
				playBack = true;
				sendUpdates = sUpdates;
				targetTime = Time.fixedDeltaTime * sendUpdates;
				targetTimeMul = 1f / targetTime;
				startTime = Time.time;
				playbackSpeed = tTime;
			}
		}

		//Write the position and rotation to the data structure
		public virtual void Tick (ref RecordData data) {
			writer = new NetworkWriter ();
			writer.Write (transform.position);
			writer.Write (transform.rotation);
			data.bytes = writer.AsArray ();
		}

	}
}
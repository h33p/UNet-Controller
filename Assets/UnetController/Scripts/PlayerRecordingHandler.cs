using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {
	public class PlayerRecordingHandler : BaseRecordingHandler {

		Results resStart;
		Results resEnd;

		public Controller controller;

		protected override void LateUpdate () {
			base.LateUpdate ();
		}

		public override void Init () {
			controller.playbackMode = true;
		}

		public override void SetData (RecordData dataStart, RecordData dataEnd, int sUpdates, float tTime) {
			base.SetData (dataStart, dataEnd, sUpdates, tTime);

			resStart = resEnd;
			resEnd = new Results (transform.position, transform.rotation, new Vector3(0,0,0), readerEnd.ReadSingle (), readerEnd.ReadVector3 (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), 0f, 0f, new Vector3 (0,0,0), true, false, readerEnd.ReadPackedUInt32 ());

			controller.PlaybackSetResults (resStart, resEnd, sendUpdates, playbackSpeed);

		}

		public override void Tick (ref RecordData data) {
			base.Tick (ref data);

			Results temp = controller.GetResults ();
			writer.Write(temp.camX);
			writer.Write(temp.speed);
			writer.Write(temp.isGrounded);
			writer.Write(temp.jumped);
			writer.Write(temp.crouch);
			writer.WritePackedUInt32(temp.timestamp);
			data.bytes = writer.AsArray ();
		}

	}
}
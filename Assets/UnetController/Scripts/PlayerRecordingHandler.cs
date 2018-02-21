using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {
	public class PlayerRecordingHandler : BaseRecordingHandler {

		Results resStart;
		Results resEnd;

		public Controller controller;
		public RagdollManager ragdollManager;
		Vector3[] bonePositions;
		Quaternion[] boneRotations;

		protected override void LateUpdate () {
			base.LateUpdate ();
		}

		public override void Init () {
			controller.playbackMode = true;
		}

		public override void SetData (RecordData dataStart, RecordData dataEnd, int sUpdates, float tTime, uint version) {
			base.SetData (dataStart, dataEnd, sUpdates, tTime, version);

			resStart = resEnd;
			switch (version) {
			case 1:
				resEnd = new Results (transform.position, transform.rotation, new Vector3 (0, 0, 0), readerEnd.ReadSingle (), readerEnd.ReadVector3 (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), 0f, 0f, new Vector3 (0, 0, 0), true, false, false, 0, readerEnd.ReadPackedUInt32 ());
				break;
			default:
				resEnd = new Results (transform.position, transform.rotation, new Vector3 (0, 0, 0), readerEnd.ReadSingle (), readerEnd.ReadVector3 (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), readerEnd.ReadBoolean (), 0f, 0f, new Vector3 (0, 0, 0), true, false, readerEnd.ReadBoolean (), 0, readerEnd.ReadPackedUInt32 ());
				if (resEnd.ragdoll) {
					uint bL = readerEnd.ReadPackedUInt32 ();
					bonePositions = new Vector3 [bL];
					boneRotations = new Quaternion [bL];
					for (uint i = 0; i < bL; i++) {
						bonePositions [i] = readerEnd.ReadVector3 ();
						boneRotations [i] = readerEnd.ReadQuaternion ();
					}
					ragdollManager.SetTargetBoneTransforms (bonePositions, boneRotations);
					//if (tTime == -1f)
					//	ragdollManager.UpdateRagdoll();
				}
				break;
			}

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
			writer.Write(temp.ragdoll);
			writer.WritePackedUInt32(temp.timestamp);

			if (temp.ragdoll) {
				ragdollManager.GetBoneTransforms (ref bonePositions, ref boneRotations);
				writer.WritePackedUInt32 ((uint)bonePositions.Length);
				for (int i = 0; i < bonePositions.Length; i++) {
					writer.Write (bonePositions [i]);
					writer.Write (boneRotations [i]);
				}
			}

			data.bytes = writer.AsArray ();
		}

	}
}
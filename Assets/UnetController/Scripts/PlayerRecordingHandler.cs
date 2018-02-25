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

		//This mask describes how we what data we are going to save, it is everything, but some empty bits 0-10 bit range (on bits are on camX, speed, flags, timestamp)
		const uint bMaskV3 = 0xFFFFFC39;
		const uint bMaskV4 = 0xFFFFFE39;

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
				resEnd = new Results (transform.position, transform.rotation, new Vector3 (0, 0, 0), readerEnd.ReadSingle (), readerEnd.ReadVector3 (), (readerEnd.ReadBoolean() ? Flags.IS_GROUNDED : 0) | (readerEnd.ReadBoolean() ? Flags.JUMPED : 0) | (readerEnd.ReadBoolean() ? Flags.CROUCHED : 0) | Flags.AI_ENABLED, 0f, 0f, 0, readerEnd.ReadPackedUInt32 ());
				break;
			case 2:
				resEnd = new Results(transform.position, transform.rotation, new Vector3(0, 0, 0), readerEnd.ReadSingle(), readerEnd.ReadVector3(), (readerEnd.ReadBoolean() ? Flags.IS_GROUNDED : 0) | (readerEnd.ReadBoolean() ? Flags.JUMPED : 0) | (readerEnd.ReadBoolean() ? Flags.CROUCHED : 0) | (readerEnd.ReadBoolean() ? Flags.RAGDOLL : 0) | Flags.AI_ENABLED, 0f, 0f, 0, readerEnd.ReadPackedUInt32 ());
				if (resEnd.flags & Flags.RAGDOLL) {
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
			default:
				resEnd = new Results(transform.position, transform.rotation, new Vector3(0, 0, 0), 0f, new Vector3(0, 0, 0), 0u, 0f, 0f, 0, 0u);

				controller.ReadResults(readerEnd, ref resEnd, version == 3 ? bMaskV3 : bMaskV4);

				if (controller.readVarValues != null) controller.readVarValues(readerEnd, 0, true, true);

				if (resEnd.flags & Flags.RAGDOLL) {
					uint bL = readerEnd.ReadPackedUInt32();
					bonePositions = new Vector3[bL];
					boneRotations = new Quaternion[bL];
					for (uint i = 0; i < bL; i++) {
						bonePositions[i] = readerEnd.ReadVector3();
						boneRotations[i] = readerEnd.ReadQuaternion();
					}
					ragdollManager.SetTargetBoneTransforms(bonePositions, boneRotations);
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

			controller.WriteResults(writer, ref temp, bMaskV4);
			if (controller.writeVarValues != null) controller.writeVarValues(writer, true);

			if (temp.flags & Flags.RAGDOLL) {
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
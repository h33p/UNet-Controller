using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	public struct RigidbodyRecord {
		float angularDrag;
		Vector3 angularVelocity;
		Vector3 inertiaTensor;
		Quaternion inertiaTensorRotation;
		bool isKinematic;
		float mass;
		float maxAngularVelocity;
		float maxDepenetrationVelocity;
		Vector3 velocity;

		public void UpdateRecord(Rigidbody rb) {
			angularDrag = rb.angularDrag;
			angularVelocity = rb.angularVelocity;
			inertiaTensor = rb.inertiaTensor;
			inertiaTensorRotation = rb.inertiaTensorRotation;
			isKinematic = rb.isKinematic;
			mass = rb.mass;
			maxAngularVelocity = rb.maxAngularVelocity;
			velocity = rb.velocity;
		}

		public void ApplyToRigidbody(Rigidbody rb) {
			rb.angularDrag = angularDrag;
			rb.angularVelocity = angularVelocity;
			rb.inertiaTensor = inertiaTensor;
			rb.inertiaTensorRotation = inertiaTensorRotation;
			rb.isKinematic = isKinematic;
			rb.mass = mass;
			rb.maxAngularVelocity = maxAngularVelocity;
			rb.velocity = velocity;
		}
	}

	public class RigidbodyRollback : MonoBehaviour {

		new public Rigidbody rigidbody;

		private RigidbodyRecord[] records = new RigidbodyRecord[200];
		private int recordCount = -1;
		private RigidbodyRecord record;

		public void FixedTick(int tick) {
			if (recordCount < 0)
				recordCount = records.Length;
			records[tick % recordCount].UpdateRecord(rigidbody);
		}

		public void RollbackTo(int tick) {
			if (recordCount < 0)
				recordCount = records.Length;
			record.UpdateRecord(rigidbody);
			records[tick % recordCount].ApplyToRigidbody(rigidbody);
		}

		public void RestoreRollback() {
			record.ApplyToRigidbody(rigidbody);
		}
	}
}
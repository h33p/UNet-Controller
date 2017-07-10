using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	[System.Serializable]
	public struct RagdollBone {
		public Transform transform;
		public Rigidbody rigidbody;
		public Vector3 savedPosition;
		public Quaternion savedRotation;
		public Vector3 playbackTargetPos;
		public Quaternion playbackTargetRot;
	}

	public class RagdollManager : MonoBehaviour {

		bool _enabled;
		public bool ragdollEnabled {
			get {
				return _enabled;
			}
		}

		public float disableTime = 1f;
		float disableStart;
		float playbackStartTime = 0f;

		RagdollBone[] bones;

		public Transform rootTransformer;
		public Rigidbody rootRigidbody;

		public Animator anim;
		public Controller controller;

		private bool added = false;

		//Gets all bones used with a rigidbody
		void GetRagdollBones(bool setKinematicsTrue) {
			Rigidbody[] rigids = GetComponentsInChildren<Rigidbody> ();
			bones = new RagdollBone[rigids.Length];
			for (int i = 0; i < bones.Length; i++) {
				bones [i].transform = rigids [i].transform;
				bones [i].rigidbody = rigids [i];
				if (setKinematicsTrue)
					bones [i].rigidbody.isKinematic = true;
			}
		}

		void Awake () {
			GetRagdollBones (true);
		}

		void Start () {
			if (!added) {
				controller.tickUpdate += this.TickUpdate;
				added = true;
			}
		}

		void OnEnable () {
			if (!added) {
				controller.tickUpdate += this.TickUpdate;
				added = true;
			}
		}

		void OnDisable () {
			if (added) {
				controller.tickUpdate -= this.TickUpdate;
				added = false;
			}
		}

		void Update () {
			//if (Input.GetKeyDown (KeyCode.F))
			//	SetRagdoll (!ragdollEnabled);
		}

		void LateUpdate () {
			//Interpolates from the ragdoll state to the animation state
			if (!ragdollEnabled && Time.time - disableStart < disableTime) {
				for (int i = 0; i < bones.Length; i++) {
					bones [i].transform.localPosition = Vector3.Lerp (bones [i].savedPosition, bones [i].transform.localPosition, (Time.time - disableStart) / disableTime);
					bones [i].transform.localRotation = Quaternion.Lerp (bones [i].savedRotation, bones [i].transform.localRotation, (Time.time - disableStart) / disableTime);
				}
			} else if (ragdollEnabled) {
				if (!controller.playbackMode) {
					Vector3 rtPos = rootTransformer.position;
					Quaternion rtRot = rootTransformer.rotation;
					transform.position = rtPos;
					rootTransformer.position = rtPos;
					rootTransformer.rotation = rtRot;
				} else {
					for (int i = 0; i < bones.Length; i++) {
						bones [i].transform.localPosition = Vector3.Lerp (bones [i].savedPosition, bones [i].playbackTargetPos, RecordingManager.singleton.lerpTime);
						bones [i].transform.localRotation = Quaternion.Lerp (bones [i].savedRotation, bones [i].playbackTargetRot, RecordingManager.singleton.lerpTime);
					}
				}
			}
		}

		public void SetTargetBoneTransforms(Vector3[] positions, Quaternion[] rotations) {
			for (int i = 0; i < bones.Length; i++) {
				bones [i].savedPosition = bones [i].playbackTargetPos;
				bones [i].playbackTargetPos = positions [i];
				bones [i].savedRotation = bones [i].playbackTargetRot;
				bones [i].playbackTargetRot = rotations [i];
			}
			playbackStartTime = Time.time;
		}

		public void GetBoneTransforms (ref Vector3[] positions, ref Quaternion[] rotations) {
			if (positions == null || positions.Length != bones.Length)
				positions = new Vector3 [bones.Length];
			if (rotations == null || rotations.Length != bones.Length)
				rotations = new Quaternion [bones.Length];

			for (int i = 0; i < bones.Length; i++) {
				positions [i] = bones [i].transform.localPosition;
				rotations [i] = bones [i].transform.localRotation;
			}
		}

		public void SetRagdoll(bool enable) {
			if (enable) {
				if (!controller.playbackMode) {
					for (int i = 0; i < bones.Length; i++) {
						bones [i].rigidbody.isKinematic = false;
					}
				} else {
					for (int i = 0; i < bones.Length; i++) {
						bones [i].savedPosition = bones [i].transform.localPosition;
						bones [i].savedRotation = bones [i].transform.localRotation;
						bones [i].rigidbody.isKinematic = true;
					}
				}
				anim.enabled = false;
				controller.SetRagdoll (true);
				rootRigidbody.velocity = controller.controller.velocity;
				rootRigidbody.angularVelocity = new Vector3 (0, 0, 0);
			} else {
				disableStart = Time.time;
				for (int i = 0; i < bones.Length; i++) {
					bones [i].savedPosition = bones [i].transform.localPosition;
					bones [i].savedRotation = bones [i].transform.localRotation;
					bones [i].rigidbody.isKinematic = true;
				}
				anim.enabled = true;
				controller.SetRagdoll (false);
				controller.SetVelocity (rootRigidbody.velocity);
				anim.SetTrigger ("ExitRagdoll");
			}
			_enabled = enable;
		}

		public void TickUpdate (Results res) {
			if (res.ragdoll && !_enabled)
				SetRagdoll (true);
			if (!res.ragdoll && _enabled)
				SetRagdoll (false);
		}
	}

}

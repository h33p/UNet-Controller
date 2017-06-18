#define DEBUG_DRAW

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public class IKHandler : MonoBehaviour {

		public Animator anim;
		public AnimationManager manager;

		private Transform _transform;
		public Transform myTransform {
			get {
				if (_transform == null)
					_transform = transform;
				return _transform;
			}
		}

		public bool legIKActive = true;

		public string leftZName = "IKLeftZ";
		public string leftXName = "IKLeftX";
		public string rightZName = "IKRightZ";
		public string rightXName = "IKRightX";

		public Transform leftFootOverride;
		public Transform rightFootOverride;

		public bool handIKActive = true;

		public string leftArmedName = "LeftArmed";
		public string rightArmedName = "RightArmed";

		public LayerMask raycastMask;

		public Transform leftHandOverride;
		public Transform rightHandOverride;

		public float lHWeight;
		public float rHWeight;

		public bool leftHForce = false;
		public bool rightHForce = false;

		Transform leftFoot;
		Transform rightFoot;

		public Vector3 leftFootStart;
		Vector3 leftCurStart;
		Vector3 leftPos;
		Quaternion leftRot;

		public Vector3 rightFootStart;
		Vector3 rightCurStart;
		Vector3 rightPos;
		Quaternion rightRot;

		float leftTargetHeight;
		float leftCurrentHeight;
		float leftLastHeight;
		float leftTime;
		float leftStepTime;
		float leftHeightDelta;

		Quaternion leftRotTarget;
		Quaternion leftRotCur;
		Quaternion leftRotLast;

		float rightTargetHeight;
		float rightCurrentHeight;
		float rightLastHeight;
		float rightTime;
		float rightStepTime;
		float rightHeightDelta;

		Quaternion rightRotTarget;
		Quaternion rightRotCur;
		Quaternion rightRotLast;

		void Start () {
			leftFoot = anim.GetBoneTransform (HumanBodyBones.LeftFoot);
			rightFoot = anim.GetBoneTransform (HumanBodyBones.RightFoot);
		}

		float GetHeight (Vector3 startPos, Vector3 speed, float stepTime, float stepSize, ref Quaternion normalRotation) {
			Vector3 posExtrapolate = startPos + myTransform.TransformDirection(speed) * stepTime + new Vector3(0, 0.4f, 0);
			#if (DEBUG_DRAW)
			Debug.DrawLine (posExtrapolate, posExtrapolate + new Vector3 (0, 0.3f, 0), Color.yellow, stepTime);
			#endif

			RaycastHit hit;
			if (Physics.Raycast (posExtrapolate, new Vector3 (0, -1, 0), out hit, 1f, raycastMask)) {
				#if (DEBUG_DRAW)
				Debug.DrawLine (hit.point, hit.point + new Vector3 (0, 0.3f, 0), Color.red, stepTime);
				#endif
				normalRotation = Quaternion.FromToRotation (myTransform.up, hit.normal) * transform.rotation;
				return hit.point.y;
			}

			normalRotation = Quaternion.FromToRotation (transform.up, new Vector3(0, 1, 0)) * myTransform.rotation;
			return startPos.y;
		}

		public void StepLeft () {
			leftLastHeight = leftTargetHeight;
			leftStepTime = anim.GetFloat ("StepTime");
			leftTime = Time.time;
			#if (DEBUG_DRAW)
			Debug.DrawLine (transform.TransformPoint (leftFootStart), transform.TransformPoint (leftFootStart) + new Vector3 (0, 0, 0.3f), Color.blue, leftStepTime);
			#endif
			leftRotLast = leftRotTarget;
			leftTargetHeight = GetHeight (transform.TransformPoint (leftFootStart), manager.curSpeed, leftStepTime, anim.GetFloat ("Step"), ref leftRotTarget);
		}

		public void StepRight () {
			rightLastHeight = rightTargetHeight;
			rightStepTime = anim.GetFloat ("StepTime");
			rightTime = Time.time;
			#if (DEBUG_DRAW)
			Debug.DrawLine (transform.TransformPoint (rightFootStart), transform.TransformPoint (rightFootStart) + new Vector3 (0, 0, 0.3f), Color.blue, rightStepTime);
			#endif
			rightRotLast = rightRotTarget;
			rightTargetHeight = GetHeight (transform.TransformPoint (rightFootStart), manager.curSpeed, rightStepTime, anim.GetFloat ("Step"), ref rightRotTarget);
		}

		void OnAnimatorIK () {
			#region FOOT_IK
			if (legIKActive) {
				if (leftFootOverride == null) {
					leftCurStart = myTransform.TransformPoint (leftFootStart);
					if (leftStepTime > 0.01f) {
						leftCurrentHeight = Mathf.Lerp (leftLastHeight, leftTargetHeight, (Time.time - leftTime) / leftStepTime);
						leftRotCur = Quaternion.Lerp (leftRotLast, leftRotTarget, (Time.time - leftTime) / leftStepTime);
					} else {
						leftCurrentHeight = leftTargetHeight;
						leftRotCur = leftRotTarget;
					}
					leftHeightDelta = leftCurStart.y - leftCurrentHeight;
					leftPos = Vector3.Lerp (leftFoot.position + new Vector3 (0, leftHeightDelta, 0), leftPos, anim.GetFloat ("LeftStick"));
					leftRot = Quaternion.Lerp (leftFoot.rotation, leftRotCur, anim.GetFloat ("LeftStick"));
				} else {
					leftPos = leftFootOverride.position;
					leftRot = leftFootOverride.rotation;
				}
				anim.SetIKPosition (AvatarIKGoal.LeftFoot, leftPos);
				anim.SetIKPositionWeight (AvatarIKGoal.LeftFoot, 1f);
				anim.SetIKRotation (AvatarIKGoal.LeftFoot, leftRot);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftFoot, 0f);


				if (rightFootOverride == null) {
					rightCurStart = myTransform.TransformPoint (rightFootStart);
					if (rightStepTime > 0.01f) {
						rightCurrentHeight = Mathf.Lerp (rightLastHeight, rightTargetHeight, (Time.time - rightTime) / rightStepTime);
						rightRotCur = Quaternion.Lerp (rightRotLast, rightRotTarget, (Time.time - rightTime) / rightStepTime);
					} else {
						rightCurrentHeight = rightTargetHeight;
						rightRotCur = rightRotTarget;
					}
					rightHeightDelta = rightCurStart.y - rightCurrentHeight;
					rightPos = Vector3.Lerp (rightFoot.position + new Vector3 (0, rightHeightDelta, 0), rightPos, anim.GetFloat ("RightStick"));
					rightRot = Quaternion.Lerp (rightFoot.rotation, rightRotCur, anim.GetFloat ("RightStick"));
				} else {
					rightPos = rightFootOverride.position;
					rightRot = rightFootOverride.rotation;
				}
				anim.SetIKPosition (AvatarIKGoal.RightFoot, rightPos);
				anim.SetIKPositionWeight (AvatarIKGoal.RightFoot, 1f);
				anim.SetIKRotation (AvatarIKGoal.RightFoot, rightRot);
				anim.SetIKRotationWeight (AvatarIKGoal.RightFoot, 0f);

			} else {
				anim.SetIKPositionWeight (AvatarIKGoal.LeftFoot, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftFoot, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.RightFoot, 0f);
				anim.SetIKPositionWeight (AvatarIKGoal.RightFoot, 0f);
			}
			#endregion

			#region HAND_IK
			if (handIKActive) {

			} else {
				anim.SetIKPositionWeight (AvatarIKGoal.LeftHand, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftHand, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.RightHand, 0f);
				anim.SetIKPositionWeight (AvatarIKGoal.RightHand, 0f);
			}
			#endregion

		}
	}
}
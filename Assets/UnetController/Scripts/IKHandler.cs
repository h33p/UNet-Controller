//#define DEBUG_DRAW

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

		#region FOOT_IK_VARS
		public bool legIKActive = true;
		public bool legIKRotActive = false;

		public string leftZName = "IKLeftZ";
		public string leftXName = "IKLeftX";
		public string rightZName = "IKRightZ";
		public string rightXName = "IKRightX";

		public Transform leftFootOverride;
		public Transform rightFootOverride;

		Transform leftFoot;
		Transform rightFoot;

		public Vector3 leftFootStart;
		//Vector3 leftCurStart;
		Vector3 leftPos;
		Quaternion leftRot;

		public Vector3 rightFootStart;
		//Vector3 rightCurStart;
		Vector3 rightPos;
		Quaternion rightRot;

		public AnimationCurve heightLerpCurve;

		float leftTargetHeight;
		float leftCurrentHeight;
		float leftLastHeight;
		float leftTime;
		float leftStepTime;
		float leftTargetWeight;
		float leftCurrentWeight;
		float leftLastWeight;

		Quaternion leftRotTarget;
		Quaternion leftRotCur;
		Quaternion leftRotLast;

		float rightTargetHeight;
		float rightCurrentHeight;
		float rightLastHeight;
		float rightTime;
		float rightStepTime;
		float rightTargetWeight;
		float rightCurrentWeight;
		float rightLastWeight;

		Quaternion rightRotTarget;
		Quaternion rightRotCur;
		Quaternion rightRotLast;

		#endregion

		public bool interpolateModel = false;
		public Transform modelTransform;

		float modelTargetHeight;
		float modelLastHeight;
		float modelCurrentHeight;
		float modelTime;
		float modelStepTime;
		float modelHeightDelta;


		void Start () {
			leftFoot = anim.GetBoneTransform (HumanBodyBones.LeftFoot);
			rightFoot = anim.GetBoneTransform (HumanBodyBones.RightFoot);
		}

		float GetHeight (Vector3 startPos, Vector3 speed, float stepTime, float stepSize, ref Quaternion normalRotation, ref bool didHit) {
			//Calculates the raycast point using some extrapolation and data provided by the animation
			Vector3 posExtrapolate = startPos + myTransform.TransformDirection(speed) * stepTime + myTransform.TransformDirection(speed).normalized * stepSize + new Vector3(0, 0.4f + stepSize, 0);
			#if (DEBUG_DRAW)
			Debug.DrawLine (posExtrapolate, posExtrapolate + new Vector3 (0, 0.3f, 0), Color.yellow, stepTime);
			#endif

			RaycastHit hit;
			//Actual raycast
			if (Physics.Raycast (posExtrapolate, new Vector3 (0, -1, 0), out hit, 1f + stepSize * 2, raycastMask)) {
				#if (DEBUG_DRAW)
				Debug.DrawLine (hit.point, hit.point + new Vector3 (0, 0.3f, 0), Color.red, stepTime);
				Debug.DrawLine (startPos, hit.point, Color.magenta, stepTime);
				#endif
				//Set the rotation target, does not work well yet
				normalRotation = Quaternion.FromToRotation (myTransform.up, hit.normal) * transform.rotation;
				didHit = true;
				return hit.point.y;
			}
			//We did not hit the ground
			didHit = false;
			normalRotation = Quaternion.FromToRotation (transform.up, new Vector3(0, 1, 0)) * myTransform.rotation;
			return startPos.y;
		}


		//A function that gets a all the values needed by the IK before the foot is raised from the floor
		void Step(ref float stepTime, ref float time, ref float lastWeight, ref float targetWeight, ref float currentWeight, ref float lastHeight, ref float currentHeight, ref float targetHeight, ref Vector3 footStart, ref Quaternion rotLast, ref Quaternion rotTarget) {

			//If last weight is zero, do not set lastHeight to the currentHeight, because after a long fall, the leg will glitch during first step
			lastHeight = (targetWeight > 0.5f) ? currentHeight : myTransform.position.y + footStart.y;
			stepTime = anim.GetFloat ("StepTime");
			time = Time.time;
			lastWeight = currentWeight;
			rotLast = rotTarget;

			modelLastHeight = modelCurrentHeight;
			modelStepTime = stepTime;
			modelTime = Time.time;

			#if (DEBUG_DRAW)
			Debug.DrawLine (transform.TransformPoint (footStart), transform.TransformPoint (footStart) + new Vector3 (0, 0, 0.3f), Color.green, stepTime);
			#endif

			bool hit = false;
			targetHeight = GetHeight (transform.TransformPoint (footStart), manager.curSpeed, stepTime, anim.GetFloat ("Step"), ref rotTarget, ref hit);
			modelTargetHeight = Mathf.Min(rightTargetHeight, leftTargetHeight);
			//Sets the weight based on if the raycast hit the ground or not
			targetWeight = (hit ? 1f : 0f);
		}

		public void StepLeft (AnimationEvent evt) {
			if (evt.animatorClipInfo.weight > 0.5)
				Step (ref leftStepTime, ref leftTime, ref leftLastWeight, ref leftTargetWeight, ref leftCurrentWeight, ref leftLastHeight, ref leftCurrentHeight, ref leftTargetHeight, ref leftFootStart, ref leftRotLast, ref leftRotTarget);
		}

		public void StepRight (AnimationEvent evt) {
			if (evt.animatorClipInfo.weight > 0.5)
				Step (ref rightStepTime, ref rightTime, ref rightLastWeight, ref rightTargetWeight, ref rightCurrentWeight, ref rightLastHeight, ref rightCurrentHeight, ref rightTargetHeight, ref rightFootStart, ref rightRotLast, ref rightRotTarget);
		}

		//The part where all the IK point setting happens
		void OnAnimatorIK () {
			#region FOOT_IK
			//Leg IK part
			if (legIKActive) {
				//If the override is enabled, use the override transforms
				if (leftFootOverride == null) {
					//leftCurStart = myTransform.TransformPoint (leftFootStart);
					//If the step time is not small, interpolate between the targets and weights, or just set them to the targets
					leftTargetWeight = anim.GetBool("isGrounded") ? leftTargetWeight : 0;
					if (leftStepTime > 0.01f) {
						leftCurrentHeight = Mathf.Lerp (leftLastHeight, leftTargetHeight, heightLerpCurve.Evaluate((Time.time - leftTime) / leftStepTime));
						leftRotCur = Quaternion.Lerp (leftRotLast, leftRotTarget, heightLerpCurve.Evaluate((Time.time - leftTime) / leftStepTime));
						leftCurrentWeight = Mathf.Lerp(leftLastWeight, leftTargetWeight, (Time.time - leftTime) / leftStepTime);
					} else {
						leftCurrentHeight = leftTargetHeight;
						leftRotCur = leftRotTarget;
						leftCurrentWeight = leftTargetHeight;
					}
					leftPos = Vector3.Lerp (leftFoot.position + new Vector3 (0, leftCurrentHeight - myTransform.position.y, 0), leftPos, anim.GetFloat ("LeftStick"));
					leftRot = leftRotCur; //Quaternion.Lerp (leftFoot.rotation, leftRotCur, anim.GetFloat ("LeftStick"));
				} else {
					leftPos = leftFootOverride.position;
					leftRot = leftFootOverride.rotation;
				}
				//Set the weights
				anim.SetIKPosition (AvatarIKGoal.LeftFoot, leftPos);
				anim.SetIKPositionWeight (AvatarIKGoal.LeftFoot, leftCurrentWeight);
				anim.SetIKRotation (AvatarIKGoal.LeftFoot, leftRot);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftFoot, legIKRotActive? anim.GetFloat ("LeftStick") : 0f);

				//The same for the right side
				if (rightFootOverride == null) {
					//rightCurStart = myTransform.TransformPoint (rightFootStart);
					rightTargetWeight = anim.GetBool("isGrounded") ? rightTargetWeight : 0;
					if (rightStepTime > 0.01f) {
						rightCurrentHeight = Mathf.Lerp (rightLastHeight, rightTargetHeight, heightLerpCurve.Evaluate((Time.time - rightTime) / rightStepTime));
						rightRotCur = Quaternion.Lerp (rightRotLast, rightRotTarget, heightLerpCurve.Evaluate((Time.time - rightTime) / rightStepTime));
						rightCurrentWeight = Mathf.Lerp(rightLastWeight, rightTargetWeight, (Time.time - rightTime) / rightStepTime);
					} else {
						rightCurrentHeight = rightTargetHeight;
						rightRotCur = rightRotTarget;
						rightCurrentWeight = rightTargetWeight;
					}
					rightPos = Vector3.Lerp (rightFoot.position + new Vector3 (0, rightCurrentHeight - myTransform.position.y, 0), rightPos, anim.GetFloat ("RightStick"));
					rightRot = rightRotCur; //Quaternion.Lerp (rightFoot.rotation, rightRotCur, anim.GetFloat ("RightStick"));
				} else {
					rightPos = rightFootOverride.position;
					rightRot = rightFootOverride.rotation;
				}
				anim.SetIKPosition (AvatarIKGoal.RightFoot, rightPos);
				anim.SetIKPositionWeight (AvatarIKGoal.RightFoot, rightCurrentWeight);
				anim.SetIKRotation (AvatarIKGoal.RightFoot, rightRot);
				anim.SetIKRotationWeight (AvatarIKGoal.RightFoot, legIKRotActive? anim.GetFloat ("RightStick") : 0f);

			} else {
				//Set all the weights to zero
				anim.SetIKPositionWeight (AvatarIKGoal.LeftFoot, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftFoot, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.RightFoot, 0f);
				anim.SetIKPositionWeight (AvatarIKGoal.RightFoot, 0f);
			}
			#endregion

			//A buggy character root transform interpolation to make the character look more natural
			if (interpolateModel) {
				if (modelStepTime > 0.01f)
					modelCurrentHeight = Mathf.Lerp (modelLastHeight, modelTargetHeight, (Time.time - modelTime) / modelStepTime);
				else
					modelCurrentHeight = modelTargetHeight;

				modelTransform.position = new Vector3(modelTransform.position.x, modelCurrentHeight, modelTransform.position.z);
			}

			#region HAND_IK
			//Hand IK part
			if (handIKActive) {

			} else {
				//Set all the weights to zero
				anim.SetIKPositionWeight (AvatarIKGoal.LeftHand, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.LeftHand, 0f);
				anim.SetIKRotationWeight (AvatarIKGoal.RightHand, 0f);
				anim.SetIKPositionWeight (AvatarIKGoal.RightHand, 0f);
			}
			#endregion

		}
	}
}
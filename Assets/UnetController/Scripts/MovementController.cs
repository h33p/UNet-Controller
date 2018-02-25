using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	public class MovementController : Controller {

		//Private variables used to optimize for mobile's instruction set
		private float strafeToSpeedCurveScaleMul;
		private float _strafeToSpeedCurveScale;

		private float snapInvert;

		protected override void OnStart() {

			if (data.snapSize > 0)
				snapInvert = 1f / data.snapSize;

			if (data.strafeToSpeedCurveScale != _strafeToSpeedCurveScale) {
				_strafeToSpeedCurveScale = data.strafeToSpeedCurveScale;
				strafeToSpeedCurveScaleMul = 1f / data.strafeToSpeedCurveScale;
			}

			base.OnStart();
		}

		protected sealed override void RunCommand(ref Results inpRes, Inputs inp) {

			float time = GameManager.curtime;
			GameManager.curtime = inp.timestamp * _sendUpdates * Time.fixedDeltaTime;

			RunPreMove(ref inpRes, ref inp);
			inpRes = MoveCharacter(inpRes, inp, Time.fixedDeltaTime * _sendUpdates, data.maxSpeed);
			RunPostMove(ref inpRes, ref inp);

			//Notify the game manager
			GameManager.PlayerTick(this, lastResults); //clientInputs [clientInputs.Count - 1]);

			GameManager.curtime = time;
		}

		//Data not to be messed with. Needs to be outside the function due to OnControllerColliderHit
		Vector3 hitNormal;

		//Actual movement code. Mostly isolated, except transform
		Results MoveCharacter(Results inpRes, Inputs inp, float deltaMultiplier, Vector3 maxSpeed) {

			//If controlled outside, return results with the current transform position.
			if (inpRes.flags & Flags.CONTROLLED_OUTSIDE) {
				inpRes.speed = myTransform.position - inpRes.position;
				return new Results(myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.flags, 0, 0, inpRes.ragdollTime, inp.timestamp);
			}

			//Calculates if ragdoll should be disabled
			if (inpRes.flags & Flags.RAGDOLL) {
				inpRes.speed = myTransform.position - inpRes.position;
				if (inpRes.speed.magnitude >= data.ragdollStopVelocity)
					inpRes.ragdollTime = inp.timestamp;
				if (inp.timestamp - inpRes.ragdollTime >= ragdollTime)
					inpRes.flags &= ~Flags.RAGDOLL;
				return new Results(myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.flags, 0, 0, inpRes.ragdollTime, inp.timestamp);
			}

			//Clamp camera angles
			inp.y = Mathf.Clamp(inp.y, dataInp.camMinY, dataInp.camMaxY);

			if (inp.x > 360f)
				inp.x -= 360f;
			else if (inp.x < 0f)
				inp.x += 360f;

			//Save current position and rotation to restore after the move
			Vector3 pos = myTransform.position;
			Quaternion rot = myTransform.rotation;

			//Set the position and rotation to the last results ones
			myTransform.position = inpRes.position;
			myTransform.rotation = inpRes.rotation;

			Vector3 tempSpeed = myTransform.InverseTransformDirection(inpRes.speed);

			myTransform.rotation = Quaternion.Euler(new Vector3(0, inp.x, 0));

			//Character sliding of surfaces
			if (!(inpRes.flags & Flags.IS_GROUNDED)) {
				//inpRes.speed.x += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.x * (inpRes.speed.y > 0 ? 0 : -inpRes.speed.y) * (1f - data.slideFriction);
				inpRes.speed.x += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.x * (1f - data.slideFriction);
				//inpRes.speed.z += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.z * (inpRes.speed.y > 0 ? 0 : -inpRes.speed.y) * (1f - data.slideFriction);
				inpRes.speed.z += (1f - inpRes.groundNormal.y) * inpRes.groundNormal.z * (1f - data.slideFriction);
			}

			Vector3 localSpeed = myTransform.InverseTransformDirection(inpRes.speed);
			Vector3 localSpeed2 = Vector3.Lerp(myTransform.InverseTransformDirection(inpRes.speed), tempSpeed, data.velocityTransferCurve.Evaluate(Mathf.Abs(inpRes.rotation.eulerAngles.y - inp.x) / (deltaMultiplier * data.velocityTransferDivisor)));

			if (!(inpRes.flags & Flags.IS_GROUNDED) && data.strafing)
				AirStrafe(ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed, ref localSpeed2);
			else
				localSpeed = localSpeed2;

			BaseMovement(ref inpRes, ref inp, ref deltaMultiplier, ref maxSpeed, ref localSpeed);

			float tY = myTransform.position.y;

			//Convert the local coordinates to the world ones
			inpRes.speed = transform.TransformDirection(localSpeed);
			hitNormal = new Vector3(0, 0, 0);

			//Set the speed to the curve values. Allowing to limit the speed
			inpRes.speed.x = data.finalSpeedCurve.Evaluate(inpRes.speed.x);
			inpRes.speed.y = data.finalSpeedCurve.Evaluate(inpRes.speed.y);
			inpRes.speed.z = data.finalSpeedCurve.Evaluate(inpRes.speed.z);

			//Move the controller
			controller.Move(inpRes.speed * deltaMultiplier);
			//This code continues after OnControllerColliderHit gets called (if it does)

			if (Vector3.Angle(Vector3.up, hitNormal) <= data.slopeLimit)
				inpRes.flags |= Flags.IS_GROUNDED;
			else
				inpRes.flags &= ~Flags.IS_GROUNDED;

			//float speed = inpRes.speed.y;
			inpRes.speed = (transform.position - inpRes.position) / deltaMultiplier;

			//if (inpRes.speed.y > 0)
			//	inpRes.speed.y = Mathf.Min (inpRes.speed.y, Mathf.Max(0, speed));
			//else
			//	inpRes.speed.y = Mathf.Max (inpRes.speed.y, Mathf.Min(0, speed));
			//inpRes.speed.y = speed;

			float gpt = 1f;
			float gp = myTransform.position.y;

			//WIP, broken, Handles hitting ground while spacebar is pressed. It determines how much time was left to move based on at which height the player hit the ground. Some math involved.
			if (data.handleMidTickJump && !(inpRes.flags & Flags.IS_GROUNDED) && tY - gp >= 0 && inp.keys & Keys.JUMP && (controller.isGrounded || Physics.Raycast(myTransform.position + controller.center, Vector3.down, (controller.height / 2) + (controller.skinWidth * 1.5f)))) {
				float oSpeed = inpRes.speed.y;
				gpt = (tY - gp) / (-oSpeed);
				inpRes.speed.y = data.speedJump + ((Physics.gravity.y / 2) * Mathf.Abs((1f - gpt) * deltaMultiplier));
				Debug.Log(inpRes.speed.y + " " + gpt);
				controller.Move(myTransform.TransformDirection(0, inpRes.speed.y * deltaMultiplier, 0));
				inpRes.flags |= Flags.IS_GROUNDED;
				Debug.DrawLine(new Vector3(myTransform.position.x, gp, myTransform.position.z), myTransform.position, Color.blue, deltaMultiplier);
				inpRes.flags |= Flags.JUMPED;
			}

			//If snapping is enabled, then do it
			if (data.snapSize > 0f)
				myTransform.position = new Vector3(Mathf.Round(myTransform.position.x * snapInvert) * data.snapSize, Mathf.Round(myTransform.position.y * snapInvert) * data.snapSize, Mathf.Round(myTransform.position.z * snapInvert) * data.snapSize);

			//If grounded set the speed to the gravity
			if (inpRes.flags & Flags.IS_GROUNDED)
				localSpeed.y = Physics.gravity.y * Mathf.Clamp(deltaMultiplier, 1f, 1f);

			if (inpRes.speed.magnitude > data.ragdollStartVelocity) {
				inpRes.flags |= Flags.RAGDOLL;
				inpRes.ragdollTime = inp.timestamp;
			}

			//Generate the return value
			inpRes = new Results(myTransform.position, myTransform.rotation, hitNormal, inp.y, inpRes.speed, inpRes.flags, gp, gpt, inpRes.ragdollTime, inp.timestamp);

			//Set back the position and rotation
			myTransform.position = pos;
			myTransform.rotation = rot;

			return inpRes;
		}

		//The part which determines if the controller was hit or not
		void OnControllerColliderHit(ControllerColliderHit hit) {
			hitNormal = hit.normal;
		}

		//Handles strafing in air
		public void AirStrafe(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed, ref Vector3 localSpeed2) {
			if (inpRes.flags & Flags.IS_GROUNDED)
				return;

			float tAccel = data.strafeAngleCurve.Evaluate(Mathf.Abs(inpRes.rotation.eulerAngles.y.ClampAngle() - inp.x.ClampAngle()) / deltaMultiplier);
			bool rDir = (inpRes.rotation.eulerAngles.y.ClampAngle() - inp.x.ClampAngle()) > 0;

			if (((inp.inputs.x > 0f && !rDir) || (inp.inputs.x < 0f && rDir)) && inp.inputs.y == 0) {
				if (localSpeed.z >= 0) {
					localSpeed.z = localSpeed2.z + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
					localSpeed.x = localSpeed2.x;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.z = localSpeed.z + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.x < 0f && !rDir) || inp.inputs.x > 0f && rDir) && inp.inputs.y == 0) {
				if (localSpeed.z <= 0) {
					localSpeed.z = localSpeed2.z - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
					localSpeed.x = localSpeed2.x;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.z = localSpeed.z - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.z) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.y > 0f && !rDir) || (inp.inputs.y < 0f && rDir)) && inp.inputs.x == 0) {
				if (localSpeed.x <= 0) {
					localSpeed.x = localSpeed2.x - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
					localSpeed.z = localSpeed2.z;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.x = localSpeed.x - tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
			} else if (((inp.inputs.y > 0f && rDir) || (inp.inputs.y < 0f && !rDir)) && inp.inputs.x == 0) {
				if (localSpeed.x >= 0) {
					localSpeed.x = localSpeed2.x + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
					localSpeed.z = localSpeed2.z;
					localSpeed.y = localSpeed2.y;
				} else
					localSpeed.x = localSpeed.x + tAccel * data.strafeToSpeedCurve.Evaluate(Mathf.Abs(localSpeed.x) * strafeToSpeedCurveScaleMul);
			}
		}

		//The movement part
		public void BaseMovement(ref Results inpRes, ref Inputs inp, ref float deltaMultiplier, ref Vector3 maxSpeed, ref Vector3 localSpeed) {

			//Gets the target maximum speed
			if (inp.keys & Keys.CROUCH) {
				maxSpeed = data.maxSpeedCrouch;
				inpRes.flags |= Flags.CROUCHED;
				controller.height = Mathf.Clamp(controller.height - crouchSwitchMul, data.controllerHeightCrouch, data.controllerHeightNormal);
				controller.center = new Vector3(0, controller.height * data.controllerCentreMultiplier, 0);
			} else {
				if (inpRes.flags & Flags.CROUCHED) {
					inpRes.flags &= ~Flags.CROUCHED;

					Collider[] hits;

					hits = Physics.OverlapCapsule(inpRes.position + new Vector3(0f, data.controllerHeightCrouch, 0f), inpRes.position + new Vector3(0f, data.controllerHeightNormal, 0f), controller.radius);

					for (int i = 0; i < hits.Length; i++)
						if (hits[i].transform.root != myTransform.root) {
							inpRes.flags |= Flags.CROUCHED;
							inp.keys |= Keys.CROUCH;
							maxSpeed = data.maxSpeedCrouch;
							break;
						}
				}
				if (!(inpRes.flags & Flags.CROUCHED)) {
					controller.height = Mathf.Clamp(controller.height + crouchSwitchMul, data.controllerHeightCrouch, data.controllerHeightNormal);
					controller.center = new Vector3(0, controller.height * data.controllerCentreMultiplier, 0);
				} else {
					controller.height = data.controllerHeightCrouch;
					controller.center = new Vector3(0, controller.height * data.controllerCentreMultiplier, 0);
				}
			}

			if (!(inp.keys & Keys.JUMP))
				inpRes.flags &= ~Flags.JUMPED;

			if (inpRes.flags & Flags.IS_GROUNDED && inp.keys & Keys.JUMP && !(inpRes.flags & (Flags.CROUCHED | Flags.JUMPED))) {
				localSpeed.y = data.speedJump;
				if (!data.allowBunnyhopping)
					inpRes.flags |= Flags.JUMPED;
			} else if (!(inpRes.flags & Flags.IS_GROUNDED))
				localSpeed.y += Physics.gravity.y * deltaMultiplier;
			else
				localSpeed.y = -1f;

			if (inpRes.flags & Flags.IS_GROUNDED) {

				if (Mathf.Sign(localSpeed.z * inp.inputs.y) == 1 && inp.inputs.y != 0 && Mathf.Abs(localSpeed.z) <= maxSpeed.z * Mathf.Abs(inp.inputs.y)) {
					localSpeed.z = Mathf.Clamp(localSpeed.z + (inp.inputs.y > 0 ? data.accelerationForward : -data.accelerationBack) * deltaMultiplier,
						-maxSpeed.z * Mathf.Abs(inp.inputs.y),
						maxSpeed.z * Mathf.Abs(inp.inputs.y));
				} else if (inp.inputs.y == 0 || Mathf.Abs(localSpeed.z) > maxSpeed.z * Mathf.Abs(inp.inputs.y)) {
					localSpeed.z = Mathf.Clamp(localSpeed.z + (data.decceleration * -Mathf.Sign(localSpeed.z)) * deltaMultiplier,
						localSpeed.z >= 0 ? 0 : -maxSpeed.z,
						localSpeed.z <= 0 ? 0 : maxSpeed.z);
				} else {
					localSpeed.z = Mathf.Clamp(localSpeed.z + data.accelerationStop * inp.inputs.y * deltaMultiplier,
						localSpeed.z >= 0 ? -data.accelerationBack * deltaMultiplier : -maxSpeed.z,
						localSpeed.z <= 0 ? data.accelerationForward * deltaMultiplier : maxSpeed.z);
				}

				if (Mathf.Sign(localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0 && Mathf.Abs(localSpeed.x) <= maxSpeed.x * Mathf.Abs(inp.inputs.x)) {
					localSpeed.x = Mathf.Clamp(localSpeed.x + Mathf.Sign(inp.inputs.x) * data.accelerationSides * deltaMultiplier,
						-maxSpeed.x * ((Mathf.Sign(localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0) ? Mathf.Abs(inp.inputs.x) : 1f - Mathf.Abs(inp.inputs.x)),
						maxSpeed.x * ((Mathf.Sign(localSpeed.x * inp.inputs.x) == 1 && inp.inputs.x != 0) ? Mathf.Abs(inp.inputs.x) : 1f - Mathf.Abs(inp.inputs.x)));
				} else if (inp.inputs.x == 0 || Mathf.Abs(localSpeed.x) > maxSpeed.x * Mathf.Abs(inp.inputs.x)) {
					localSpeed.x = Mathf.Clamp(localSpeed.x + (data.decceleration * -Mathf.Sign(localSpeed.x)) * deltaMultiplier,
						localSpeed.x >= 0 ? 0 : -maxSpeed.x,
						localSpeed.x <= 0 ? 0 : maxSpeed.x);
				} else {
					localSpeed.x = Mathf.Clamp(localSpeed.x + data.accelerationStop * inp.inputs.x * deltaMultiplier,
						localSpeed.x >= 0 ? -data.accelerationBack * deltaMultiplier : -maxSpeed.x,
						localSpeed.x <= 0 ? data.accelerationForward * deltaMultiplier : maxSpeed.x);
				}
			}
		}
	}
}
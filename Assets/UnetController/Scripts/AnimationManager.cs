using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public class AnimationManager : MonoBehaviour {

		public Animator anim;
		public Controller controller;

		public float groundedMinTime = 0.2f;
		private float lastNotGrounded;

		private Results firstResult;
		private Results secondResult;

		private float callTime;

		private bool added;

		public Vector3 curSpeed;
		public string speedx = "SpeedX";
		public string speedy = "SpeedY";
		public string speedz = "SpeedZ";
		public string grounded = "isGrounded";
		public bool invertGrounded = false;
		public string crouching = "Crouching";
		public string jump = "Jump";

		private int t = 0;

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
			t = 0;
		}

		void Update () {
			if (anim == null) {
				Debug.LogError ("No animator attached! Disabling.");
				this.enabled = false;
				return;
			}

			if (t < 2)
				return;

			float time = (Time.time - callTime) / (Time.fixedDeltaTime * controller.lSendUpdates);

			curSpeed = Vector3.Lerp (firstResult.speed, secondResult.speed, time);
			anim.SetFloat (speedx, curSpeed.x);
			anim.SetFloat (speedy, curSpeed.y);
			anim.SetFloat (speedz, curSpeed.z);
			if ((!(secondResult.flags & Flags.IS_GROUNDED) && firstResult.flags & Flags.IS_GROUNDED) || secondResult.flags & Flags.IS_GROUNDED)
				lastNotGrounded = Time.fixedTime;
			if (Time.fixedTime - lastNotGrounded > groundedMinTime)
				anim.SetBool (grounded, invertGrounded ? !(secondResult.flags & Flags.IS_GROUNDED) : secondResult.flags & Flags.IS_GROUNDED);
			else
				anim.SetBool (grounded, !invertGrounded);
			anim.SetBool (crouching, secondResult.flags & Flags.CROUCHED);
		}

		public void TickUpdate (Results res, bool inLagCompensation) {

			if (!this.enabled)
				return;

			if (inLagCompensation) {
				anim.Update(0);
				return;
			}

			if (controller.playbackMode)
				anim.speed = controller.playbackSpeed;

			if (t == 0) {
				firstResult = res;
				firstResult.speed = controller.myTransform.InverseTransformDirection (firstResult.speed);
				t++;
			} else {
				firstResult = secondResult;
				secondResult = res;
				secondResult.speed = controller.myTransform.InverseTransformDirection (secondResult.speed);
				t++;
				callTime = Time.fixedTime;
				if (secondResult.flags & Flags.JUMPED && firstResult.flags & Flags.IS_GROUNDED && !(firstResult.flags & Flags.JUMPED))
					anim.SetTrigger (jump);
			}
		}
	}
}

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

			float time = (Time.time - callTime) / (Time.fixedDeltaTime * controller.sendUpdates);

			anim.SetFloat (speedx, Mathf.Lerp(firstResult.speed.x, secondResult.speed.x, time));
			anim.SetFloat (speedy, Mathf.Lerp(firstResult.speed.y, secondResult.speed.y, time));
			anim.SetFloat (speedz, Mathf.Lerp(firstResult.speed.z, secondResult.speed.z, time));
			if ((!secondResult.isGrounded && firstResult.isGrounded) || secondResult.isGrounded)
				lastNotGrounded = Time.fixedTime;
			if (Time.fixedTime - lastNotGrounded > groundedMinTime)
				anim.SetBool (grounded, invertGrounded ? !secondResult.isGrounded : secondResult.isGrounded);
			else
				anim.SetBool (grounded, !invertGrounded);
			anim.SetBool (crouching, secondResult.crouch);
		}

		public void TickUpdate (Results res) {

			if (!this.enabled)
				return;

			if (controller.playbackMode)
				anim.speed = controller.playbackSpeed;

			if (t == 0) {
				firstResult = res;
				firstResult.speed = controller.myTransform.InverseTransformDirection (firstResult.speed);
				t++;
			}else {
				firstResult = secondResult;
				secondResult = res;
				secondResult.speed = controller.myTransform.InverseTransformDirection (secondResult.speed);
				t++;
				callTime = Time.fixedTime;
				if (secondResult.jumped && firstResult.isGrounded && !firstResult.jumped)
					anim.SetTrigger (jump);
			}
		}
	}
}

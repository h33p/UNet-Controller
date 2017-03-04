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

		private int t = 0;

		void OnDisable () {
			t = 0;
		}

		void Start () {
			
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

			anim.SetFloat ("SpeedX", Mathf.Lerp(firstResult.speed.x, secondResult.speed.x, time));
			anim.SetFloat ("SpeedY", Mathf.Lerp(firstResult.speed.y, secondResult.speed.y, time));
			anim.SetFloat ("SpeedZ", Mathf.Lerp(firstResult.speed.z, secondResult.speed.z, time));
			if ((!secondResult.isGrounded && firstResult.isGrounded) || secondResult.isGrounded)
				lastNotGrounded = Time.fixedTime;
			if (Time.fixedTime - lastNotGrounded > groundedMinTime)
				anim.SetBool ("isGrounded", secondResult.isGrounded);
			else
				anim.SetBool ("isGrounded", true);
			anim.SetBool ("Crouching", secondResult.crouch);
		}

		public void TickUpdate (Results res) {
			if (!this.enabled)
				return;

			if (!anim.enabled)
				anim.enabled = true;
			
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
					anim.SetTrigger ("Jump");
			}
		}
	}
}

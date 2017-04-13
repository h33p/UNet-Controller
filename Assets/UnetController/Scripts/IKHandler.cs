using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public class IKHandler : MonoBehaviour {

		public Animator anim;

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

		public Transform leftHandOverride;
		public Transform rightHandOverride;

		public float lHWeight;
		public float rHWeight;

		public bool leftHForce = false;
		public bool rightHForce = false;

		void OnAnimatorIK (){
		
		}
	}
}
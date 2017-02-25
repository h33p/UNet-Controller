using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public enum MoveType
	{
		UpdateOnce = 1,
		UpdateOnceAndLerp = 2,
		UpdateOnceAndSLerp = 3
	};

	[CreateAssetMenu(fileName = "Controller Data", menuName = "UNet Controller/Controller Data", order = 1)]
	public class ControllerDataObject : ScriptableObject {

		[Tooltip("Maximum speed when sprinting in X and Z directions.")]
		public Vector3 maxSpeedSprint = new Vector3 (3f, 0f, 7f);
		[Tooltip("Maximum speed when not sprinting in X and Z directions.")]
		public Vector3 maxSpeedNormal = new Vector3 (1f, 0f, 1.5f);
		[Tooltip("Maximum speed when crouching in X and Z directions.")]
		public Vector3 maxSpeedCrouch = new Vector3 (1f, 0f, 1.5f);

		[Tooltip("Maximum surface angle to be considered as ground.")]
		public float slopeLimit = 45f;

		[Tooltip("Controller height while standing.")]
		public float controllerHeightNormal = 1.8f;
		[Tooltip("Controller height while crouching.")]
		public float controllerHeightCrouch = 0.8f;
		[Tooltip("Number of seconds for the controller to switch states of crouching.")]
		public float controllerCrouchSwitch = 0.2f;
		[Tooltip("Set the character controller centre to the height multiplied by this value.")]
		[Range(0,2)]
		public float controllerCentreMultiplier = 0.5f;

		[Tooltip("Final speed curve.")]
		public AnimationCurve finalSpeedCurve;

		[Tooltip("Slide friction.")]
		[Range(0,1)]
		public float slideFriction = 0.3f;

		[Tooltip("Normal acceleration when moving forward.")]
		public float accelerationForward = 6f;
		[Tooltip("Acceleration when moving backwards.")]
		public float accelerationBack = 3f;
		[Tooltip("Decceleration when the opposite key to the moving direction is pressed.")]
		public float accelerationStop = 8f;
		[Tooltip("Decceleration while not pressing anything.")]
		public float decceleration = 2f;
		[Tooltip("Normal acceleration to the sides of the player.")]
		public float accelerationSides = 4f;
		[Tooltip("Upwards speed to be set while jumping.")]
		public float speedJump = 3f;

		[Tooltip("Enables strafing dynamics.")]
		public bool strafing = false;
		[Tooltip("Acceleration while strafing.")]
		public float accelerationStrafe = 6f;
		[Tooltip("Curve which multiplies strafing acceleration based on the angle difference (in degrees per second).")]
		public AnimationCurve strafeAngleCurve;
		[Tooltip("Strafing acceleration to speed multiplier.")]
		public AnimationCurve strafeToSpeedCurve;

		[Tooltip("Curve for transfering velocity from world to local space.")]
		public AnimationCurve velocityTransferCurve;
		[Tooltip("Multiplier for the data inside velocity transfer curve's evaluation input value.")]
		public float velocityTransferDivisor;

		[Tooltip("The speed at point 1 in the strafe to speed curve.")]
		public float strafeToSpeedCurveScale = 18f;

		[Tooltip("The closest multiple of the value the player position is set to.")]
		[Range(0,1)]
		public float snapSize = 0.02f;

		[Tooltip("What position difference should the server tolerate sent by the client. NOTE: For all toleration options, CLIENT_TRUST has to be uncommented inside the controller script.")]
		[Range(0,1)]
		public float clientPositionToleration = 0.02f;
		[Tooltip("What speed difference should the server tolerate sent by the client.")]
		[Range(0,5)]
		public float clientSpeedToleration = 0.3f;
		[Tooltip("Crouch should match with server.")]
		public bool clientCrouchMatch = false;
		[Tooltip("Grounded should match with server.")]
		public bool clientGroundedMatch = true;


		[Tooltip("Period in seconds how often the network events happen. Note, not the actual value is used, the closest multiple of FixedUpdates is calculated and it is used instead.")]
		[Range (0.01f, 1f)]
		public float sendRate = 0.1f;

		[Tooltip("Maximum number of inputs to store. The higher the number, the bigger the latency can be to have smooth reconceliation. However, if the latency is big, this can result in big performance overhead.")]
		[Range (1, 300)]
		public int inputsToStore = 10;

		[Tooltip("Number of server results to buffer. This stores the minimum nuber of server results, keeps them sorted and once another update comes, takes the first result and uses in reconciliation. Good for latency differences.")]
		[Range (1, 20)]
		public int serverResultsBuffer = 3;
		[Tooltip("Same as above, but on the server side, using player inputs.")]
		[Range (1, 20)]
		public int clientInputsBuffer = 3;

		[Tooltip("Movement type to use. UpdateOnceAndLerp works the best.")]
		public MoveType movementType;

		[Tooltip("In development feature to handle hitting and jumping of the ground while mid-tick.")]
		public bool handleMidTickJump = false;

		[Tooltip("Debug mode, enable to use Network Data Analyzer")]
		public bool debug = false;
	}
}
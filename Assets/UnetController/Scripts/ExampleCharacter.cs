using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {
	public class ExampleCharacter : MovementController
	{
		const uint keyAttack = (1 << 2);

		public GameObject hitBall;
		public GameObject wallBall;

		public object thing = 3;
		[SerializeField]
		public PredVar_uint ammo = new PredVar_uint(50);
		private PredVar_float nextShootTime = new PredVar_float(0);
#region AI

		//AI part
		[System.NonSerialized]
		public bool aiEnabled;
		//A value to check by scripts enabling AI so they can identify themselves
		[System.NonSerialized]
		public short aiEnablerCode;

		//All such settings should go to a separate ScriptableObject to save memory, while on the other hand, stack allocation is faster than heap one
		[Tooltip("Distance which is used to check the distance to the current AI target to see when it is reached.")]
		public float aiTargetDistanceXZ = 0.1f;
		[Tooltip("Distance in Y axis. Should be a bit higher than XZ due to possible misalignment.")]
		public float aiTargetDistanceY = 0.5f;
		[Tooltip("Speed at which the AI rotates towards the target.")]
		public float aiTargetRotationSpeed = 10f;

		private Vector3 aiTarget = new Vector3(0, 0, 0);

		private PredVar_Vector3 aiTarget1;
		private PredVar_Vector3 aiTarget2;
		private PredVar_byte aiTargetReached;

		public void SetAITarget(Vector3 target) {
			aiTargetReached.value = 1;
			aiTarget2.value = target;
		}

		public void SetAITarget(Vector3 target1, Vector3 target2) {
			aiTargetReached.value = 0;
			aiTarget1.value = target1;
			aiTarget2.value = target2;
		}
#endregion

		protected override void InputUpdate(ref Inputs inputs) {
			base.InputUpdate(ref inputs);
            inputs.keys.Set(keyAttack, Input.GetMouseButtonDown(0));
		}

		private RaycastHit hitinfo;

		protected override void RunPreMove(ref Results results, ref Inputs inputs) {

			results.flags.Set(Flags.AI_ENABLED, aiEnabled);
			if(aiEnabled) {
				if(aiTargetReached == 0)
					aiTarget = aiTarget1;
				else if(aiTargetReached == 1)
					aiTarget = aiTarget2;
				else
					results.flags &= ~Flags.AI_ENABLED;
			}

			if(results.flags & Flags.AI_ENABLED) {
				float targetRotation = Quaternion.LookRotation(aiTarget - results.position).eulerAngles.y;
				inputs.x = targetRotation;
				inputs.inputs.y = 1;
			}
		}

		protected override void RunPostMove(ref Results results, ref Inputs inputs) {

			if (results.flags & Flags.AI_ENABLED && Vector2.Distance(new Vector2(results.position.x, results.position.z), new Vector2(aiTarget.x, aiTarget.z)) <= aiTargetDistanceXZ && Mathf.Abs(lastResults.position.y - aiTarget.y) <= aiTargetDistanceY)
				aiTargetReached.value++;

			if (inputs.keys.IsSet(keyAttack) && ammo > 0 && GameManager.curtime >= nextShootTime) {
				ammo.value--;
				nextShootTime.value = GameManager.curtime + 0.5f;
				Debug.Log("Shoot, and now we got " + ammo.value + " ammo.");
				Vector3 start = camTargetFPS.position;
				Vector3 direction = Quaternion.Euler(inputs.y, inputs.x, 0) * Vector3.forward;
				if (Physics.Raycast(start + direction * 0.5f, direction, out hitinfo, 500f)) {
					bool isPlayer = false;
					//Very bad practice to compare tags like this, but this is just an example
					if (hitinfo.transform.root.tag == "Player")
						isPlayer = true;

					//Server only data part, idealy, this should be only for actions that do not need to be predicted
					if (isServer) {
						if (isPlayer)
							Destroy(Instantiate(hitBall, hitinfo.point, new Quaternion(0, 0, 0, 1)), 5);
						else
							Destroy(Instantiate(wallBall, hitinfo.point, new Quaternion(0, 0, 0, 1)), 5);
						LagCompensation.DebugLagCompensationSpawn(hitBall);
					}
				}
			}
		}
	}
}

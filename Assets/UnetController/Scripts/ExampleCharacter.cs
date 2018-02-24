using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {
	public class ExampleCharacter : Controller
	{
		const uint keyAttack = (1 << 2);

		public GameObject hitBall;
		public GameObject wallBall;

		[SerializeField]
		private PredVar_uint ammo = new PredVar_uint(50);
		private PredVar_float nextShootTime = new PredVar_float(0);

		protected override void InputUpdate(ref Inputs inputs) {
			base.InputUpdate(ref inputs);
			inputs.keys.Set(keyAttack, Input.GetMouseButtonDown(0));
		}

		private RaycastHit hitinfo;

		protected override void RunPostMove(ref Results results, ref Inputs inputs) {
			base.RunPostMove(ref results, ref inputs);
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
							Destroy(GameObject.Instantiate(hitBall, hitinfo.point, new Quaternion(0, 0, 0, 1)), 5);
						else
							Destroy(GameObject.Instantiate(wallBall, hitinfo.point, new Quaternion(0, 0, 0, 1)), 5);
						LagCompensation.DebugLagCompensationSpawn(hitBall);
					}
				}
			}
		}
	}
}

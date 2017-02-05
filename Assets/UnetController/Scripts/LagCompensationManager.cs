using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	public class LagCompensationManager : MonoBehaviour {

		public static List<Controller> controllers = new List<Controller> ();

		public static void SetGlobalState (long tick) {
			foreach (Controller c in controllers) {
				foreach (Results r in c.clientResults) {
					if (r.timestamp == tick) {
						c.myTransform.position = r.position;
						c.myTransform.rotation = r.rotation;
						break;
					}
				}
			}
		}

		public static bool Raycast(long tick, Transform rootTransform, Vector3 origin, bool rootDirection, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = -5, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
			SetGlobalState (tick);
			if (rootTransform == null)
				return Physics.Raycast (origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
			else if (rootDirection)
				return Physics.Raycast (rootTransform.TransformPoint(origin), rootTransform.TransformDirection(direction), out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
			else
				return Physics.Raycast (rootTransform.TransformPoint(origin), direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
		}

		public static bool Linecast (long tick, Vector3 start, Vector3 end, out RaycastHit hitInfo, int layerMask = -5, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) {
			Vector3 direction = end - start;
			return Raycast (tick, null, start, false, direction, out hitInfo, direction.magnitude, layerMask, queryTriggerInteraction);
		}

		public static void RegisterController (Controller controller) {
			if (controllers == null)
				controllers = new List<Controller> ();
			if (!controllers.Contains (controller))
				controllers.Add (controller);
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace GreenByteSoftware.UNetController {

	public class SyncedGlobals : NetworkBehaviour {

		[SyncVar]
		public uint currentTick = 0;

		public static bool sIsServer = false;

		int updateCount = 0;

		void FixedUpdate() {

			if (isServer) {
				updateCount++;
				sIsServer = true;

				if (updateCount > GameManager.sendUpdates)
					currentTick++;

				if (currentTick > 0 && GameManager.players.Count == 0)
					currentTick = 0;
			} else
				sIsServer = false;

			GameManager.tick = currentTick;
		}
	}
}

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

		public override float GetNetworkSendInterval() {
			if (GameManager.settings != null)
				return GameManager.settings.sendRate;
			else
				return 0.1f;
		}

		void FixedUpdate() {

			if (isServer) {
				updateCount++;
				sIsServer = true;

				if (updateCount >= GameManager.sendUpdates) {
                    currentTick++;
					updateCount = 0;
                }

				if (currentTick > 0 && GameManager.players.Count == 0)
					currentTick = 0;
			} else
				sIsServer = false;

			GameManager.tick = currentTick;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	[CreateAssetMenu(fileName = "Network Settings", menuName = "UNet Controller/Network Settings", order = 1)]
	public class NetworkSettingsObject : ScriptableObject {

		[Tooltip("Period in seconds how often the network events happen. Note, not the actual value is used, the closest multiple of FixedUpdates is calculated and it is used instead.")]
		[Range (0.01f, 1f)]
		public float sendRate = 0.1f;

		[Tooltip("A list of game objects that can be recorded and then spawned back to be replayed.")]
		public GameObject[] recordGameObjects;

	}
}
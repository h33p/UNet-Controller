using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	[CreateAssetMenu(fileName = "Network Settings", menuName = "UNet Controller/Network Settings", order = 1)]
	public class NetworkSettingsObject : ScriptableObject {

		[Tooltip("Period in seconds how often the network events happen. Note, not the actual value is used, the closest multiple of FixedUpdates is calculated and it is used instead.")]
		[Range (0.01f, 1f)]
		public float sendRate = 0.1f;

		[Tooltip("Rate at which a full inputs and results packets are sent.")]
		[Range(1, 100)]
		public int maxDeltaTicks = 30;

		[Tooltip("Size in ticks of the sliding window for the offset of running commands on the server side (preventing speed-hacks).")]
		[Range(1, 200)]
		public int slidingWindowSize = 60;

		[Tooltip("Maximum acceptable desynchronization of the sliding window between the server and the client. Reaching this point, the client adjusts the current tick be in sync again.")]
		[Range(1, 200)]
		public int maxSlidingWindowInaccuracy = 40;

		[Tooltip("A list of game objects that can be recorded and then spawned back to be replayed.")]
		public GameObject[] recordGameObjects;

		[Tooltip("Maximum amount of history to store for lag compensation.")]
		[Range(0.01f, 1f)]
		public float lagCompensationAmount = 1f;

		[Tooltip("Use FixedUpdate loop for the inputs generation.")]
		public bool useFixedUpdate = true;
	}
}
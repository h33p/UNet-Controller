using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {

	[System.Serializable]
	public struct SmallResults
	{
		public Vector3 position;
		public Quaternion rotation;
		public uint timestamp;

		public SmallResults (Vector3 pos, Quaternion rot, uint tick) {
			position = pos;
			rotation = rot;
			timestamp = tick;
		}

		public override string ToString () {
			return "" + position + "\n"
				+ rotation + "\n"
				+ timestamp + "\n";
		}
	}

	public class RecordableObject : MonoBehaviour {

		// Use this for initialization
		void Start () {
			
		}
		
		// Update is called once per frame
		void Update () {
			
		}
	}
}

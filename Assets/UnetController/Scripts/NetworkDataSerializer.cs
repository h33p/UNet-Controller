using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace GreenByteSoftware.UNetController {

	[System.Serializable]
	public class NetworkDataTick {
		
		public Extensions.SInputs input;
		public Extensions.SResults result;

		public NetworkDataTick(Inputs inp, Results res) {
			input = new Extensions.SInputs(inp);
			result = new Extensions.SResults(res);
		}
	}

	[System.Serializable]
	public class NetworkDataPlayer {
		public List<NetworkDataTick> data;

		public NetworkDataPlayer () {
			data = new List<NetworkDataTick> (500);
		}

		public NetworkDataPlayer (string filename) {
			BinaryFormatter bf = new BinaryFormatter ();
			FileStream file = File.Open (filename, FileMode.Open);

			NetworkDataPlayer dataS = (NetworkDataPlayer)bf.Deserialize (file);
			file.Close ();
			data = dataS.data;
		}

		public bool Save (string filename) {
			BinaryFormatter bf = new BinaryFormatter ();
			FileStream file;
			if (File.Exists (Path.Combine (Application.persistentDataPath, filename)))
				file = File.Open (Path.Combine (Application.persistentDataPath, filename), FileMode.Open);
			else
				file = File.Open (Path.Combine (Application.persistentDataPath, filename), FileMode.Create);

			bf.Serialize (file, this);
			file.Close ();
			return true;
		}
	}

	public class NetworkDataSerializer : MonoBehaviour {

		private string filename;
		private NetworkDataPlayer playerData;
		public Controller controller;
		private bool added;

		void OnEnable () {
			filename = Extensions.GenerateGUID ()+".netdat";
			playerData = new NetworkDataPlayer ();
			if (!added) {
				controller.tickUpdateDebug += this.Tick;
				added = true;
			}
		}

		void OnDisable () {
			if (added) {
				controller.tickUpdateDebug -= this.Tick;
				added = false;
			}
		}

		void OnDestroy () {
			if (this.enabled)
				playerData.Save (filename);
			if (added) {
				controller.tickUpdateDebug -= this.Tick;
				added = false;
			}
		}

		void OnApplicationQuit () {
			if (this.enabled)
				playerData.Save (filename);
			if (added) {
				controller.tickUpdateDebug -= this.Tick;
				added = false;
			}
		}

		public void Tick (Inputs inp, Results res) {
			if (this.enabled)
				playerData.data.Add (new NetworkDataTick (inp, res));
		}
	}
}

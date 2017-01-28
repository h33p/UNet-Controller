using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Debug_UI : MonoBehaviour {


	public static Debug_UI singleton;

	public Text posText;
	public Text sposText;
	public Text stposText;
	public Text deltaText;
	public Text tickText;
	public Text stickText;

	void Awake () {
		singleton = this;
	}

	public static void UpdateUI (Vector3 pos, Vector3 sPos, Vector3 stPos, int tick, int serverTick) {
		if (singleton == null)
			return;
		singleton.posText.text = "CurPos: "+pos.ToString ();
		singleton.sposText.text = "ServerPos: "+sPos.ToString ();
		singleton.stposText.text = "ServerTickPos: "+stPos.ToString ();
		singleton.deltaText.text = "Delta: "+(stPos - sPos).ToString ();
		singleton.tickText.text = "Local Tick: "+tick;
		singleton.stickText.text = "Server Tick: "+serverTick;

	}
}

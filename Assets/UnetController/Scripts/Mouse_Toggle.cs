using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mouse_Toggle : MonoBehaviour {

	void Update () {
		if (Input.GetKeyDown (KeyCode.Escape) && Cursor.lockState == CursorLockMode.None) {
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		} else if (Input.GetKeyDown (KeyCode.Escape)) {
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
	}
}

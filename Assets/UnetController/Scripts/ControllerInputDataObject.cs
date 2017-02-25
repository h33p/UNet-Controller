using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GreenByteSoftware.UNetController {
	[CreateAssetMenu(fileName = "Controller Data", menuName = "UNet Controller/Controller Input Data", order = 1)]
	public class ControllerInputDataObject : ScriptableObject {

		[Tooltip("Mouse sensitivity.")]
		public float rotateSensitivity = 1f;

		[Tooltip("Maximum camera rotation on horizontal axis")]
		public float camMaxY = 90f;
		[Tooltip("Minimum camera rotation on horizontal axis")]
		public float camMinY = -90f;

		[Tooltip("Rotation interpolation speed when in third person mode.")]
		public float rotInterp = 10f;

	}
}
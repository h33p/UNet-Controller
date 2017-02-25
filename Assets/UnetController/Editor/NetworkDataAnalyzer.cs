using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace GreenByteSoftware.UNetController {
	public class NetworkDataAnalyzer : EditorWindow {

		NetworkDataPlayer data1;
		string path1 = "";
		int t1;
		NetworkDataPlayer data2;
		string path2 = "";
		int t2;
		static GUIStyle errorText;
		static GUIStyle errorDrop;


		Vector2 scrollPos;

		int maxLength = -1;
		bool[,] opened;
		static bool[] error;
		static bool[] error1;

		int state = 0;
		int data2Offset = 0;

		[MenuItem ("Window/UNet Controller/Network Data Analyzer")]
		static void Init () {
			errorText = new GUIStyle(EditorStyles.label);
			errorText.normal.textColor = Color.red;
			errorDrop = new GUIStyle(EditorStyles.foldout);
			errorDrop.normal.textColor = Color.red;
			errorDrop.active.textColor = Color.red;
			errorDrop.focused.textColor = Color.red;
			errorDrop.hover.textColor = Color.red;
			errorDrop.onNormal.textColor = Color.red;
			errorDrop.onActive.textColor = Color.red;
			errorDrop.onFocused.textColor = Color.red;
			errorDrop.onHover.textColor = Color.red;

			NetworkDataAnalyzer window = (NetworkDataAnalyzer)EditorWindow.GetWindow (typeof (NetworkDataAnalyzer));
			window.Show();
		}

		void RecalculateErrors() {

			for (int i = Mathf.Max(0, -data2Offset); i < maxLength; i++) {

				error [i] = false;
				error1 [i] = false;

				if (data2.data [i + data2Offset].input.inputs != data1.data [i].input.inputs)
					error[i] = true;
				
				if (!error[i] && data2.data [i + data2Offset].input.x != data1.data [i].input.x)
					error[i] = true;

				if (!error[i] && data2.data [i + data2Offset].input.y != data1.data [i].input.y)
					error[i] = true;
				
				if (!error[i] && data2.data [i + data2Offset].input.jump != data1.data [i].input.jump)
					error[i] = true;
				
				if (!error[i] && data2.data [i + data2Offset].input.crouch != data1.data [i].input.crouch)
					error[i] = true;
				
				if (!error[i] && data2.data [i + data2Offset].input.sprint != data1.data [i].input.sprint)
					error[i] = true;
				
				if (!error[i] && data2.data [i + data2Offset].input.timestamp != data1.data [i].input.timestamp)
					error[i] = true;
				

				if (error1[i] && data2.data [i + data2Offset].result.position != data1.data [i].result.position)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.rotation != data1.data [i].result.rotation)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.speed != data1.data [i].result.speed)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.isGrounded != data1.data [i].result.isGrounded)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.jumped != data1.data [i].result.jumped)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.crouch != data1.data [i].result.crouch)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
					error1[i] = true;
				
				if (!error1[i] && data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
					error1[i] = true;
				
				if (!error1 [i] && data2.data [i + data2Offset].result.timestamp != data1.data [i].result.timestamp)
					error1 [i] = true;
			}
		}

		void OnGUI () {
			scrollPos = GUILayout.BeginScrollView(scrollPos, false, false);
			EditorGUI.indentLevel++;

			EditorGUILayout.BeginVertical ("Box");
			EditorGUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Choose data file"))
				path1 = EditorUtility.OpenFilePanel ("Select data file", Application.persistentDataPath, "netdat");
			EditorGUILayout.LabelField ("File name: "+path1, EditorStyles.label);
			EditorGUILayout.EndHorizontal ();
			EditorGUILayout.EndVertical ();

			EditorGUILayout.BeginVertical ("Box");
			EditorGUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Choose data file"))
				path2 = EditorUtility.OpenFilePanel ("Select data file", Application.persistentDataPath, "netdat");
			EditorGUILayout.LabelField ("File name: "+path2, EditorStyles.label);
			EditorGUILayout.EndHorizontal ();
			EditorGUILayout.EndVertical ();

			if (GUILayout.Button ("Analyze data")) {
				if ((path1 == "" || !File.Exists (path1)) && (path2 == "" || !File.Exists (path2))) {
					EditorUtility.DisplayDialog ("Error!", "Some files are not selected correctly!", "Ok");
					return;
				} else {
					state = 0;
					maxLength = -1;
					try {
						data1 = new NetworkDataPlayer(path1);

					}
					catch (Exception e) {
						EditorUtility.DisplayDialog ("Error!", "Failed to read the first file.\n\nException:\n"+e.StackTrace, "Ok");
						state = -1;
						path1 = "";
					}

					try {
						data2 = new NetworkDataPlayer(path2);
					}
					catch (Exception e) {
						EditorUtility.DisplayDialog ("Error!", "Failed to read the second file.\n\nException:\n"+e.StackTrace, "Ok");
						state = -1;
						path2 = "";
					}
					state++;
				}
			}
			if (state == 1) {

				try {
					if (maxLength == -1) {

						maxLength = Mathf.Max (data1.data.Count, data2.data.Count);
						opened = new bool[maxLength, 3];

						error = new bool[maxLength];
						error1 = new bool[maxLength];
						RecalculateErrors ();
					}

					int lastOffset = data2Offset;
					data2Offset = EditorGUILayout.IntField("Second data offset", data2Offset, EditorStyles.numberField);

					if (lastOffset != data2Offset)
						RecalculateErrors ();

					for (int i = 0; i < maxLength; i++) {

						if (data1.data.Count > i && data2.data.Count - data2Offset > i && i + data2Offset > 0) {

							if (!error[i] && !error1[i])
								opened [i, 0] = Foldout (opened [i, 0], "Tick " + i, true, EditorStyles.foldout);
							else
								opened [i, 0] = Foldout (opened [i, 0], "Tick " + data1.data [i].input.timestamp, true, errorDrop);

							if (opened [i, 0]) {

								EditorGUI.indentLevel++;

								if (!error[i])
									opened [i, 1] = Foldout (opened [i, 1], "Input", true, EditorStyles.foldout);
								else
									opened [i, 1] = Foldout (opened [i, 1], "Input", true, errorDrop);

								if (opened [i, 1]) {

									EditorGUI.indentLevel++;
									EditorGUILayout.BeginHorizontal ();

									EditorGUILayout.BeginVertical ("Box");

									EditorGUILayout.LabelField ("Input: " + data1.data [i].input.inputs, EditorStyles.label);
									EditorGUILayout.LabelField ("X: " + data1.data [i].input.x, EditorStyles.label);
									EditorGUILayout.LabelField ("Y: " + data1.data [i].input.y, EditorStyles.label);
									EditorGUILayout.LabelField ("Jump: " + data1.data [i].input.jump, EditorStyles.label);
									EditorGUILayout.LabelField ("Crouch: " + data1.data [i].input.crouch, EditorStyles.label);
									EditorGUILayout.LabelField ("Sprint: " + data1.data [i].input.sprint, EditorStyles.label);
									EditorGUILayout.LabelField ("Timestamp: " + data1.data [i].input.timestamp, EditorStyles.label);

									EditorGUILayout.EndVertical ();

									EditorGUILayout.BeginVertical ("Box");

									EditorGUILayout.LabelField ("Input: " + data2.data [i + data2Offset].input.inputs, EditorStyles.label);
									EditorGUILayout.LabelField ("X: " + data2.data [i + data2Offset].input.x, EditorStyles.label);
									EditorGUILayout.LabelField ("Y: " + data2.data [i + data2Offset].input.y, EditorStyles.label);
									EditorGUILayout.LabelField ("Jump: " + data2.data [i + data2Offset].input.jump, EditorStyles.label);
									EditorGUILayout.LabelField ("Crouch: " + data2.data [i + data2Offset].input.crouch, EditorStyles.label);
									EditorGUILayout.LabelField ("Sprint: " + data2.data [i + data2Offset].input.sprint, EditorStyles.label);
									EditorGUILayout.LabelField ("Timestamp: " + data2.data [i + data2Offset].input.timestamp, EditorStyles.label);

									EditorGUILayout.EndVertical ();

									EditorGUILayout.BeginVertical ("Box");

									if (data2.data [i + data2Offset].input.inputs != data1.data [i].input.inputs)
										EditorGUILayout.LabelField ("Input: " + ((Vector2)data1.data [i].input.inputs - (Vector2)data2.data [i + data2Offset].input.inputs), errorText);
									else
										EditorGUILayout.LabelField ("Input: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.x != data1.data [i].input.x)
										EditorGUILayout.LabelField ("X: " + (data1.data [i].input.x - data2.data [i + data2Offset].input.x), errorText);
									else
										EditorGUILayout.LabelField ("X: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.y != data1.data [i].input.y)
										EditorGUILayout.LabelField ("Y: " + (data1.data [i].input.y - data2.data [i + data2Offset].input.y), errorText);
									else
										EditorGUILayout.LabelField ("Y: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.jump != data1.data [i].input.jump)
										EditorGUILayout.LabelField ("Jump: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Jump: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.crouch != data1.data [i].input.crouch)
										EditorGUILayout.LabelField ("Crouch: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Crouch: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.sprint != data1.data [i].input.sprint)
										EditorGUILayout.LabelField ("Sprint: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Sprint: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].input.timestamp != data1.data [i].input.timestamp)
										EditorGUILayout.LabelField ("Timestamp: " + (data1.data [i].input.timestamp - data2.data [i + data2Offset].input.timestamp), errorText);
									else
										EditorGUILayout.LabelField ("Timestamp: Match", EditorStyles.label);



									EditorGUILayout.EndVertical ();

									EditorGUILayout.EndHorizontal ();
									EditorGUI.indentLevel--;
								}


								if (!error1[i])
									opened [i, 2] = Foldout (opened [i, 2], "Output", true, EditorStyles.foldout);
								else
									opened [i, 2] = Foldout (opened [i, 2], "Output", true, errorDrop);

								if (opened [i, 2]) {
									EditorGUI.indentLevel++;
									EditorGUILayout.BeginHorizontal ();

									EditorGUILayout.BeginVertical ("Box");

									EditorGUILayout.LabelField ("Position: " + data1.data [i].result.position, EditorStyles.label);
									EditorGUILayout.LabelField ("Rotation: " + data1.data [i].result.rotation, EditorStyles.label);
									EditorGUILayout.LabelField ("CamX: " + data1.data [i].result.camX, EditorStyles.label);
									EditorGUILayout.LabelField ("Speed: " + data1.data [i].result.speed, EditorStyles.label);
									EditorGUILayout.LabelField ("IsGrounded: " + data1.data [i].result.isGrounded, EditorStyles.label);
									EditorGUILayout.LabelField ("Jumped: " + data1.data [i].result.jumped, EditorStyles.label);
									EditorGUILayout.LabelField ("Crouch: " + data1.data [i].result.crouch, EditorStyles.label);
									EditorGUILayout.LabelField ("GroundPoint: " + data1.data [i].result.groundPoint, EditorStyles.label);
									EditorGUILayout.LabelField ("GroundPointTime: " + data1.data [i].result.groundPointTime, EditorStyles.label);
									EditorGUILayout.LabelField ("Timestamp: " + data1.data [i].result.timestamp, EditorStyles.label);

									EditorGUILayout.EndVertical ();

									EditorGUILayout.BeginVertical ("Box");

									EditorGUILayout.LabelField ("Position: " + data2.data [i + data2Offset].result.position, EditorStyles.label);
									EditorGUILayout.LabelField ("Rotation: " + data2.data [i + data2Offset].result.rotation, EditorStyles.label);
									EditorGUILayout.LabelField ("CamX: " + data2.data [i + data2Offset].result.camX, EditorStyles.label);
									EditorGUILayout.LabelField ("Speed: " + data2.data [i + data2Offset].result.speed, EditorStyles.label);
									EditorGUILayout.LabelField ("IsGrounded: " + data2.data [i + data2Offset].result.isGrounded, EditorStyles.label);
									EditorGUILayout.LabelField ("Jumped: " + data2.data [i + data2Offset].result.jumped, EditorStyles.label);
									EditorGUILayout.LabelField ("Crouch: " + data2.data [i + data2Offset].result.crouch, EditorStyles.label);
									EditorGUILayout.LabelField ("GroundPoint: " + data2.data [i + data2Offset].result.groundPoint, EditorStyles.label);
									EditorGUILayout.LabelField ("GroundPointTime: " + data2.data [i + data2Offset].result.groundPointTime, EditorStyles.label);
									EditorGUILayout.LabelField ("Timestamp: " + data2.data [i + data2Offset].result.timestamp, EditorStyles.label);

									EditorGUILayout.EndVertical ();

									EditorGUILayout.BeginVertical ("Box");

									if (data2.data [i + data2Offset].result.position != data1.data [i].result.position)
										EditorGUILayout.LabelField ("Position: " + ((Vector3)data1.data [i].result.position - (Vector3)data2.data [i + data2Offset].result.position), errorText);
									else
										EditorGUILayout.LabelField ("Position: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.rotation != data1.data [i].result.rotation)
										EditorGUILayout.LabelField ("Rotation: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Rotation: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
										EditorGUILayout.LabelField ("CamX: " + (data1.data [i].result.camX - data2.data [i + data2Offset].result.camX), errorText);
									else
										EditorGUILayout.LabelField ("CamX: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.speed != data1.data [i].result.speed)
										EditorGUILayout.LabelField ("Speed: " + ((Vector3)data1.data [i].result.speed - (Vector3)data2.data [i + data2Offset].result.speed), errorText);
									else
										EditorGUILayout.LabelField ("Speed: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.isGrounded != data1.data [i].result.isGrounded)
										EditorGUILayout.LabelField ("IsGrounded: No Match", errorText);
									else
										EditorGUILayout.LabelField ("IsGrounded: Match", EditorStyles.label);
									
									if (data2.data [i + data2Offset].result.jumped != data1.data [i].result.jumped)
										EditorGUILayout.LabelField ("Jump: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Jump: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.crouch != data1.data [i].result.crouch)
										EditorGUILayout.LabelField ("Crouch: No Match", errorText);
									else
										EditorGUILayout.LabelField ("Crouch: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
										EditorGUILayout.LabelField ("GroundPoint: " + (data1.data [i].result.groundPoint - data2.data [i + data2Offset].result.groundPoint), errorText);
									else
										EditorGUILayout.LabelField ("GroundPoint: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.camX != data1.data [i].result.camX)
										EditorGUILayout.LabelField ("GroundPointTime: " + (data1.data [i].result.groundPointTime - data2.data [i + data2Offset].result.groundPointTime), errorText);
									else
										EditorGUILayout.LabelField ("GroundPointTime: Match", EditorStyles.label);

									if (data2.data [i + data2Offset].result.timestamp != data1.data [i].result.timestamp)
										EditorGUILayout.LabelField ("Timestamp: " + (data1.data [i].result.timestamp - data2.data [i + data2Offset].result.timestamp), errorText);
									else
										EditorGUILayout.LabelField ("Timestamp: Match", EditorStyles.label);

									EditorGUILayout.EndVertical ();

									EditorGUILayout.EndHorizontal ();
									EditorGUI.indentLevel--;
								}

								EditorGUI.indentLevel--;
							}
						}
					}
				}
				catch(Exception e) {
					Debug.LogError (e);
					return;
				}
			}
			EditorGUI.indentLevel--;
			GUILayout.EndScrollView();
		}

		public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick, GUIStyle style)
		{
			Rect position = GUILayoutUtility.GetRect(40f, 40f, 16f, 16f, style);
			return EditorGUI.Foldout(position, foldout, content, toggleOnLabelClick, style);
		}

		public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick, GUIStyle style) {
			return Foldout(foldout, new GUIContent(content), toggleOnLabelClick, style);
		}

		public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick) {
			return Foldout(foldout, new GUIContent(content), toggleOnLabelClick, EditorStyles.foldout);
		}
	}
}
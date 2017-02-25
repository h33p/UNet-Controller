using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace GreenByteSoftware.UNetController {
	public static class Extensions {

		static long guidCount;

		public static string ResultsCompared (ref Results r1, ref Results r2) {
			return "" + r1.position + "\t|\t" + r2.position + "\t|\t" + (r1.position - r2.position) + "\n"
				+ r1.rotation.eulerAngles + "\t|\t" + r2.rotation.eulerAngles + "\t|\t" + (r1.rotation.eulerAngles - r2.rotation.eulerAngles) + "\n"
				+ r1.camX + "\t|\t" + r2.camX + "\t|\t" + (r1.camX - r2.camX) + "\n"
				+ r1.speed + "\t|\t" + r2.speed + "\t|\t" + (r1.speed - r2.speed) + "\n"
				+ r1.isGrounded + "\t\t|\t" + r2.isGrounded + "\t|\t" + (r1.isGrounded == r2.isGrounded) + "\n"
				+ r1.jumped + "\t\t|\t" + r2.jumped + "\t|\t" + (r1.jumped == r2.jumped) + "\n"
				+ r1.crouch + "\t\t|\t" + r2.crouch + "\t|\t" + (r1.crouch == r2.crouch) + "\n"
				+ r1.groundPoint + "\t|\t" + r2.groundPoint + "\t|\t" + (r1.groundPoint- r2.groundPoint) + "\n"
				+ r1.groundPointTime + "\t|\t" + r2.groundPointTime + "\t|\t" + (r1.groundPointTime - r2.groundPointTime) + "\n"
				+ r1.timestamp + "\t\t|\t" + r2.timestamp + "\n";
		}

		public static string GenerateGUID(){

			DateTime epochStart = new System.DateTime(1970, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
			double timestamp = (System.DateTime.UtcNow - epochStart).TotalSeconds;
			guidCount++;
			string uniqueID = Application.systemLanguage +"-"+Application.platform.ToString() 
				+"-"+String.Format("{0:X}", (int) timestamp)
				+"-"+String.Format("{0:X}", (int) (Time.time*1000000))
				+"-"+String.Format("{0:X}", UnityEngine.Random.Range(1000000,9999999))
				+"-"+String.Format("{0:X}", guidCount);

			return uniqueID;
		}

		public static bool AlmostEquals(this float float1, float float2, float precision)
		{
			return (Mathf.Abs(float1 - float2) <= precision);
		}

		[System.Serializable]
		public struct SVector2 {
			public float x;
			public float y;

			public SVector2 (Vector2 value) {
				x = value.x;
				y = value.y;
			}

			public SVector2 (float newX, float newY) {
				x = newX;
				y = newY;
			}

			public SVector2 (int newX, int newY) {
				x = (float)newX;
				y = (float)newY;
			}

			static public implicit operator Vector2(SVector2 value) {
				return new Vector2 (value.x, value.y);
			}

			static public implicit operator SVector2(Vector2 value) {
				return new SVector2 (value);
			}

			static public implicit operator string(SVector2 value) {
				return "( " + value.x + ", " + value.y + " )";
			}
		}

		[System.Serializable]
		public struct SVector3 {
			public float x;
			public float y;
			public float z;

			public SVector3 (Vector3 value) {
				x = value.x;
				y = value.y;
				z = value.z;
			}

			public SVector3 (float newX, float newY, float newZ) {
				x = newX;
				y = newY;
				z = newZ;
			}

			static public implicit operator Vector3(SVector3 value) {
				return new Vector3 (value.x, value.y, value.z);
			}

			static public implicit operator SVector3(Vector3 value) {
				return new SVector3 (value);
			}

			static public implicit operator string(SVector3 value) {
				return "( " + value.x + ", " + value.y + ", " + value.z + " )";
			}
		}

		[System.Serializable]
		public struct SVector4 {
			public float x;
			public float y;
			public float z;
			public float w;

			public SVector4 (Vector4 value) {
				x = value.x;
				y = value.y;
				z = value.z;
				w = value.w;
			}

			public SVector4 (float newX, float newY, float newZ, float newW) {
				x = newX;
				y = newY;
				z = newZ;
				w = newW;
			}

			public SVector4 (int newX, int newY, int newZ, int newW) {
				x = (float)newX;
				y = (float)newY;
				z = (float)newZ;
				w = (float)newW;
			}

			static public implicit operator Vector4(SVector4 value) {
				return new Vector4 (value.x, value.y, value.z, value.w);
			}

			static public implicit operator SVector4(Vector4 value) {
				return new SVector4 (value);
			}

			static public implicit operator string(SVector4 value) {
				return "( " + value.x + ", " + value.y + ", " + value.z + ", " + value.w + " )";
			}
		}

		[System.Serializable]
		public struct SQuaternion {
			public float x;
			public float y;
			public float z;
			public float w;

			public SQuaternion (Quaternion value) {
				x = value.x;
				y = value.y;
				z = value.z;
				w = value.w;
			}

			public SQuaternion (float newX, float newY, float newZ, float newW) {
				x = newX;
				y = newY;
				z = newZ;
				w = newW;
			}

			public SQuaternion (int newX, int newY, int newZ, int newW) {
				x = (float)newX;
				y = (float)newY;
				z = (float)newZ;
				w = (float)newW;
			}

			static public implicit operator Quaternion(SQuaternion value) {
				return new Quaternion (value.x, value.y, value.z, value.w);
			}

			static public implicit operator SQuaternion(Quaternion value) {
				return new SQuaternion (value);
			}

			static public implicit operator string(SQuaternion value) {
				return "( " + value.x + ", " + value.y + ", " + value.z + ", " + value.w + " )";
			}
		}

		[System.Serializable]
		public struct SInputs
		{

			public SVector2 inputs;
			public float x;
			public float y;
			public bool jump;
			public bool crouch;
			public bool sprint;
			public int timestamp;

			public SInputs (Inputs inp) {
				inputs = inp.inputs;
				x = inp.x;
				y = inp.y;
				jump = inp.jump;
				crouch = inp.crouch;
				sprint = inp.sprint;
				timestamp = inp.timestamp;
			}

		}

		[System.Serializable]
		public struct SResults
		{
			public SVector3 position;
			public SQuaternion rotation;
			public float camX;
			public SVector3 speed;
			public bool isGrounded;
			public bool jumped;
			public bool crouch;
			public float groundPoint;
			public float groundPointTime;
			public int timestamp;

			public SResults (Results res) {
				position = res.position;
				rotation = res.rotation;
				camX = res.camX;
				speed = res.speed;
				isGrounded = res.isGrounded;
				jumped = res.jumped;
				crouch = res.crouch;
				groundPoint = res.groundPoint;
				groundPointTime = res.groundPointTime;
				timestamp = res.timestamp;
			}
		}
	}
}
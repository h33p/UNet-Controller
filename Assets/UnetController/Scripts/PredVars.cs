using UnityEngine;
#if ENABLE_MIRROR
using Mirror;
#else
using UnityEngine.Networking;
#endif

namespace GreenByteSoftware.UNetController {

	public delegate void RestorePredictedVars();
#if (LONG_PREDMASK)
	public delegate void GetVarsMask(ref ulong mask);
	public delegate void ReadValue(NetworkReader reader, ulong mask, bool setValue, bool initialState);
#else
	public delegate void GetVarsMask(ref uint mask);
	public delegate void ReadValue(NetworkReader reader, uint mask, bool setValue, bool initialState);
#endif
	public delegate void WriteValue(NetworkWriter writer, bool initialState);

	//A hack needed to iterate every PredVar in the controller, also makes some other things a bit easier
	public interface IPredVarBase {
		void Initialize(Controller controller);
	}

	[System.Serializable]
	public class PredVar<T> : IPredVarBase {
		public T value {
			get { return _value; }
			set { masked = true; _value = value; }
		}

		[SerializeField]
		private T _value;
		private T netValue;
		bool initialized;
		bool masked;
#if (LONG_PREDMASK)
		ulong bitmask;
#else
		uint bitmask;
#endif

		public static implicit operator PredVar<T>(T value) {
			return new PredVar<T>(value);
		}

		public static implicit operator T(PredVar<T> value) {
			return value._value;
		}

		public T GetNetworked() {
			return netValue;
		}

		public PredVar(T val) {
			_value = val;
			netValue = val;
			initialized = false;
			masked = true;
			bitmask = 0;
		}

		public void RestoreNetworkedValue() {
			_value = netValue;
		}

		public void Initialize(Controller controller) {
			if (!initialized) {

#if (LONG_PREDMASK)
				if (controller.curPredIndex >= 64)
					throw new UnityException("The number of PredVars exceed the maximum amount of 64!");
#else
				if (controller.curPredIndex >= 32)
					throw new UnityException("The number of PredVars exceed the maximum amount of 32!");
#endif

				controller.restorePredictedVars += RestoreNetworkedValue;
				controller.readVarValues += ReadValue;
				controller.writeVarValues += WriteValue;
				controller.getVarMask += GetVarsMask;
				initialized = true;
				masked = true;
#if (LONG_PREDMASK)
				bitmask = (1ul << controller.curPredIndex);
#else
				bitmask = (1u << controller.curPredIndex);
#endif
			}
		}

		protected virtual T ReadVal(NetworkReader reader) {
			throw (new UnityException("ReadVal was called on a base class!"));
		}

#if (LONG_PREDMASK)
		public void ReadValue(NetworkReader reader, ulong mask, bool setValue, bool initialState) {
#else
		public void ReadValue(NetworkReader reader, uint mask, bool setValue, bool initialState) {
#endif
			if (initialState || (mask & bitmask) != 0)
				netValue = ReadVal(reader);
			if (setValue)
				_value = netValue;
		}

#if (LONG_PREDMASK)
		public void GetVarsMask(ref ulong mask) {
#else
		public void GetVarsMask(ref uint mask) {
#endif
			if (masked)
				mask |= bitmask;
		}

		protected virtual void WriteVal(NetworkWriter writer, T value) {
			throw (new UnityException("WriteVal was called on a base class!"));
		}

		public void WriteValue(NetworkWriter writer, bool initialState) {
			if (masked || initialState) {
				WriteVal(writer, _value);
				if (!initialState)
					masked = false;
			}
		}
	}

	[System.Serializable]
	public class PredVar_uint : PredVar<uint> {
		public PredVar_uint(uint val) : base(val) { }

		protected override uint ReadVal(NetworkReader reader) {
			return reader.ReadPackedUInt32();
		}

		protected override void WriteVal(NetworkWriter writer, uint value) {
			writer.WritePackedUInt32(value);
		}
	}

	[System.Serializable]
	public class PredVar_ulong : PredVar<ulong> {
		public PredVar_ulong() : base(0) { }
		public PredVar_ulong(ulong val) : base(val) { }

		protected override ulong ReadVal(NetworkReader reader) {
			return reader.ReadPackedUInt64();
		}

		protected override void WriteVal(NetworkWriter writer, ulong value) {
			writer.WritePackedUInt64(value);
		}
	}

	[System.Serializable]
	public class PredVar_int : PredVar<int> {
		public PredVar_int() : base(0) { }
		public PredVar_int(int val) : base(val) { }

		protected override int ReadVal(NetworkReader reader) {
			return reader.ReadInt32();
		}

		protected override void WriteVal(NetworkWriter writer, int value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_long : PredVar<long> {
		public PredVar_long() : base(0) { }
		public PredVar_long(long val) : base(val) { }

		protected override long ReadVal(NetworkReader reader) {
			return reader.ReadInt64();
		}

		protected override void WriteVal(NetworkWriter writer, long value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_float : PredVar<float> {
		public PredVar_float() : base(0) { }
		public PredVar_float(float val) : base(val) { }

		protected override float ReadVal(NetworkReader reader) {
			return reader.ReadSingle();
		}

		protected override void WriteVal(NetworkWriter writer, float value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_double : PredVar<double> {
		public PredVar_double() : base(0) { }
		public PredVar_double(double val) : base(val) { }

		protected override double ReadVal(NetworkReader reader) {
			return reader.ReadDouble();
		}

		protected override void WriteVal(NetworkWriter writer, double value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_Vector2 : PredVar<Vector2> {
		public PredVar_Vector2() : base(default(Vector2)) { }
		public PredVar_Vector2(Vector2 val) : base(val) { }

		protected override Vector2 ReadVal(NetworkReader reader) {
			return reader.ReadVector2();
		}

		protected override void WriteVal(NetworkWriter writer, Vector2 value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_Vector3 : PredVar<Vector3> {
		public PredVar_Vector3() : base(new Vector3(0, 0, 0)) { }
		public PredVar_Vector3(Vector3 val) : base(val) { }

		protected override Vector3 ReadVal(NetworkReader reader) {
			return reader.ReadVector3();
		}

		protected override void WriteVal(NetworkWriter writer, Vector3 value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_Vector4 : PredVar<Vector4> {
		public PredVar_Vector4() : base(default(Vector4)) { }
		public PredVar_Vector4(Vector4 val) : base(val) { }

		protected override Vector4 ReadVal(NetworkReader reader) {
			return reader.ReadVector4();
		}

		protected override void WriteVal(NetworkWriter writer, Vector4 value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_Quaternion : PredVar<Quaternion> {
		public PredVar_Quaternion() : base(default(Quaternion)) { }
		public PredVar_Quaternion(Quaternion val) : base(val) { }

		protected override Quaternion ReadVal(NetworkReader reader) {
			return reader.ReadQuaternion();
		}

		protected override void WriteVal(NetworkWriter writer, Quaternion value) {
			writer.Write(value);
		}
	}

	[System.Serializable]
	public class PredVar_byte : PredVar<byte> {
		public PredVar_byte() : base(0) { }
		public PredVar_byte(byte val) : base(val) { }

		protected override byte ReadVal(NetworkReader reader) {
			return reader.ReadByte();
		}

		protected override void WriteVal(NetworkWriter writer, byte value) {
			writer.Write(value);
		}
	}
}

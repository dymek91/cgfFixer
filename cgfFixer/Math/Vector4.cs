//using CryEngine.Common;

using System;
using System.Globalization;
using Vec4 = CryEngine.Vector4;
using System.Runtime.InteropServices;

namespace CryEngine
{
    [StructLayout(LayoutKind.Explicit)]
    public struct byte_array
    {
        [FieldOffset(0)]
        public byte byte1;

        [FieldOffset(1)]
        public byte byte2;

        [FieldOffset(2)]
        public byte byte3;

        [FieldOffset(3)]
        public byte byte4;

        [FieldOffset(0)]
        public uint uint1;

        [FieldOffset(0)]
        public int int1;

        [FieldOffset(0)]
        public float float1;
    }
    public class Vector4
	{

		private float _x;
		private float _y;
		private float _z;
		private float _w;

        private byte_array xBA = new byte_array();
        private byte_array yBA = new byte_array();
        private byte_array zBA = new byte_array();
        private byte_array wBA = new byte_array();

        //      public float x { get { return _x; } set { _x = value; xBA.float1 = value; } }
        //public float y { get { return _y; } set { _y = value; yBA.float1 = value; } }
        //public float z { get { return _z; } set { _z = value; zBA.float1 = value; } }
        //public float w { get { return _w; } set { _w = value; wBA.float1 = value; } }

        //public float X { get { return _x; } set { _x = value; xBA.float1 = value; } }
        //public float Y { get { return _y; } set { _y = value; yBA.float1 = value; } }
        //public float Z { get { return _z; } set { _z = value; zBA.float1 = value; } }
        //public float W { get { return _w; } set { _w = value; wBA.float1 = value; } }

        public float x { get { return xBA.float1; } set { _x = value; xBA.float1 = value; } }
        public float y { get { return yBA.float1; } set { _y = value; yBA.float1 = value; } }
        public float z { get { return zBA.float1; } set { _z = value; zBA.float1 = value; } }
        public float w { get { return wBA.float1; } set { _w = value; wBA.float1 = value; } }

        public float X { get { return xBA.float1; } set { _x = value; xBA.float1 = value; } }
        public float Y { get { return yBA.float1; } set { _y = value; yBA.float1 = value; } }
        public float Z { get { return zBA.float1; } set { _z = value; zBA.float1 = value; } }
        public float W { get { return wBA.float1; } set { _w = value; wBA.float1 = value; } }

        public int xint { get { return xBA.int1; } set { xBA.int1 = value; } }
        public int yint { get { return yBA.int1; } set { yBA.int1 = value; } }
        public int zint { get { return zBA.int1; } set { zBA.int1 = value; } }
        public int wint { get { return wBA.int1; } set {  wBA.int1 = value; } }

        public uint xuint { get { return xBA.uint1; } set { xBA.uint1 = value; } }
        public uint yuint { get { return yBA.uint1; } set { yBA.uint1 = value; } }
        public uint zuint { get { return zBA.uint1; } set { zBA.uint1 = value; } }
        public uint wuint { get { return wBA.uint1; } set { wBA.uint1 = value; } }

        public Vector4(float xCoord, float yCoord, float zCoord, float wCoord)
		{
			_x = xCoord; xBA.float1 = xCoord;
            _y = yCoord; yBA.float1 = yCoord;
            _z = zCoord; zBA.float1 = zCoord;
            _w = wCoord; wBA.float1 = wCoord;
        }

		#region Overrides
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;

				hash = hash * 23 + X.GetHashCode();
				hash = hash * 23 + Y.GetHashCode();
				hash = hash * 23 + Z.GetHashCode();
				hash = hash * 23 + W.GetHashCode();

				return hash;
			}
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			if (obj is Vector4 || obj is Vec4)
				return this == (Vector4)obj;

			return false;
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.CurrentCulture, "{0},{1},{2},{3}", X, Y, Z, W);
		}
		#endregion

		#region Conversions
		//public static implicit operator Vec4(Vector4 managedVector)
		//{
		//	return new Vec4(managedVector.X, managedVector.Y, managedVector.Z, managedVector.W);
		//}

		//public static implicit operator Vector4(Vec4 nativeVector)
		//{
		//	if(nativeVector == null)
		//	{
		//		return new Vector4();
		//	}

		//	return new Vector4(nativeVector.x, nativeVector.y, nativeVector.z, nativeVector.w);
		//}

		public static implicit operator Vector4(Vector3 vec3)
		{
			return new Vector4(vec3.x, vec3.y, vec3.z, 0);
		}
		#endregion

		#region Operators
		public static bool operator ==(Vector4 left, Vector4 right)
		{
			if ((object)right == null)
				return (object)left == null;

			return ((left.X == right.X) && (left.Y == right.Y) && (left.Z == right.Z) && (left.W == right.W));
		}

		public static bool operator !=(Vector4 left, Vector4 right)
		{
			return !(left == right);
		}
		#endregion

		#region Functions
		public bool IsZero(float epsilon = 0)
		{
			return (Math.Abs(x) <= epsilon) && (Math.Abs(y) <= epsilon) && (Math.Abs(z) <= epsilon) && (Math.Abs(w) <= epsilon);
		}
        public float Dot(Vector4 v)
        {
            return X * v.X + Y * v.Y + Z * v.Z + W * v.W;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets individual axes by index
        /// </summary>
        /// <param name="index">Index, 0 - 3 where 0 is X and 3 is W</param>
        /// <returns>The axis value</returns>
        public float this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
						return x;
					case 1:
						return y;
					case 2:
						return z;
					case 3:
						return W;

					default:
						throw new ArgumentOutOfRangeException("index", "Indices must run from 0 to 3!");
				}
			}
			set
			{
				switch (index)
				{
					case 0:
						x = value;
						break;
					case 1:
						y = value;
						break;
					case 2:
						z = value;
						break;
					case 3:
						W = value;
						break;

					default:
						throw new ArgumentOutOfRangeException("index", "Indices must run from 0 to 3!");
				}
			}
		}
		#endregion

	}
}

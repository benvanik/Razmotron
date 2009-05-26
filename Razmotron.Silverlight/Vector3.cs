using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Razmotron
{
	[StructLayout( LayoutKind.Sequential, Pack = 4 )]
	public struct Vector3
	{
		public float X;
		public float Y;
		public float Z;

		public Vector3( float x, float y, float z )
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
		}

		public readonly static Vector3 Zero = new Vector3();
		public readonly static Vector3 One = new Vector3( 1, 1, 1 );
		public readonly static Vector3 Up = new Vector3( 0, 1, 0 );

		#region Vector Math

		public float Length
		{
			get
			{
				return MathHelper.Sqrt( ( this.X * this.X ) + ( this.Y * this.Y ) + ( this.Z * this.Z ) );
			}
		}

		public float LengthSquared
		{
			get
			{
				return ( this.X * this.X ) + ( this.Y * this.Y ) + ( this.Z * this.Z );
			}
		}

		public static float DotProduct( Vector3 v1, Vector3 v2 )
		{
			return ( v1.X * v2.X ) + ( v1.Y * v2.Y ) + ( v1.Z * v2.Z );
		}

		public static float DotProduct( ref Vector3 v1, ref Vector3 v2 )
		{
			return ( v1.X * v2.X ) + ( v1.Y * v2.Y ) + ( v1.Z * v2.Z );
		}

		public static Vector3 CrossProduct( Vector3 v1, Vector3 v2 )
		{
			return new Vector3(
				v1.Y * v2.Z - v1.Z * v2.Y,
				v1.Z * v2.X - v1.X * v2.Z,
				v1.X * v2.Y - v1.Y * v2.X );
		}

		public static void CrossProduct( ref Vector3 v1, ref Vector3 v2, out Vector3 result )
		{
			result = new Vector3(
				v1.Y * v2.Z - v1.Z * v2.Y,
				v1.Z * v2.X - v1.X * v2.Z,
				//-( v1.X * v2.Z - v1.Z * v2.X ),
				v1.X * v2.Y - v1.Y * v2.X );
		}

		public void Negate()
		{
			this.X *= -1;
			this.Y *= -1;
			this.Z *= -1;
		}

		public static Vector3 operator -( Vector3 v )
		{
			return new Vector3( -v.X, -v.Y, -v.Z );
		}

		public void Normalize()
		{
			float lengthSquared = this.LengthSquared;
			float inverseSqrt = MathHelper.InvSqrt( lengthSquared );
			this.X *= inverseSqrt;
			this.Y *= inverseSqrt;
			this.Z *= inverseSqrt;
		}

		#endregion

		#region Arithmetic

		public static void Add( ref Vector3 v1, ref Vector3 v2, out Vector3 result )
		{
			result = new Vector3( v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z );
		}

		public static Vector3 operator +( Vector3 v1, Vector3 v2 )
		{
			return new Vector3( v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z );
		}

		public static void Add( ref Vector3 v1, float scalar, out Vector3 result )
		{
			result = new Vector3( v1.X + scalar, v1.Y + scalar, v1.Z + scalar );
		}

		public static Vector3 operator +( Vector3 v1, float scalar )
		{
			return new Vector3( v1.X + scalar, v1.Y + scalar, v1.Z + scalar );
		}

		public static void Subtract( ref Vector3 v1, ref Vector3 v2, out Vector3 result )
		{
			result = new Vector3( v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z );
		}

		public static Vector3 operator -( Vector3 v1, Vector3 v2 )
		{
			return new Vector3( v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z );
		}

		public static void Multiply( ref Vector3 v, float scalar, out Vector3 result )
		{
			result = new Vector3( v.X * scalar, v.Y * scalar, v.Z * scalar );
		}

		public static Vector3 operator *( float scalar, Vector3 v )
		{
			return new Vector3( v.X * scalar, v.Y * scalar, v.Z * scalar );
		}

		public static Vector3 operator *( Vector3 v, float scalar )
		{
			return new Vector3( v.X * scalar, v.Y * scalar, v.Z * scalar );
		}

		public static void Divide( ref Vector3 v, float scalar, out Vector3 result )
		{
			float div = 1.0f / scalar;
			result = new Vector3( v.X * div, v.Y * div, v.Z * div );
		}

		public static Vector3 operator /( Vector3 v, float scalar )
		{
			float div = 1.0f / scalar;
			return new Vector3( v.X * div, v.Y * div, v.Z * div );
		}

		#endregion

		#region Matrix / Quaternion

		// TODO: cleanup
		public static void Transform( ref Vector3 value, ref Quaternion rotation, out Vector3 result )
		{
			float xxx = rotation.X * ( rotation.X + rotation.X );
			float wxx = rotation.W * ( rotation.X + rotation.X );
			float xyy = rotation.X * ( rotation.Y + rotation.Y );
			float yyy = rotation.Y * ( rotation.Y + rotation.Y );
			float wyy = rotation.W * ( rotation.Y + rotation.Y );
			float xzz = rotation.X * ( rotation.Z + rotation.Z );
			float yzz = rotation.Y * ( rotation.Z + rotation.Z );
			float zzz = rotation.Z * ( rotation.Z + rotation.Z );
			float wzz = rotation.W * ( rotation.Z + rotation.Z );
			result = new Vector3();
			result.X = ( ( value.X * ( ( 1.0f - yyy ) - zzz ) ) + ( value.Y * ( xyy - wzz ) ) ) + ( value.Z * ( xzz + wyy ) );
			result.Y = ( ( value.X * ( xyy + wzz ) ) + ( value.Y * ( ( 1.0f - xxx ) - zzz ) ) ) + ( value.Z * ( yzz - wxx ) );
			result.Z = ( ( value.X * ( xzz - wyy ) ) + ( value.Y * ( yzz + wxx ) ) ) + ( value.Z * ( ( 1.0f - xxx ) - yyy ) );
		}

		#endregion

		#region Equality

		public static bool operator ==( Vector3 v1, Vector3 v2 )
		{
			return Equals( v1, v2 );
		}

		public static bool operator !=( Vector3 v1, Vector3 v2 )
		{
			return !Equals( v1, v2 );
		}

		public override int GetHashCode()
		{
			return this.X.GetHashCode() ^ this.Y.GetHashCode() ^ this.Z.GetHashCode();
		}

		public override bool Equals( object obj )
		{
			if( ( obj == null ) || !( obj is Vector3 ) )
				return false;
			else
				return Equals( this, ( Vector3 )obj );
		}

		public static bool Equals( Vector3 v1, Vector3 v2 )
		{
			return ( v1.X == v2.X ) && ( v1.Y == v2.Y ) && ( v1.Z == v2.Z );
		}

		public static bool Equals( ref Vector3 v1, ref Vector3 v2 )
		{
			return ( v1.X == v2.X ) && ( v1.Y == v2.Y ) && ( v1.Z == v2.Z );
		}

		#endregion

		public override string ToString()
		{
			return string.Format( "({0},{1},{2})", this.X, this.Y, this.Z );
		}
	}
}

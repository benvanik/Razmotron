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
	public struct Matrix
	{
		public float M11, M12, M13, M14;
		public float M21, M22, M23, M24;
		public float M31, M32, M33, M34;
		public float M41, M42, M43, M44;

		public static readonly Matrix Identity = new Matrix(
			1, 0, 0, 0,
			0, 1, 0, 0,
			0, 0, 1, 0,
			0, 0, 0, 1 );

		public Matrix(
			float m11, float m12, float m13, float m14,
			float m21, float m22, float m23, float m24,
			float m31, float m32, float m33, float m34,
			float m41, float m42, float m43, float m44 )
		{
			M11 = m11; M12 = m12; M13 = m13; M14 = m14;
			M21 = m21; M22 = m22; M23 = m23; M24 = m24;
			M31 = m31; M32 = m32; M33 = m33; M34 = m34;
			M41 = m41; M42 = m42; M43 = m43; M44 = m44;
		}

		public void Set(
			float m11, float m12, float m13, float m14,
			float m21, float m22, float m23, float m24,
			float m31, float m32, float m33, float m34,
			float m41, float m42, float m43, float m44 )
		{
			M11 = m11; M12 = m12; M13 = m13; M14 = m14;
			M21 = m21; M22 = m22; M23 = m23; M24 = m24;
			M31 = m31; M32 = m32; M33 = m33; M34 = m34;
			M41 = m41; M42 = m42; M43 = m43; M44 = m44;
		}

		public void Set( float[] value )
		{
			M11 = value[ 0 ]; M12 = value[ 4 ]; M13 = value[ 8 ]; M14 = value[ 12 ];
			M21 = value[ 1 ]; M22 = value[ 5 ]; M23 = value[ 9 ]; M24 = value[ 13 ];
			M31 = value[ 2 ]; M32 = value[ 6 ]; M33 = value[ 10 ]; M34 = value[ 14 ];
			M41 = value[ 3 ]; M42 = value[ 7 ]; M43 = value[ 11 ]; M44 = value[ 15 ];
		}

		// TODO: cleanup
		public static void CreateFromQuaternion( ref Quaternion quaternion, out Matrix result )
		{
			float num9 = quaternion.X * quaternion.X;
			float num8 = quaternion.Y * quaternion.Y;
			float num7 = quaternion.Z * quaternion.Z;
			float num6 = quaternion.X * quaternion.Y;
			float num5 = quaternion.Z * quaternion.W;
			float num4 = quaternion.Z * quaternion.X;
			float num3 = quaternion.Y * quaternion.W;
			float num2 = quaternion.Y * quaternion.Z;
			float num = quaternion.X * quaternion.W;
			result.M11 = 1f - ( 2f * ( num8 + num7 ) );
			result.M12 = 2f * ( num6 + num5 );
			result.M13 = 2f * ( num4 - num3 );
			result.M14 = 0f;
			result.M21 = 2f * ( num6 - num5 );
			result.M22 = 1f - ( 2f * ( num7 + num9 ) );
			result.M23 = 2f * ( num2 + num );
			result.M24 = 0f;
			result.M31 = 2f * ( num4 + num3 );
			result.M32 = 2f * ( num2 - num );
			result.M33 = 1f - ( 2f * ( num8 + num9 ) );
			result.M34 = 0f;
			result.M41 = 0f;
			result.M42 = 0f;
			result.M43 = 0f;
			result.M44 = 1f;
		}

		public float OffsetX
		{
			get { return M41; }
			set { M41 = value; }
		}

		public float OffsetY
		{
			get { return M42; }
			set { M42 = value; }
		}

		public float OffsetZ
		{
			get { return M43; }
			set { M43 = value; }
		}

		public bool IsIdentity
		{
			get
			{
				return M11 == 1 && M22 == 1 && M33 == 1 && M44 == 1 && M12 == 0 && M13 == 0 && M14 == 0 && M21 == 0 && M23 == 0 && M24 == 0 && M31 == 0 && M32 == 0 && M34 == 0 && M41 == 0 && M42 == 0 && M43 == 0;
			}
		}

		public bool IsAffine
		{
			get
			{
				return M14 == 0 && M24 == 0 && M34 == 0 && M44 == 1;
			}
		}

		public bool HasInverse
		{
			get
			{
				return !MathHelper.IsZero( this.Determinant );
			}
		}

		public float Determinant
		{
			get
			{
				float num1 = M33 * M44 - M34 * M43;
				float num2 = M32 * M44 - M34 * M42;
				float num3 = M31 * M44 - M34 * M41;
				float num4 = M32 * M43 - M33 * M42;
				float num5 = M31 * M43 - M33 * M41;
				float num6 = M31 * M42 - M32 * M41;
				return M11 * ( M22 * num1 - M23 * num2 + M24 * num4 ) -
					   M12 * ( M21 * num1 - M23 * num3 + M24 * num5 ) +
					   M13 * ( M21 * num2 - M22 * num3 + M24 * num6 ) -
					   M14 * ( M21 * num4 - M22 * num5 + M23 * num6 );
			}
		}

		public static Matrix Multiply( Matrix m1, Matrix m2 )
		{
			Matrix result;
			Matrix.Multiply( ref m1, ref m2, out result );
			return result;
		}

		public static Matrix Multiply( ref Matrix m1, ref Matrix m2 )
		{
			Matrix result;
			Matrix.Multiply( ref m1, ref m2, out result );
			return result;
		}

		public static void Multiply( ref Matrix m1, ref Matrix m2, out Matrix result )
		{
			if( m1.IsIdentity == true )
				result = m2;
			if( m2.IsIdentity == true )
				result = m1;
			else if( m1.IsAffine == true )
			{
				result = new Matrix();
				result.M11 = m1.M11 * m2.M11 + m1.M12 * m2.M21 + m1.M13 * m2.M31;
				result.M12 = m1.M11 * m2.M12 + m1.M12 * m2.M22 + m1.M13 * m2.M32;
				result.M13 = m1.M11 * m2.M13 + m1.M12 * m2.M23 + m1.M13 * m2.M33;
				result.M14 = m1.M11 * m2.M14 + m1.M12 * m2.M24 + m1.M13 * m2.M34;
				result.M21 = m1.M21 * m2.M11 + m1.M22 * m2.M21 + m1.M23 * m2.M31;
				result.M22 = m1.M21 * m2.M12 + m1.M22 * m2.M22 + m1.M23 * m2.M32;
				result.M23 = m1.M21 * m2.M13 + m1.M22 * m2.M23 + m1.M23 * m2.M33;
				result.M24 = m1.M21 * m2.M14 + m1.M22 * m2.M24 + m1.M23 * m2.M34;
				result.M31 = m1.M31 * m2.M11 + m1.M32 * m2.M21 + m1.M33 * m2.M31;
				result.M32 = m1.M31 * m2.M12 + m1.M32 * m2.M22 + m1.M33 * m2.M32;
				result.M33 = m1.M31 * m2.M13 + m1.M32 * m2.M23 + m1.M33 * m2.M33;
				result.M34 = m1.M31 * m2.M14 + m1.M32 * m2.M24 + m1.M33 * m2.M34;
				result.M41 = m1.M41 * m2.M11 + m1.M42 * m2.M21 + m1.M43 * m2.M31 + m1.M44 * m2.M41;
				result.M42 = m1.M41 * m2.M12 + m1.M42 * m2.M22 + m1.M43 * m2.M32 + m1.M44 * m2.M42;
				result.M43 = m1.M41 * m2.M13 + m1.M42 * m2.M23 + m1.M43 * m2.M33 + m1.M44 * m2.M43;
				result.M44 = m1.M41 * m2.M14 + m1.M42 * m2.M24 + m1.M43 * m2.M34 + m1.M44 * m2.M44;
			}
			else
			{
				result = new Matrix();
				result.M11 = m1.M11 * m2.M11 + m1.M12 * m2.M21 + m1.M13 * m2.M31 + m1.M14 * m2.M41;
				result.M12 = m1.M11 * m2.M12 + m1.M12 * m2.M22 + m1.M13 * m2.M32 + m1.M14 * m2.M42;
				result.M13 = m1.M11 * m2.M13 + m1.M12 * m2.M23 + m1.M13 * m2.M33 + m1.M14 * m2.M43;
				result.M14 = m1.M11 * m2.M14 + m1.M12 * m2.M24 + m1.M13 * m2.M34 + m1.M14 * m2.M44;
				result.M21 = m1.M21 * m2.M11 + m1.M22 * m2.M21 + m1.M23 * m2.M31 + m1.M24 * m2.M41;
				result.M22 = m1.M21 * m2.M12 + m1.M22 * m2.M22 + m1.M23 * m2.M32 + m1.M24 * m2.M42;
				result.M23 = m1.M21 * m2.M13 + m1.M22 * m2.M23 + m1.M23 * m2.M33 + m1.M24 * m2.M43;
				result.M24 = m1.M21 * m2.M14 + m1.M22 * m2.M24 + m1.M23 * m2.M34 + m1.M24 * m2.M44;
				result.M31 = m1.M31 * m2.M11 + m1.M32 * m2.M21 + m1.M33 * m2.M31 + m1.M34 * m2.M41;
				result.M32 = m1.M31 * m2.M12 + m1.M32 * m2.M22 + m1.M33 * m2.M32 + m1.M34 * m2.M42;
				result.M33 = m1.M31 * m2.M13 + m1.M32 * m2.M23 + m1.M33 * m2.M33 + m1.M34 * m2.M43;
				result.M34 = m1.M31 * m2.M14 + m1.M32 * m2.M24 + m1.M33 * m2.M34 + m1.M34 * m2.M44;
				result.M41 = m1.M41 * m2.M11 + m1.M42 * m2.M21 + m1.M43 * m2.M31 + m1.M44 * m2.M41;
				result.M42 = m1.M41 * m2.M12 + m1.M42 * m2.M22 + m1.M43 * m2.M32 + m1.M44 * m2.M42;
				result.M43 = m1.M41 * m2.M13 + m1.M42 * m2.M23 + m1.M43 * m2.M33 + m1.M44 * m2.M43;
				result.M44 = m1.M41 * m2.M14 + m1.M42 * m2.M24 + m1.M43 * m2.M34 + m1.M44 * m2.M44;
			}
		}

		public static Matrix operator *( Matrix m1, Matrix m2 )
		{
			Matrix result;
			Matrix.Multiply( ref m1, ref m2, out result );
			return result;
		}

		public static void Multiply( ref Matrix m, ref Vector4 v, out Vector4 result )
		{
			result.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31 + v.W * m.M41;
			result.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32 + v.W * m.M42;
			result.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33 + v.W * m.M43;
			result.W = v.X * m.M14 + v.Y * m.M24 + v.Z * m.M34 + v.W * m.M44;
		}

		public Vector3 Transform( Vector3 v )
		{
			Vector3 result;
			Matrix.Transform( ref this, ref v, out result );
			return result;
		}

		public static void Transform( ref Matrix m, ref Vector3 v, out Vector3 result )
		{
			result = new Vector3();
			result.X = v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31;
			result.Y = v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32;
			result.Z = v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33;
		}

		public Vector3 TransformAffine( Vector3 p )
		{
			Vector3 result;
			Matrix.TransformAffine( ref this, ref p, out result );
			return result;
		}

		public static void TransformAffine( ref Matrix m, float x, float y, float z, out Vector3 result )
		{
			result = new Vector3();
			float d = 1.0f / ( x * m.M14 + y * m.M24 + z * m.M34 + m.M44 );
			result.X = x * m.M11 + y * m.M21 + z * m.M31 + m.M41 * d;
			result.Y = x * m.M12 + y * m.M22 + z * m.M32 + m.M42 * d;
			result.Z = x * m.M13 + y * m.M23 + z * m.M33 + m.M43 * d;
		}

		public static void TransformAffine( ref Matrix m, ref Vector3 p, out Vector3 result )
		{
			// Almost never identity, so skip check
			//if( m.IsIdentity == true )
			//{
			//    result = p;
			//    return;
			//}
			result = new Vector3();
			result.X = p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.M41;
			result.Y = p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.M42;
			result.Z = p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.M43;
			//if( !m.IsAffine )
			if( !( m.M14 == 0 && m.M24 == 0 && m.M34 == 0 && m.M44 == 1 ) )
			{
				float d = 1.0f / ( p.X * m.M14 + p.Y * m.M24 + p.Z * m.M34 + m.M44 );
				result.X *= d;
				result.Y *= d;
				result.Z *= d;
			}
		}

		// Optimized version - try the normal one first!
		public static void TransformAffine( ref Matrix m, bool isAffine, ref Vector3 p, out Vector3 result )
		{
			result = new Vector3();
			result.X = p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.M41;
			result.Y = p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.M42;
			result.Z = p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.M43;
			if( !isAffine )
			{
				float d = 1.0f / ( p.X * m.M14 + p.Y * m.M24 + p.Z * m.M34 + m.M44 );
				result.X *= d;
				result.Y *= d;
				result.Z *= d;
			}
		}

		public void Scale( Vector3 v )
		{
			this.Scale( ref v );
		}

		public void Scale( ref Vector3 v )
		{
			M11 *= v.X;
			M12 *= v.X;
			M13 *= v.X;
			M14 *= v.X;
			M21 *= v.Y;
			M22 *= v.Y;
			M23 *= v.Y;
			M24 *= v.Y;
			M31 *= v.Z;
			M32 *= v.Z;
			M33 *= v.Z;
			M34 *= v.Z;
		}

		public void Transpose()
		{
			float temp = M12;
			M12 = M21;
			M21 = temp;
			temp = M13;
			M13 = M31;
			M31 = temp;
			temp = M14;
			M14 = M41;
			M41 = temp;
			temp = M23;
			M23 = M32;
			M32 = temp;
			temp = M24;
			M24 = M42;
			M42 = temp;
			temp = M34;
			M34 = M43;
			M43 = temp;
		}

		public static void Invert( ref Matrix m, out Matrix result )
		{
			result = m;
			result.Invert();
		}

		public void Invert()
		{
			if( this.IsIdentity == true )
			{
				// The matrix is the identity matrix, nothing to do
				return;
			}
			else
			{
				float num1 = M33 * M44 - M34 * M43;
				float num2 = M32 * M44 - M34 * M42;
				float num3 = M31 * M44 - M34 * M41;
				float num4 = M32 * M43 - M33 * M42;
				float num5 = M31 * M43 - M33 * M41;
				float num6 = M31 * M42 - M32 * M41;

				//float determinant = this.Determinant;
				float determinant = M11 * ( M22 * num1 - M23 * num2 + M24 * num4 ) -
									M12 * ( M21 * num1 - M23 * num3 + M24 * num5 ) +
									M13 * ( M21 * num2 - M22 * num3 + M24 * num6 ) -
									M14 * ( M21 * num4 - M22 * num5 + M23 * num6 );
				if( MathHelper.IsZero( determinant ) )
					return;

				float num7 = M33 * M44 - M34 * M43;
				float num8 = M32 * M44 - M34 * M42;
				float num9 = M31 * M44 - M34 * M41;
				float num10 = M32 * M43 - M33 * M42;
				float num11 = M31 * M43 - M33 * M41;
				float num12 = M31 * M42 - M32 * M41;

				float num13 = M23 * M44 - M24 * M43;
				float num14 = M22 * M44 - M24 * M42;
				float num15 = M21 * M44 - M24 * M41;
				float num16 = M22 * M43 - M23 * M42;
				float num17 = M21 * M43 - M23 * M41;
				float num18 = M21 * M42 - M22 * M41;

				float num19 = M23 * M34 - M24 * M33;
				float num20 = M22 * M34 - M24 * M32;
				float num21 = M21 * M34 - M24 * M31;
				float num22 = M22 * M33 - M23 * M32;
				float num23 = M21 * M33 - M23 * M31;
				float num24 = M21 * M32 - M22 * M31;

				float inverseDet = 1.0f / determinant;
				M11 = ( M22 * num1 - M23 * num2 + M24 * num4 ) * inverseDet;
				M12 = -( M21 * num1 - M23 * num3 + M24 * num5 ) * inverseDet;
				M13 = ( M21 * num2 - M22 * num3 + M24 * num6 ) * inverseDet;
				M14 = -( M21 * num4 - M22 * num5 + M23 * num6 ) * inverseDet;
				M21 = -( M12 * num7 - M13 * num8 + M14 * num10 ) * inverseDet;
				M22 = ( M11 * num7 - M13 * num9 + M14 * num11 ) * inverseDet;
				M23 = -( M11 * num8 - M12 * num9 + M14 * num12 ) * inverseDet;
				M24 = ( M11 * num10 - M12 * num11 + M13 * num12 ) * inverseDet;
				M31 = ( M12 * num13 - M13 * num14 + M14 * num16 ) * inverseDet;
				M32 = -( M11 * num13 - M13 * num15 + M14 * num17 ) * inverseDet;
				M33 = ( M11 * num14 - M12 * num15 + M14 * num18 ) * inverseDet;
				M34 = -( M11 * num16 - M12 * num17 + M13 * num18 ) * inverseDet;
				M41 = -( M12 * num19 - M13 * num20 + M14 * num22 ) * inverseDet;
				M42 = ( M11 * num19 - M13 * num21 + M14 * num23 ) * inverseDet;
				M43 = -( M11 * num20 - M12 * num21 + M14 * num24 ) * inverseDet;
				M44 = ( M11 * num22 - M12 * num23 + M13 * num24 ) * inverseDet;
			}
		}

		public void ScaleAt( Vector3 scale, Vector3 center )
		{
			this.ScaleAt( ref scale, ref center );
		}

		public void ScaleAt( ref Vector3 scale, ref Vector3 center )
		{
			if( this.IsIdentity == true )
			{
				M11 = scale.X;
				M22 = scale.Y;
				M33 = scale.Z;
				M44 = 1.0f;
				M41 = center.X - ( center.X * scale.X );
				M42 = center.Y - ( center.Y * scale.Y );
				M43 = center.Z - ( center.Z * scale.Z );
			}
			else
			{
				M11 = M11 * scale.X + M14 * center.X - M14 * scale.X * center.X;
				M12 = M12 * scale.Y + M14 * center.Y - M14 * scale.Y * center.Y;
				M13 = M13 * scale.Z + M14 * center.Z - M14 * scale.Z * center.Z;

				M21 = M21 * scale.X + M24 * center.X - M24 * scale.X * center.X;
				M22 = M22 * scale.Y + M24 * center.Y - M24 * scale.Y * center.Y;
				M23 = M23 * scale.Z + M24 * center.Z - M24 * scale.Z * center.Z;

				M31 = M31 * scale.X + M34 * center.X - M34 * scale.X * center.X;
				M32 = M32 * scale.Y + M34 * center.Y - M34 * scale.Y * center.Y;
				M33 = M33 * scale.Z + M34 * center.Z - M34 * scale.Z * center.Z;

				M41 = M41 * scale.X + M44 * center.X - M44 * scale.X * center.X;
				M42 = M42 * scale.Y + M44 * center.Y - M44 * scale.Y * center.Y;
				M43 = M43 * scale.Z + M44 * center.Z - M44 * scale.Z * center.Z;
			}
		}

		public static Matrix Translation( Vector3 v )
		{
			Matrix m = Matrix.Identity;
			m.M41 = v.X;
			m.M42 = v.Y;
			m.M43 = v.Z;
			return m;
		}

		public void Translate( Vector3 v )
		{
			this.Translate( ref v );
		}

		public void Translate( ref Vector3 v )
		{
			float m41 = M41, m42 = M42, m43 = M43, m44 = M44;
			M41 = v.X * M11 + v.Y * M21 + v.Z * M31 + m41;
			M42 = v.X * M12 + v.Y * M22 + v.Z * M32 + m42;
			M43 = v.X * M13 + v.Y * M23 + v.Z * M33 + m43;
			M44 = v.X * M14 + v.Y * M24 + v.Z * M34 + m44;
		}

		public static Matrix Rotation( Vector3 v )
		{
			// TODO: ensure this is right
			Matrix mx = Matrix.Identity;
			mx.M22 = ( float )Math.Cos( v.X );
			mx.M23 = ( float )Math.Sin( v.X );
			mx.M32 = -( float )Math.Sin( v.X );
			mx.M33 = ( float )Math.Cos( v.X );
			Matrix my = Matrix.Identity;
			my.M11 = ( float )Math.Cos( v.Y );
			my.M13 = -( float )Math.Sin( v.Y );
			my.M31 = ( float )Math.Sin( v.Y );
			my.M33 = ( float )Math.Cos( v.Y );
			Matrix mz = Matrix.Identity;
			mz.M11 = ( float )Math.Cos( v.Z );
			mz.M12 = ( float )Math.Sin( v.Z );
			mz.M21 = -( float )Math.Sin( v.Z );
			mz.M22 = ( float )Math.Cos( v.Z );
			Matrix m;
			Matrix.Multiply( ref mx, ref my, out m );
			Matrix.Multiply( ref m, ref mz, out mx );
			return mx;
		}

		#region Equality

		public static bool Equals( ref Matrix m1, ref Matrix m2 )
		{
			return ( m1.M11 == m2.M11 ) && ( m1.M12 == m2.M12 ) && ( m1.M13 == m2.M13 ) && ( m1.M14 == m2.M14 ) &&
				   ( m1.M21 == m2.M21 ) && ( m1.M22 == m2.M22 ) && ( m1.M23 == m2.M23 ) && ( m1.M24 == m2.M24 ) &&
				   ( m1.M31 == m2.M31 ) && ( m1.M32 == m2.M32 ) && ( m1.M33 == m2.M33 ) && ( m1.M34 == m2.M34 ) &&
				   ( m1.M41 == m2.M41 ) && ( m1.M42 == m2.M42 ) && ( m1.M43 == m2.M43 ) && ( m1.M44 == m2.M44 );
		}

		#endregion

		public override string ToString()
		{
			if( this.IsIdentity == true )
				return "Identity";
			else
			{
				return String.Format(
					"{0},{1},{2},{3}{16} " +
					"{4},{5},{6},{7}{16} " +
					"{8},{9},{10},{11}{16} " +
					"{12},{13},{14},{15} ",
					M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, M41, M42, M43, M44, Environment.NewLine );
			}
		}
	}
}

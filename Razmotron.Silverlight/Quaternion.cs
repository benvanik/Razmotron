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
	public struct Quaternion
	{
		public float X;
		public float Y;
		public float Z;
		public float W;

		public readonly static Quaternion Identity = new Quaternion( 0, 0, 0, 1 );

		public Quaternion( float x, float y, float z, float w )
		{
			this.X = x;
			this.Y = y;
			this.Z = z;
			this.W = w;
		}

		public Quaternion( ref Vector3 v, float scalar )
		{
			this.X = v.X;
			this.Y = v.Y;
			this.Z = v.Z;
			this.W = scalar;
		}

		public float Length
		{
			get
			{
				return MathHelper.Sqrt( ( this.X * this.X ) + ( this.Y * this.Y ) + ( this.Z * this.Z ) + ( this.W * this.W ) );
			}
		}

		public float LengthSquared
		{
			get
			{
				return ( this.X * this.X ) + ( this.Y * this.Y ) + ( this.Z * this.Z ) + ( this.W * this.W );
			}
		}

		public static void CreateFromYawPitchRoll( float yaw, float pitch, float roll, out Quaternion result )
		{
			float rsin = ( float )Math.Sin( roll / 2.0f );
			float rcos = ( float )Math.Cos( roll / 2.0f );
			float psin = ( float )Math.Sin( pitch / 2.0f );
			float pcos = ( float )Math.Cos( pitch / 2.0f );
			float ysin = ( float )Math.Sin( yaw / 2.0f );
			float ycos = ( float )Math.Cos( yaw / 2.0f );
			result.X = ( ( ycos * psin ) * rcos ) + ( ( ysin * pcos ) * rsin );
			result.Y = ( ( ysin * pcos ) * rcos ) - ( ( ycos * psin ) * rsin );
			result.Z = ( ( ycos * pcos ) * rsin ) - ( ( ysin * psin ) * rcos );
			result.W = ( ( ycos * pcos ) * rcos ) + ( ( ysin * psin ) * rsin );
		}
	}
}

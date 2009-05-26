using System;
using System.Net;
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
	public static class MathHelper
	{
		public const float E = 2.718282f;
		public const float Log2E = 1.442695f;
		public const float Log10E = 0.4342945f;
		public const float Pi = 3.141593f;
		public const float PiOver2 = 1.570796f;
		public const float PiOver4 = 0.7853982f;
		public const float TwoPi = 6.283185f;
		public const float Deg2Rad = Pi / 180.0f;

		public const float Epsilon = 1.0e-6f;

		public static bool IsZero( float value )
		{
			return Math.Abs( value ) < Epsilon;
		}

		public static bool AreEqual( float x, float y )
		{
			return Math.Abs( x - y ) < Epsilon;
		}

		public static float Sqrt( float value )
		{
			// TODO: fast sqrt
			return ( float )Math.Sqrt( value );
		}

		public static float InvSqrt( float value )
		{
			// TODO: fast sqrt
			return ( 1.0f / ( float )Math.Sqrt( value ) );
		}

		public static int Clamp( int value, int min, int max )
		{
			return Math.Min( Math.Max( value, min ), max );
		}

		public static float Clamp( float value, float min, float max )
		{
			return Math.Min( Math.Max( value, min ), max );
		}

		public static double Clamp( double value, double min, double max )
		{
			return Math.Min( Math.Max( value, min ), max );
		}

		public static Vector3 Clamp( Vector3 value, Vector3 min, Vector3 max )
		{
			return new Vector3(
				Math.Min( Math.Max( value.X, min.X ), max.X ),
				Math.Min( Math.Max( value.Y, min.Y ), max.Y ),
				Math.Min( Math.Max( value.Z, min.Z ), max.Z )
				);
		}

		public static void Clamp( ref Vector3 value, ref Vector3 min, ref Vector3 max, out Vector3 result )
		{
			result = new Vector3();
			result.X = Math.Min( Math.Max( value.X, min.X ), max.X );
			result.Y = Math.Min( Math.Max( value.Y, min.Y ), max.Y );
			result.Z = Math.Min( Math.Max( value.Z, min.Z ), max.Z );
		}
	}

}

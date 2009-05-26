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
	public partial class GL
	{
		public void gluOrtho2Df( float left, float right, float bottom, float top )
		{
			glOrthof( left, right, bottom, top, -1, 1 );
		}

		public void gluPerspectivef( float fovy, float aspect, float zNear, float zFar )
		{
			float radians = fovy / 2.0f * MathHelper.Pi / 180.0f;
			float deltaZ = zFar - zNear;
			float sine = ( float )Math.Sin( radians );
			if( ( deltaZ == 0 ) || ( sine == 0 ) || ( aspect == 0 ) )
			{
				return;
			}
			float cotangent = ( float )Math.Cos( radians ) / sine;

			Matrix m = Matrix.Identity;
			m.M11 = cotangent / aspect;
			m.M22 = cotangent;
			m.M33 = -( zFar - zNear ) / deltaZ;
			m.M34 = -1;
			m.M43 = -( 2.0f * zNear * zFar ) / deltaZ;
			m.M44 = 0;
			glMultMatrixf( ref m );
		}

		public void gluLookAtf( float eyex, float eyey, float eyez, float centerx, float centery, float centerz, float upx, float upy, float upz )
		{
			Vector3 forward = new Vector3()
			{
				X = centerx - eyex,
				Y = centery - eyey,
				Z = centerz - eyez,
			};
			forward.Normalize();
			Vector3 up = new Vector3()
			{
				X = upx,
				Y = upy,
				Z = upz,
			};

			// side = forward x up
			Vector3 side;
			Vector3.CrossProduct( ref forward, ref up, out side );
			side.Normalize();

			// up = side x forward
			Vector3.CrossProduct( ref side, ref forward, out up );

			Matrix m = Matrix.Identity;
			m.M11 = side.X;
			m.M21 = side.Y;
			m.M31 = side.Z;
			m.M12 = up.X;
			m.M22 = up.Y;
			m.M32 = up.Z;
			m.M13 = -forward.X;
			m.M23 = -forward.Y;
			m.M33 = -forward.Z;
			glMultMatrixf( ref m );
			glTranslatef( -eyex, -eyey, -eyez );
		}

		public bool gluProjectf( float objx, float objy, float objz, ref Matrix modelMatrix, ref Matrix projMatrix, int[] viewport, out float winx, out float winy, out float winz )
		{
			Vector4 vin = new Vector4()
			{
				X = objx,
				Y = objy,
				Z = objz,
				W = 1.0f
			};
			Vector4 vout;
			Matrix.Multiply( ref modelMatrix, ref vin, out vout );
			Matrix.Multiply( ref projMatrix, ref vout, out vin );
			if( MathHelper.AreEqual( vin.W, 0.0f ) == true )
			{
				winx = winy = winz = 0.0f;
				return false;
			}
			vin.X /= vin.W; vin.Y /= vin.W; vin.Z /= vin.W;
			// Make [0,1]
			vin.X = ( vin.X * 0.5f ) + 0.5f;
			vin.Y = ( vin.Y * 0.5f ) + 0.5f;
			vin.Z = ( vin.Z * 0.5f ) + 0.5f;
			// Put in viewport space
			winx = vin.X * viewport[ 2 ] + viewport[ 0 ];
			winy = vin.Y * viewport[ 3 ] + viewport[ 1 ];
			winz = vin.Z;
			return true;
		}

		public bool gluUnProjectf( float winx, float winy, float winz, ref Matrix modelMatrix, ref Matrix projMatrix, int[] viewport, out float objx, out float objy, out float objz )
		{
			Matrix m;
			Matrix.Multiply( ref modelMatrix, ref projMatrix, out m );
			m.Invert();

			// Take from viewport space
			Vector4 vin = new Vector4()
			{
				X = ( winx - viewport[ 0 ] ) / viewport[ 2 ],
				Y = ( winy - viewport[ 1 ] ) / viewport[ 3 ],
				Z = winz,
				W = 1.0f
			};
			// Make [-1,1]
			vin.X = ( vin.X * 2.0f ) - 1.0f;
			vin.Y = ( vin.Y * 2.0f ) - 1.0f;
			vin.Z = ( vin.Z * 2.0f ) - 1.0f;

			Vector4 vout;
			Matrix.Multiply( ref m, ref vin, out vout );
			if( vout.W == 0.0f )
			{
				objx = objy = objz = 0.0f;
				return false;
			}
			objx = vout.X / vout.W;
			objy = vout.Y / vout.W;
			objz = vout.Z / vout.W;
			return true;
		}

		public bool gluUnProject4f( float winx, float winy, float winz, float clipw, ref Matrix modelMatrix, ref Matrix projMatrix, int[] viewport, float near, float far, out    float objx, out float objy, out float objz, out float objw )
		{
			Matrix m;
			Matrix.Multiply( ref modelMatrix, ref projMatrix, out m );
			m.Invert();

			// Take from viewport space
			Vector4 vin = new Vector4()
			{
				X = ( winx - viewport[ 0 ] ) / viewport[ 2 ],
				Y = ( winy - viewport[ 1 ] ) / viewport[ 3 ],
				Z = ( winz - near ) / ( far - near ),
				W = clipw
			};
			// Make [-1,1]
			vin.X = ( vin.X * 2.0f ) - 1.0f;
			vin.Y = ( vin.Y * 2.0f ) - 1.0f;
			vin.Z = ( vin.Z * 2.0f ) - 1.0f;

			Vector4 vout;
			Matrix.Multiply( ref m, ref vin, out vout );
			if( vout.W == 0.0f )
			{
				objx = objy = objz = objw = 0.0f;
				return false;
			}
			objx = vout.X;
			objy = vout.Y;
			objz = vout.Z;
			objw = vout.W;
			return true;
		}

		public void gluPickMatrixf( float x, float y, float deltax, float deltay, int[] viewport )
		{
			if( ( deltax <= 0 ) || ( deltay <= 0 ) )
				return;
			glTranslatef(
				( viewport[ 2 ] - 2 * ( x - viewport[ 0 ] ) ) / deltax,
				( viewport[ 3 ] - 2 * ( y - viewport[ 1 ] ) ) / deltay,
				0.0f );
			glScalef( viewport[ 2 ] / deltax, viewport[ 3 ] / deltay, 1.0f );
		}
	}
}

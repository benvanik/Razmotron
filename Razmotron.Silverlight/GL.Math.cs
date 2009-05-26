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
		internal int Viewport_X, Viewport_Y, Viewport_Width, Viewport_Height;
		internal int Viewport_Right, Viewport_Bottom;
		internal float Viewport_Near = 0.0f, Viewport_Far = 1.0f;
		internal int Scissor_X, Scissor_Y, Scissor_Width, Scissor_Height;
		internal int State_DepthFunc;
		internal bool State_DepthMask;
		internal float State_ClearDepth = 1.0f;
		private const float MaximumDepth = ushort.MaxValue;

		internal int State_MatrixMode = GL_MODELVIEW;
		// SX = M11 SY = M22 SZ = M33
		// TX = M14 TY = M24 TZ = M34
		internal Matrix M_Viewport;
		private const int MatrixStackCount = 16;
		internal Matrix[] M_Proj = new Matrix[ MatrixStackCount ];
		internal int M_ProjIndex;
		internal Matrix[] M_ModelView = new Matrix[ MatrixStackCount ];
		internal int M_ModelViewIndex;
		internal Matrix[] M_Texture = new Matrix[ MatrixStackCount ];
		internal int M_TextureIndex;

		private void SetupMath()
		{
			this.BuildViewportMatrix();
			for( int n = 0; n < MatrixStackCount; n++ )
			{
				M_Proj[ n ] = Matrix.Identity;
				M_ModelView[ n ] = Matrix.Identity;
				M_Texture[ n ] = Matrix.Identity;
			}
		}

		public void glDepthFunc( int func ) { State_DepthFunc = func; }
		public void glDepthMask( bool flag ) { State_DepthMask = flag; }
		public void glDepthRangef( float zNear, float zFar )
		{
			Viewport_Near = zNear;
			Viewport_Far = zFar;
			this.BuildViewportMatrix();
		}
		public void glClearDepthf( float depth ) { State_ClearDepth = depth; }

		public void glViewport( int x, int y, int width, int height )
		{
			Viewport_X = x;
			Viewport_Y = y;
			Viewport_Width = width;
			Viewport_Height = height;
			Viewport_Right = x + width;
			Viewport_Bottom = y + height;
			this.BuildViewportMatrix();
		}

		internal void BuildViewportMatrix()
		{
			M_Viewport = new Matrix();
			M_Viewport.M11 = Viewport_Width / 2.0f;
			M_Viewport.M14 = M_Viewport.M11 + Viewport_X;
			M_Viewport.M22 = Viewport_Height / 2.0f;
			M_Viewport.M24 = M_Viewport.M22 + Viewport_Y;
			M_Viewport.M33 = MaximumDepth * ( ( Viewport_Far - Viewport_Near ) / 2.0f );
			M_Viewport.M34 = MaximumDepth * ( ( Viewport_Far - Viewport_Near ) / 2.0f + Viewport_Near );
		}

		public void glScissor( int x, int y, int width, int height )
		{
			Scissor_X = x;
			Scissor_Y = y;
			Scissor_Width = width;
			Scissor_Height = height;
		}

		public void glClipPlanef( int plane, float[/*4*/] equation )
		{
		}

		public void glGetClipPlanef( int plane, out float[/*4*/] eqn )
		{
			eqn = new float[ 4 ];
		}

		public void glMatrixMode( int mode )
		{
			State_MatrixMode = mode;
		}

		public void glFrustumf( float left, float right, float bottom, float top, float zNear, float zFar )
		{
			Matrix m;
			float x = ( 2.0f * zNear ) / ( right - left );
			float y = ( 2.0f * zNear ) / ( top - bottom );
			float a = ( right + left ) / ( right - left );
			float b = ( top + bottom ) / ( top - bottom );
			float c = -( zFar + zNear ) / ( zFar - zNear );
			float d = -( 2.0f * zFar * zNear ) / ( zFar - zNear );  /* error? */
			m.M11 = x; m.M12 = 0; m.M13 = a; m.M14 = 0;
			m.M21 = 0; m.M22 = y; m.M23 = b; m.M24 = 0;
			m.M31 = 0; m.M32 = 0; m.M33 = c; m.M34 = d;
			m.M41 = 0; m.M42 = 0; m.M43 = -1; m.M44 = 0;
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ] = m;
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ] = m;
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ] = m;
					break;
			}
		}

		public void glOrthof( float left, float right, float bottom, float top, float zNear, float zFar )
		{
			Matrix m;
			m.M11 = 2.0f / ( right - left );
			m.M12 = 0.0f;
			m.M13 = 0.0f;
			m.M14 = -( right + left ) / ( right - left );
			m.M21 = 0.0f;
			m.M22 = 2.0f / ( top - bottom );
			m.M23 = 0.0f;
			m.M24 = -( top + bottom ) / ( top - bottom );
			m.M31 = 0.0f;
			m.M32 = 0.0f;
			m.M33 = -2.0f / ( zFar - zNear );
			m.M34 = -( zFar + zNear ) / ( zFar - zNear );
			m.M41 = 0.0f;
			m.M42 = 0.0f;
			m.M43 = 0.0f;
			m.M44 = 1.0f;
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ] = m;
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ] = m;
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ] = m;
					break;
			}
		}

		public void glLoadIdentity()
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ] = Matrix.Identity;
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ] = Matrix.Identity;
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ] = Matrix.Identity;
					break;
			}
		}

		public void glLoadMatrixf( float[] m )
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ].Set( m );
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ].Set( m );
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ].Set( m );
					break;
			}
		}

		public void glLoadMatrixf( ref Matrix m )
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ] = m;
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ] = m;
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ] = m;
					break;
			}
		}

		public void glPushMatrix()
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex + 1 ] = M_Proj[ M_ProjIndex ];
					M_ProjIndex++;
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex + 1 ] = M_ModelView[ M_ModelViewIndex ];
					M_ModelViewIndex++;
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex + 1 ] = M_Texture[ M_TextureIndex ];
					M_TextureIndex++;
					break;
			}
		}

		public void glPopMatrix()
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_ProjIndex--;
					break;
				case GL_MODELVIEW:
					M_ModelViewIndex--;
					break;
				case GL_TEXTURE:
					M_TextureIndex--;
					break;
			}
		}

		public void glMultMatrixf( float[] m )
		{
			Matrix mat = new Matrix();
			mat.Set( m );
			glMultMatrixf( ref mat );
		}

		public void glMultMatrixf( ref Matrix m )
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ] = Matrix.Multiply( ref m, ref M_Proj[ M_ProjIndex ] );
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ] = Matrix.Multiply( ref m, ref M_ModelView[ M_ModelViewIndex ] );
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ] = Matrix.Multiply( ref m, ref M_Texture[ M_TextureIndex ] );
					break;
			}
		}

		public void glRotatef( float angle, ref Vector3 v )
		{
			glRotatef( angle, v.X, v.Y, v.Z );
		}

		public void glRotatef( float angle, float x, float y, float z )
		{
			if( angle == 0.0f )
				return;

			// Normalize
			float inverseSqrt = MathHelper.InvSqrt( ( x * x ) + ( y * y ) + ( z * z ) );
			x *= inverseSqrt;
			y *= inverseSqrt;
			z *= inverseSqrt;

			angle = angle * MathHelper.Deg2Rad;
			float s = ( float )Math.Sin( angle );
			float c = ( float )Math.Cos( angle );
			float ab = x * y * ( 1 - c );
			float bc = y * z * ( 1 - c );
			float ca = z * x * ( 1 - c );

			Matrix m = Matrix.Identity;
			float t = x * x;
			m.M11 = t + c * ( 1 - t );
			m.M32 = bc - x * s;
			m.M23 = bc + x * s;

			t = y * y;
			m.M22 = t + c * ( 1 - t );
			m.M31 = ca + y * s;
			m.M13 = ca - y * s;

			t = z * z;
			m.M33 = t + c * ( 1 - t );
			m.M21 = ab - z * s;
			m.M12 = ab + z * s;

			glMultMatrixf( ref m );
		}

		public void glRotate( ref Quaternion q )
		{
			Matrix m;
			Matrix.CreateFromQuaternion( ref q, out m );
			glMultMatrixf( ref m );
		}

		public void glScalef( float x, float y, float z )
		{
			Vector3 v = new Vector3( x, y, z );
			glScalef( ref v );
		}

		public void glScalef( ref Vector3 v )
		{
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ].Scale( ref v );
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ].Scale( ref v );
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ].Scale( ref v );
					break;
			}
		}

		public void glTranslatef( float x, float y, float z )
		{
			Vector3 v = new Vector3( x, y, z );
			glTranslatef( ref v );
		}

		public void glTranslatef( ref Vector3 v )
		{
			// TODO: optimize
			switch( State_MatrixMode )
			{
				case GL_PROJECTION:
					M_Proj[ M_ProjIndex ].Translate( ref v );
					break;
				case GL_MODELVIEW:
					M_ModelView[ M_ModelViewIndex ].Translate( ref v );
					break;
				case GL_TEXTURE:
					M_Texture[ M_TextureIndex ].Translate( ref v );
					break;
			}
		}

		// Fixed
#if false
		public void glClipPlanex( int plane, GLfixed[/*4*/] equation ) { }
		public void glGetClipPlanex( int pname, out GLfixed[/*4*/] eqn ) { eqn = new GLfixed[ 4 ]; }
		public void glFrustumx( GLfixed left, GLfixed right, GLfixed bottom, GLfixed top, GLfixed zNear, GLfixed zFar ) { }
		public void glLoadMatrixx( GLfixed[] m ) { }
		public void glMultMatrixx( GLfixed[] m ) { }
		public void glOrthox( GLfixed left, GLfixed right, GLfixed bottom, GLfixed top, GLfixed zNear, GLfixed zFar ) { }
		public void glRotatex( GLfixed angle, GLfixed x, GLfixed y, GLfixed z ) { }
		public void glScalex( GLfixed x, GLfixed y, GLfixed z ) { }
		public void glTranslatex( GLfixed x, GLfixed y, GLfixed z ) { }
#endif
	}
}

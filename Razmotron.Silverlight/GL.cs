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
using Razmotron.Rasterization;

namespace Razmotron
{
	public partial class GL
	{
		public RasterBuffer RasterBuffer;
		internal float[] State_ClearColor = new float[] { 0, 0, 0, 1 };

		public GL( RasterBuffer rasterBuffer )
		{
			RasterBuffer = rasterBuffer;
			this.SetupMath();
			this.SetupDrawing();
		}

		#region Primary Info / Error States

		internal bool HasError;
		internal int Error = GL_NO_ERROR;

		public string glGetString( int name )
		{
			switch( name )
			{
				case GL_VENDOR:
					return "Ben Vanik (noxa)";
				case GL_RENDERER:
					return "Razmotron";
				case GL_VERSION:
					return "OpenGL ES-CM 1.1";
				case GL_EXTENSIONS:
					return "OES_single_precision OES_point_size_array OES_point_sprite";
				default:
					return null;
			}
		}

		public int glGetError()
		{
			// Allow more errors
			HasError = false;
			return this.Error;
		}

		internal void glSetError( int error )
		{
			// Spec says (I think) that only the first error is recorded until a glGetError calls is made
			if( HasError == true )
				Error = error;
		}

		#endregion

		#region Getters
#if false
		public void glGetBooleanv( int pname, bool[] params_ ) { }
		public void glGetFloatv( int pname, float[] params_ ) { }
		public void glGetIntegerv( int pname, int[] params_ ) { }
		public void glGetPointerv( int pname, object params_ )
		{
			switch( pname )
			{
				case GL_VERTEX_ARRAY_POINTER:
				case GL_NORMAL_ARRAY_POINTER:
				case GL_COLOR_ARRAY_POINTER:
				case GL_TEXTURE_COORD_ARRAY_POINTER:
				case GL_POINT_SIZE_ARRAY_POINTER_OES:
					break;
			}
		}
#endif
		#endregion

		public void glClear( uint mask )
		{
			if( ( mask & GL_COLOR_BUFFER_BIT ) != 0 )
			{
				RasterBuffer.Clear( Color.FromArgb(
					( byte )MathHelper.Clamp( State_ClearColor[ 3 ] * byte.MaxValue, byte.MinValue, byte.MaxValue ),
					( byte )MathHelper.Clamp( State_ClearColor[ 0 ] * byte.MaxValue, byte.MinValue, byte.MaxValue ),
					( byte )MathHelper.Clamp( State_ClearColor[ 1 ] * byte.MaxValue, byte.MinValue, byte.MaxValue ),
					( byte )MathHelper.Clamp( State_ClearColor[ 2 ] * byte.MaxValue, byte.MinValue, byte.MaxValue ) ) );
			}
			if( ( mask & GL_DEPTH_BUFFER_BIT ) != 0 )
				RasterBuffer.ClearZBuffer( State_ClearDepth );
			if( ( mask & GL_STENCIL_BUFFER_BIT ) != 0 )
			{
				// ?
			}
		}

		public void glFlush()
		{
			// ?
		}

		public void glFinish()
		{
			// ?
		}

		internal bool State_CullFace = false;
		internal int State_CullFaceMode = GL_BACK;
		internal int State_LogicOp;
		internal int State_FrontFace = GL_CCW;
		internal int State_ShadeModel = GL_SMOOTH;
		internal int State_PerspectiveCorrection = GL_NICEST;
		internal uint State_ColorMask; // RGBAb
		internal int State_AlphaFunc;
		internal float State_AlphaRef;
		internal int State_BlendFactorS;
		internal int State_BlendFactorD;

		private void EnableDisable( int cap, bool value )
		{
			switch( cap )
			{
				case GL_POINT_SMOOTH:
				case GL_POINT_SPRITE_OES:
					break;
				case GL_LINE_SMOOTH:
					break;
				case GL_CULL_FACE:
					State_CullFace = value;
					break;
				case GL_POLYGON_OFFSET_FILL:
					break;
			}
		}

		public void glEnable( int cap )
		{
			this.EnableDisable( cap, true );
		}
		public void glDisable( int cap )
		{
			this.EnableDisable( cap, false );
		}
		public bool glIsEnabled( int cap )
		{
			return false;
		}

		public void glCullFace( int mode ) { State_CullFaceMode = mode; }
		public void glLogicOp( int opcode ) { State_LogicOp = opcode; }
		public void glFrontFace( int mode ) { State_FrontFace = mode; }
		public void glShadeModel( int mode ) { State_ShadeModel = mode; }
		public void glHint( int target, int mode )
		{
			switch( target )
			{
				case GL_PERSPECTIVE_CORRECTION_HINT:
					State_PerspectiveCorrection = mode;
					break;
				case GL_POINT_SMOOTH_HINT:
				case GL_LINE_SMOOTH_HINT:
				case GL_FOG_HINT:
				case GL_GENERATE_MIPMAP_HINT:
					// TODO
					break;
			}
		}
		public void glColorMask( uint red, uint green, uint blue, uint alpha ) { State_ColorMask = ( red << 3 ) | ( green << 2 ) | ( blue << 1 ) | alpha; }
		public void glAlphaFunc( int func, float ref_ ) { State_AlphaFunc = func; State_AlphaRef = ref_; }
		public void glBlendFunc( int sfactor, int dfactor ) { State_BlendFactorS = sfactor; State_BlendFactorD = dfactor; }

		public void glClearColor( float red, float green, float blue, float alpha )
		{
			State_ClearColor[ 0 ] = red;
			State_ClearColor[ 1 ] = green;
			State_ClearColor[ 2 ] = blue;
			State_ClearColor[ 3 ] = alpha;
		}

		public void glReadPixels( int x, int y, int width, int height, int format, int type, byte[] pixels ) { }

#if false
		public void glStencilFunc( int func, int ref_, uint mask ) { }
		public void glStencilMask( uint mask ) { }
		public void glStencilOp( int fail, int zfail, int zpass ) { }
		public void glClearStencil( int s ) { }
#endif



#if false
		public void glAlphaFuncx( int func, GLclampx ref_ ) { }
		public void glClearColorx( GLclampx red, GLclampx green, GLclampx blue, GLclampx alpha ) { }
		public void glDepthRangex( GLclampx zNear, GLclampx zFar ) { }
		public void glClearDepthx( GLclampx depth ) { }
		public void glGetFixedv( int pname, GLfixed[] params_ ) { }
#endif

#if false

		public void glFogf( int pname, float param ) { }
		public void glFogfv( int pname, float[] params_ ) { }
		public void glFogx( int pname, GLfixed param ) { }
		public void glFogxv( int pname, GLfixed[] params_ ) { }

		public void glLightModelf( int pname, float param ) { }
		public void glLightModelfv( int pname, float[] params_ ) { }
		public void glLightf( int light, int pname, float param ) { }
		public void glLightfv( int light, int pname, float[] params_ ) { }
		public void glGetLightfv( int light, int pname, float[] params_ ) { }
		public void glLightModelx( int pname, GLfixed param ) { }
		public void glLightModelxv( int pname, GLfixed[] params_ ) { }
		public void glLightx( int light, int pname, GLfixed param ) { }
		public void glLightxv( int light, int pname, GLfixed[] params_ ) { }
		public void glGetLightxv( int light, int pname, GLfixed[] params_ ) { }

		public void glLineWidth( float width ) { }
		public void glLineWidthx( GLfixed width ) { }

		public void glMaterialf( int face, int pname, float param ) { }
		public void glMaterialfv( int face, int pname, float[] params_ ) { }
		public void glGetMaterialfv( int face, int pname, float[] params_ ) { }
		public void glMaterialx( int face, int pname, GLfixed param ) { }
		public void glMaterialxv( int face, int pname, GLfixed[] params_ ) { }
		public void glGetMaterialxv( int face, int pname, GLfixed[] params_ ) { }

		public void glPolygonOffset( float factor, float units ) { }
		public void glPolygonOffsetx( GLfixed factor, GLfixed units ) { }

		public void glSampleCoverage( float value, bool invert ) { }
		public void glSampleCoveragex( GLclampx value, bool invert ) { }

#endif

		public void glTexEnvf( int target, int pname, float param ) { }
		public void glTexEnvi( int target, int pname, int param ) { }
		public void glTexEnvfv( int target, int pname, float[] params_ ) { }
		public void glTexEnviv( int target, int pname, int[] params_ ) { }
		public void glGetTexEnvfv( int env, int pname, float[] params_ ) { }
		public void glGetTexEnviv( int env, int pname, int[] params_ ) { }
#if false
		public void glTexEnvx( int target, int pname, GLfixed param ) { }
		public void glTexEnvxv( int target, int pname, GLfixed[] params_ ) { }
		public void glGetTexEnvxv( int env, int pname, GLfixed[] params_ ) { }
#endif
	}
}

//#define ALPHA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Razmotron.Rasterization;

namespace Razmotron.Test.Sample3
{
	public partial class Page : UserControl
	{
		private Camera _camera;
		private bool _isScaling = false;
		private GL gl;

		private DateTime _lastFpsTime;
		private int _frameAccum;

		public Page()
		{
			InitializeComponent();

			this.Loaded += new RoutedEventHandler( Page_Loaded );
		}

		private void Page_Loaded( object sender, RoutedEventArgs e )
		{
#if ALPHA
			this.Surface.ColorMode = ColorMode.RGBA;
#else
			this.Surface.ColorMode = ColorMode.RGB;
#endif

			_isScaling = true;
			this.EnableScaling();

			_camera = new Camera();
			gl = new GL( this.Surface.RasterBuffer );
			gl.glClearColor( 0.0f, 0.0f, 0.0f, 0.0f );
			this.MakeTexture();

			CompositionTarget.Rendering += new EventHandler( CompositionTarget_Rendering );
		}

		private void CompositionTarget_Rendering( object sender, EventArgs e )
		{
			this.UpdateOSD();

			if( gl.RasterBuffer == null )
				gl.RasterBuffer = this.Surface.RasterBuffer;
			this.Surface.BeginUpdate();

			this.RenderCore();

			this.Surface.EndUpdate();
		}

		private void UpdateOSD()
		{
			_frameAccum++;
			DateTime now = DateTime.Now;
			double delta = ( now - _lastFpsTime ).TotalSeconds;
			if( delta >= 1.0 )
			{
				double fps = _frameAccum / delta;
				_lastFpsTime = now;
				_frameAccum = 0;
				fpsTextBlock.Text = string.Format( "FPS: {0}", ( int )Math.Round( fps ) );
			}
		}

		private float _rotation;
		private float[] _vertices = new float[]{
		    -1,-1,0,
		    1,-1,0,
		    0,1,0,
		};
		private float[] _colors = new float[]{
		    1,0,0,1,
		    0,1,0,1,
		    0,0,1,1,
		};
		private float[] _texCoords = new float[]{
			0,0,
			1,0,
			0.5f,1,
		};

		private void SetPerspective( float fovy, float aspect, float zNear, float zFar )
		{
			float xmin, xmax, ymin, ymax;

			ymax = zNear * ( float )Math.Tan( fovy * MathHelper.Deg2Rad / 2.0f );
			ymin = -ymax;
			xmin = ymin * aspect;
			xmax = ymax * aspect;

			ymin *= 2;
			ymax *= 2;

			gl.glFrustumf( xmin, xmax, -ymin, -ymax, zNear, zFar );
		}

		private uint textureId;
		private const int TextureSize = 256;

		private void MakeTexture()
		{
			uint[] newIds = new uint[ 1 ];
			gl.glGenTextures( newIds );
			textureId = newIds[ 0 ];

			gl.glBindTexture( GL.GL_TEXTURE_2D, textureId );

			byte[] data = new byte[ TextureSize * TextureSize * 3 ];
			for( int y = 0; y < TextureSize; y++ )
			{
				if( ( ( y % 10 ) - 5 ) < 0 )
					continue;
				for( int x = 0; x < TextureSize; x++ )
				{
					byte b = ( byte )( ( ( ( x % 10 ) - 5 ) < 0 ) ? 255 : 0 );
					data[ ( y * TextureSize * 3 ) + ( x * 3 ) + 0 ] = b;
					data[ ( y * TextureSize * 3 ) + ( x * 3 ) + 1 ] = 0;
					data[ ( y * TextureSize * 3 ) + ( x * 3 ) + 2 ] = 0;
				}
			}

			gl.glTexImage2D( GL.GL_TEXTURE_2D, 0, GL.GL_RGB, TextureSize, TextureSize, 0, GL.GL_RGB, GL.GL_UNSIGNED_BYTE, data );
		}

		private float tex_tx = 0.0f;
		private float tex_ty = 0.0f;
		private float tex_sx = 1.0f;
		private float tex_sy = 1.0f;
		private float tex_rx = 0.0f;
		private float tex_ry = 0.0f;
		private float tex_rz = 0.0f;

		private void RenderCore()
		{
			float aspectRatio = this.Surface.RasterBuffer.Width / ( float )this.Surface.RasterBuffer.Height;
			_camera.SetPerspective( MathHelper.PiOver4, aspectRatio );
			_camera.Calculate();

			gl.glViewport( 0, 0, this.Surface.RasterBuffer.Width, this.Surface.RasterBuffer.Height );
			gl.glMatrixMode( GL.GL_PROJECTION );
			gl.glLoadIdentity();
			//gl.glOrthof( -5, 5, -5, 5, 0, 10 );
			this.SetPerspective( MathHelper.PiOver4, this.Surface.RasterBuffer.AspectRatio, 0.1f, 1000.0f );
			//gl.glLoadMatrixf( ref _camera.Projection );

			gl.glClear( GL.GL_COLOR_BUFFER_BIT );
			//gl.glEnable( GL.GL_CULL_FACE );
			gl.glFrontFace( GL.GL_CW );

			gl.glMatrixMode( GL.GL_TEXTURE );
			Matrix tr = Matrix.Rotation( new Vector3( tex_rx, tex_ry, tex_rz ) );
			Matrix ts = Matrix.Identity;
			ts.Scale( new Vector3( tex_sx, tex_sy, 1.0f ) );
			Matrix tt = Matrix.Translation( new Vector3( tex_tx, tex_ty, 0.0f ) );
			Matrix tf;
			Matrix.Multiply( ref tt, ref ts, out tf );
			Matrix.Multiply( ref tr, ref tf, out tt );
			gl.glLoadMatrixf( ref tt );

			gl.glEnableClientState( GL.GL_VERTEX_ARRAY );
			//gl.glEnableClientState( GL.GL_COLOR_ARRAY );
			gl.glEnableClientState( GL.GL_TEXTURE_COORD_ARRAY );
			gl.glVertexPointer( 3, 0, _vertices, 0 );
			//gl.glColorPointer( 4, 0, _colors, 0 );
			gl.glColor4f( 1.0f, 1.0f, 1.0f, 1.0f );
			gl.glTexCoordPointer( 2, 0, _texCoords, 0 );

			gl.glMatrixMode( GL.GL_MODELVIEW );
			gl.glLoadIdentity();
			gl.glMultMatrixf( ref _camera.View );
			for( int n = 0; n < 1; n++ )
			{
				gl.glPushMatrix();
				gl.glTranslatef( 0, 0, 10 );
				gl.glRotatef( _rotation, 0.0f, 1.0f, 0.0f );
				_rotation += 1f;
				gl.glDrawArrays( GL.GL_TRIANGLES, 0, 3 );
				gl.glPopMatrix();
			}

			//gl.glPopMatrix();

			//gl.glMatrixMode( GL.GL_MODELVIEW );
			//gl.glLoadIdentity();
			//gl.glTranslatef( 0, 0, -10 );
			//gl.glRotatef( _rotation++, 0, 1, 0 );

			//gl.glEnableClientState( GL.GL_VERTEX_ARRAY );
			//gl.glVertexPointer( 3, 0, _vertices, 0 );
			//gl.glEnableClientState( GL.GL_COLOR_ARRAY );
			//gl.glColorPointer( 4, 0, _colors, 0 );

			//gl.glDrawArrays( GL.GL_TRIANGLES, 0, 3 );

			gl.glFinish();
		}

		private void EnableScaling()
		{
			this.Surface.HorizontalAlignment = HorizontalAlignment.Left;
			this.Surface.VerticalAlignment = VerticalAlignment.Top;
			this.Surface.Width = this.Width / 1.5;
			this.Surface.Height = this.Height / 1.5;
			this.Surface.RenderTransform = new ScaleTransform()
			{
				ScaleX = 1.5,
				ScaleY = 1.5,
			};
		}

		private void DisableScaling()
		{
			this.Surface.Width = this.Width;
			this.Surface.Height = this.Height;
			this.Surface.RenderTransform = null;
		}

		protected override void OnKeyDown( KeyEventArgs e )
		{
			switch( e.Key )
			{
				case Key.Space:
					_camera.Position = new Vector3( 0, 0, 0 );
					_camera.Angles = new Vector3();
					break;
				case Key.C:
					_isScaling = !_isScaling;
					if( _isScaling == true )
						this.EnableScaling();
					else
						this.DisableScaling();
					break;
				case Key.J:
					tex_tx += 0.01f;
					break;
				case Key.L:
					tex_tx -= 0.01f;
					break;
				case Key.I:
					tex_ty += 0.01f;
					break;
				case Key.K:
					tex_ty -= 0.01f;
					break;
				case Key.Y:
					tex_sx += 0.01f;
					break;
				case Key.H:
					tex_sx -= 0.01f;
					break;
				case Key.NumPad1:
					tex_rx += 0.01f;
					break;
				case Key.NumPad2:
					tex_rx -= 0.01f;
					break;
				case Key.NumPad4:
					tex_ry += 0.01f;
					break;
				case Key.NumPad5:
					tex_ry -= 0.01f;
					break;
				case Key.NumPad7:
					tex_rz += 0.01f;
					break;
				case Key.NumPad8:
					tex_rz -= 0.01f;
					break;
			}
			e.Handled = _camera.HandleKeyDown( e.Key, e.PlatformKeyCode );
			base.OnKeyDown( e );
		}

		protected override void OnKeyUp( KeyEventArgs e )
		{
			e.Handled = _camera.HandleKeyUp( e.Key, e.PlatformKeyCode );
			base.OnKeyUp( e );
		}
	}
}

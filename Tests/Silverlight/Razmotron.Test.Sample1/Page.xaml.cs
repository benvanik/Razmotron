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

namespace Razmotron.Test.Sample1
{
	public partial class Page : UserControl
	{
		private Camera _camera;
		private RasterBuffer _rasterBuffer;
		private bool _enableZBuffer = true;
		private bool _isScaling = false;

		private DateTime _lastFpsTime;
		private int _frameAccum;

		public Page()
		{
			InitializeComponent();

			this.Loaded += new RoutedEventHandler( Page_Loaded );
		}

		private void Page_Loaded( object sender, RoutedEventArgs e )
		{
#if PALETTIZED
			this.Surface.ColorMode = ColorMode.Palettized;
			this.BuildPalette();
#else
			this.Surface.ColorMode = ColorMode.RGBA;
#endif

			multiScaleImage.Source = new DeepZoomImageTileSource( new Uri( "http://noxa.org/seadragon/Panoramas1/dzc_output_images/sodo7.xml" ) );

			_isScaling = true;
			this.EnableScaling();

			_camera = new Camera();
			_rasterBuffer = this.Surface.RasterBuffer;

			CompositionTarget.Rendering += new EventHandler( CompositionTarget_Rendering );
		}

		private void CompositionTarget_Rendering( object sender, EventArgs e )
		{
			this.UpdateOSD();

			if( _rasterBuffer == null )
				_rasterBuffer = this.Surface.RasterBuffer;

			this.Surface.BeginUpdate();

			if( _rasterBuffer != null )
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

		private void BuildPalette()
		{
			byte[] palette = new byte[ 256 * 3 ];
			Random r = new Random();
			for( int n = 0, offset = 0; n < 256; n++, offset += 3 )
			{
				palette[ offset + 0 ] = ( byte )r.Next( 0, byte.MaxValue );
				palette[ offset + 1 ] = ( byte )r.Next( 0, byte.MaxValue );
				palette[ offset + 2 ] = ( byte )r.Next( 0, byte.MaxValue );
			}
			palette[ 0 ] = 0;
			palette[ 1 ] = 0;
			palette[ 2 ] = 0;
			this.Surface.RasterBuffer.SetPalette( palette );
		}

		private void RenderCore()
		{
			float aspectRatio = _rasterBuffer.Width / ( float )_rasterBuffer.Height;
			_camera.SetPerspective( MathHelper.PiOver4, aspectRatio );
			_camera.Calculate();

			if( _enableZBuffer == true )
				_rasterBuffer.ClearZBuffer( float.MaxValue );
#if PALETTIZED
			_rasterBuffer.Clear( 0 );
#else
			_rasterBuffer.Clear( Colors.Transparent );
#endif

			PixelBuffer pb;
			_rasterBuffer.LockPixels( out pb );

			// Render here
			for( int y = 50; y < 100; y++ )
			{
				int idx = pb.Offset + ( pb.Stride * y );
				for( int x = 50; x < 200; x++ )
				{
					pb.Buffer[ idx + ( x * 4 ) + 0 ] = byte.MaxValue;
					pb.Buffer[ idx + ( x * 4 ) + 1 ] = 0;
					pb.Buffer[ idx + ( x * 4 ) + 2 ] = 0;
					pb.Buffer[ idx + ( x * 4 ) + 3 ] = 150;
				}
			}

			_rasterBuffer.UnlockPixels();
		}

		private void EnableScaling()
		{
			this.Surface.HorizontalAlignment = HorizontalAlignment.Left;
			this.Surface.VerticalAlignment = VerticalAlignment.Top;
			this.Surface.Width = this.Width / 2.0;
			this.Surface.Height = this.Height / 2.0;
			this.Surface.RenderTransform = new ScaleTransform()
			{
				ScaleX = 2.0,
				ScaleY = 2.0,
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
				case Key.X:
					_enableZBuffer = !_enableZBuffer;
					break;
				case Key.C:
					_isScaling = !_isScaling;
					if( _isScaling == true )
						this.EnableScaling();
					else
						this.DisableScaling();
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

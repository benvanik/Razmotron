using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Razmotron.Rasterization;
using System.Threading;

namespace Razmotron.Controls
{
	public class RenderSurfaceControl : UserControl
	{
		private Image _image;
#if WPF
		private WriteableBitmap _bitmap;
#else
		private BitmapImage _bitmap;
#endif
		private RasterBuffer _buffer;

		public RasterBuffer RasterBuffer { get { return _buffer; } }

		public RenderSurfaceControl()
		{
			_image = new Image();
			_image.IsHitTestVisible = false;
#if WPF
			_bitmap = null;
#else
			_bitmap = new BitmapImage();
			_image.Source = _bitmap;
#endif
			this.Content = _image;

			this.Loaded += new RoutedEventHandler( RenderSurfaceControl_Loaded );
		}

		#region Dependency Properties

		public ColorMode ColorMode
		{
			get { return ( ColorMode )GetValue( ColorModeProperty ); }
			set { SetValue( ColorModeProperty, value ); }
		}
		public static readonly DependencyProperty ColorModeProperty =
			DependencyProperty.Register( "ColorMode", typeof( ColorMode ), typeof( RenderSurfaceControl ), new PropertyMetadata( ColorMode.RGB, BufferPropertyChanged ) );

		private static void BufferPropertyChanged( DependencyObject obj, DependencyPropertyChangedEventArgs e )
		{
			RenderSurfaceControl control = obj as RenderSurfaceControl;
			control.ResetBuffer( ( int )control.Width, ( int )control.Height );
		}

		#endregion

		private void RenderSurfaceControl_Loaded( object sender, RoutedEventArgs e )
		{
		}

		private void ResetBuffer( int width, int height )
		{
			if( _buffer == null )
				_buffer = new RasterBuffer( this.ColorMode );

			// Save palette
			byte[] palette = _buffer.GetPalette();

			// Perform resize
			bool didChange = _buffer.Resize( width, height );

			// Restore palette (if needed)
			if( didChange == true )
				_buffer.SetPalette( palette );
			
#if WPF
			_bitmap = new WriteableBitmap( _buffer.Width, _buffer.Height, 72, 72, PixelFormats.Rgb24, null );
			_image.Source = _bitmap;
#endif
		}

		public void BeginUpdate()
		{
			if( _buffer == null )
				this.ResetBuffer( ( int )this.Width, ( int )this.Height );
		}

		public void EndUpdate()
		{
			if( _buffer == null )
				return;
#if WPF
			// NOT efficient - would be better to have the rasterbuffer write directly into the backbuffer of the bitmap
			PixelBuffer pb;
			_buffer.LockPixels( out pb );
			_bitmap.Lock();
			unsafe
			{
				fixed( byte* ptr = &pb.Buffer[ pb.Offset ] )
				{
					for( int y = 0; y < pb.Height; y++ )
					{
						int sourceOffset = ( y * pb.Stride );
						_bitmap.WritePixels( new Int32Rect( 0, y, pb.Width, 1 ), new IntPtr( ptr + sourceOffset ), pb.Width * pb.BytesPerPixel, pb.Width * pb.BytesPerPixel );
					}
				}
			}
			_bitmap.Unlock();
			_buffer.UnlockPixels();
#else
			_bitmap.SetSource( _buffer.GetStream() );
#endif
		}

		protected override Size ArrangeOverride( Size finalSize )
		{
			this.ResetBuffer( ( int )finalSize.Width, ( int )finalSize.Height );
			return base.ArrangeOverride( finalSize );
		}
	}
}

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

namespace Razmotron.Rasterization
{
	public class Surface
	{
		public readonly int Width;
		public readonly int Height;
		public readonly ColorMode ColorMode;
		public readonly int BytesPerPixel;
		public readonly byte[] Data;

		public Surface( int width, int height, ColorMode colorMode )
		{
			this.Width = width;
			this.Height = height;
			this.ColorMode = colorMode;
			this.BytesPerPixel = ( int )colorMode;
			this.Data = new byte[ width * height * ( int )colorMode ];
		}

		public Surface( int width, int height, ColorMode colorMode, byte[] data )
		{
			if( data.Length != ( width * height * ( int )colorMode ) )
				throw new ArgumentOutOfRangeException( "data", "data is not the right size" );
			this.Width = width;
			this.Height = height;
			this.ColorMode = colorMode;
			this.BytesPerPixel = ( int )colorMode;
			this.Data = data;
		}

		public void Blit( RasterBuffer rasterBuffer, int dx, int dy )
		{
			this.Blit( rasterBuffer, dx, dy, 0, 0, this.Width, this.Height );
		}

		public void Blit( RasterBuffer rasterBuffer, int dx, int dy, int sx, int sy, int sw, int sh )
		{
			if( rasterBuffer.ColorMode != this.ColorMode )
				throw new NotSupportedException( "Can only blit to raster buffers with the same color mode" );

			PixelBuffer pb;
			rasterBuffer.LockPixels( out pb );

			int stride = this.Width * this.BytesPerPixel;
			int sourceOffset = ( sy * stride ) + ( sx * this.BytesPerPixel );
			int targetOffset = ( dy * pb.Stride ) + ( dx * pb.BytesPerPixel );
			for( int row = sy; row < sh; row++ )
			{
				Buffer.BlockCopy( this.Data, sourceOffset, pb.Buffer, targetOffset, sw * this.BytesPerPixel );
				sourceOffset += stride;
				targetOffset += pb.Stride;
			}

			rasterBuffer.UnlockPixels();
		}

		public void Blit( RasterBuffer rasterBuffer, int dx, int dy, int dw, int dh )
		{
			this.Blit( rasterBuffer, dx, dy, dw, dh, 0, 0, this.Width, this.Height );
		}

		public void Blit( RasterBuffer rasterBuffer, int dx, int dy, int dw, int dh, int sx, int sy, int sw, int sh )
		{
			// Need to scale
			throw new NotImplementedException();
		}
	}
}

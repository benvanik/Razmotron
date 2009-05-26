using System;
using System.IO;
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

namespace Razmotron.Rasterization
{
	public partial class RasterBuffer
	{
		private static readonly byte[] PNGHeader = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
		private static readonly byte[] IHDR = new byte[] { 73, 72, 68, 82 };
		private static readonly byte[] PLTE = new byte[] { 80, 76, 84, 69 };
		private static readonly byte[] IDAT = new byte[] { 73, 68, 65, 84 };
		private static readonly byte[] IEND = new byte[] { 73, 69, 78, 68 };
		private const int PaletteSize = 256 * 3;

		private ColorMode _colorMode;
		private int _bpp;
		private int _width = -1;
		private int _height;

		private byte[] _buffer;
		private MemoryStream _ms;
		private int _paletteOffset;
		private int _dataOffset;
		private int _dataSize;
		private float[] _zbuffer;

		private byte[] _clearBuffer;
		private byte[] _clearRow;
		private int _clearColorIndex;
		private Color _clearColor;
		private float _clearDepth = float.MaxValue;
		private float[] _clearZBuffer;

		public bool EnableZBuffer = true;
		public bool EnableZWrite = true;

		public RasterBuffer( ColorMode colorMode )
		{
			_colorMode = colorMode;
			_bpp = ( int )colorMode;
			this.Resize( 0, 0 );
		}

		public ColorMode ColorMode { get { return _colorMode; } }
		public bool HasAlpha { get { return _colorMode == ColorMode.RGBA; } }
		public bool HasPalette { get { return _colorMode == ColorMode.Palettized; } }
		public int BytesPerPixel { get { return _bpp; } }
		public int Width { get { return _width; } }
		public int Height { get { return _height; } }
		public float AspectRatio { get { return _width / ( float )_height; } }

		public Stream GetStream()
		{
			_ms.Seek( 0, SeekOrigin.Begin );
			return _ms;
		}

		public bool Resize( int width, int height )
		{
			if( ( _width == width ) && ( _height == height ) )
				return false;
			_width = width;
			_height = height;

			// Determine sizing with compression blocks
			_dataSize = 2 + ( ( _width * _bpp ) + 6 ) * _height;

			// Z-buffer
			_zbuffer = new float[ _width * _height ];

			// Zero row helper for fast clears
			_clearRow = new byte[ _width * _bpp ];
			_clearBuffer = new byte[ _dataSize ];
			_clearColorIndex = -1;
			if( _colorMode == ColorMode.RGB )
				_clearColor = Colors.Black;
			else
				_clearColor = Colors.Transparent;
			_clearZBuffer = new float[ _width * _height ];
			if( _clearDepth != 0.0f )
			{
				for( int n = 0; n < _clearZBuffer.Length; n++ )
					_clearZBuffer[ n ] = _clearDepth;
			}

			int overhead = PNGHeader.Length + ( 12 + 13 ) + 12 + 12;
			if( _colorMode == ColorMode.Palettized )
				overhead += 12 + PaletteSize;
			_buffer = new byte[ overhead + _dataSize ];
			_ms = new MemoryStream( _buffer, true );
			BinaryWriter wr = new BinaryWriter( _ms );

			wr.Write( PNGHeader );

			// IHDR
			wr.Write( hton( 13 ) ); // Chunk length
			wr.Write( IHDR );
			wr.Write( hton( _width ) );
			wr.Write( hton( _height ) );
			wr.Write( ( byte )8 ); // bit depth - 8 bits per channel
			switch( _colorMode )
			{
				case ColorMode.Palettized:
					wr.Write( ( byte )3 );
					break;
				case ColorMode.RGB:
					wr.Write( ( byte )2 );
					break;
				case ColorMode.RGBA:
					wr.Write( ( byte )6 );
					break;
			}
			wr.Write( new byte[ 3 ] ); // ignored
			wr.Write( hton( 0 ) ); // CRC

			if( _colorMode == ColorMode.Palettized )
			{
				// PLTE
				wr.Write( hton( PaletteSize ) ); // Chunk length
				wr.Write( PLTE );
				_paletteOffset = ( int )wr.BaseStream.Position;
				wr.Seek( PaletteSize, SeekOrigin.Current );
				wr.Write( hton( 0 ) ); // CRC
			}

			// IDAT
			wr.Write( hton( _dataSize ) ); // Chunk length
			wr.Write( IDAT );
			wr.Write( ( byte )0x78 ); // Compression method - see ftp://ftp.isi.edu/in-notes/rfc1950.txt
			wr.Write( ( byte )0xDA ); // Flags
			_dataOffset = ( int )wr.BaseStream.Position + 6;
			int segmentSize = _width * _bpp + 1;
			for( int y = 0; y < _height; y++ )
			{
				if( y == ( _height - 1 ) )
				{
					// Last segment
					wr.Write( ( byte )1 );
				}
				else
					wr.Write( ( byte )0 );
				wr.Write( BitConverter.GetBytes( segmentSize ), 0, 2 );
				wr.Write( BitConverter.GetBytes( ~segmentSize ), 0, 2 );
				wr.Write( ( byte )0 );
				wr.Seek( _width * _bpp, SeekOrigin.Current );
			}
			wr.Write( hton( 0 ) ); // CRC

			// Setup clear buffer
			Buffer.BlockCopy( _buffer, _dataOffset, _clearBuffer, 0, _dataSize );

			// IEND
			wr.Write( hton( 0 ) ); // Chunk length
			wr.Write( IEND );
			wr.Write( hton( 0 ) ); // CRC
			wr.Flush();

			return true;
		}

		private static int hton( int data )
		{
			return ( ( data & 0x000000FF ) << 24 ) | ( ( data & 0x0000FF00 ) << 8 ) | ( ( data & 0x00FF0000 ) >> 8 ) | ( int )( ( data & 0xFF000000L ) >> 24 );
		}

		public void LockPalette( out PaletteBuffer pb )
		{
			pb = new PaletteBuffer();
			if( _colorMode == ColorMode.Palettized )
			{
				pb.Buffer = _buffer;
				pb.Offset = _paletteOffset;
				pb.Length = PaletteSize;
			}
		}

		public void UnlockPalette()
		{
		}

		public void LockPixels( out PixelBuffer pb )
		{
			pb = new PixelBuffer()
			{
				Buffer = _buffer,
				Offset = _dataOffset,
				Width = _width,
				Height = _height,
				Stride = _width * _bpp + 6,
				BytesPerPixel = _bpp,
				ZBuffer = _zbuffer,
			};
		}

		public void UnlockPixels()
		{
		}

		public byte[] GetPalette()
		{
			if( this.ColorMode != ColorMode.Palettized )
				return null;
			PaletteBuffer pb;
			this.LockPalette( out pb );
			byte[] palette = new byte[ pb.Length ];
			Buffer.BlockCopy( pb.Buffer, pb.Offset, palette, 0, pb.Length );
			this.UnlockPalette();
			return palette;
		}

		public void SetPalette( byte[] palette )
		{
			if( this.ColorMode != ColorMode.Palettized )
				return;
			PaletteBuffer pb;
			this.LockPalette( out pb );
			if( palette.Length != pb.Length )
				throw new ArgumentOutOfRangeException( "palette", "palette size must be " + pb.Length + "b" );
			Buffer.BlockCopy( palette, 0, pb.Buffer, pb.Offset, pb.Length );
			this.UnlockPalette();
		}

		public void ClearZBuffer( float depth )
		{
			if( _clearDepth != depth )
			{
				_clearDepth = depth;
				if( depth == 0.0f )
					Array.Clear( _clearZBuffer, 0, _width * _height );
				else
				{
					for( int n = 0; n < _clearZBuffer.Length; n++ )
						_clearZBuffer[ n ] = _clearDepth;
				}
			}

			Buffer.BlockCopy( _clearZBuffer, 0, _zbuffer, 0, _zbuffer.Length * 4 );
		}

		public void Clear( byte colorIndex )
		{
			this.Clear( colorIndex, 0, 0, _width - 1, _height - 1 );
		}

		public void Clear( byte colorIndex, int left, int top, int right, int bottom )
		{
			if( _colorMode != ColorMode.Palettized )
				throw new NotSupportedException();
			if( _clearColorIndex != colorIndex )
			{
				for( int n = 0; n < _clearRow.Length; n++ )
					_clearRow[ n ] = colorIndex;
				_clearColorIndex = colorIndex;
			}
			PixelBuffer pb;
			this.LockPixels( out pb );
			int stride = ( right - left ) * pb.BytesPerPixel;
			for( int y = top; y <= bottom; y++ )
			{
				int offset = pb.Offset + ( y * pb.Stride ) + ( left * pb.BytesPerPixel );
				Buffer.BlockCopy( _clearRow, 0, pb.Buffer, offset, stride );
			}
			this.UnlockPixels();
		}

		public void Clear( Color color )
		{
			this.Clear( color, 0, 0, _width - 1, _height - 1 );
		}

		public void Clear( Color color, int left, int top, int right, int bottom )
		{
			if( _colorMode == ColorMode.Palettized )
				throw new NotSupportedException();
			PixelBuffer pb;
			this.LockPixels( out pb );
			if( _clearColor != color )
			{
				if( _colorMode == ColorMode.RGB )
				{
					for( int n = 0; n < _clearRow.Length; n += 3 )
					{
						_clearRow[ n + 0 ] = color.R;
						_clearRow[ n + 1 ] = color.G;
						_clearRow[ n + 2 ] = color.B;
					}
				}
				else
				{
					for( int n = 0; n < _clearRow.Length; n += 4 )
					{
						_clearRow[ n + 0 ] = color.R;
						_clearRow[ n + 1 ] = color.G;
						_clearRow[ n + 2 ] = color.B;
						_clearRow[ n + 3 ] = color.A;
					}
				}
				int stride = ( right - left + 1 ) * pb.BytesPerPixel;
				for( int y = top; y <= bottom; y++ )
				{
					int offset = ( y * pb.Stride ) + ( left * pb.BytesPerPixel );
					Buffer.BlockCopy( _clearRow, 0, _clearBuffer, offset, stride );
				}
				_clearColor = color;
			}
			Buffer.BlockCopy( _clearBuffer, 0, pb.Buffer, pb.Offset, _dataSize );
			this.UnlockPixels();
		}
	}
}

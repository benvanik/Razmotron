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
	public struct TextureLevel
	{
		public readonly int Width;
		public readonly int WidthLog2;
		public readonly int Height;
		public readonly int Stride;
		public readonly int BytesPerPixel;
		public readonly byte[] Data;

		public TextureLevel( int width, int height, int stride, int bytesPerPixel, byte[] data )
		{
			this.Width = width;
			this.WidthLog2 = ( int )( Math.Log( Width ) / Math.Log( 2 ) );
			this.Height = height;
			this.Stride = stride;
			this.BytesPerPixel = bytesPerPixel;
			this.Data = data;
		}
	}

	public class TextureObject : IDisposable
	{
		public bool IsReady;
		public int Width;
		public int Height;
		public int Border;
		public int UnpackAlignment = 4;
		public int MinFilter;
		public int MagFilter;

		public int BaseLevel;
		public TextureLevel[] Levels;

		~TextureObject()
		{
			this.Dispose( false );
		}

		public void Dispose()
		{
			this.Dispose( true );
			GC.SuppressFinalize( this );
		}

		private void Dispose( bool user )
		{
		}
	}
}

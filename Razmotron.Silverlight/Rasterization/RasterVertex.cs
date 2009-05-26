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
using System.Runtime.InteropServices;

namespace Razmotron.Rasterization
{
	[Flags]
	public enum RasterVertexFormat
	{
		Position = 0x0,
		Normal = 0x1,
		Color = 0x2,
		Texture = 0x4,
	}

	public enum RasterVertexState
	{
		Normal = 0,
		Clipped,
	}

	// Stupid Silverlight CLR does not allow fixed arrays in structs because that's 'unsafe'... bastards
	[StructLayout( LayoutKind.Explicit, Pack = 4 )]
	public struct RasterVertex
	{
		[FieldOffset( 0 )]
		public float X;
		[FieldOffset( 4 )]
		public float Y;
		[FieldOffset( 8 )]
		public float Z;
		[FieldOffset( 12 )]
		public float W;
		//[FieldOffset( 0 )]
		//public fixed float Position[ 4 ];
		[FieldOffset( 0 )]
		public int Xx;
		[FieldOffset( 4 )]
		public int Yx;
		[FieldOffset( 8 )]
		public int Zx;
		[FieldOffset( 12 )]
		public int Wx;
		//[FieldOffset( 0 )]
		//public fixed int Positionx[ 4 ];

		[FieldOffset( 16 )]
		public float NX;
		[FieldOffset( 20 )]
		public float NY;
		[FieldOffset( 24 )]
		public float NZ;
		//[FieldOffset( 16 )]
		//public fixed float Normal[ 3 ];
		[FieldOffset( 16 )]
		public int NXx;
		[FieldOffset( 20 )]
		public int NYx;
		[FieldOffset( 24 )]
		public int NZx;
		//[FieldOffset( 16 )]
		//public fixed int Normalx[ 3 ];

		// TODO: use fixed?
		[FieldOffset( 28 )]
		public float R;
		[FieldOffset( 32 )]
		public float G;
		[FieldOffset( 36 )]
		public float B;
		[FieldOffset( 40 )]
		public float A;
		//[FieldOffset( 28 )]
		//public fixed float Color[ 4 ];
		[FieldOffset( 28 )]
		public int Rx;
		[FieldOffset( 32 )]
		public int Gx;
		[FieldOffset( 36 )]
		public int Bx;
		[FieldOffset( 40 )]
		public int Ax;
		//[FieldOffset( 28 )]
		//public fixed int Colorx[ 4 ];

		[FieldOffset( 44 )]
		public float S;
		[FieldOffset( 48 )]
		public float T;
		//[FieldOffset( 52 )]
		//public float R;
		//[FieldOffset( 56 )]
		//public float Q;
		//[FieldOffset( 44 )]
		//public fixed float[] TexCoords[ 4 ];
		[FieldOffset( 44 )]
		public int Sx;
		[FieldOffset( 48 )]
		public int Tx;
		//[FieldOffset( 52 )]
		//public int Rx;
		//[FieldOffset( 56 )]
		//public int Qx;
		//[FieldOffset( 44 )]
		//public fixed int[] TexCoordsx[ 4 ];

		// Projected (screen space) coordinates
		[FieldOffset( 60 )]
		public float PX;
		[FieldOffset( 64 )]
		public float PY;
		[FieldOffset( 68 )]
		public int PXt;
		[FieldOffset( 72 )]
		public int PYt;

		[FieldOffset( 68 )]
		public RasterVertexState State;
	}
}

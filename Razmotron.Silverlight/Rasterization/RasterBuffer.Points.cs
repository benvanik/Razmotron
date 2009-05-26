using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
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
		// The trick we use here is to have two code paths:
		// Small (< some threshold of points):
		//  - Walk points, transform, write to raster buffer
		// Large (> some threshold of points):
		//  - Use multiple threads to walk points, transform, and build _rasterPointCache
		//  - Write _rasterPointCache to raster buffer
		/*
		[StructLayout( LayoutKind.Explicit )]
		private struct RasterPoint
		{
			[FieldOffset( 0 )]
			public int N; // offset of byte start in buffer
			[FieldOffset( 4 )]
			public float IA; // 1.0 - alpha
			[FieldOffset( 5 )]
			public byte R; // RGB are premultiplied by alpha
			[FieldOffset( 6 )]
			public byte G;
			[FieldOffset( 7 )]
			public byte B;
			[FieldOffset( 4 )]
			public byte Color;
		}
		private RasterPoint[] _rasterPointCache;
		private int _rasterPointCount;

		private void RenderPoints( Camera camera, VertexPC[] points, int offset, int count, bool immediate )
		{
			PixelBuffer pb;
			this.LockPixels( out pb );
			int halfWidth = pb.Width / 2;
			int halfHeight = pb.Height / 2;
			float aspectRatio = pb.Width / ( float )pb.Height;
			bool isViewAffine = camera.View.IsAffine;

			for( int index = offset; index < offset + count; index++ )
			{
				// Clip check against Z in camera space
				Vector3 cp;
				Matrix.TransformAffine( ref camera.View, isViewAffine, ref points[ index ].Position, out cp );
				if( ( cp.Z > camera.FarZ ) || ( cp.Z < camera.NearZ ) )
					continue;

				// World->Screen
				Vector3 tp;
				//Matrix.TransformAffine( ref _camera.Projection, false, ref cp, out tp );
				// UNSAFE: no clue if this works for anything but the settings I had when I wrote it
				float det = 1.0f / ( cp.Z * camera.Projection.M34 );
				tp.X = ( cp.X * camera.Projection.M11 ) * det;
				tp.Y = ( cp.Y * camera.Projection.M22 ) * det;

				// Adjust screen space
				int sx = ( int )( tp.X + halfWidth );
				int sy = ( int )( tp.Y * aspectRatio + halfHeight );

				// Cheap clip on other dimensions
				if( ( sx < 0 ) || ( sy < 0 ) || ( sx >= pb.Width ) || ( sy >= pb.Height ) )
					continue;

				// Alpha - is this right?
				float alpha = ( points[ index ].A / ( float )255.0f );

				// Fog
				//if( ( cp.Z > _fogStart ) && ( _enableFog == true ) )
				//    alpha *= ( _fogEnd - cp.Z ) * _fogInvDistance;

				// If fully transparent, ignore
				// TODO: see if this check is worth it
				if( alpha == 0.0f )
				{
					_pointDroppedAccum++;
					continue;
				}

				// Z check and write
				// TODO: move someplace safe? in raster step?
				if( this.EnableZBuffer == true )
				{
					int zoffset = ( sy * pb.Width ) + sx;
					float existingZ = pb.ZBuffer[ zoffset ];
					if( existingZ < cp.Z )
					{
						_pointDroppedAccum++;
						continue;
					}
					if( this.EnableZWrite == true )
						pb.ZBuffer[ zoffset ] = cp.Z;
				}

				int n = pb.Offset + ( sy * pb.Stride ) + ( sx * pb.BytesPerPixel );
				if( immediate == true )
				{
					if( _colorMode == ColorMode.Palettized )
						pb.Buffer[ n ] = points[ index ].ColorIndex;
					else if( alpha < 1.0f )
					{
						float invAlpha = 1.0f - alpha;
						pb.Buffer[ n + 0 ] = ( byte )( ( points[ index ].R * alpha ) + ( invAlpha * pb.Buffer[ n + 0 ] ) );
						pb.Buffer[ n + 1 ] = ( byte )( ( points[ index ].G * alpha ) + ( invAlpha * pb.Buffer[ n + 1 ] ) );
						pb.Buffer[ n + 2 ] = ( byte )( ( points[ index ].B * alpha ) + ( invAlpha * pb.Buffer[ n + 2 ] ) );
					}
					else
					{
						pb.Buffer[ n + 0 ] = points[ index ].R;
						pb.Buffer[ n + 1 ] = points[ index ].G;
						pb.Buffer[ n + 2 ] = points[ index ].B;
					}
					_pointAccum++;
				}
				else
				{
					int vertex = Interlocked.Increment( ref _rasterPointCount ) - 1;
					_rasterPointCache[ vertex ].N = n;
					if( _colorMode == ColorMode.Palettized )
						_rasterPointCache[ vertex ].Color = points[ index ].ColorIndex;
					else if( alpha < 1.0f )
					{
						_rasterPointCache[ vertex ].R = ( byte )( points[ index ].R * alpha );
						_rasterPointCache[ vertex ].G = ( byte )( points[ index ].G * alpha );
						_rasterPointCache[ vertex ].B = ( byte )( points[ index ].B * alpha );
						_rasterPointCache[ vertex ].IA = 0.0f;
					}
					else
					{
						_rasterPointCache[ vertex ].R = points[ index ].R;
						_rasterPointCache[ vertex ].G = points[ index ].G;
						_rasterPointCache[ vertex ].B = points[ index ].B;
						_rasterPointCache[ vertex ].IA = 1.0f - alpha;
					}
				}
			}

			this.UnlockPixels();
		}

		private void CommitPointRasterize()
		{
			PixelBuffer pb;
			this.LockPixels( out pb );

			if( _colorMode == ColorMode.Palettized )
			{
				for( int n = 0; n < _rasterPointCount; n++ )
					pb.Buffer[ _rasterPointCache[ n ].N ] = _rasterPointCache[ n ].Color;
			}
			else
			{
				// TODO: support RGBA
				for( int n = 0; n < _rasterPointCount; n++ )
				{
					int pos = _rasterPointCache[ n ].N;
					float inverseAlpha = _rasterPointCache[ n ].IA;
					if( inverseAlpha != 0.0f )
					{
						pb.Buffer[ pos + 0 ] = ( byte )( _rasterPointCache[ n ].R + inverseAlpha * pb.Buffer[ pos + 0 ] );
						pb.Buffer[ pos + 1 ] = ( byte )( _rasterPointCache[ n ].G + inverseAlpha * pb.Buffer[ pos + 1 ] );
						pb.Buffer[ pos + 2 ] = ( byte )( _rasterPointCache[ n ].B + inverseAlpha * pb.Buffer[ pos + 2 ] );
					}
					else
					{
						pb.Buffer[ pos + 0 ] = _rasterPointCache[ n ].R;
						pb.Buffer[ pos + 1 ] = _rasterPointCache[ n ].G;
						pb.Buffer[ pos + 2 ] = _rasterPointCache[ n ].B;
					}
				}
			}

			this.UnlockPixels();
			_pointAccum += _rasterPointCount;
			_rasterPointCount = 0;
		}
		 */
	}
}

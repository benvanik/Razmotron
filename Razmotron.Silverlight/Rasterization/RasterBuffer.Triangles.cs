//#define ALPHA

using System;
using System.Diagnostics;
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
		public void DrawTriangle( ref PixelBuffer pb, ref RasterPolygon polygon, bool useFixed )
		{
			Debug.Assert( polygon.State != RasterPolygonState.BackfaceCulled );
			if( useFixed == true )
				throw new NotImplementedException( "fixed math not implemented" );

			// Don't support perspective yet
			polygon.PerspectiveTexturing = false;

			// TODO: any way to make this cleaner?
			if( ( polygon.VertexFormat & RasterVertexFormat.Color ) == RasterVertexFormat.Color )
			{
				if( ( polygon.VertexFormat & RasterVertexFormat.Texture ) == RasterVertexFormat.Texture )
				{
					// Vary color and texture
					// TODO: DrawTriangleVaryColorPerspectiveTexture not implemented
					//if( polygon.PerspectiveTexturing == true )
					//    this.DrawTriangleVaryColorPerspectiveTexture( ref pb, ref polygon );
					//else
					this.DrawTriangleVaryColorAffineTexture( ref pb, ref polygon );
				}
				else
				{
					// Vary color
					this.DrawTriangleVaryColor( ref pb, ref polygon );
				}
			}
			else if( ( polygon.VertexFormat & RasterVertexFormat.Texture ) == RasterVertexFormat.Texture )
			{
				// Vary texture
				if( polygon.PerspectiveTexturing == true )
					this.DrawTriangleSolidColorPerspectiveTexture( ref pb, ref polygon );
				else
					this.DrawTriangleSolidColorAffineTexture( ref pb, ref polygon );
			}
			else
			{
				// Solid color
				this.DrawTriangleSolidColor( ref pb, ref polygon );
			}
		}

		// fixed point mathematics constants
		private const int FIXP16_SHIFT = 16;
		private const int FIXP16_MAG = 65536;
		private const int FIXP16_DP_MASK = 0x0000ffff;
		private const int FIXP16_WP_MASK = unchecked( ( int )0xffff0000 );
		private const int FIXP16_ROUND_UP = 0x00008000;
		private const int FIXP28_SHIFT = 28; // used for 1/z buffering
		private const int FIXP22_SHIFT = 22;  // used for u/z, v/z perspective texture mapping

		// defines for texture mapper triangular analysis
		private const int TRI_TYPE_NONE = 0;
		private const int TRI_TYPE_FLAT_TOP = 1;
		private const int TRI_TYPE_FLAT_BOTTOM = 2;
		private const int TRI_TYPE_FLAT_MASK = 3;
		private const int TRI_TYPE_GENERAL = 4;
		private const int INTERP_LHS = 0;
		private const int INTERP_RHS = 1;
		private const int MAX_VERTICES_PER_POLY = 6;

		#region Solid Color
		private void DrawTriangleSolidColor( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
			RasterVertex[] vertices = polygon.Vertices;
			int v0 = polygon.v0, v1 = polygon.v1, v2 = polygon.v2;

			int temp = 0,
				tri_type = TRI_TYPE_NONE,
				irestart = INTERP_LHS;

			int dx, dy, dyl, dyr,       // general deltas
				xi, yi,                 // the current interpolated x,y
				xstart,
				xend,
				ystart,
				yrestart,
				yend,
				xl,
				dxdyl,
				xr,
				dxdyr;

			int x0, y0,    // cached vertices
				x1, y1,
				x2, y2;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width, max_clip_y = pb.Height;

			int screen_ptr = 0;

			byte[] color = new byte[ 4 ]{
                ( byte )( polygon.Color[ 0 ] * 255.0f ),
                ( byte )( polygon.Color[ 1 ] * 255.0f ),
                ( byte )( polygon.Color[ 2 ] * 255.0f ),
                ( byte )( polygon.Color[ 3 ] * 255.0f ),
            };

			// apply fill convention to coordinates
			vertices[ v0 ].PXt = ( int )( vertices[ v0 ].PX + 0.5f );
			vertices[ v0 ].PYt = ( int )( vertices[ v0 ].PY + 0.5f );
			vertices[ v1 ].PXt = ( int )( vertices[ v1 ].PX + 0.5f );
			vertices[ v1 ].PYt = ( int )( vertices[ v1 ].PY + 0.5f );
			vertices[ v2 ].PXt = ( int )( vertices[ v2 ].PX + 0.5f );
			vertices[ v2 ].PYt = ( int )( vertices[ v2 ].PY + 0.5f );

			// first trivial clipping rejection tests 
			if( ( ( vertices[ v0 ].PYt < min_clip_y ) && ( vertices[ v1 ].PYt < min_clip_y ) && ( vertices[ v2 ].PYt < min_clip_y ) ) ||
				( ( vertices[ v0 ].PYt > max_clip_y ) && ( vertices[ v1 ].PYt > max_clip_y ) && ( vertices[ v2 ].PYt > max_clip_y ) ) ||
				( ( vertices[ v0 ].PXt < min_clip_x ) && ( vertices[ v1 ].PXt < min_clip_x ) && ( vertices[ v2 ].PXt < min_clip_x ) ) ||
				( ( vertices[ v0 ].PXt > max_clip_x ) && ( vertices[ v1 ].PXt > max_clip_x ) && ( vertices[ v2 ].PXt > max_clip_x ) ) )
				return;

			// sort vertices
			if( vertices[ v1 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v1; v1 = temp; //SWAP(v0,v1,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v2; v2 = temp; //SWAP(v0,v2,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v1 ].PYt )
			{
				temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
			}

			// now test for trivial flat sided cases
			if( MathHelper.AreEqual( vertices[ v0 ].PYt, vertices[ v1 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_TOP;

				// sort vertices left to right
				if( vertices[ v1 ].PXt < vertices[ v0 ].PXt )
				{
					temp = v0; v0 = v1; v1 = temp;        //SWAP(v0,v1,temp);
				}
			}
			// now test for trivial flat sided cases
			else if( MathHelper.AreEqual( vertices[ v1 ].PYt, vertices[ v2 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_BOTTOM;

				// sort vertices left to right
				if( vertices[ v2 ].PXt < vertices[ v1 ].PXt )
				{
					temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
				}
			}
			else
			{
				// must be a general triangle
				tri_type = TRI_TYPE_GENERAL;
			}

			// extract vertices for processing, now that we have order
			x0 = ( int )( vertices[ v0 ].PXt + 0.0f );
			y0 = ( int )( vertices[ v0 ].PYt + 0.0f );

			x1 = ( int )( vertices[ v1 ].PXt + 0.0f );
			y1 = ( int )( vertices[ v1 ].PYt + 0.0f );

			x2 = ( int )( vertices[ v2 ].PXt + 0.0f );
			y2 = ( int )( vertices[ v2 ].PYt + 0.0f );

			// degenerate triangle
			if( ( ( x0 == x1 ) && ( x1 == x2 ) ) || ( ( y0 == y1 ) && ( y1 == y2 ) ) )
				return;

			// set interpolation restart value
			yrestart = y1;

			// what kind of triangle
			if( ( tri_type & TRI_TYPE_FLAT_MASK ) != 0 )
			{
				if( tri_type == TRI_TYPE_FLAT_TOP )
				{
					// compute all deltas
					dy = ( y2 - y0 );

					dxdyl = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dxdyr = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x1 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x1 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}
				else
				{
					// must be flat bottom

					// compute all deltas
					dy = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dy;
					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x0 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}

				// test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						dx = ( xend - xstart );

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// reset vars
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write texel
							pb.Buffer[ screen_ptr + xi ] = color[ 0 ];
							pb.Buffer[ screen_ptr + xi + 1 ] = color[ 1 ];
							pb.Buffer[ screen_ptr + xi + 2 ] = color[ 2 ];
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
						}

						// interpolate x along right and left edge
						xl += dxdyl;
						xr += dxdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

					} // end for y
				}
				else
				{
					// non-clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						dx = ( xend - xstart );

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write texel
							pb.Buffer[ screen_ptr + xi ] = color[ 0 ];
							pb.Buffer[ screen_ptr + xi + 1 ] = color[ 1 ];
							pb.Buffer[ screen_ptr + xi + 2 ] = color[ 2 ];
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
						} // end for xi

						// interpolate x,z along right and left edge
						xl += dxdyl;
						xr += dxdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

					} // end for y
				}
			}
			else if( tri_type == TRI_TYPE_GENERAL )
			{
				// first test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// pre-test y clipping status
				if( y1 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y2 - y1 );

					dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dyr = ( min_clip_y - y0 );
					dyl = ( min_clip_y - y1 );

					// computer new LHS starting values
					xl = dxdyl * dyl + ( x1 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dyr + ( x0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr > dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; //SWAP(dxdyl,dxdyr,temp);
						temp = xl; xl = xr; xr = temp; //SWAP(xl,xr,temp);
						temp = x1; x1 = x2; x2 = temp; //SWAP(x1,x2,temp);
						temp = y1; y1 = y2; y2 = temp; //SWAP(y1,y2,temp);
						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else if( y0 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dy = ( min_clip_y - y0 );

					// computer new LHS starting values
					xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; //SWAP(dxdyl,dxdyr,temp);
						temp = xl; xl = xr; xr = temp; //SWAP(xl,xr,temp);
						temp = x1; x1 = x2; x2 = temp; //SWAP(x1,x2,temp);
						temp = y1; y1 = y2; y2 = temp; //SWAP(y1,y2,temp);
						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else
				{
					// no initial y clipping

					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;

					// no clipping y

					// set starting values
					xl = ( x0 << FIXP16_SHIFT );
					xr = ( x0 << FIXP16_SHIFT );

					// set starting y
					ystart = y0;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; //SWAP(dxdyl,dxdyr,temp);
						temp = xl; xl = xr; xr = temp; //SWAP(xl,xr,temp);
						temp = x1; x1 = x2; x2 = temp; //SWAP(x1,x2,temp);
						temp = y1; y1 = y2; y2 = temp; //SWAP(y1,y2,temp);
						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version
					// x clipping	

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						dx = ( xend - xstart );

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// set x to left clip edge
							xstart = min_clip_x;

						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write texel
							pb.Buffer[ screen_ptr + xi ] = color[ 0 ];
							pb.Buffer[ screen_ptr + xi + 1 ] = color[ 1 ];
							pb.Buffer[ screen_ptr + xi + 2 ] = color[ 2 ];
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];

						} // end for xi

						// interpolate z,x along right and left edge
						xl += dxdyl;
						xr += dxdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag

							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
							}
						}
					} // end for y
				}
				else
				{
					// no x clipping
					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						dx = ( xend - xstart );

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write texel
							pb.Buffer[ screen_ptr + xi ] = color[ 0 ];
							pb.Buffer[ screen_ptr + xi + 1 ] = color[ 1 ];
							pb.Buffer[ screen_ptr + xi + 2 ] = color[ 2 ];
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
						} // end for xi

						// interpolate x,z along right and left edge
						xl += dxdyl;
						xr += dxdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag

							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
							}
						}
					} // end for y
				}
			}
		}
		#endregion

		#region Vary Color
		private void DrawTriangleVaryColor( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
			// this function draws a gouraud shaded polygon, based on the affine texture mapper, instead
			// of interpolating the texture coordinates, we simply interpolate the (R,G,B) values across
			// the polygons, I simply needed at another interpolant, I have mapped u->red, v->green, w->blue

			RasterVertex[] vertices = polygon.Vertices;
			int v0 = polygon.v0, v1 = polygon.v1, v2 = polygon.v2;

			int temp = 0,
				tri_type = TRI_TYPE_NONE,
				irestart = INTERP_LHS;

			int dx, dy, dyl, dyr,      // general deltas
				du, dv, dw,
				xi, yi,              // the current interpolated x,y
				ui, vi, wi,           // the current interpolated u,v
				xstart,
				xend,
				ystart,
				yrestart,
				yend,
				xl,
				dxdyl,
				xr,
				dxdyr,
				dudyl,
				ul,
				dvdyl,
				vl,
				dwdyl,
				wl,
				dudyr,
				ur,
				dvdyr,
				vr,
				dwdyr,
				wr;

			int x0, y0, tu0, tv0, tw0,    // cached vertices
				x1, y1, tu1, tv1, tw1,
				x2, y2, tu2, tv2, tw2;

			int r_base0, g_base0, b_base0,
				r_base1, g_base1, b_base1,
				r_base2, g_base2, b_base2;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width, max_clip_y = pb.Height;

			int screen_ptr = 0;

			// apply fill convention to coordinates
			vertices[ v0 ].PXt = ( int )( vertices[ v0 ].PX + 0.5f );
			vertices[ v0 ].PYt = ( int )( vertices[ v0 ].PY + 0.5f );
			vertices[ v1 ].PXt = ( int )( vertices[ v1 ].PX + 0.5f );
			vertices[ v1 ].PYt = ( int )( vertices[ v1 ].PY + 0.5f );
			vertices[ v2 ].PXt = ( int )( vertices[ v2 ].PX + 0.5f );
			vertices[ v2 ].PYt = ( int )( vertices[ v2 ].PY + 0.5f );

			// first trivial clipping rejection tests 
			if( ( ( vertices[ v0 ].PYt < min_clip_y ) && ( vertices[ v1 ].PYt < min_clip_y ) && ( vertices[ v2 ].PYt < min_clip_y ) ) ||
				( ( vertices[ v0 ].PYt > max_clip_y ) && ( vertices[ v1 ].PYt > max_clip_y ) && ( vertices[ v2 ].PYt > max_clip_y ) ) ||
				( ( vertices[ v0 ].PXt < min_clip_x ) && ( vertices[ v1 ].PXt < min_clip_x ) && ( vertices[ v2 ].PXt < min_clip_x ) ) ||
				( ( vertices[ v0 ].PXt > max_clip_x ) && ( vertices[ v1 ].PXt > max_clip_x ) && ( vertices[ v2 ].PXt > max_clip_x ) ) )
				return;

			// sort vertices
			if( vertices[ v1 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v1; v1 = temp; //SWAP(v0,v1,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v2; v2 = temp; //SWAP(v0,v2,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v1 ].PYt )
			{
				temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
			}

			// now test for trivial flat sided cases
			if( MathHelper.AreEqual( vertices[ v0 ].PYt, vertices[ v1 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_TOP;

				// sort vertices left to right
				if( vertices[ v1 ].PXt < vertices[ v0 ].PXt )
				{
					temp = v0; v0 = v1; v1 = temp;        //SWAP(v0,v1,temp);
				}
			}
			// now test for trivial flat sided cases
			else if( MathHelper.AreEqual( vertices[ v1 ].PYt, vertices[ v2 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_BOTTOM;

				// sort vertices left to right
				if( vertices[ v2 ].PXt < vertices[ v1 ].PXt )
				{
					temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
				}
			}
			else
			{
				// must be a general triangle
				tri_type = TRI_TYPE_GENERAL;
			}

			// assume 5.6.5 format -- sorry!
			// we can't afford a function call in the inner loops, so we must write 
			// two hard coded versions, if we want support for both 5.6.5, and 5.5.5
			//_RGB565FROM16BIT( face->lit_color[ v0 ], &r_base0, &g_base0, &b_base0 );
			//_RGB565FROM16BIT( face->lit_color[ v1 ], &r_base1, &g_base1, &b_base1 );
			//_RGB565FROM16BIT( face->lit_color[ v2 ], &r_base2, &g_base2, &b_base2 );
			r_base0 = ( byte )( vertices[ v0 ].R * 255.0f );
			g_base0 = ( byte )( vertices[ v0 ].G * 255.0f );
			b_base0 = ( byte )( vertices[ v0 ].B * 255.0f );
			//a_base0 = ( byte )( vertices[ v0 ].A * 255.0f );
			r_base1 = ( byte )( vertices[ v1 ].R * 255.0f );
			g_base1 = ( byte )( vertices[ v1 ].G * 255.0f );
			b_base1 = ( byte )( vertices[ v1 ].B * 255.0f );
			//a_base1 = ( byte )( vertices[ v1 ].A * 255.0f );
			r_base2 = ( byte )( vertices[ v2 ].R * 255.0f );
			g_base2 = ( byte )( vertices[ v2 ].G * 255.0f );
			b_base2 = ( byte )( vertices[ v2 ].B * 255.0f );
			//a_base2 = ( byte )( vertices[ v2 ].A * 255.0f );

			// scale to 8 bit 
			r_base0 <<= 3;
			g_base0 <<= 2;
			b_base0 <<= 3;

			// scale to 8 bit 
			r_base1 <<= 3;
			g_base1 <<= 2;
			b_base1 <<= 3;

			// scale to 8 bit 
			r_base2 <<= 3;
			g_base2 <<= 2;
			b_base2 <<= 3;

			// extract vertices for processing, now that we have order
			x0 = ( int )( vertices[ v0 ].PXt + 0.0f );
			y0 = ( int )( vertices[ v0 ].PYt + 0.0f );
			tu0 = r_base0;
			tv0 = g_base0;
			tw0 = b_base0;

			x1 = ( int )( vertices[ v1 ].PXt + 0.0f );
			y1 = ( int )( vertices[ v1 ].PYt + 0.0f );
			tu1 = r_base1;
			tv1 = g_base1;
			tw1 = b_base1;

			x2 = ( int )( vertices[ v2 ].PXt + 0.0f );
			y2 = ( int )( vertices[ v2 ].PYt + 0.0f );
			tu2 = r_base2;
			tv2 = g_base2;
			tw2 = b_base2;

			// degenerate triangle
			if( ( ( x0 == x1 ) && ( x1 == x2 ) ) || ( ( y0 == y1 ) && ( y1 == y2 ) ) )
				return;

			// set interpolation restart value
			yrestart = y1;

			// what kind of triangle
			if( ( tri_type & TRI_TYPE_FLAT_MASK ) != 0 )
			{
				if( tri_type == TRI_TYPE_FLAT_TOP )
				{
					// compute all deltas
					dy = ( y2 - y0 );

					dxdyl = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyl = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dy;
					dwdyr = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
						wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x1 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu1 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv1 << FIXP16_SHIFT );
						wr = dwdyr * dy + ( tw1 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x1 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );
						wl = ( tw0 << FIXP16_SHIFT );

						ur = ( tu1 << FIXP16_SHIFT );
						vr = ( tv1 << FIXP16_SHIFT );
						wr = ( tw1 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}
				else
				{
					// must be flat bottom

					// compute all deltas
					dy = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
						wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );
						wr = dwdyr * dy + ( tw0 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x0 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );
						wl = ( tw0 << FIXP16_SHIFT );

						ur = ( tu0 << FIXP16_SHIFT );
						vr = ( tv0 << FIXP16_SHIFT );
						wr = ( tw0 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}

				}

				// test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							wi += dx * dw;

							// reset vars
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							pb.Buffer[ screen_ptr + xi ] = ( byte )( ui >> ( FIXP16_SHIFT + 3 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( vi >> ( FIXP16_SHIFT + 2 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( wi >> ( FIXP16_SHIFT + 3 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( ui >> ( FIXP16_SHIFT + 3 ) ) << 11 ) + ( ( vi >> ( FIXP16_SHIFT + 2 ) ) << 5 ) + ( wi >> ( FIXP16_SHIFT + 3 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
				else
				{
					// non-clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel 5.6.5
							pb.Buffer[ screen_ptr + xi ] = ( byte )( ui >> ( FIXP16_SHIFT + 3 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( vi >> ( FIXP16_SHIFT + 2 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( wi >> ( FIXP16_SHIFT + 3 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( ui >> ( FIXP16_SHIFT + 3 ) ) << 11 ) + ( ( vi >> ( FIXP16_SHIFT + 2 ) ) << 5 ) + ( wi >> ( FIXP16_SHIFT + 3 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
			}
			else if( tri_type == TRI_TYPE_GENERAL )
			{
				// first test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// pre-test y clipping status
				if( y1 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y2 - y1 );

					dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dyr = ( min_clip_y - y0 );
					dyl = ( min_clip_y - y1 );

					// computer new LHS starting values
					xl = dxdyl * dyl + ( x1 << FIXP16_SHIFT );

					ul = dudyl * dyl + ( tu1 << FIXP16_SHIFT );
					vl = dvdyl * dyl + ( tv1 << FIXP16_SHIFT );
					wl = dwdyl * dyl + ( tw1 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dyr + ( x0 << FIXP16_SHIFT );

					ur = dudyr * dyr + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dyr + ( tv0 << FIXP16_SHIFT );
					wr = dwdyr * dyr + ( tw0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr > dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else if( y0 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dy = ( min_clip_y - y0 );

					// computer new LHS starting values
					xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
					ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
					vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
					wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );
					wr = dwdyr * dy + ( tw0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else
				{
					// no initial y clipping

					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					// no clipping y

					// set starting values
					xl = ( x0 << FIXP16_SHIFT );
					xr = ( x0 << FIXP16_SHIFT );

					ul = ( tu0 << FIXP16_SHIFT );
					vl = ( tv0 << FIXP16_SHIFT );
					wl = ( tw0 << FIXP16_SHIFT );

					ur = ( tu0 << FIXP16_SHIFT );
					vr = ( tv0 << FIXP16_SHIFT );
					wr = ( tw0 << FIXP16_SHIFT );

					// set starting y
					ystart = y0;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version
					// x clipping	

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							wi += dx * dw;

							// set x to left clip edge
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							pb.Buffer[ screen_ptr + xi ] = ( byte )( ui >> ( FIXP16_SHIFT + 3 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( vi >> ( FIXP16_SHIFT + 2 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( wi >> ( FIXP16_SHIFT + 3 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( ui >> ( FIXP16_SHIFT + 3 ) ) << 11 ) + ( ( vi >> ( FIXP16_SHIFT + 2 ) ) << 5 ) + ( wi >> ( FIXP16_SHIFT + 3 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
								dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );
								wl = ( tw1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								wl += dwdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;
								dwdyr = ( ( tw1 - tw2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );
								wr = ( tw2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								wr += dwdyr;
							}
						}
					} // end for y
				}
				else
				{
					// no x clipping
					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							pb.Buffer[ screen_ptr + xi ] = ( byte )( ui >> ( FIXP16_SHIFT + 3 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( vi >> ( FIXP16_SHIFT + 2 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( wi >> ( FIXP16_SHIFT + 3 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( ui >> ( FIXP16_SHIFT + 3 ) ) << 11 ) + ( ( vi >> ( FIXP16_SHIFT + 2 ) ) << 5 ) + ( wi >> ( FIXP16_SHIFT + 3 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
								dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );
								wl = ( tw1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								wl += dwdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;
								dwdyr = ( ( tw1 - tw2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );
								wr = ( tw2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								wr += dwdyr;
							}
						}
					} // end for y
				}
			}
		}
		#endregion

		#region Solid Color / Affine Texture
		private void DrawTriangleSolidColorAffineTexture( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
			// this function draws a textured triangle in 16-bit mode with flat shading

			RasterVertex[] vertices = polygon.Vertices;
			int v0 = polygon.v0, v1 = polygon.v1, v2 = polygon.v2;
			byte[] textureLevel0 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].Data;
			// extract base 2 of texture width
			int texture_shift2 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].WidthLog2;
			int texture_bpp = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].BytesPerPixel;

			int temp = 0,
				tri_type = TRI_TYPE_NONE,
				irestart = INTERP_LHS;

			int dx, dy, dyl, dyr,      // general deltas
				du, dv,
				xi, yi,              // the current interpolated x,y
				ui, vi,              // the current interpolated u,v
				xstart,
				xend,
				ystart,
				yrestart,
				yend,
				xl,
				dxdyl,
				xr,
				dxdyr,
				dudyl,
				ul,
				dvdyl,
				vl,
				dudyr,
				ur,
				dvdyr,
				vr;

			ushort r_base, g_base, b_base,
				   r_textel, g_textel, b_textel;

			int x0, y0, tu0, tv0,    // cached vertices
				x1, y1, tu1, tv1,
				x2, y2, tu2, tv2;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width, max_clip_y = pb.Height;

			int screen_ptr = 0;

			// apply fill convention to coordinates
			vertices[ v0 ].PXt = ( int )( vertices[ v0 ].PX + 0.5f );
			vertices[ v0 ].PYt = ( int )( vertices[ v0 ].PY + 0.5f );
			vertices[ v1 ].PXt = ( int )( vertices[ v1 ].PX + 0.5f );
			vertices[ v1 ].PYt = ( int )( vertices[ v1 ].PY + 0.5f );
			vertices[ v2 ].PXt = ( int )( vertices[ v2 ].PX + 0.5f );
			vertices[ v2 ].PYt = ( int )( vertices[ v2 ].PY + 0.5f );

			// first trivial clipping rejection tests 
			if( ( ( vertices[ v0 ].PYt < min_clip_y ) && ( vertices[ v1 ].PYt < min_clip_y ) && ( vertices[ v2 ].PYt < min_clip_y ) ) ||
				( ( vertices[ v0 ].PYt > max_clip_y ) && ( vertices[ v1 ].PYt > max_clip_y ) && ( vertices[ v2 ].PYt > max_clip_y ) ) ||
				( ( vertices[ v0 ].PXt < min_clip_x ) && ( vertices[ v1 ].PXt < min_clip_x ) && ( vertices[ v2 ].PXt < min_clip_x ) ) ||
				( ( vertices[ v0 ].PXt > max_clip_x ) && ( vertices[ v1 ].PXt > max_clip_x ) && ( vertices[ v2 ].PXt > max_clip_x ) ) )
				return;

			// sort vertices
			if( vertices[ v1 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v1; v1 = temp; //SWAP(v0,v1,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v2; v2 = temp; //SWAP(v0,v2,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v1 ].PYt )
			{
				temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
			}

			// now test for trivial flat sided cases
			if( MathHelper.AreEqual( vertices[ v0 ].PYt, vertices[ v1 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_TOP;

				// sort vertices left to right
				if( vertices[ v1 ].PXt < vertices[ v0 ].PXt )
				{
					temp = v0; v0 = v1; v1 = temp;        //SWAP(v0,v1,temp);
				}
			}
			// now test for trivial flat sided cases
			else if( MathHelper.AreEqual( vertices[ v1 ].PYt, vertices[ v2 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_BOTTOM;

				// sort vertices left to right
				if( vertices[ v2 ].PXt < vertices[ v1 ].PXt )
				{
					temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
				}
			}
			else
			{
				// must be a general triangle
				tri_type = TRI_TYPE_GENERAL;
			}

			// extract vertices for processing, now that we have order
			x0 = ( int )( vertices[ v0 ].PXt + 0.0f );
			y0 = ( int )( vertices[ v0 ].PYt + 0.0f );
			tu0 = ( int )( polygon.Vertices[ v0 ].S );
			tv0 = ( int )( polygon.Vertices[ v0 ].T );

			x1 = ( int )( vertices[ v1 ].PXt + 0.0f );
			y1 = ( int )( vertices[ v1 ].PYt + 0.0f );
			tu1 = ( int )( polygon.Vertices[ v1 ].S );
			tv1 = ( int )( polygon.Vertices[ v1 ].T );

			x2 = ( int )( vertices[ v2 ].PXt + 0.0f );
			y2 = ( int )( vertices[ v2 ].PYt + 0.0f );
			tu2 = ( int )( polygon.Vertices[ v2 ].S );
			tv2 = ( int )( polygon.Vertices[ v2 ].T );

			// extract base color of lit poly, so we can modulate texture a bit
			// for lighting
			//_RGB565FROM16BIT( face->lit_color[ 0 ], &r_base, &g_base, &b_base );
			r_base = ( byte )( polygon.Color[ 0 ] * 255.0f );
			g_base = ( byte )( polygon.Color[ 1 ] * 255.0f );
			b_base = ( byte )( polygon.Color[ 2 ] * 255.0f );
			//a_base = ( byte )( polygon.Color[ 3 ] * 255.0f );

			// degenerate triangle
			if( ( ( x0 == x1 ) && ( x1 == x2 ) ) || ( ( y0 == y1 ) && ( y1 == y2 ) ) )
				return;

			// set interpolation restart value
			yrestart = y1;

			// what kind of triangle
			if( ( tri_type & TRI_TYPE_FLAT_MASK ) != 0 )
			{
				if( tri_type == TRI_TYPE_FLAT_TOP )
				{
					// compute all deltas
					dy = ( y2 - y0 );

					dxdyl = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x1 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu1 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv1 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x1 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );

						ur = ( tu1 << FIXP16_SHIFT );
						vr = ( tv1 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}

				}
				else
				{
					// must be flat bottom

					// compute all deltas
					dy = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x0 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );

						ur = ( tu0 << FIXP16_SHIFT );
						vr = ( tv0 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}

				// test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;

							// reset vars
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tidx = MathHelper.Clamp( ( ( ui >> FIXP16_SHIFT ) + ( ( vi >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
				else
				{
					// non-clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tidx = MathHelper.Clamp( ( ( ui >> FIXP16_SHIFT ) + ( ( vi >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
			}
			else if( tri_type == TRI_TYPE_GENERAL )
			{
				// first test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// pre-test y clipping status
				if( y1 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y2 - y1 );

					dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dyr = ( min_clip_y - y0 );
					dyl = ( min_clip_y - y1 );

					// computer new LHS starting values
					xl = dxdyl * dyl + ( x1 << FIXP16_SHIFT );
					ul = dudyl * dyl + ( tu1 << FIXP16_SHIFT );
					vl = dvdyl * dyl + ( tv1 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dyr + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dyr + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dyr + ( tv0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr > dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else if( y0 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dy = ( min_clip_y - y0 );

					// computer new LHS starting values
					xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
					ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
					vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else
				{
					// no initial y clipping

					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;

					// no clipping y

					// set starting values
					xl = ( x0 << FIXP16_SHIFT );
					xr = ( x0 << FIXP16_SHIFT );

					ul = ( tu0 << FIXP16_SHIFT );
					vl = ( tv0 << FIXP16_SHIFT );

					ur = ( tu0 << FIXP16_SHIFT );
					vr = ( tv0 << FIXP16_SHIFT );

					// set starting y
					ystart = y0;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version
					// x clipping	

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;

							// set x to left clip edge
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							//screen_ptr[xi] = textureLevel0[(ui >> FIXP16_SHIFT) + ((vi >> FIXP16_SHIFT) << texture_shift2)];
							// get textel first
							int tidx = MathHelper.Clamp( ( ( ui >> FIXP16_SHIFT ) + ( ( vi >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
							}
						}
					} // end for y
				}
				else
				{
					// no x clipping
					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tidx = MathHelper.Clamp( ( ( ui >> FIXP16_SHIFT ) + ( ( vi >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
							}
						}
					} // end for y
				}
			}
		}
		#endregion

		#region Solid Color / Perspective Texture
		private void DrawTriangleSolidColorPerspectiveTexture( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
			// this function draws a textured triangle in 16-bit mode using a 1/z buffer and piecewise linear
			// perspective correct texture mappping, 1/z, u/z, v/z are interpolated down each edge then to draw
			// each span U and V are computed for each end point and the space is broken up into 32 pixel
			// spans where the correct U,V is computed at each point along the span, but linearly interpolated
			// across the span

			RasterVertex[] vertices = polygon.Vertices;
			int v0 = polygon.v0, v1 = polygon.v1, v2 = polygon.v2;
			byte[] textureLevel0 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].Data;
			// extract base 2 of texture width
			int texture_shift2 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].WidthLog2;
			int texture_bpp = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].BytesPerPixel;
			int texture_width = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].Width;
			int texture_height = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].Height;

			int temp = 0,
				tri_type = TRI_TYPE_NONE,
				irestart = INTERP_LHS;

			int dx, dy, dyl, dyr,      // general deltas
				du, dv, dz,
				xi, yi,              // the current interpolated x,y
				ui, vi, zi,           // the current interpolated u,v,z
				xstart,
				xend,
				ystart,
				yrestart,
				yend,
				xl,
				dxdyl,
				xr,
				dxdyr,
				dudyl,
				ul,
				dvdyl,
				vl,
				dzdyl,
				zl,
				dudyr,
				ur,
				dvdyr,
				vr,
				dzdyr,
				zr;

			int x0, y0, tu0, tv0, tz0,    // cached vertices
				x1, y1, tu1, tv1, tz1,
				x2, y2, tu2, tv2, tz2;

			ushort r_base, g_base, b_base,
				   r_textel, g_textel, b_textel;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width, max_clip_y = pb.Height;

			int screen_ptr = 0;

			// apply fill convention to coordinates
			vertices[ v0 ].PXt = ( int )( vertices[ v0 ].PX + 0.5f );
			vertices[ v0 ].PYt = ( int )( vertices[ v0 ].PY + 0.5f );
			vertices[ v1 ].PXt = ( int )( vertices[ v1 ].PX + 0.5f );
			vertices[ v1 ].PYt = ( int )( vertices[ v1 ].PY + 0.5f );
			vertices[ v2 ].PXt = ( int )( vertices[ v2 ].PX + 0.5f );
			vertices[ v2 ].PYt = ( int )( vertices[ v2 ].PY + 0.5f );

			// first trivial clipping rejection tests 
			if( ( ( vertices[ v0 ].PYt < min_clip_y ) && ( vertices[ v1 ].PYt < min_clip_y ) && ( vertices[ v2 ].PYt < min_clip_y ) ) ||
				( ( vertices[ v0 ].PYt > max_clip_y ) && ( vertices[ v1 ].PYt > max_clip_y ) && ( vertices[ v2 ].PYt > max_clip_y ) ) ||
				( ( vertices[ v0 ].PXt < min_clip_x ) && ( vertices[ v1 ].PXt < min_clip_x ) && ( vertices[ v2 ].PXt < min_clip_x ) ) ||
				( ( vertices[ v0 ].PXt > max_clip_x ) && ( vertices[ v1 ].PXt > max_clip_x ) && ( vertices[ v2 ].PXt > max_clip_x ) ) )
				return;

			// sort vertices
			if( vertices[ v1 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v1; v1 = temp; //SWAP(v0,v1,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v2; v2 = temp; //SWAP(v0,v2,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v1 ].PYt )
			{
				temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
			}

			// now test for trivial flat sided cases
			if( MathHelper.AreEqual( vertices[ v0 ].PYt, vertices[ v1 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_TOP;

				// sort vertices left to right
				if( vertices[ v1 ].PXt < vertices[ v0 ].PXt )
				{
					temp = v0; v0 = v1; v1 = temp;        //SWAP(v0,v1,temp);
				}
			}
			// now test for trivial flat sided cases
			else if( MathHelper.AreEqual( vertices[ v1 ].PYt, vertices[ v2 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_BOTTOM;

				// sort vertices left to right
				if( vertices[ v2 ].PXt < vertices[ v1 ].PXt )
				{
					temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
				}
			}
			else
			{
				// must be a general triangle
				tri_type = TRI_TYPE_GENERAL;
			}

			// extract vertices for processing, now that we have order
			x0 = ( int )( vertices[ v0 ].PXt + 0.0f );
			y0 = ( int )( vertices[ v0 ].PYt + 0.0f );
			tu0 = ( ( int )( polygon.Vertices[ v0 ].S + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v0 ].Z + 0.5f );
			tv0 = ( ( int )( polygon.Vertices[ v0 ].T + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v0 ].Z + 0.5f );
			tz0 = ( 1 << FIXP28_SHIFT ) / ( int )( polygon.Vertices[ v0 ].Z + 0.5f );

			x1 = ( int )( vertices[ v1 ].PXt + 0.0f );
			y1 = ( int )( vertices[ v1 ].PYt + 0.0f );
			tu1 = ( ( int )( polygon.Vertices[ v1 ].S + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v1 ].Z + 0.5f );
			tv1 = ( ( int )( polygon.Vertices[ v1 ].T + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v1 ].Z + 0.5f );
			tz1 = ( 1 << FIXP28_SHIFT ) / ( int )( polygon.Vertices[ v1 ].Z + 0.5f );

			x2 = ( int )( vertices[ v2 ].PXt + 0.0f );
			y2 = ( int )( vertices[ v2 ].PYt + 0.0f );
			tu2 = ( ( int )( polygon.Vertices[ v2 ].S + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v2 ].Z + 0.5f );
			tv2 = ( ( int )( polygon.Vertices[ v2 ].T + 0.5f ) << FIXP22_SHIFT ) / ( int )( polygon.Vertices[ v2 ].Z + 0.5f );
			tz2 = ( 1 << FIXP28_SHIFT ) / ( int )( polygon.Vertices[ v2 ].Z + 0.5f );

			// extract base color of lit poly, so we can modulate texture a bit
			// for lighting
			//_RGB565FROM16BIT( face->lit_color[ 0 ], &r_base, &g_base, &b_base );
			r_base = ( byte )( polygon.Color[ 0 ] * 255.0f );
			g_base = ( byte )( polygon.Color[ 1 ] * 255.0f );
			b_base = ( byte )( polygon.Color[ 2 ] * 255.0f );
			//a_base = ( byte )( polygon.Color[ 3 ] * 255.0f );

			// degenerate triangle
			if( ( ( x0 == x1 ) && ( x1 == x2 ) ) || ( ( y0 == y1 ) && ( y1 == y2 ) ) )
				return;

			// set interpolation restart value
			yrestart = y1;

			// what kind of triangle
			if( ( tri_type & TRI_TYPE_FLAT_MASK ) != 0 )
			{
				if( tri_type == TRI_TYPE_FLAT_TOP )
				{
					// compute all deltas
					dy = ( y2 - y0 );

					dxdyl = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu2 - tu0 ) << 0 ) / dy;
					dvdyl = ( ( tv2 - tv0 ) << 0 ) / dy;
					dzdyl = ( ( tz2 - tz0 ) << 0 ) / dy;

					dxdyr = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu1 ) << 0 ) / dy;
					dvdyr = ( ( tv2 - tv1 ) << 0 ) / dy;
					dzdyr = ( ( tz2 - tz1 ) << 0 ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << 0 );
						vl = dvdyl * dy + ( tv0 << 0 );
						zl = dzdyl * dy + ( tz0 << 0 );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x1 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu1 << 0 );
						vr = dvdyr * dy + ( tv1 << 0 );
						zr = dzdyr * dy + ( tz1 << 0 );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x1 << FIXP16_SHIFT );

						ul = ( tu0 << 0 );
						vl = ( tv0 << 0 );
						zl = ( tz0 << 0 );

						ur = ( tu1 << 0 );
						vr = ( tv1 << 0 );
						zr = ( tz1 << 0 );

						// set starting y
						ystart = y0;
					}
				}
				else
				{
					// must be flat bottom

					// compute all deltas
					dy = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu1 - tu0 ) << 0 ) / dy;
					dvdyl = ( ( tv1 - tv0 ) << 0 ) / dy;
					dzdyl = ( ( tz1 - tz0 ) << 0 ) / dy;

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu0 ) << 0 ) / dy;
					dvdyr = ( ( tv2 - tv0 ) << 0 ) / dy;
					dzdyr = ( ( tz2 - tz0 ) << 0 ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << 0 );
						vl = dvdyl * dy + ( tv0 << 0 );
						zl = dzdyl * dy + ( tz0 << 0 );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu0 << 0 );
						vr = dvdyr * dy + ( tv0 << 0 );
						zr = dzdyr * dy + ( tz0 << 0 );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x0 << FIXP16_SHIFT );

						ul = ( tu0 << 0 );
						vl = ( tv0 << 0 );
						zl = ( tz0 << 0 );

						ur = ( tu0 << 0 );
						vr = ( tv0 << 0 );
						zr = ( tz0 << 0 );

						// set starting y
						ystart = y0;
					}
				}

				// test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						zi = zl + 0; // ????
						ui = ul + 0;
						vi = vl + 0;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dz = ( zr - zl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dz = ( zr - zl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							zi += dx * dz;

							// reset vars
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tx = MathHelper.Clamp( ( ( ui << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi ), 0, texture_width );
							int ty = MathHelper.Clamp( ( ( vi << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi ), 0, texture_height );
							int tidx = ( tx + ( ty << texture_shift2 ) ) * texture_bpp;
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v,z
							ui += du;
							vi += dv;
							zi += dz;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						zl += dzdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						zr += dzdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
				else
				{
					// non-clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						zi = zl + 0; // ????
						ui = ul + 0;
						vi = vl + 0;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dz = ( zr - zl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dz = ( zr - zl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tx = MathHelper.Clamp( ( ui << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_width );
							int ty = MathHelper.Clamp( ( vi << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_height );
							int tidx = ( tx + ( ty << texture_shift2 ) ) * texture_bpp;
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v,z
							ui += du;
							vi += dv;
							zi += dz;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						zl += dzdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						zr += dzdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
			}
			else if( tri_type == TRI_TYPE_GENERAL )
			{
				// first test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// pre-test y clipping status
				if( y1 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y2 - y1 );

					dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu2 - tu1 ) << 0 ) / dyl;
					dvdyl = ( ( tv2 - tv1 ) << 0 ) / dyl;
					dzdyl = ( ( tz2 - tz1 ) << 0 ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << 0 ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << 0 ) / dyr;
					dzdyr = ( ( tz2 - tz0 ) << 0 ) / dyr;

					// compute overclip
					dyr = ( min_clip_y - y0 );
					dyl = ( min_clip_y - y1 );

					// computer new LHS starting values
					xl = dxdyl * dyl + ( x1 << FIXP16_SHIFT );
					ul = dudyl * dyl + ( tu1 << 0 );
					vl = dvdyl * dyl + ( tv1 << 0 );
					zl = dzdyl * dyl + ( tz1 << 0 );

					// compute new RHS starting values
					xr = dxdyr * dyr + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dyr + ( tu0 << 0 );
					vr = dvdyr * dyr + ( tv0 << 0 );
					zr = dzdyr * dyr + ( tz0 << 0 );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr > dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dzdyl; dzdyl = dzdyr; dzdyr = temp; // SWAP( dzdyl, dzdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = zl; zl = zr; zr = temp; // SWAP( zl, zr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tz1; tz1 = tz2; tz2 = temp; // SWAP( tz1, tz2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else if( y0 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << 0 ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << 0 ) / dyl;
					dzdyl = ( ( tz1 - tz0 ) << 0 ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << 0 ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << 0 ) / dyr;
					dzdyr = ( ( tz2 - tz0 ) << 0 ) / dyr;

					// compute overclip
					dy = ( min_clip_y - y0 );

					// computer new LHS starting values
					xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
					ul = dudyl * dy + ( tu0 << 0 );
					vl = dvdyl * dy + ( tv0 << 0 );
					zl = dzdyl * dy + ( tz0 << 0 );

					// compute new RHS starting values
					xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dy + ( tu0 << 0 );
					vr = dvdyr * dy + ( tv0 << 0 );
					zr = dzdyr * dy + ( tz0 << 0 );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dzdyl; dzdyl = dzdyr; dzdyr = temp; // SWAP( dzdyl, dzdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = zl; zl = zr; zr = temp; // SWAP( zl, zr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tz1; tz1 = tz2; tz2 = temp; // SWAP( tz1, tz2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else
				{
					// no initial y clipping

					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << 0 ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << 0 ) / dyl;
					dzdyl = ( ( tz1 - tz0 ) << 0 ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << 0 ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << 0 ) / dyr;
					dzdyr = ( ( tz2 - tz0 ) << 0 ) / dyr;

					// no clipping y

					// set starting values
					xl = ( x0 << FIXP16_SHIFT );
					xr = ( x0 << FIXP16_SHIFT );

					ul = ( tu0 << 0 );
					vl = ( tv0 << 0 );
					zl = ( tz0 << 0 );

					ur = ( tu0 << 0 );
					vr = ( tv0 << 0 );
					zr = ( tz0 << 0 );

					// set starting y
					ystart = y0;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dzdyl; dzdyl = dzdyr; dzdyr = temp; // SWAP( dzdyl, dzdyr, temp );
						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = zl; zl = zr; zr = temp; // SWAP( zl, zr, temp );
						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tz1; tz1 = tz2; tz2 = temp; // SWAP( tz1, tz2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version
					// x clipping	

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						zi = zl + 0; // ????
						ui = ul + 0;
						vi = vl + 0;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dz = ( zr - zl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dz = ( zr - zl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							zi += dx * dz;

							// set x to left clip edge
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tx = MathHelper.Clamp( ( ui << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_width );
							int ty = MathHelper.Clamp( ( vi << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_height );
							int tidx = ( tx + ( ty << texture_shift2 ) ) * texture_bpp;
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v,z
							ui += du;
							vi += dv;
							zi += dz;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						zl += dzdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						zr += dzdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << 0 ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << 0 ) / dyl;
								dzdyl = ( ( tz2 - tz1 ) << 0 ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << 0 );
								vl = ( tv1 << 0 );
								zl = ( tz1 << 0 );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								zl += dzdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << 0 ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << 0 ) / dyr;
								dzdyr = ( ( tz1 - tz2 ) << 0 ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << 0 );
								vr = ( tv2 << 0 );
								zr = ( tz2 << 0 );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								zr += dzdyr;
							}
						}
					} // end for y
				}
				else
				{
					// no x clipping
					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v interpolants
						zi = zl + 0; // ????
						ui = ul + 0;
						vi = vl + 0;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dz = ( zr - zl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dz = ( zr - zl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel
							// get textel first
							int tx = MathHelper.Clamp( ( ui << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_width );
							int ty = MathHelper.Clamp( ( vi << ( FIXP28_SHIFT - FIXP22_SHIFT ) ) / zi, 0, texture_height );
							int tidx = ( tx + ( ty << texture_shift2 ) ) * texture_bpp;
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with lit background color
							r_textel *= r_base;
							g_textel *= g_base;
							b_textel *= b_base;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> 8 );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> 8 );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> 5 ) + ( ( g_textel >> 6 ) << 5 ) + ( ( r_textel >> 5 ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							zi += dz;
						} // end for xi

						// interpolate u,v,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						zl += dzdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						zr += dzdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << 0 ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << 0 ) / dyl;
								dzdyl = ( ( tz2 - tz1 ) << 0 ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << 0 );
								vl = ( tv1 << 0 );
								zl = ( tz1 << 0 );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								zl += dzdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << 0 ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << 0 ) / dyr;
								dzdyr = ( ( tz1 - tz2 ) << 0 ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << 0 );
								vr = ( tv2 << 0 );
								zr = ( tz2 << 0 );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								zr += dzdyr;
							}
						}
					} // end for y
				}
			}
		}
		#endregion

		#region Vary Color / Affine Texture
		private void DrawTriangleVaryColorAffineTexture( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
			// this function draws a textured gouraud shaded polygon, based on the affine texture mapper, 
			// we simply interpolate the (R,G,B) values across the polygons along with the texture coordinates
			// and then modulate to get the final color 

			RasterVertex[] vertices = polygon.Vertices;
			int v0 = polygon.v0, v1 = polygon.v1, v2 = polygon.v2;
			byte[] textureLevel0 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].Data;
			// extract base 2 of texture width
			int texture_shift2 = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].WidthLog2;
			int texture_bpp = polygon.Textures[ 0 ].Levels[ polygon.Textures[ 0 ].BaseLevel ].BytesPerPixel;

			int temp = 0,
				tri_type = TRI_TYPE_NONE,
				irestart = INTERP_LHS;

			int dx, dy, dyl, dyr,      // general deltas
				du, dv, dw, ds, dt,
				xi, yi,              // the current interpolated x,y
				ui, vi, wi, si, ti,    // the current interpolated u,v
				xstart,
				xend,
				ystart,
				yrestart,
				yend,
				xl,
				dxdyl,
				xr,
				dxdyr,
				dudyl,
				ul,
				dvdyl,
				vl,
				dwdyl,
				wl,
				dsdyl,
				sl,
				dtdyl,
				tl,
				dudyr,
				ur,
				dvdyr,
				vr,
				dwdyr,
				wr,
				dsdyr,
				sr,
				dtdyr,
				tr;

			int x0, y0, tu0, tv0, tw0, ts0, tt0,    // cached vertices
				x1, y1, tu1, tv1, tw1, ts1, tt1,
				x2, y2, tu2, tv2, tw2, ts2, tt2;

			int r_base0, g_base0, b_base0,
				r_base1, g_base1, b_base1,
				r_base2, g_base2, b_base2;

			int r_textel, g_textel, b_textel;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width, max_clip_y = pb.Height;

			int screen_ptr = 0;

			// apply fill convention to coordinates
			vertices[ v0 ].PXt = ( int )( vertices[ v0 ].PX + 0.5f );
			vertices[ v0 ].PYt = ( int )( vertices[ v0 ].PY + 0.5f );
			vertices[ v1 ].PXt = ( int )( vertices[ v1 ].PX + 0.5f );
			vertices[ v1 ].PYt = ( int )( vertices[ v1 ].PY + 0.5f );
			vertices[ v2 ].PXt = ( int )( vertices[ v2 ].PX + 0.5f );
			vertices[ v2 ].PYt = ( int )( vertices[ v2 ].PY + 0.5f );

			// first trivial clipping rejection tests 
			if( ( ( vertices[ v0 ].PYt < min_clip_y ) && ( vertices[ v1 ].PYt < min_clip_y ) && ( vertices[ v2 ].PYt < min_clip_y ) ) ||
				( ( vertices[ v0 ].PYt > max_clip_y ) && ( vertices[ v1 ].PYt > max_clip_y ) && ( vertices[ v2 ].PYt > max_clip_y ) ) ||
				( ( vertices[ v0 ].PXt < min_clip_x ) && ( vertices[ v1 ].PXt < min_clip_x ) && ( vertices[ v2 ].PXt < min_clip_x ) ) ||
				( ( vertices[ v0 ].PXt > max_clip_x ) && ( vertices[ v1 ].PXt > max_clip_x ) && ( vertices[ v2 ].PXt > max_clip_x ) ) )
				return;

			// sort vertices
			if( vertices[ v1 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v1; v1 = temp; //SWAP(v0,v1,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v0 ].PYt )
			{
				temp = v0; v0 = v2; v2 = temp; //SWAP(v0,v2,temp);
			}
			if( vertices[ v2 ].PYt < vertices[ v1 ].PYt )
			{
				temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
			}

			// now test for trivial flat sided cases
			if( MathHelper.AreEqual( vertices[ v0 ].PYt, vertices[ v1 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_TOP;

				// sort vertices left to right
				if( vertices[ v1 ].PXt < vertices[ v0 ].PXt )
				{
					temp = v0; v0 = v1; v1 = temp;        //SWAP(v0,v1,temp);
				}
			}
			// now test for trivial flat sided cases
			else if( MathHelper.AreEqual( vertices[ v1 ].PYt, vertices[ v2 ].PYt ) )
			{
				// set triangle type
				tri_type = TRI_TYPE_FLAT_BOTTOM;

				// sort vertices left to right
				if( vertices[ v2 ].PXt < vertices[ v1 ].PXt )
				{
					temp = v1; v1 = v2; v2 = temp; //SWAP(v1,v2,temp);
				}
			}
			else
			{
				// must be a general triangle
				tri_type = TRI_TYPE_GENERAL;
			}

			// assume 5.6.5 format -- sorry!
			// we can't afford a function call in the inner loops, so we must write 
			// two hard coded versions, if we want support for both 5.6.5, and 5.5.5
			//_RGB565FROM16BIT( face->lit_color[ v0 ], &r_base0, &g_base0, &b_base0 );
			//_RGB565FROM16BIT( face->lit_color[ v1 ], &r_base1, &g_base1, &b_base1 );
			//_RGB565FROM16BIT( face->lit_color[ v2 ], &r_base2, &g_base2, &b_base2 );
			r_base0 = ( byte )( vertices[ v0 ].R * 255.0f );
			g_base0 = ( byte )( vertices[ v0 ].G * 255.0f );
			b_base0 = ( byte )( vertices[ v0 ].B * 255.0f );
			//a_base0 = ( byte )( vertices[ v0 ].A * 255.0f );
			r_base1 = ( byte )( vertices[ v1 ].R * 255.0f );
			g_base1 = ( byte )( vertices[ v1 ].G * 255.0f );
			b_base1 = ( byte )( vertices[ v1 ].B * 255.0f );
			//a_base1 = ( byte )( vertices[ v1 ].A * 255.0f );
			r_base2 = ( byte )( vertices[ v2 ].R * 255.0f );
			g_base2 = ( byte )( vertices[ v2 ].G * 255.0f );
			b_base2 = ( byte )( vertices[ v2 ].B * 255.0f );
			//a_base2 = ( byte )( vertices[ v2 ].A * 255.0f );

			// extract vertices for processing, now that we have order
			x0 = ( int )( vertices[ v0 ].PXt + 0.0f );
			y0 = ( int )( vertices[ v0 ].PYt + 0.0f );
			ts0 = ( int )( polygon.Vertices[ v0 ].S );
			tt0 = ( int )( polygon.Vertices[ v0 ].T );
			tu0 = r_base0;
			tv0 = g_base0;
			tw0 = b_base0;

			x1 = ( int )( vertices[ v1 ].PXt + 0.0f );
			y1 = ( int )( vertices[ v1 ].PYt + 0.0f );
			ts1 = ( int )( polygon.Vertices[ v1 ].S );
			tt1 = ( int )( polygon.Vertices[ v1 ].T );
			tu1 = r_base1;
			tv1 = g_base1;
			tw1 = b_base1;

			x2 = ( int )( vertices[ v2 ].PXt + 0.0f );
			y2 = ( int )( vertices[ v2 ].PYt + 0.0f );
			ts2 = ( int )( polygon.Vertices[ v2 ].S );
			tt2 = ( int )( polygon.Vertices[ v2 ].T );
			tu2 = r_base2;
			tv2 = g_base2;
			tw2 = b_base2;

			// degenerate triangle
			if( ( ( x0 == x1 ) && ( x1 == x2 ) ) || ( ( y0 == y1 ) && ( y1 == y2 ) ) )
				return;

			// set interpolation restart value
			yrestart = y1;

			// what kind of triangle
			if( ( tri_type & TRI_TYPE_FLAT_MASK ) != 0 )
			{
				if( tri_type == TRI_TYPE_FLAT_TOP )
				{
					// compute all deltas
					dy = ( y2 - y0 );

					dxdyl = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyl = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dy;

					dsdyl = ( ( ts2 - ts0 ) << FIXP16_SHIFT ) / dy;
					dtdyl = ( ( tt2 - tt0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dy;
					dwdyr = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dy;

					dsdyr = ( ( ts2 - ts1 ) << FIXP16_SHIFT ) / dy;
					dtdyr = ( ( tt2 - tt1 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
						wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

						sl = dsdyl * dy + ( ts0 << FIXP16_SHIFT );
						tl = dtdyl * dy + ( tt0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x1 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu1 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv1 << FIXP16_SHIFT );
						wr = dwdyr * dy + ( tw1 << FIXP16_SHIFT );

						sr = dsdyr * dy + ( ts1 << FIXP16_SHIFT );
						tr = dtdyr * dy + ( tt1 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x1 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );
						wl = ( tw0 << FIXP16_SHIFT );

						sl = ( ts0 << FIXP16_SHIFT );
						tl = ( tt0 << FIXP16_SHIFT );

						ur = ( tu1 << FIXP16_SHIFT );
						vr = ( tv1 << FIXP16_SHIFT );
						wr = ( tw1 << FIXP16_SHIFT );

						sr = ( ts1 << FIXP16_SHIFT );
						tr = ( tt1 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}
				else
				{
					// must be flat bottom

					// compute all deltas
					dy = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dy;

					dsdyl = ( ( ts1 - ts0 ) << FIXP16_SHIFT ) / dy;
					dtdyl = ( ( tt1 - tt0 ) << FIXP16_SHIFT ) / dy;

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dy;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dy;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dy;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dy;

					dsdyr = ( ( ts2 - ts0 ) << FIXP16_SHIFT ) / dy;
					dtdyr = ( ( tt2 - tt0 ) << FIXP16_SHIFT ) / dy;

					// test for y clipping
					if( y0 < min_clip_y )
					{
						// compute overclip
						dy = ( min_clip_y - y0 );

						// computer new LHS starting values
						xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
						ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
						vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
						wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

						sl = dsdyl * dy + ( ts0 << FIXP16_SHIFT );
						tl = dtdyl * dy + ( tt0 << FIXP16_SHIFT );

						// compute new RHS starting values
						xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
						ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
						vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );
						wr = dwdyr * dy + ( tw0 << FIXP16_SHIFT );

						sr = dsdyr * dy + ( ts0 << FIXP16_SHIFT );
						tr = dtdyr * dy + ( tt0 << FIXP16_SHIFT );

						// compute new starting y
						ystart = min_clip_y;
					}
					else
					{
						// no clipping

						// set starting values
						xl = ( x0 << FIXP16_SHIFT );
						xr = ( x0 << FIXP16_SHIFT );

						ul = ( tu0 << FIXP16_SHIFT );
						vl = ( tv0 << FIXP16_SHIFT );
						wl = ( tw0 << FIXP16_SHIFT );

						sl = ( ts0 << FIXP16_SHIFT );
						tl = ( tt0 << FIXP16_SHIFT );

						ur = ( tu0 << FIXP16_SHIFT );
						vr = ( tv0 << FIXP16_SHIFT );
						wr = ( tw0 << FIXP16_SHIFT );

						sr = ( ts0 << FIXP16_SHIFT );
						tr = ( tt0 << FIXP16_SHIFT );

						// set starting y
						ystart = y0;
					}
				}

				// test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						si = sl + FIXP16_ROUND_UP;
						ti = tl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;

							ds = ( sr - sl ) / dx;
							dt = ( tr - tl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );

							ds = ( sr - sl );
							dt = ( tr - tl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							wi += dx * dw;

							si += dx * ds;
							ti += dx * dt;

							// reset vars
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5

							// get textel first
							int tidx = MathHelper.Clamp( ( ( si >> FIXP16_SHIFT ) + ( ( ti >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with gouraud shading
							r_textel *= ui;
							g_textel *= vi;
							b_textel *= wi;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> ( FIXP16_SHIFT + 8 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> ( FIXP16_SHIFT + 8 ) ) + ( ( g_textel >> ( FIXP16_SHIFT + 8 ) ) << 5 ) + ( ( r_textel >> ( FIXP16_SHIFT + 8 ) ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;

							si += ds;
							ti += dt;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						sl += dsdyl;
						tl += dtdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						sr += dsdyr;
						tr += dtdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
				else
				{
					// non-clip version

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						si = sl + FIXP16_ROUND_UP;
						ti = tl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;

							ds = ( sr - sl ) / dx;
							dt = ( tr - tl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );

							ds = ( sr - sl );
							dt = ( tr - tl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							//screen_ptr[xi] = ( (ui >> (FIXP16_SHIFT+3)) << 11) + 
							//                 ( (vi >> (FIXP16_SHIFT+2)) << 5) + 
							//                   (wi >> (FIXP16_SHIFT+3) );   

							// get textel first
							int tidx = MathHelper.Clamp( ( ( si >> FIXP16_SHIFT ) + ( ( ti >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with gouraud shading
							r_textel *= ui;
							g_textel *= vi;
							b_textel *= wi;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> ( FIXP16_SHIFT + 8 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> ( FIXP16_SHIFT + 8 ) ) + ( ( g_textel >> ( FIXP16_SHIFT + 8 ) ) << 5 ) + ( ( r_textel >> ( FIXP16_SHIFT + 8 ) ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;

							si += ds;
							ti += dt;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						sl += dsdyl;
						tl += dtdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						sr += dsdyr;
						tr += dtdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;
					} // end for y
				}
			}
			else if( tri_type == TRI_TYPE_GENERAL )
			{

				// first test for bottom clip, always
				if( ( yend = y2 ) > max_clip_y )
					yend = max_clip_y;

				// pre-test y clipping status
				if( y1 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y2 - y1 );

					dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

					dsdyl = ( ( ts2 - ts1 ) << FIXP16_SHIFT ) / dyl;
					dtdyl = ( ( tt2 - tt1 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					dsdyr = ( ( ts2 - ts0 ) << FIXP16_SHIFT ) / dyr;
					dtdyr = ( ( tt2 - tt0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dyr = ( min_clip_y - y0 );
					dyl = ( min_clip_y - y1 );

					// computer new LHS starting values
					xl = dxdyl * dyl + ( x1 << FIXP16_SHIFT );

					ul = dudyl * dyl + ( tu1 << FIXP16_SHIFT );
					vl = dvdyl * dyl + ( tv1 << FIXP16_SHIFT );
					wl = dwdyl * dyl + ( tw1 << FIXP16_SHIFT );

					sl = dsdyl * dyl + ( ts1 << FIXP16_SHIFT );
					tl = dtdyl * dyl + ( tt1 << FIXP16_SHIFT );

					// compute new RHS starting values
					xr = dxdyr * dyr + ( x0 << FIXP16_SHIFT );

					ur = dudyr * dyr + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dyr + ( tv0 << FIXP16_SHIFT );
					wr = dwdyr * dyr + ( tw0 << FIXP16_SHIFT );

					sr = dsdyr * dyr + ( ts0 << FIXP16_SHIFT );
					tr = dtdyr * dyr + ( tt0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr > dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );

						temp = dsdyl; dsdyl = dsdyr; dsdyr = temp; // SWAP( dsdyl, dsdyr, temp );
						temp = dtdyl; dtdyl = dtdyr; dtdyr = temp; // SWAP( dtdyl, dtdyr, temp );

						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );

						temp = sl; sl = sr; sr = temp; // SWAP( sl, sr, temp );
						temp = tl; tl = tr; tr = temp; // SWAP( tl, tr, temp );

						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );

						temp = ts1; ts1 = ts2; ts2 = temp; // SWAP( ts1, ts2, temp );
						temp = tt1; tt1 = tt2; tt2 = temp; // SWAP( tt1, tt2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else if( y0 < min_clip_y )
				{
					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dyl;

					dsdyl = ( ( ts1 - ts0 ) << FIXP16_SHIFT ) / dyl;
					dtdyl = ( ( tt1 - tt0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					dsdyr = ( ( ts2 - ts0 ) << FIXP16_SHIFT ) / dyr;
					dtdyr = ( ( tt2 - tt0 ) << FIXP16_SHIFT ) / dyr;

					// compute overclip
					dy = ( min_clip_y - y0 );

					// computer new LHS starting values
					xl = dxdyl * dy + ( x0 << FIXP16_SHIFT );
					ul = dudyl * dy + ( tu0 << FIXP16_SHIFT );
					vl = dvdyl * dy + ( tv0 << FIXP16_SHIFT );
					wl = dwdyl * dy + ( tw0 << FIXP16_SHIFT );

					sl = dsdyl * dy + ( ts0 << FIXP16_SHIFT );
					tl = dtdyl * dy + ( tt0 << FIXP16_SHIFT );


					// compute new RHS starting values
					xr = dxdyr * dy + ( x0 << FIXP16_SHIFT );
					ur = dudyr * dy + ( tu0 << FIXP16_SHIFT );
					vr = dvdyr * dy + ( tv0 << FIXP16_SHIFT );
					wr = dwdyr * dy + ( tw0 << FIXP16_SHIFT );

					sr = dsdyr * dy + ( ts0 << FIXP16_SHIFT );
					tr = dtdyr * dy + ( tt0 << FIXP16_SHIFT );

					// compute new starting y
					ystart = min_clip_y;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );

						temp = dsdyl; dsdyl = dsdyr; dsdyr = temp; // SWAP( dsdyl, dsdyr, temp );
						temp = dtdyl; dtdyl = dtdyr; dtdyr = temp; // SWAP( dtdyl, dtdyr, temp );

						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );

						temp = sl; sl = sr; sr = temp; // SWAP( sl, sr, temp );
						temp = tl; tl = tr; tr = temp; // SWAP( tl, tr, temp );

						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );

						temp = ts1; ts1 = ts2; ts2 = temp; // SWAP( ts1, ts2, temp );
						temp = tt1; tt1 = tt2; tt2 = temp; // SWAP( tt1, tt2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}
				else
				{
					// no initial y clipping

					// compute all deltas
					// LHS
					dyl = ( y1 - y0 );

					dxdyl = ( ( x1 - x0 ) << FIXP16_SHIFT ) / dyl;
					dudyl = ( ( tu1 - tu0 ) << FIXP16_SHIFT ) / dyl;
					dvdyl = ( ( tv1 - tv0 ) << FIXP16_SHIFT ) / dyl;
					dwdyl = ( ( tw1 - tw0 ) << FIXP16_SHIFT ) / dyl;

					dsdyl = ( ( ts1 - ts0 ) << FIXP16_SHIFT ) / dyl;
					dtdyl = ( ( tt1 - tt0 ) << FIXP16_SHIFT ) / dyl;

					// RHS
					dyr = ( y2 - y0 );

					dxdyr = ( ( x2 - x0 ) << FIXP16_SHIFT ) / dyr;
					dudyr = ( ( tu2 - tu0 ) << FIXP16_SHIFT ) / dyr;
					dvdyr = ( ( tv2 - tv0 ) << FIXP16_SHIFT ) / dyr;
					dwdyr = ( ( tw2 - tw0 ) << FIXP16_SHIFT ) / dyr;

					dsdyr = ( ( ts2 - ts0 ) << FIXP16_SHIFT ) / dyr;
					dtdyr = ( ( tt2 - tt0 ) << FIXP16_SHIFT ) / dyr;

					// no clipping y

					// set starting values
					xl = ( x0 << FIXP16_SHIFT );
					xr = ( x0 << FIXP16_SHIFT );

					ul = ( tu0 << FIXP16_SHIFT );
					vl = ( tv0 << FIXP16_SHIFT );
					wl = ( tw0 << FIXP16_SHIFT );

					sl = ( ts0 << FIXP16_SHIFT );
					tl = ( tt0 << FIXP16_SHIFT );

					ur = ( tu0 << FIXP16_SHIFT );
					vr = ( tv0 << FIXP16_SHIFT );
					wr = ( tw0 << FIXP16_SHIFT );

					sr = ( ts0 << FIXP16_SHIFT );
					tr = ( tt0 << FIXP16_SHIFT );

					// set starting y
					ystart = y0;

					// test if we need swap to keep rendering left to right
					if( dxdyr < dxdyl )
					{
						temp = dxdyl; dxdyl = dxdyr; dxdyr = temp; // SWAP( dxdyl, dxdyr, temp );
						temp = dudyl; dudyl = dudyr; dudyr = temp; // SWAP( dudyl, dudyr, temp );
						temp = dvdyl; dvdyl = dvdyr; dvdyr = temp; // SWAP( dvdyl, dvdyr, temp );
						temp = dwdyl; dwdyl = dwdyr; dwdyr = temp; // SWAP( dwdyl, dwdyr, temp );

						temp = dsdyl; dsdyl = dsdyr; dsdyr = temp; // SWAP( dsdyl, dsdyr, temp );
						temp = dtdyl; dtdyl = dtdyr; dtdyr = temp; // SWAP( dtdyl, dtdyr, temp );


						temp = xl; xl = xr; xr = temp; // SWAP( xl, xr, temp );
						temp = ul; ul = ur; ur = temp; // SWAP( ul, ur, temp );
						temp = vl; vl = vr; vr = temp; // SWAP( vl, vr, temp );
						temp = wl; wl = wr; wr = temp; // SWAP( wl, wr, temp );

						temp = sl; sl = sr; sr = temp; // SWAP( sl, sr, temp );
						temp = tl; tl = tr; tr = temp; // SWAP( tl, tr, temp );

						temp = x1; x1 = x2; x2 = temp; // SWAP( x1, x2, temp );
						temp = y1; y1 = y2; y2 = temp; // SWAP( y1, y2, temp );
						temp = tu1; tu1 = tu2; tu2 = temp; // SWAP( tu1, tu2, temp );
						temp = tv1; tv1 = tv2; tv2 = temp; // SWAP( tv1, tv2, temp );
						temp = tw1; tw1 = tw2; tw2 = temp; // SWAP( tw1, tw2, temp );


						temp = ts1; ts1 = ts2; ts2 = temp; // SWAP( ts1, ts2, temp );
						temp = tt1; tt1 = tt2; tt2 = temp; // SWAP( tt1, tt2, temp );

						// set interpolation restart
						irestart = INTERP_RHS;
					}
				}

				// test for horizontal clipping
				if( ( x0 < min_clip_x ) || ( x0 > max_clip_x ) ||
					( x1 < min_clip_x ) || ( x1 > max_clip_x ) ||
					( x2 < min_clip_x ) || ( x2 > max_clip_x ) )
				{
					// clip version
					// x clipping	

					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );

					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						si = sl + FIXP16_ROUND_UP;
						ti = tl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;

							ds = ( sr - sl ) / dx;
							dt = ( tr - tl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );

							ds = ( sr - sl );
							dt = ( tr - tl );
						}

						///////////////////////////////////////////////////////////////////////

						// test for x clipping, LHS
						if( xstart < min_clip_x )
						{
							// compute x overlap
							dx = min_clip_x - xstart;

							// slide interpolants over
							ui += dx * du;
							vi += dx * dv;
							wi += dx * dw;

							si += dx * ds;
							ti += dx * dt;

							// set x to left clip edge
							xstart = min_clip_x;
						}

						// test for x clipping RHS
						if( xend > max_clip_x )
							xend = max_clip_x;

						///////////////////////////////////////////////////////////////////////

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							//screen_ptr[xi] = ( (ui >> (FIXP16_SHIFT+3)) << 11) + 
							//                 ( (vi >> (FIXP16_SHIFT+2)) << 5) + 
							//                   (wi >> (FIXP16_SHIFT+3) );   

							// get textel first
							int tidx = MathHelper.Clamp( ( ( si >> FIXP16_SHIFT ) + ( ( ti >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with gouraud shading
							r_textel *= ui;
							g_textel *= vi;
							b_textel *= wi;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> ( FIXP16_SHIFT + 8 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> ( FIXP16_SHIFT + 8 ) ) + ( ( g_textel >> ( FIXP16_SHIFT + 8 ) ) << 5 ) + ( ( r_textel >> ( FIXP16_SHIFT + 8 ) ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;

							si += ds;
							ti += dt;

						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						sl += dsdyl;
						tl += dtdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						sr += dsdyr;
						tr += dtdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
								dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

								dsdyl = ( ( ts2 - ts1 ) << FIXP16_SHIFT ) / dyl;
								dtdyl = ( ( tt2 - tt1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );
								wl = ( tw1 << FIXP16_SHIFT );

								sl = ( ts1 << FIXP16_SHIFT );
								tl = ( tt1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								wl += dwdyl;

								sl += dsdyl;
								tl += dtdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;
								dwdyr = ( ( tw1 - tw2 ) << FIXP16_SHIFT ) / dyr;

								dsdyr = ( ( ts1 - ts2 ) << FIXP16_SHIFT ) / dyr;
								dtdyr = ( ( tt1 - tt2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );
								wr = ( tw2 << FIXP16_SHIFT );

								sr = ( ts2 << FIXP16_SHIFT );
								tr = ( tt2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								wr += dwdyr;

								sr += dsdyr;
								tr += dtdyr;
							}
						}
					} // end for y
				}
				else
				{
					// no x clipping
					// point screen ptr to starting line
					screen_ptr = pb.Offset + ( ystart * pb.Stride );
					for( yi = ystart; yi < yend; yi++ )
					{
						// compute span endpoints
						xstart = ( ( xl + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );
						xend = ( ( xr + FIXP16_ROUND_UP ) >> FIXP16_SHIFT );

						// compute starting points for u,v,w interpolants
						ui = ul + FIXP16_ROUND_UP;
						vi = vl + FIXP16_ROUND_UP;
						wi = wl + FIXP16_ROUND_UP;

						si = sl + FIXP16_ROUND_UP;
						ti = tl + FIXP16_ROUND_UP;

						// compute u,v interpolants
						if( ( dx = ( xend - xstart ) ) > 0 )
						{
							du = ( ur - ul ) / dx;
							dv = ( vr - vl ) / dx;
							dw = ( wr - wl ) / dx;

							ds = ( sr - sl ) / dx;
							dt = ( tr - tl ) / dx;
						}
						else
						{
							du = ( ur - ul );
							dv = ( vr - vl );
							dw = ( wr - wl );

							ds = ( sr - sl );
							dt = ( tr - tl );
						}

						// draw span
						for( xi = xstart * pb.BytesPerPixel; xi < xend * pb.BytesPerPixel; xi += pb.BytesPerPixel )
						{
							// write textel assume 5.6.5
							//screen_ptr[xi] = ( (ui >> (FIXP16_SHIFT+3)) << 11) + 
							//                 ( (vi >> (FIXP16_SHIFT+2)) << 5) + 
							//                   (wi >> (FIXP16_SHIFT+3) );   

							// get textel first
							int tidx = MathHelper.Clamp( ( ( si >> FIXP16_SHIFT ) + ( ( ti >> FIXP16_SHIFT ) << texture_shift2 ) ) * texture_bpp, 0, textureLevel0.Length - texture_bpp );
							r_textel = textureLevel0[ tidx ];
							g_textel = textureLevel0[ tidx + 1 ];
							b_textel = textureLevel0[ tidx + 2 ];
							//a_textel = textureLevel0[ tidx + 3 ];

							// modulate textel with gouraud shading
							r_textel *= ui;
							g_textel *= vi;
							b_textel *= wi;

							// finally write pixel, note that we did the math such that the results are r*32, g*64, b*32
							// hence we need to divide the results by 32,64,32 respetively, BUT since we need to shift
							// the results to fit into the destination 5.6.5 word, we can take advantage of the shifts
							// and they all cancel out for the most part, but we will need logical anding, we will do
							// it later when we optimize more...
							pb.Buffer[ screen_ptr + xi ] = ( byte )( r_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 1 ] = ( byte )( g_textel >> ( FIXP16_SHIFT + 8 ) );
							pb.Buffer[ screen_ptr + xi + 2 ] = ( byte )( b_textel >> ( FIXP16_SHIFT + 8 ) );
#if ALPHA
							pb.Buffer[ screen_ptr + xi + 3 ] = byte.MaxValue;
#endif
							//pb.Buffer[ screen_ptr + xi+3 ] = color[ 3 ];
							//screen_ptr[ xi ] = ( ( b_textel >> ( FIXP16_SHIFT + 8 ) ) + ( ( g_textel >> ( FIXP16_SHIFT + 8 ) ) << 5 ) + ( ( r_textel >> ( FIXP16_SHIFT + 8 ) ) << 11 ) );

							// interpolate u,v
							ui += du;
							vi += dv;
							wi += dw;

							si += ds;
							ti += dt;
						} // end for xi

						// interpolate u,v,w,x along right and left edge
						xl += dxdyl;
						ul += dudyl;
						vl += dvdyl;
						wl += dwdyl;

						sl += dsdyl;
						tl += dtdyl;

						xr += dxdyr;
						ur += dudyr;
						vr += dvdyr;
						wr += dwdyr;

						sr += dsdyr;
						tr += dtdyr;

						// advance screen ptr
						screen_ptr += pb.Stride;

						// test for yi hitting second region, if so change interpolant
						if( yi == yrestart )
						{
							// test interpolation side change flag
							if( irestart == INTERP_LHS )
							{
								// LHS
								dyl = ( y2 - y1 );

								dxdyl = ( ( x2 - x1 ) << FIXP16_SHIFT ) / dyl;
								dudyl = ( ( tu2 - tu1 ) << FIXP16_SHIFT ) / dyl;
								dvdyl = ( ( tv2 - tv1 ) << FIXP16_SHIFT ) / dyl;
								dwdyl = ( ( tw2 - tw1 ) << FIXP16_SHIFT ) / dyl;

								dsdyl = ( ( ts2 - ts1 ) << FIXP16_SHIFT ) / dyl;
								dtdyl = ( ( tt2 - tt1 ) << FIXP16_SHIFT ) / dyl;

								// set starting values
								xl = ( x1 << FIXP16_SHIFT );
								ul = ( tu1 << FIXP16_SHIFT );
								vl = ( tv1 << FIXP16_SHIFT );
								wl = ( tw1 << FIXP16_SHIFT );

								sl = ( ts1 << FIXP16_SHIFT );
								tl = ( tt1 << FIXP16_SHIFT );

								// interpolate down on LHS to even up
								xl += dxdyl;
								ul += dudyl;
								vl += dvdyl;
								wl += dwdyl;

								sl += dsdyl;
								tl += dtdyl;
							}
							else
							{
								// RHS
								dyr = ( y1 - y2 );

								dxdyr = ( ( x1 - x2 ) << FIXP16_SHIFT ) / dyr;
								dudyr = ( ( tu1 - tu2 ) << FIXP16_SHIFT ) / dyr;
								dvdyr = ( ( tv1 - tv2 ) << FIXP16_SHIFT ) / dyr;
								dwdyr = ( ( tw1 - tw2 ) << FIXP16_SHIFT ) / dyr;

								dsdyr = ( ( ts1 - ts2 ) << FIXP16_SHIFT ) / dyr;
								dtdyr = ( ( tt1 - tt2 ) << FIXP16_SHIFT ) / dyr;

								// set starting values
								xr = ( x2 << FIXP16_SHIFT );
								ur = ( tu2 << FIXP16_SHIFT );
								vr = ( tv2 << FIXP16_SHIFT );
								wr = ( tw2 << FIXP16_SHIFT );

								sr = ( ts2 << FIXP16_SHIFT );
								tr = ( tt2 << FIXP16_SHIFT );

								// interpolate down on RHS to even up
								xr += dxdyr;
								ur += dudyr;
								vr += dvdyr;
								wr += dwdyr;

								sr += dsdyr;
								tr += dtdyr;
							}
						}
					} // end for y
				}
			}
		}
		#endregion

		#region Vary Color / Perspective Texture
		//???
		private void DrawTriangleVaryColorPerspectiveTexture( ref PixelBuffer pb, ref RasterPolygon polygon )
		{
		}
		#endregion
	}
}

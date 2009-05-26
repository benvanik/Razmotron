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
	public partial class RasterBuffer
	{
		public void DrawLine( ref PixelBuffer pb, ref RasterLine line, bool useFixed )
		{
			if( useFixed == true )
				throw new NotImplementedException( "fixed math not implemented" );

			int x0 = ( int )( line.Vertices[ line.v0 ].PX + 0.5f );
			int y0 = ( int )( line.Vertices[ line.v0 ].PY + 0.5f );
			int x1 = ( int )( line.Vertices[ line.v1 ].PX + 0.5f );
			int y1 = ( int )( line.Vertices[ line.v1 ].PY + 0.5f );
			if( ClipLine( ref pb, ref x0, ref y0, ref x1, ref y1 ) == false )
				return;
			if( ( line.VertexFormat & RasterVertexFormat.Color ) == RasterVertexFormat.Color )
			{
#if false
                this.DrawLineVaryColor( ref pb, ref line );
#else
				line.Color[ 0 ] = line.Vertices[ line.v0 ].R;
				line.Color[ 1 ] = line.Vertices[ line.v0 ].G;
				line.Color[ 2 ] = line.Vertices[ line.v0 ].B;
				line.Color[ 3 ] = line.Vertices[ line.v0 ].A;
				this.DrawLineSolidColor( ref pb, ref line, x0, y0, x1, y1 );
#endif
			}
			else
			{
				this.DrawLineSolidColor( ref pb, ref line, x0, y0, x1, y1 );
			}
		}

		#region Clipping
		private const int CLIP_CODE_C = 0x0000;
		private const int CLIP_CODE_N = 0x0008;
		private const int CLIP_CODE_S = 0x0004;
		private const int CLIP_CODE_E = 0x0002;
		private const int CLIP_CODE_W = 0x0001;
		private const int CLIP_CODE_NE = 0x000a;
		private const int CLIP_CODE_SE = 0x0006;
		private const int CLIP_CODE_NW = 0x0009;
		private const int CLIP_CODE_SW = 0x0005;
		private bool ClipLine( ref PixelBuffer pb, ref int x0, ref int y0, ref int x1, ref int y1 )
		{
			// internal clipping codes
			int xc0 = x0, yc0 = y0,
				xc1 = x1, yc1 = y1;

			// TODO: inline
			int min_clip_x = 0, min_clip_y = 0, max_clip_x = pb.Width - 1, max_clip_y = pb.Height - 1;

			// determine codes for p1 and p2
			int p1_code = 0;
			if( y0 < min_clip_y )
				p1_code |= CLIP_CODE_N;
			else
				if( y0 > max_clip_y )
					p1_code |= CLIP_CODE_S;

			if( x0 < min_clip_x )
				p1_code |= CLIP_CODE_W;
			else
				if( x0 > max_clip_x )
					p1_code |= CLIP_CODE_E;
			int p2_code = 0;
			if( y1 < min_clip_y )
				p2_code |= CLIP_CODE_N;
			else
				if( y1 > max_clip_y )
					p2_code |= CLIP_CODE_S;

			if( x1 < min_clip_x )
				p2_code |= CLIP_CODE_W;
			else
				if( x1 > max_clip_x )
					p2_code |= CLIP_CODE_E;

			// try and trivially reject
			if( ( p1_code & p2_code ) != 0 )
				return false;

			// test for totally visible, if so leave points untouched
			if( p1_code == 0 && p2_code == 0 )
				return true;

			// determine end clip point for p1
			switch( p1_code )
			{
				case CLIP_CODE_C: break;
				case CLIP_CODE_N:
					{
						yc0 = min_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( min_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
					} break;
				case CLIP_CODE_S:
					{
						yc0 = max_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( max_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
					} break;
				case CLIP_CODE_W:
					{
						xc0 = min_clip_x;
						yc0 = ( int )( y0 + 0.5f + ( min_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
					} break;
				case CLIP_CODE_E:
					{
						xc0 = max_clip_x;
						yc0 = ( int )( y0 + 0.5f + ( max_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
					} break;
				// these cases are more complex, must compute 2 intersections
				case CLIP_CODE_NE:
					{
						// north hline intersection
						yc0 = min_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( min_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc0 < min_clip_x || xc0 > max_clip_x )
						{
							// east vline intersection
							xc0 = max_clip_x;
							yc0 = ( int )( y0 + 0.5f + ( max_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
						}
					} break;
				case CLIP_CODE_SE:
					{
						// south hline intersection
						yc0 = max_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( max_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc0 < min_clip_x || xc0 > max_clip_x )
						{
							// east vline intersection
							xc0 = max_clip_x;
							yc0 = ( int )( y0 + 0.5f + ( max_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
						}
					} break;
				case CLIP_CODE_NW:
					{
						// north hline intersection
						yc0 = min_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( min_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc0 < min_clip_x || xc0 > max_clip_x )
						{
							xc0 = min_clip_x;
							yc0 = ( int )( y0 + 0.5f + ( min_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
						}
					} break;
				case CLIP_CODE_SW:
					{
						// south hline intersection
						yc0 = max_clip_y;
						xc0 = ( int )( x0 + 0.5f + ( max_clip_y - y0 ) * ( x1 - x0 ) / ( y1 - y0 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc0 < min_clip_x || xc0 > max_clip_x )
						{
							xc0 = min_clip_x;
							yc0 = ( int )( y0 + 0.5f + ( min_clip_x - x0 ) * ( y1 - y0 ) / ( x1 - x0 ) );
						}
					} break;
				default: break;
			}

			// determine clip point for p2
			switch( p2_code )
			{
				case CLIP_CODE_C: break;
				case CLIP_CODE_N:
					{
						yc1 = min_clip_y;
						xc1 = x1 + ( min_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 );
					} break;
				case CLIP_CODE_S:
					{
						yc1 = max_clip_y;
						xc1 = x1 + ( max_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 );
					} break;
				case CLIP_CODE_W:
					{
						xc1 = min_clip_x;
						yc1 = y1 + ( min_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 );
					} break;
				case CLIP_CODE_E:
					{
						xc1 = max_clip_x;
						yc1 = y1 + ( max_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 );
					} break;
				// these cases are more complex, must compute 2 intersections
				case CLIP_CODE_NE:
					{
						// north hline intersection
						yc1 = min_clip_y;
						xc1 = ( int )( x1 + 0.5f + ( min_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc1 < min_clip_x || xc1 > max_clip_x )
						{
							// east vline intersection
							xc1 = max_clip_x;
							yc1 = ( int )( y1 + 0.5f + ( max_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 ) );
						}
					} break;
				case CLIP_CODE_SE:
					{
						// south hline intersection
						yc1 = max_clip_y;
						xc1 = ( int )( x1 + 0.5f + ( max_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc1 < min_clip_x || xc1 > max_clip_x )
						{
							// east vline intersection
							xc1 = max_clip_x;
							yc1 = ( int )( y1 + 0.5f + ( max_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 ) );
						}
					} break;
				case CLIP_CODE_NW:
					{
						// north hline intersection
						yc1 = min_clip_y;
						xc1 = ( int )( x1 + 0.5f + ( min_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc1 < min_clip_x || xc1 > max_clip_x )
						{
							xc1 = min_clip_x;
							yc1 = ( int )( y1 + 0.5f + ( min_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 ) );
						}
					} break;
				case CLIP_CODE_SW:
					{
						// south hline intersection
						yc1 = max_clip_y;
						xc1 = ( int )( x1 + 0.5f + ( max_clip_y - y1 ) * ( x0 - x1 ) / ( y0 - y1 ) );
						// test if intersection is valid, of so then done, else compute next
						if( xc1 < min_clip_x || xc1 > max_clip_x )
						{
							xc1 = min_clip_x;
							yc1 = ( int )( y1 + 0.5f + ( min_clip_x - x1 ) * ( y0 - y1 ) / ( x0 - x1 ) );
						}
					} break;
				default: break;
			}

			// do bounds check
			if( ( xc0 < min_clip_x ) || ( xc0 > max_clip_x ) ||
				( yc0 < min_clip_y ) || ( yc0 > max_clip_y ) ||
				( xc1 < min_clip_x ) || ( xc1 > max_clip_x ) ||
				( yc1 < min_clip_y ) || ( yc1 > max_clip_y ) )
				return false;

			x0 = xc0; y0 = yc0;
			x1 = xc1; y1 = yc1;
			return true;
		}
		#endregion

		#region Solid Color
		private void DrawLineSolidColor( ref PixelBuffer pb, ref RasterLine line, int x0, int y0, int x1, int y1 )
		{
			byte[] color = new byte[ 4 ]{
                ( byte )( line.Color[ 0 ] * 255.0f ),
                ( byte )( line.Color[ 1 ] * 255.0f ),
                ( byte )( line.Color[ 2 ] * 255.0f ),
                ( byte )( line.Color[ 3 ] * 255.0f ),
            };

			// pre-compute first pixel address in video buffer based on 16bit data
			int pbOffset = pb.Offset + ( y0 * pb.Stride ) + ( x0 * pb.BytesPerPixel );

			// compute horizontal and vertical deltas
			int dx = x1 - x0; // diff in x's
			int dy = y1 - y0; // diff in y's
			// test which direction the line is going in i.e. slope angle
			int x_inc; // step along x
			if( dx >= 0 )
			{
				// moving right
				x_inc = pb.BytesPerPixel;
			}
			else
			{
				// moving left
				x_inc = -pb.BytesPerPixel;
				dx = -dx;  // need absolute value
			}
			// test y component of slope
			int y_inc; // step along y
			if( dy >= 0 )
			{
				// moving down
				y_inc = pb.Stride;
			}
			else
			{
				// moving up
				y_inc = -pb.Stride;
				dy = -dy;  // need absolute value
			}
			// compute (dx,dy) * 2
			int dx2 = dx * 2;
			int dy2 = dy * 2;
			// now based on which delta is greater we can draw the line
			if( dx > dy )
			{
				int error = dy2 - dx; // the discriminant i.e. error i.e. decision variable
				// draw the line
				if( pb.BytesPerPixel == 3 )
				{
					for( int index = 0; index <= dx; index++ )
					{
						// set the pixel
						pb.Buffer[ pbOffset ] = color[ 0 ];
						pb.Buffer[ pbOffset + 1 ] = color[ 1 ];
						pb.Buffer[ pbOffset + 2 ] = color[ 2 ];
						// test if error has overflowed
						if( error >= 0 )
						{
							error -= dx2;
							// move to next line
							pbOffset += y_inc;
						}
						// adjust the error term
						error += dy2;
						// move to the next pixel
						pbOffset += x_inc;
					}
				}
				else
				{
					for( int index = 0; index <= dx; index++ )
					{
						pb.Buffer[ pbOffset ] = color[ 0 ];
						pb.Buffer[ pbOffset + 1 ] = color[ 1 ];
						pb.Buffer[ pbOffset + 2 ] = color[ 2 ];
						pb.Buffer[ pbOffset + 3 ] = color[ 3 ];
						if( error >= 0 )
						{
							error -= dx2;
							pbOffset += y_inc;
						}
						error += dy2;
						pbOffset += x_inc;
					}
				}
			}
			else
			{
				int error = dx2 - dy; // the discriminant i.e. error i.e. decision variable
				// draw the line
				if( pb.BytesPerPixel == 3 )
				{
					for( int index = 0; index <= dy; index++ )
					{
						// set the pixel
						pb.Buffer[ pbOffset ] = color[ 0 ];
						pb.Buffer[ pbOffset + 1 ] = color[ 1 ];
						pb.Buffer[ pbOffset + 2 ] = color[ 2 ];
						// test if error overflowed
						if( error >= 0 )
						{
							error -= dy2;
							// move to next line
							pbOffset += x_inc;
						}
						// adjust the error term
						error += dx2;
						// move to the next pixel
						pbOffset += y_inc;
					}
				}
				else
				{
					for( int index = 0; index <= dy; index++ )
					{
						pb.Buffer[ pbOffset ] = color[ 0 ];
						pb.Buffer[ pbOffset + 1 ] = color[ 1 ];
						pb.Buffer[ pbOffset + 2 ] = color[ 2 ];
						pb.Buffer[ pbOffset + 3 ] = color[ 3 ];
						if( error >= 0 )
						{
							error -= dy2;
							pbOffset += x_inc;
						}
						error += dx2;
						pbOffset += y_inc;
					}
				}
			}
		}
		#endregion

		#region Vary Color
		private void DrawLineVaryColor( ref PixelBuffer pb, ref RasterLine line, int x0, int y0, int x1, int y1 )
		{
		}
		#endregion
	}
}

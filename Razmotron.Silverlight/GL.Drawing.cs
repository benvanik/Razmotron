//#define DRAW_LINE

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
using Razmotron.Internal;
using Razmotron.Rasterization;

namespace Razmotron
{
	public partial class GL
	{
		internal Vector3 State_Normal = new Vector3( 0, 0, 1 );
		internal float[] State_Color = new float[ 4 ] { 1, 1, 1, 1 };
		internal float[][] State_TexCoord = new float[ TextureStages ][]{
				new float[]{ 0, 0, 0, 1 },
				new float[]{ 0, 0, 0, 1 },
			};
		internal float State_PointSize = 1.0f;

		internal const int TextureStages = 2;
		internal int State_TextureStage;

		#region DrawingPointer
		private struct DrawingPointer
		{
			public readonly int Size;
			public readonly int Stride;
			public readonly int Offset;
			public float[] Floats;
			public byte[] Bytes;
			public DrawingPointer( int size, int stride, int offset )
			{
				this.Size = size;
				this.Stride = stride;
				this.Offset = offset;
				this.Floats = null;
				this.Bytes = null;
			}
		}
		private DrawingPointer Ptr_Vertex;
		private DrawingPointer Ptr_Normal;
		private DrawingPointer Ptr_Color;
		private DrawingPointer[] Ptr_TexCoord = new DrawingPointer[ TextureStages ];
		private DrawingPointer Ptr_PointSize;
		internal bool Ptr_VertexEnabled;
		internal bool Ptr_NormalEnabled;
		internal bool Ptr_ColorEnabled;
		internal bool Ptr_TexCoordEnabled;
		internal bool Ptr_PointSizeEnabled;
		#endregion

		private const int VertexCacheCount = 16 * 1024;
		private const int IndexCacheCount = 16 * 1024;
		private RasterVertex[] _vertices = new RasterVertex[ VertexCacheCount ];
		private ushort[] _indices = new ushort[ IndexCacheCount ];
		private ushort[] _ascIndices = new ushort[ IndexCacheCount ]; // 0, 1, 2, ...

		private void SetupDrawing()
		{
			for( ushort n = 0; n < _ascIndices.Length; n++ )
				_ascIndices[ n ] = n;
		}

		#region Default States / Pointers

		public void glActiveTexture( int texture ) { State_TextureStage = texture - GL_TEXTURE0; }
		public void glClientActiveTexture( int texture ) { State_TextureStage = texture - GL_TEXTURE0; }

		public void glNormal3f( float nx, float ny, float nz )
		{
			State_Normal = new Vector3( nx, ny, nz );
		}
		public void glNormal( ref Vector3 n )
		{
			State_Normal = n;
		}
		public void glColor4f( float red, float green, float blue, float alpha )
		{
			State_Color[ 0 ] = red;
			State_Color[ 1 ] = green;
			State_Color[ 2 ] = blue;
			State_Color[ 3 ] = alpha;
		}
		public void glColor4ub( byte red, byte green, byte blue, byte alpha )
		{
			State_Color[ 0 ] = red / ( float )byte.MaxValue;
			State_Color[ 1 ] = green / ( float )byte.MaxValue;
			State_Color[ 2 ] = blue / ( float )byte.MaxValue;
			State_Color[ 3 ] = alpha / ( float )byte.MaxValue;
		}
		public void glColor( Color color )
		{
			State_Color[ 0 ] = color.R / ( float )byte.MaxValue;
			State_Color[ 1 ] = color.G / ( float )byte.MaxValue;
			State_Color[ 2 ] = color.B / ( float )byte.MaxValue;
			State_Color[ 3 ] = color.A / ( float )byte.MaxValue;
		}
		public void glMultiTexCoord4f( int target, float s, float t, float r, float q )
		{
			State_TexCoord[ target - GL_TEXTURE0 ][ 0 ] = s;
			State_TexCoord[ target - GL_TEXTURE0 ][ 1 ] = t;
			State_TexCoord[ target - GL_TEXTURE0 ][ 2 ] = r;
			State_TexCoord[ target - GL_TEXTURE0 ][ 3 ] = q;
		}

		//public void glPointParameterf( int pname, float param ) { }
		//public void glPointParameterfv( int pname, float[] params_ ) { }
		public void glPointSize( float size ) { State_PointSize = size; }

		// size 2,3,4, byte, short, fixed, float
		//public void glVertexPointer( int size, int type, int stride, void* pointer ) { }
		public void glVertexPointer( int size, int stride, float[] pointer, int offset )
		{
			Ptr_Vertex = new DrawingPointer( size, stride, offset );
			Ptr_Vertex.Floats = pointer;
		}

		// size 3, byte, short, fixed, float
		//public void glNormalPointer( int type, int stride, void* pointer ) { }
		public void glNormalPointer( int size, int stride, float[] pointer, int offset )
		{
			Ptr_Normal = new DrawingPointer( size, stride, offset );
			Ptr_Normal.Floats = pointer;
		}

		// size 4, ubyte, fixed, float
		//public void glColorPointer( int size, int type, int stride, void* pointer ) { }
		public void glColorPointer( int size, int stride, float[] pointer, int offset )
		{
			Ptr_Color = new DrawingPointer( size, stride, offset );
			Ptr_Color.Floats = pointer;
		}

		// size 2,3,4, byte, short, fixed, float
		//public void glTexCoordPointer( int size, int type, int stride, void* pointer ) { }
		public void glTexCoordPointer( int size, int stride, float[] pointer, int offset )
		{
			Ptr_TexCoord[ State_TextureStage ] = new DrawingPointer( size, stride, offset );
			Ptr_TexCoord[ State_TextureStage ].Floats = pointer;
		}

		// size 1, fixed, float
		//public void glPointSizePointerOES( int type, int stride, void* pointer ){}
		public void glPointSizePointerOES( int stride, float[] pointer, int offset )
		{
			Ptr_PointSize = new DrawingPointer( 1, stride, offset );
			Ptr_PointSize.Floats = pointer;
		}

		public void glEnableClientState( int array )
		{
			switch( array )
			{
				case GL_VERTEX_ARRAY:
					Ptr_VertexEnabled = true;
					break;
				case GL_NORMAL_ARRAY:
					Ptr_NormalEnabled = true;
					break;
				case GL_COLOR_ARRAY:
					Ptr_ColorEnabled = true;
					break;
				case GL_TEXTURE_COORD_ARRAY:
					Ptr_TexCoordEnabled = true;
					break;
				case GL_POINT_SIZE_ARRAY_OES:
					Ptr_PointSizeEnabled = true;
					break;
			}
		}

		public void glDisableClientState( int array )
		{
			switch( array )
			{
				case GL_VERTEX_ARRAY:
					Ptr_VertexEnabled = false;
					break;
				case GL_NORMAL_ARRAY:
					Ptr_NormalEnabled = false;
					break;
				case GL_COLOR_ARRAY:
					Ptr_ColorEnabled = false;
					break;
				case GL_TEXTURE_COORD_ARRAY:
					Ptr_TexCoordEnabled = false;
					break;
				case GL_POINT_SIZE_ARRAY_OES:
					Ptr_PointSizeEnabled = false;
					break;
			}
		}

		#endregion

		public void glDrawArrays( int mode, int first, int count )
		{
			glDrawElements( mode, count, _ascIndices, first );
		}

		public void glDrawElements( int mode, int count, byte[] indices, int offset )
		{
			// TODO: actually process drawelements byte right, as opposed to this nasty laziness
			for( int n = 0; n < count; n++ )
				_indices[ n ] = indices[ offset + n ];
			glDrawElements( mode, count, _indices, 0 );
		}

		public void glDrawElements( int mode, int count, ushort[] indices, int offset )
		{
			if( Ptr_VertexEnabled == false )
				return;

			float viewportWidthHalf = Viewport_Width / 2.0f;
			float viewportHeightHalf = Viewport_Height / 2.0f;
			float viewportAspectRatio = Viewport_Width / ( float )Viewport_Height;
			Matrix projectionMatrix = M_Proj[ M_ProjIndex ];
			bool projectionIsAffine = projectionMatrix.IsAffine;
			Matrix modelViewMatrix = M_ModelView[ M_ModelViewIndex ];
			bool modelViewIsIdentity = modelViewMatrix.IsIdentity;
			bool modelViewIsAffine = modelViewMatrix.IsAffine;
			Matrix textureMatrix = M_Texture[ M_TextureIndex ];
			bool textureIsIdentity = textureMatrix.IsIdentity;
			bool textureIsAffine = textureMatrix.IsAffine;
			Matrix modelProjMatrix;
			Matrix.Multiply( ref modelViewMatrix, ref projectionMatrix, out modelProjMatrix );
			bool modelProjMatrixIsAffine = modelProjMatrix.IsAffine;
			Matrix finalProjectionMatrix;
			Matrix.Multiply( ref projectionMatrix, ref M_Viewport, out finalProjectionMatrix );

			RasterVertexFormat vertexFormat = RasterVertexFormat.Position | RasterVertexFormat.Normal;
			if( ( State_ShadeModel == GL_SMOOTH ) && ( Ptr_ColorEnabled == true ) )
				vertexFormat |= RasterVertexFormat.Color;
			if( ( State_Texture[ 0 ] != null ) && ( Ptr_TexCoordEnabled == true ) ) // TODO: something with tex coord state?
				vertexFormat |= RasterVertexFormat.Texture;

			RasterPolygon polygon = new RasterPolygon();
			polygon.VertexFormat = vertexFormat;
			polygon.Vertices = _vertices;
			polygon.Color = ( float[] )State_Color.Clone();
			polygon.PerspectiveTexturing = ( State_PerspectiveCorrection == GL_NICEST );
			polygon.Textures = new TextureObject[ RasterPolygon.MaximumTextureStages ]{
                State_Texture[ 0 ],
                State_Texture[ 1 ]
            };

#if DRAW_LINE
			RasterLine line = new RasterLine();
			line.VertexFormat = polygon.VertexFormat;
			line.Vertices = polygon.Vertices;
			line.Color = ( float[] )State_Color.Clone();
#endif

			// Build RasterVertex buffer from input
			// TODO: maybe have different loops for different modes - depends on if this is a bottleneck with all the compares/etc
			for( int n = offset, v = 0; n < offset + count; n++, v++ )
			{
				int index = indices[ n ];
				{
					int idx = Ptr_Vertex.Offset + ( index * ( Ptr_Vertex.Stride + Ptr_Vertex.Size ) );
					_vertices[ v ].W = 1.0f;
					_vertices[ v ].X = Ptr_Vertex.Floats[ idx ];
					_vertices[ v ].Y = Ptr_Vertex.Floats[ idx + 1 ];
					// TODO: make this faster - eliminate switch
					if( Ptr_Vertex.Size >= 3 )
					{
						_vertices[ v ].Z = Ptr_Vertex.Floats[ idx + 2 ];
						if( Ptr_Vertex.Size == 4 )
							_vertices[ v ].W = Ptr_Vertex.Floats[ idx + 3 ];
					}
				}
				if( Ptr_NormalEnabled == true )
				{
					int idx = Ptr_Normal.Offset + ( index * ( Ptr_Normal.Stride + Ptr_Normal.Size ) );
					_vertices[ v ].NX = Ptr_Normal.Floats[ idx ];
					_vertices[ v ].NY = Ptr_Normal.Floats[ idx + 1 ];
					_vertices[ v ].NZ = Ptr_Normal.Floats[ idx + 2 ];
				}
				else
				{
					_vertices[ v ].NX = State_Normal.X; _vertices[ v ].NY = State_Normal.Y; _vertices[ v ].NZ = State_Normal.Z;
				}
				if( Ptr_ColorEnabled == true )
				{
					int idx = Ptr_Color.Offset + ( index * ( Ptr_Color.Stride + Ptr_Color.Size ) );
					_vertices[ v ].R = Ptr_Color.Floats[ idx ];
					_vertices[ v ].G = Ptr_Color.Floats[ idx + 1 ];
					_vertices[ v ].B = Ptr_Color.Floats[ idx + 2 ];
					_vertices[ v ].A = Ptr_Color.Floats[ idx + 3 ];
				}
				if( Ptr_TexCoordEnabled == true )
				{
					int idx = Ptr_TexCoord[ 0 ].Offset + ( index * ( Ptr_TexCoord[ 0 ].Stride + Ptr_TexCoord[ 0 ].Size ) );
					_vertices[ v ].S = Ptr_TexCoord[ 0 ].Floats[ idx ];
					_vertices[ v ].T = Ptr_TexCoord[ 0 ].Floats[ idx + 1 ];
#if false
                    if( Ptr_TexCoord[ 0 ].Size >= 3 )
                    {
                        _vertices[ v ].R = Ptr_TexCoord[ 0 ].Floats[ idx + 2 ];
                        if( Ptr_TexCoord[ 0 ].Size == 4 )
                            _vertices[ v ].Q = Ptr_TexCoord[ 0 ].Floats[ idx + 3 ];
                        else
                            _vertices[ v ].Q = 1.0f;
                    }
                    else
                    {
                        _vertices[ v ].R = 0.0f;
                        _vertices[ v ].Q = 1.0f;
                    }
#endif
				}
				else
				{
					_vertices[ v ].S = State_TexCoord[ 0 ][ 0 ]; _vertices[ v ].T = State_TexCoord[ 0 ][ 1 ];
#if false
                    _vertices[ v ].R = State_TexCoord[ 0 ][ 2 ]; _vertices[ v ].Q = State_TexCoord[ 0 ][ 3 ];
#endif
				}
				if( ( vertexFormat & RasterVertexFormat.Texture ) == RasterVertexFormat.Texture )
				{
					if( textureIsIdentity == false )
					{
						float v0 = _vertices[ v ].S;
						float v1 = _vertices[ v ].T;
#if true
						_vertices[ v ].S = v0 * textureMatrix.M11 + v1 * textureMatrix.M21 + textureMatrix.M41;
						_vertices[ v ].T = v0 * textureMatrix.M12 + v1 * textureMatrix.M22 + textureMatrix.M42;
#else
                        float v2 = v.R;
                        float v3 = v.Q;
                        _vertices[ v ].S = v0 * textureMatrix.M11 + v1 * textureMatrix.M21 + v2 * textureMatrix.M31 + v3 * textureMatrix.M41;
                        _vertices[ v ].T = v0 * textureMatrix.M12 + v1 * textureMatrix.M22 + v2 * textureMatrix.M32 + v3 * textureMatrix.M42;
                        _vertices[ v ].R = v0 * tmat.M13 + v1 * tmat.M23 + v2 * tmat.M33 + v3 * tmat.M42;
                        _vertices[ v ].Q = v0 * tmat.M14 + v1 * tmat.M24 + v2 * tmat.M34 + v3 * tmat.M43;
#endif
					}
					// RasterVertex expects texture coordinates multiplied out
					_vertices[ v ].S *= State_Texture[ 0 ].Width;
					_vertices[ v ].T *= State_Texture[ 0 ].Height;
					// TODO: clamp texture coords here?
				}

				// Transform
				Viewport_Far = 100.0f;
				Viewport_Near = 0.1f;
				{
					// World -> Model/View
					//Vector4 vec_w = new Vector4()
					Vector3 vec_w = new Vector3()
					{
						X = _vertices[ v ].X,
						Y = _vertices[ v ].Y,
						Z = _vertices[ v ].Z,
						//W = 1.0f,
					};
					//Vector4 vec_c;
					Vector3 vec_c;
					Matrix.TransformAffine( ref modelViewMatrix, modelViewIsAffine, ref vec_w, out vec_c );
					//Matrix.Multiply( ref modelViewMatrix, ref vec_w, out vec_c );
					// Clip against near/far plane
					if( ( vec_c.Z > Viewport_Far ) || ( vec_c.Z < Viewport_Near ) )
						_vertices[ v ].State = RasterVertexState.Clipped;
					else
					{
						// Clip against view frustum
						// TODO: clipping of vertices
						_vertices[ v ].State = RasterVertexState.Normal;
					}

					_vertices[ v ].X = vec_c.X;
					_vertices[ v ].Y = vec_c.Y;
					_vertices[ v ].Z = vec_c.Z;

					// Model/View -> Screen
					Vector3 vec_s;
					Matrix.TransformAffine( ref projectionMatrix, projectionIsAffine, ref vec_c, out vec_s );
					//Vector4 vec_s;
					//Matrix.Multiply( ref projectionMatrix, ref vec_c, out vec_s );
					//Matrix.TransformAffine( ref modelProjMatrix, modelProjMatrixIsAffine, ref vec_w, out vec_s );
					_vertices[ v ].PX = vec_s.X;
					_vertices[ v ].PY = vec_s.Y;
					//_vertices[ v ].Z = vec_s.Z;
					//float det = 1.0f / ( vec_c.Z * projectionMatrix.M34 );
					//_vertices[ v ].PX = ( vec_c.X * projectionMatrix.M11 ) * det;
					//_vertices[ v ].PY = ( vec_c.Y * projectionMatrix.M22 ) * det;

					// Adjust to screen space
					_vertices[ v ].PX = ( _vertices[ v ].PX + viewportWidthHalf );
					_vertices[ v ].PY = -_vertices[ v ].PY * viewportAspectRatio + viewportHeightHalf;
				}
			}

			/*
			//Matrix.TransformAffine( ref _projectionMatrix, false, ref cp, out tp );
			// UNSAFE: no clue if this works for anything but the settings I had when I wrote it
			float det = 1.0f / ( cp.Z * projectionMatrix.M34 );
			tp.X = ( cp.X * projectionMatrix.M11 ) * det;
			tp.Y = ( cp.Y * projectionMatrix.M22 ) * det;

			//// World->Screen
			//Vector3 tp;
			//Matrix.TransformAffine( ref finalProjectionMatrix, finalProjectionMatrix.IsAffine, ref tx, out tp );
			//// UNSAFE: no clue if this works for anything but the settings I had when I wrote it
			////float det = 1.0f / ( tx.Z * projectionMatrix.M34 );
			////tp.X = ( tx.X * projectionMatrix.M11 ) * det;
			////tp.Y = ( tx.Y * projectionMatrix.M22 ) * det;
			////tp.Z = ( tx.Z * projectionMatrix.M33 ) * det;
			 */

			PixelBuffer pb;
			this.RasterBuffer.LockPixels( out pb );
			switch( mode )
			{
				case GL_POINTS:
					break;
				case GL_LINES:
					break;
				case GL_LINE_STRIP:
				case GL_LINE_LOOP:
					break;
				case GL_TRIANGLES:
					for( int n = offset; n < offset + count; n += 3 )
					{
						polygon.v0 = indices[ n ];
						polygon.v1 = indices[ n + 1 ];
						polygon.v2 = indices[ n + 2 ];

						// Clipping
						// NOTE: vertices that are clipped will have clip flags set already
						if( ( _vertices[ polygon.v0 ].State == RasterVertexState.Clipped ) && ( _vertices[ polygon.v1 ].State == RasterVertexState.Clipped ) && ( _vertices[ polygon.v2 ].State == RasterVertexState.Clipped ) )
							continue;

						// Backface culling
						if( State_CullFace == true )
						{
							// TODO: inline this math?
							// u = v0->v1, v = v0->v2, n = u x v
							Vector3 u = new Vector3()
							{
								X = _vertices[ polygon.v1 ].X - _vertices[ polygon.v0 ].X,
								Y = _vertices[ polygon.v1 ].Y - _vertices[ polygon.v0 ].Y,
								Z = _vertices[ polygon.v1 ].Z - _vertices[ polygon.v0 ].Z,
							};
							Vector3 v = new Vector3()
							{
								X = _vertices[ polygon.v2 ].X - _vertices[ polygon.v0 ].X,
								Y = _vertices[ polygon.v2 ].Y - _vertices[ polygon.v0 ].Y,
								Z = _vertices[ polygon.v2 ].Z - _vertices[ polygon.v0 ].Z,
							};
							Vector3 normal;
							Vector3.CrossProduct( ref u, ref v, out normal );
							float dot = ( normal.X * _vertices[ polygon.v0 ].X ) + ( normal.Y * _vertices[ polygon.v0 ].Y ) + ( normal.Z * _vertices[ polygon.v0 ].Z );
							if( State_FrontFace == GL_CCW )
								dot *= -1.0f;
							if( State_CullFaceMode == GL_FRONT )
								dot *= -1.0f;
							// > 0 = visible, 0 = scathing, < 0 = invisible
							if( dot <= 0.0f )
							{
								polygon.State = RasterPolygonState.BackfaceCulled;
								continue;
							}
						}
						else
							polygon.State = RasterPolygonState.Valid;

						// etc

						this.RasterBuffer.DrawTriangle( ref pb, ref polygon, false );

#if DRAW_LINE
						line.v0 = polygon.v0; line.v1 = polygon.v1;
						this.RasterBuffer.DrawLine( ref pb, ref line, false );
						line.v0 = polygon.v1; line.v1 = polygon.v2;
						this.RasterBuffer.DrawLine( ref pb, ref line, false );
						line.v0 = polygon.v2; line.v1 = polygon.v0;
						this.RasterBuffer.DrawLine( ref pb, ref line, false );
#endif
					}
					break;
				case GL_TRIANGLE_STRIP:
					break;
				case GL_TRIANGLE_FAN:
					break;
			}
			this.RasterBuffer.UnlockPixels();
		}

		// Fixed
#if false
		public void glNormal3x( GLfixed nx, GLfixed ny, GLfixed nz ) { }
		public void glColor4x( GLfixed red, GLfixed green, GLfixed blue, GLfixed alpha ) { }
		public void glMultiTexCoord4x( int target, GLfixed s, GLfixed t, GLfixed r, GLfixed q ) { }

		public void glPointParameterx( int pname, GLfixed param ){}
		public void glPointParameterxv( int pname, GLfixed[] params_ ) { }
		public void glPointSizex( GLfixed size ) { }
		public void glPointSizePointerOES( int stride, GLfixed[] pointer, int offset ) { }
#endif
	}
}

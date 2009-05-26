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

namespace Razmotron
{
	public partial class GL
	{
		internal ObjectList<TextureObject> Textures = new ObjectList<TextureObject>();
		internal TextureObject[] State_Texture = new TextureObject[ TextureStages ];

		public bool glIsTexture( uint texture )
		{
			TextureObject value = Textures[ texture ];
			return ( value != null );
		}
		public void glGenTextures( uint[] textures )
		{
			Textures.Allocate( textures );
		}
		public void glDeleteTextures( uint[] textures )
		{
			Textures.Deallocate( textures );
		}

		// target should always be GL_TEXTURE_2D
		public void glBindTexture( int target, uint texture )
		{
			TextureObject tex = Textures[ texture ];
			// tex may be null
			State_Texture[ State_TextureStage ] = tex;
		}

		public void glTexImage2D( int target, int level, int internalformat, int width, int height, int border, int format, int type, byte[] pixels )
		{
			TextureObject tex = State_Texture[ State_TextureStage ];
			tex.Width = width;
			tex.Height = height;
			tex.BaseLevel = 0;
			tex.Levels = new TextureLevel[ 1 ] {
                new TextureLevel( width, height, width * 3, 3, pixels )
            };
		}

		//public void glTexSubImage2D( int target, int level, int xoffset, int yoffset, int width, int height, int format, int type, void* pixels ){}
		//public void glCompressedTexImage2D( int target, int level, int internalformat, int width, int height, int border, int imageSize, void* data ){}
		//public void glCompressedTexSubImage2D( int target, int level, int xoffset, int yoffset, int width, int height, int format, int imageSize, void* data ){}
		//public void glCopyTexImage2D( int target, int level, int internalformat, int x, int y, int width, int height, int border ){}
		//public void glCopyTexSubImage2D( int target, int level, int xoffset, int yoffset, int x, int y, int width, int height ){}

		//public void glGetTexParameterfv( int target, int pname, float[] params_ ){}
		//public void glGetTexParameteriv( int target, int pname, int[] params_ ){}

		//public void glTexParameterf( int target, int pname, float param ){}
		//public void glTexParameteri( int target, int pname, int param ){}
		//public void glTexParameterfv( int target, int pname, float[] params_ ){}
		//public void glTexParameteriv( int target, int pname, int[] params_ ){}

		public void glPixelStorei( int pname, int param )
		{
			switch( pname )
			{
				case GL_UNPACK_ALIGNMENT:
					{
						TextureObject tex = State_Texture[ State_TextureStage ];
						if( tex != null )
							tex.UnpackAlignment = param;
					}
					break;
			}
		}

#if false
		public void glGetTexParameterxv( int target, int pname, GLfixed[] params_ ){}
		public void glTexParameterx( int target, int pname, GLfixed param ){}
		public void glTexParameterxv( int target, int pname, GLfixed[] params_ ){}
#endif
	}
}

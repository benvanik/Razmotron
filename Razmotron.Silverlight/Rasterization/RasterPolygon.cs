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
	// NOTES:
	// - X/Y positions are expected to be in projected screen coordinates
	// - Texture coordinates are [0,tex_width] & [0,tex_height] - not [0,1]
	// - TextureData is assumed to be

	public enum RasterPolygonState
	{
		Valid,
		Clipped,
		BackfaceCulled,
	}

	public struct RasterPolygon
	{
		public RasterPolygonState State;

		public RasterVertexFormat VertexFormat;
		public RasterVertex[] Vertices;
		public int v0;
		public int v1;
		public int v2;

		public float[] Color;

		public bool PerspectiveTexturing;
		public const int MaximumTextureStages = 2;
		public int TextureStages;
		public TextureObject[] Textures;
	}
}

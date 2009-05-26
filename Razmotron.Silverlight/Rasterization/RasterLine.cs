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

	public class RasterLine
	{
		public RasterVertexFormat VertexFormat;
		public RasterVertex[] Vertices;
		public int v0;
		public int v1;

		public float[] Color;
	}
}

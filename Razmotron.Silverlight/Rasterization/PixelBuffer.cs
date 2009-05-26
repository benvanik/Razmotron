﻿using System;
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
	public struct PixelBuffer
	{
		public byte[] Buffer;
		public int Offset;
		public int Width; // Length of a row, in pixels
		public int Height;
		public int Stride; // Total length of a row, in bytes
		public int BytesPerPixel;

		public float[] ZBuffer;
	}
}

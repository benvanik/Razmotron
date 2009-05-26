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
	public struct DirtyRegion
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;

		public static readonly DirtyRegion Empty = new DirtyRegion( int.MaxValue, int.MinValue, int.MaxValue, int.MinValue );

		public DirtyRegion( int left, int right, int top, int bottom )
		{
			this.Left = left;
			this.Right = right;
			this.Top = top;
			this.Bottom = bottom;
		}

		public void Expand( int x, int y )
		{
			if( x < Left )
				Left = x;
			if( x > Right )
				Right = x;
			if( y < Top )
				Top = y;
			if( y > Bottom )
				Bottom = y;
		}

		public void Reset()
		{
			Left = int.MaxValue;
			Right = int.MinValue;
			Top = int.MaxValue;
			Bottom = int.MinValue;
		}

		public bool IsEmpty
		{
			get { return Left == int.MaxValue; }
		}
	}
}

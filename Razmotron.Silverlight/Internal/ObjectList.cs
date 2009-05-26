using System;
using System.Collections;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Razmotron.Internal
{
	internal class ObjectList<T> where T : IDisposable, new()
	{
		private T[] _list = new T[ 128 ];
		private int _count;
		private int _index;

		public void Allocate( uint[] ids )
		{
			if( _count + ids.Length > _list.Length )
				Array.Resize<T>( ref _list, _list.Length * 2 );
			_count += ids.Length;

			int m = 0;
			for( int n = 0; n < _list.Length; n++ )
			{
				int realIndex = n + _index;
				if( _list[ realIndex ] == null )
				{
					_list[ realIndex ] = new T();
					ids[ m ] = ( uint )realIndex;
					m++;
					if( m == ids.Length )
						break;
				}
				_index++;
				if( _index >= _list.Length )
					_index = 0;
			}
		}

		public void Deallocate( uint[] ids )
		{
			_count -= ids.Length;
			for( int m = 0; m < ids.Length; m++ )
			{
				T value = _list[ ids[ m ] ];
				if( value != null )
					value.Dispose();
				_list[ ids[ m ] ] = default( T );
			}
		}

		public T this[ uint id ]
		{
			get
			{
				if( ( id < 0 ) || ( id > _list.Length ) )
					return default( T );
				return _list[ id ];
			}
		}
	}
}

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

namespace Razmotron.Utilities
{
	// Fast CRC32 from http://www.cl.cam.ac.uk/research/srg/bluebook/21/crc/node6.html
	internal static class CRC32
	{
		private const uint QUOTIENT = 0x04c11db7;
		private static readonly uint[] _crcTable = GenerateTable();

		private static uint[] GenerateTable()
		{
			uint[] table = new uint[ 256 ];
			for( uint i = 0; i < 256; i++ )
			{
				uint crc = i << 24;
				for( int j = 0; j < 8; j++ )
				{
					if( ( crc & 0x80000000 ) > 0 )
						crc = ( crc << 1 ) ^ QUOTIENT;
					else
						crc = crc << 1;
				}
				table[ i ] = ( uint )IPAddress.HostToNetworkOrder( ( int )crc );
			}
			return table;
		}

		public static uint Calculate( byte[] buffer, int offset, int length )
		{
			if( length < 4 )
				return 0;
			int index = offset;
			int end = offset + length;
			uint result = ( uint )~buffer[ index++ ];
			if( BitConverter.IsLittleEndian == true )
			{
				while( index < end )
				{
					result = _crcTable[ result & 0xFF ] ^ result >> 8;
					result = _crcTable[ result & 0xFF ] ^ result >> 8;
					result = _crcTable[ result & 0xFF ] ^ result >> 8;
					result = _crcTable[ result & 0xFF ] ^ result >> 8;
					result ^= ( uint )buffer[ index++ ];
				}
			}
			else
			{
				while( index < end )
				{
					result = _crcTable[ result >> 24 ] ^ result << 8;
					result = _crcTable[ result >> 24 ] ^ result << 8;
					result = _crcTable[ result >> 24 ] ^ result << 8;
					result = _crcTable[ result >> 24 ] ^ result << 8;
					result ^= ( uint )buffer[ index++ ];
				}
			}
			return ~result;
		}
	}
}

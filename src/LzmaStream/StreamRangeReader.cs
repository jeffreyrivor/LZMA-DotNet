using System;
using System.IO;

namespace Lzma
{
    class StreamRangeReader
    {
        private const uint kTopValue = 1u << 24;

        private readonly Stream Stream;
        private uint Range = 0xFFFFFFFF;
        private uint Code;

        public StreamRangeReader(in Stream stream)
        {
            Stream = stream;

            if (Stream.ReadByte() != 0)
            {
                throw new InvalidDataException();
            }

            var buffer = new byte[sizeof(uint)];
            if (Stream.Read(buffer, 0, sizeof(uint)) != sizeof(uint))
            {
                throw new InvalidDataException();
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            Code = BitConverter.ToUInt32(buffer, 0);
            if (Code == Range)
            {
                throw new InvalidDataException();
            }
        }

        private void Normalize()
        {
            while (Range < kTopValue)
            {
                Code <<= 8;
                Code |= (byte)Stream.ReadByte();

                Range <<= 8;
            }
        }

        public bool Decode(in uint size0, in int numTotalBits)
        {
            var newBound = (Range >> numTotalBits) * size0;
            if (Code < newBound)
            {
                Range = newBound;
                Normalize();
                return false;
            }

            Range -= newBound;
            Code -= newBound;
            Normalize();
            return true;
        }

        public uint DecodeDirectBits(int numTotalBits)
        {
            var result = 0u;

            while (numTotalBits-- > 0)
            {
                Range >>= 1;

                var t = (Code - Range) >> 31;
                Code -= Range & (t - 1);
                result = (result << 1) | (1 - t);

                Normalize();
            }

            return result;
        }
    }
}

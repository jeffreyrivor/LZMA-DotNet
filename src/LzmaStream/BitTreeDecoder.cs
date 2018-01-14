namespace Lzma
{
    class BitTreeDecoder
    {
        private readonly int NumBitLevels;
        private readonly BitDecoder[] Models;

        public BitTreeDecoder(int numBitLevels)
        {
            NumBitLevels = numBitLevels;
            Models = new BitDecoder[1 << numBitLevels];
        }

        public uint Decode(in StreamRangeReader rangeReader)
        {
            var m = 1u;
            for (var i = NumBitLevels; i > 0; i--)
            {
                m = (m << 1) | Models[m].Decode(rangeReader);
            }

            return m - (1u << NumBitLevels);
        }

        public uint ReverseDecode(in StreamRangeReader rangeReader) => ReverseDecode(Models, 0, rangeReader, NumBitLevels);

        public static uint ReverseDecode(in BitDecoder[] models, in uint startIndex, in StreamRangeReader rangeReader, in int numBitLevels)
        {
            var m = 1u;
            var symbol = 0u;
            for (var bitIndex = 0; bitIndex < numBitLevels; bitIndex++)
            {
                var bit = models[startIndex + m].Decode(rangeReader);
                m <<= 1;
                m += bit;
                symbol |= (bit << bitIndex);
            }

            return symbol;
        }
    }
}

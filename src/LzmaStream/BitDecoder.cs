namespace Lzma
{
    struct BitDecoder
    {
        private const int kNumBitModelTotalBits = 11;
        private const uint kBitModelTotal = 1u << kNumBitModelTotalBits;
        private const int kNumMoveBits = 5;

        private uint? _prob;

        private uint Prob
        {
            get => _prob ?? kBitModelTotal >> 1;
            set => _prob = value;
        }

        public uint Decode(in StreamRangeReader rangeReader) => DecodeBool(rangeReader) ? 1u : 0u;

        public bool DecodeBool(in StreamRangeReader rangeReader)
        {
            if (rangeReader.Decode(Prob, kNumBitModelTotalBits))
            {
                Prob -= Prob >> kNumMoveBits;
                return true;
            }

            Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
            return false;
        }
    }
}

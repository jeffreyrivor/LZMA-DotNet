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

        public uint Decode(in StreamRangeReader rangeReader)
        {
            if (rangeReader.Decode(Prob, kNumBitModelTotalBits) == 0)
            {
                Prob += (kBitModelTotal - Prob) >> kNumMoveBits;
                return 0;
            }

            Prob -= Prob >> kNumMoveBits;
            return 1;
        }
    }
}

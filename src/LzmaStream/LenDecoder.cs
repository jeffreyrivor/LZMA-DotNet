namespace Lzma
{
    class LenDecoder
    {
        private const int kNumLowLenBits = 3;
        private const int kNumMidLenBits = 3;
        private const int kNumHighLenBits = 8;
        private const uint kNumLowLenSymbols = 1u << kNumLowLenBits;
        private const uint kNumMidLenSymbols = 1u << kNumMidLenBits;

        private BitDecoder Choice;
        private BitDecoder Choice2;
        private readonly BitTreeDecoder[] LowCoders;
        private readonly BitTreeDecoder[] MidCoders;
        private readonly BitTreeDecoder HighCoder;

        public LenDecoder(in uint numPosStates)
        {
            LowCoders = new BitTreeDecoder[numPosStates];
            MidCoders = new BitTreeDecoder[numPosStates];
            HighCoder = new BitTreeDecoder(kNumHighLenBits);

            for (var i = 0; i < numPosStates; i++)
            {
                LowCoders[i] = new BitTreeDecoder(kNumLowLenBits);
                MidCoders[i] = new BitTreeDecoder(kNumMidLenBits);
            }
        }

        public uint Decode(in StreamRangeReader rangeReader, in uint posState)
        {
            if (Choice.Decode(rangeReader) == 0)
            {
                return LowCoders[posState].Decode(rangeReader);
            }

            var symbol = kNumLowLenSymbols;

            if (Choice2.Decode(rangeReader) == 0)
            {
                symbol += MidCoders[posState].Decode(rangeReader);
            }
            else
            {
                symbol += kNumMidLenSymbols;
                symbol += HighCoder.Decode(rangeReader);
            }

            return symbol;
        }
    }
}

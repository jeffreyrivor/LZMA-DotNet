namespace Lzma
{
    class LiteralDecoder
    {
        private int NumPosBits { get; }
        private int NumPrevBits { get; }
        private uint PosMask { get; }

        private BitDecoder[,] Decoders { get; }

        public LiteralDecoder(int numPosBits, int numPrevBits)
        {
            NumPosBits = numPosBits;
            PosMask = (1u << numPosBits) - 1;
            NumPrevBits = numPrevBits;
            var numStates = 1u << (numPrevBits + numPosBits);
            Decoders = new BitDecoder[numStates, 0x300];
        }

        private uint GetState(uint pos, byte prevByte) =>
            ((pos & PosMask) << NumPrevBits) + (uint)(prevByte >> (8 - NumPrevBits));

        public byte DecodeNormal(StreamRangeReader rangeReader, uint pos, byte prevByte)
        {
            var symbol = 1u;
            var state = GetState(pos, prevByte);

            do
            {
                symbol = (symbol << 1) | Decoders[state, symbol].Decode(rangeReader);
            } while (symbol < 0x100);

            return (byte)symbol;
        }

        public byte DecodeWithMatchByte(StreamRangeReader rangeReader, uint pos, byte prevByte, byte matchByte)
        {
            var symbol = 1u;
            var state = GetState(pos, prevByte);

            do
            {
                var matchBit = (uint)(matchByte >> 7) & 1;
                matchByte <<= 1;
                var bit = Decoders[state, ((matchBit + 1) << 8) + symbol].Decode(rangeReader);
                symbol = (symbol << 1) | bit;

                if (matchBit != bit)
                {
                    while (symbol < 0x100)
                    {
                        symbol = (symbol << 1) | Decoders[state, symbol].Decode(rangeReader);
                    }

                    break;
                }
            } while (symbol < 0x100);

            return (byte)symbol;
        }
    }
}

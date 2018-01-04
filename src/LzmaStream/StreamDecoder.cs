using System;
using System.IO;

namespace Lzma
{
    class StreamDecoder
    {
        #region Constants

        const int kNumStates = 12;
        const int kNumPosStatesBitsMax = 4;

        // const int kNumLenToPosStatesBits = 2;
        const uint kNumLenToPosStates = 1u << 2; // 1 << kNumLenToPosStatesBits;

        const int kNumAlignBits = 4;

        const int kNumPosSlotBits = 6;

        const uint kStartPosModelIndex = 4;
        const uint kEndPosModelIndex = 14;

        const uint kNumFullDistances = 1u << 7; // 1u << (kEndPosModelIndex / 2);

        const uint kMatchMinLen = 2;

        #endregion

        private readonly StreamRangeReader RangeReader;

        private readonly BitDecoder[] IsMatchDecoders = new BitDecoder[kNumStates << kNumPosStatesBitsMax];
        private readonly BitDecoder[] IsRepDecoders = new BitDecoder[kNumStates];
        private readonly BitDecoder[] IsRepG0Decoders = new BitDecoder[kNumStates];
        private readonly BitDecoder[] IsRepG1Decoders = new BitDecoder[kNumStates];
        private readonly BitDecoder[] IsRepG2Decoders = new BitDecoder[kNumStates];
        private readonly BitDecoder[] IsRep0LongDecoders = new BitDecoder[kNumStates << kNumPosStatesBitsMax];

        private readonly BitDecoder[] PosDecoders = new BitDecoder[kNumFullDistances - kEndPosModelIndex];

        private readonly BitTreeDecoder PosAlignDecoder = new BitTreeDecoder(kNumAlignBits);
        private readonly BitTreeDecoder[] PosSlotDecoders = new BitTreeDecoder[kNumLenToPosStates];

        private readonly LiteralDecoder LiteralDecoder;

        private readonly LenDecoder LenDecoder;
        private readonly LenDecoder RepLenDecoder;

        private readonly uint DictionarySizeCheck;
        private readonly uint BlockSize;
        private readonly uint PosStateMask;
        private readonly ulong OutSize;

        private State State;

        private byte[] OutBlock;
        private uint OutBlockPos;

        private uint NumDecoded;

        private uint Rep0;
        private uint Rep1;
        private uint Rep2;
        private uint Rep3;

        private uint CopyBlockPos;
        private uint CopyBlockLen;

        public StreamDecoder(Stream stream)
        {
            var pb = Math.DivRem(Math.DivRem(stream.ReadByte(), 9, out var lc), 5, out var lp);

            if (lp > 8 || lc > 8 || pb > kNumPosStatesBitsMax)
            {
                throw new InvalidDataException();
            }

            var buffer = new byte[sizeof(ulong)];
            if (stream.Read(buffer, 0, sizeof(uint)) != sizeof(uint))
            {
                throw new InvalidDataException();
            }

            var dictionarySize = BitConverter.ToUInt32(buffer, 0);
            DictionarySizeCheck = Math.Max(dictionarySize, 1);
            BlockSize = Math.Max(DictionarySizeCheck, 1 << 12);
            OutBlock = new byte[BlockSize];

            if (stream.Read(buffer, 0, sizeof(ulong)) != sizeof(ulong))
            {
                throw new InvalidDataException();
            }

            OutSize = BitConverter.ToUInt64(buffer, 0);

            for (var i = 0; i < PosSlotDecoders.Length; i++)
            {
                PosSlotDecoders[i] = new BitTreeDecoder(kNumPosSlotBits);
            }

            LiteralDecoder = new LiteralDecoder(lp, lc);

            var numPosStates = 1u << pb;
            PosStateMask = numPosStates - 1;

            LenDecoder = new LenDecoder(numPosStates);
            RepLenDecoder = new LenDecoder(numPosStates);

            RangeReader = new StreamRangeReader(stream);
        }

        public int Decode(in byte[] buffer, in int offset, in int count)
        {
            var outputCount = 0;

            if (NumDecoded == 0)
            {
                if (IsMatchDecoders[State.Index << kNumPosStatesBitsMax].Decode(RangeReader) != 0)
                {
                    throw new InvalidDataException();
                }

                PutByte(buffer, offset + outputCount++, LiteralDecoder.DecodeNormal(RangeReader, 0, 0));
                State.UpdateChar();
            }
            else if (NumDecoded == OutSize)
            {
                return outputCount;
            }
            else if (CopyBlockLen > 0)
            {
                outputCount += CopyBlock(buffer, offset, count);
            }

            while (NumDecoded < OutSize && outputCount < count && offset + outputCount < buffer.Length)
            {
                var posState = NumDecoded & PosStateMask;
                if (IsMatchDecoders[(State.Index << kNumPosStatesBitsMax) + posState].Decode(RangeReader) == 0)
                {
                    var prevByte = GetByte(0);

                    var b = State.IsCharState() ?
                        LiteralDecoder.DecodeNormal(RangeReader, NumDecoded, prevByte) :
                        LiteralDecoder.DecodeWithMatchByte(RangeReader, NumDecoded, prevByte, GetByte(Rep0));

                    PutByte(buffer, offset + outputCount++, b);
                    State.UpdateChar();
                }
                else
                {
                    if (IsRepDecoders[State.Index].Decode(RangeReader) == 1)
                    {
                        if (IsRepG0Decoders[State.Index].Decode(RangeReader) == 0)
                        {
                            if (IsRep0LongDecoders[(State.Index << kNumPosStatesBitsMax) + posState].Decode(RangeReader) == 0)
                            {
                                PutByte(buffer, offset + outputCount++, GetByte(Rep0));
                                State.UpdateShortRep();
                                continue;
                            }
                        }
                        else
                        {
                            uint distance;

                            if (IsRepG1Decoders[State.Index].Decode(RangeReader) == 0)
                            {
                                distance = Rep1;
                            }
                            else
                            {
                                if (IsRepG2Decoders[State.Index].Decode(RangeReader) == 0)
                                {
                                    distance = Rep2;
                                }
                                else
                                {
                                    distance = Rep3;
                                    Rep3 = Rep2;
                                }

                                Rep2 = Rep1;
                            }

                            Rep1 = Rep0;
                            Rep0 = distance;
                        }

                        CopyBlockLen = RepLenDecoder.Decode(RangeReader, posState) + kMatchMinLen;
                        State.UpdateRep();
                    }
                    else
                    {
                        Rep3 = Rep2;
                        Rep2 = Rep1;
                        Rep1 = Rep0;

                        CopyBlockLen = LenDecoder.Decode(RangeReader, posState) + kMatchMinLen;
                        State.UpdateMatch();

                        var posSlot = PosSlotDecoders[GetLenToPosState(CopyBlockLen)].Decode(RangeReader);
                        if (posSlot >= kStartPosModelIndex)
                        {
                            var numDirectBits = (int)((posSlot >> 1) - 1);

                            Rep0 = (2 | (posSlot & 1)) << numDirectBits;
                            if (posSlot < kEndPosModelIndex)
                            {
                                Rep0 += BitTreeDecoder.ReverseDecode(PosDecoders, Rep0 - posSlot - 1, RangeReader, numDirectBits);
                            }
                            else
                            {
                                Rep0 += RangeReader.DecodeDirectBits(numDirectBits - kNumAlignBits) << kNumAlignBits;
                                Rep0 += PosAlignDecoder.ReverseDecode(RangeReader);
                            }
                        }
                        else
                        {
                            Rep0 = posSlot;
                        }
                    }

                    if (Rep0 >= NumDecoded || Rep0 >= DictionarySizeCheck)
                    {
                        if (Rep0 == 0xFFFFFFFF)
                        {
                            break;
                        }

                        throw new InvalidDataException();
                    }

                    CopyBlockPos = OutBlockPos - Rep0 - 1;
                    if (CopyBlockPos >= BlockSize)
                    {
                        CopyBlockPos += BlockSize;
                    }

                    outputCount += CopyBlock(buffer, offset + outputCount, count - outputCount);
                }
            }

            return outputCount;
        }

        private byte GetByte(uint distance)
        {
            var pos = OutBlockPos - distance - 1;
            if (pos >= BlockSize)
            {
                pos += BlockSize;
            }

            return OutBlock[pos];
        }

        private void PutByte(in byte[] buffer, in int pos, in byte b)
        {
            OutBlock[OutBlockPos++] = buffer[pos] = b;
            OutBlockPos %= BlockSize;

            NumDecoded++;
        }

        private int CopyBlock(in byte[] buffer, in int offset, in int count)
        {
            var outputCount = 0;

            while (outputCount < count && offset + outputCount < buffer.Length && CopyBlockLen-- > 0)
            {
                OutBlock[OutBlockPos++] = buffer[offset + outputCount++] = OutBlock[CopyBlockPos++];
                CopyBlockPos %= BlockSize;
                OutBlockPos %= BlockSize;

                NumDecoded++;
            }

            return outputCount;
        }

        private static uint GetLenToPosState(uint len)
        {
            len -= kMatchMinLen;
            return len < kNumLenToPosStates ? len : kNumLenToPosStates - 1;
        }
    }

    struct State
    {
        public uint Index { get; private set; }

        public bool IsCharState() => Index < 7;

        public void UpdateChar()
        {
            if (Index < 4)
            {
                Index = 0;
            }
            else
            {
                Index -= Index < 10 ? 3u : 6u;
            }
        }

        public void UpdateMatch()
        {
            Index = Index < 7 ? 7u : 10u;
        }

        public void UpdateRep()
        {
            Index = Index < 7 ? 8u : 11u;
        }

        public void UpdateShortRep()
        {
            Index = Index < 7 ? 9u : 11u;
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;

namespace Lzma
{
    public class LzmaStream : Stream
    {
        private Stream baseStream;
        private CompressionMode compressionMode;
        private bool leaveBaseStreamOpen;

        private Lazy<StreamDecoder> lazyStreamDecoder;

        public LzmaStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            baseStream = stream;
            compressionMode = mode;
            leaveBaseStreamOpen = leaveOpen;
            lazyStreamDecoder = new Lazy<StreamDecoder>(() => new StreamDecoder(baseStream), false);
        }

        public override bool CanRead => compressionMode == CompressionMode.Decompress && baseStream?.CanRead == true;

        public override bool CanSeek => false;

        public override bool CanWrite => compressionMode == CompressionMode.Compress && baseStream?.CanWrite == true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            if (CanRead)
            {
                return;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
            {
                throw new InvalidOperationException();
            }

            return lazyStreamDecoder.Value.Decode(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException();
            }

            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            Flush();

            if (disposing && !leaveBaseStreamOpen && baseStream != null)
            {
                try
                {
                    baseStream.Close();
                }
                finally
                {
                    baseStream = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}

using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public readonly struct SampleView
    {
        private readonly ZLoanedSample loaned;

        internal SampleView(ZLoanedSample loaned)
        {
            this.loaned = loaned;
        }

        public string KeyExpr => loaned.KeyExpr().AsString();

        public ZSampleKind Kind => loaned.Kind();

        public bool Express => loaned.Express();

        public ulong? Ntp64Timestamp
        {
            get
            {
                ZLoanedTimestamp ts = loaned.Timestamp();
                return ts == null ? (ulong?)null : ts.Ntp64Time();
            }
        }

        public string EncodingMime
        {
            get
            {
                ZLoanedEncoding encoding = loaned.Encoding();
                return encoding == null ? null : encoding.AsString();
            }
        }

        public int PayloadLength
        {
            get
            {
                ZLoanedBytes payload = loaned.Payload();
                return payload == null ? 0 : checked((int)payload.Len());
            }
        }

        public byte[] PayloadAsBytes()
        {
            ZLoanedBytes payload = loaned.Payload();
            return payload == null ? null : payload.ToByteArray();
        }

        /// <summary>
        /// Copies the payload directly into <paramref name="dst"/> starting at
        /// <paramref name="dstOffset"/>. Avoids the intermediate <see cref="byte"/>[] that
        /// <see cref="PayloadAsBytes"/> allocates. The destination buffer must already be
        /// at least <see cref="PayloadLength"/> bytes long.
        /// </summary>
        public int PayloadCopyTo(byte[] dst, int dstOffset = 0)
        {
            ZLoanedBytes payload = loaned.Payload();
            return payload == null ? 0 : payload.CopyTo(dst, dstOffset);
        }

        public string PayloadAsString()
        {
            ZLoanedBytes payload = loaned.Payload();
            return payload == null ? null : payload.ToStringUtf8();
        }

        public byte[] AttachmentAsBytes()
        {
            ZLoanedBytes attachment = loaned.Attachment();
            return attachment == null ? null : attachment.ToByteArray();
        }
    }
}

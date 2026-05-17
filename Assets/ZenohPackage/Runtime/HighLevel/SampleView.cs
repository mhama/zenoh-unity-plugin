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

        public byte[] PayloadAsBytes()
        {
            ZLoanedBytes payload = loaned.Payload();
            return payload == null ? null : payload.ToByteArray();
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

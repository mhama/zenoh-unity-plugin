using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public readonly struct ReplyView
    {
        private readonly ZLoanedReply loaned;

        internal ReplyView(ZLoanedReply loaned)
        {
            this.loaned = loaned;
        }

        public bool IsOk => loaned.IsOk();

        public SampleView? OkSample
        {
            get
            {
                if (!loaned.IsOk())
                    return null;
                ZLoanedSample sample = loaned.Ok();
                return sample == null ? (SampleView?)null : new SampleView(sample);
            }
        }

        public byte[] ErrorPayloadAsBytes()
        {
            if (loaned.IsOk())
                return null;
            ZLoanedReplyErr err = loaned.Err();
            if (err == null)
                return null;
            ZLoanedBytes payload = err.Payload();
            return payload == null ? null : payload.ToByteArray();
        }

        public string ErrorPayloadAsString()
        {
            if (loaned.IsOk())
                return null;
            ZLoanedReplyErr err = loaned.Err();
            if (err == null)
                return null;
            ZLoanedBytes payload = err.Payload();
            return payload == null ? null : payload.ToStringUtf8();
        }

        public string ErrorEncodingMime
        {
            get
            {
                if (loaned.IsOk())
                    return null;
                ZLoanedReplyErr err = loaned.Err();
                if (err == null)
                    return null;
                ZLoanedEncoding encoding = err.Encoding();
                return encoding == null ? null : encoding.AsString();
            }
        }
    }
}

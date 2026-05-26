using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public sealed class QueryReplyOptions
    {
        public EncodingPreset? Encoding { get; set; }
        public ZCongestionControl? CongestionControl { get; set; }
        public ZPriority? Priority { get; set; }
        public bool? IsExpress { get; set; }
        public byte[] Attachment { get; set; }
    }

    public sealed class QueryReplyErrOptions
    {
        public EncodingPreset? Encoding { get; set; }
    }

    public sealed class QueryReplyDelOptions
    {
        public ZCongestionControl? CongestionControl { get; set; }
        public ZPriority? Priority { get; set; }
        public bool? IsExpress { get; set; }
        public byte[] Attachment { get; set; }
    }

    public readonly struct QueryView
    {
        private readonly ZLoanedQuery loaned;

        internal QueryView(ZLoanedQuery loaned)
        {
            this.loaned = loaned;
        }

        public string KeyExpr => loaned.KeyExpr().AsString();

        public string Parameters => loaned.Parameters();

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

        public string EncodingMime
        {
            get
            {
                ZLoanedEncoding encoding = loaned.Encoding();
                return encoding == null ? null : encoding.AsString();
            }
        }

        public byte[] AttachmentAsBytes()
        {
            ZLoanedBytes attachment = loaned.Attachment();
            return attachment == null ? null : attachment.ToByteArray();
        }

        public void Reply(string keyExpr, byte[] payload, QueryReplyOptions options = null)
        {
            TryReplyCore(keyExpr, BytesFactory.FromBuffer(payload), options, out ZResult result);
            result.ThrowIfErr("QueryView.Reply failed");
        }

        public void Reply(string keyExpr, string payload, QueryReplyOptions options = null)
        {
            TryReplyCore(keyExpr, BytesFactory.FromString(payload), options, out ZResult result);
            result.ThrowIfErr("QueryView.Reply failed");
        }

        public bool TryReply(string keyExpr, byte[] payload, QueryReplyOptions options, out ZResult result)
        {
            return TryReplyCore(keyExpr, BytesFactory.FromBuffer(payload), options, out result);
        }

        public bool TryReply(string keyExpr, string payload, QueryReplyOptions options, out ZResult result)
        {
            return TryReplyCore(keyExpr, BytesFactory.FromString(payload), options, out result);
        }

        private bool TryReplyCore(string keyExpr, ZOwnedBytes movingPayload, QueryReplyOptions options, out ZResult result)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            byte[] keyExprBuffer = Utf8Helper.EncodeNullTerminated(keyExpr);
            result = ZOwnedKeyExpr.TryCreateFromStr(keyExprBuffer, out ZOwnedKeyExpr ownedKe);
            if (!result.IsOk)
                return false;

            ZQueryReplyOptions nativeOpts = ZQueryReplyOptions.Default();
            try
            {
                if (options?.Encoding is EncodingPreset encoding)
                    nativeOpts.SetEncoding(encoding.ToLoanedEncoding());
                if (options?.CongestionControl is ZCongestionControl cc)
                    nativeOpts.SetCongestionControl(cc);
                if (options?.Priority is ZPriority priority)
                    nativeOpts.SetPriority(priority);
                if (options?.IsExpress is bool express)
                    nativeOpts.SetIsExpress(express);
                if (options?.Attachment is byte[] attachment)
                    nativeOpts.SetAttachment(BytesFactory.FromBuffer(attachment));

                result = loaned.Reply(ownedKe.Loan(), movingPayload, nativeOpts);
                return result.IsOk;
            }
            finally
            {
                nativeOpts.Dispose();
                ownedKe.Dispose();
            }
        }

        public void ReplyErr(byte[] payload, QueryReplyErrOptions options = null)
        {
            TryReplyErrCore(BytesFactory.FromBuffer(payload), options, out ZResult result);
            result.ThrowIfErr("QueryView.ReplyErr failed");
        }

        public void ReplyErr(string payload, QueryReplyErrOptions options = null)
        {
            TryReplyErrCore(BytesFactory.FromString(payload), options, out ZResult result);
            result.ThrowIfErr("QueryView.ReplyErr failed");
        }

        public bool TryReplyErr(byte[] payload, QueryReplyErrOptions options, out ZResult result)
        {
            return TryReplyErrCore(BytesFactory.FromBuffer(payload), options, out result);
        }

        public bool TryReplyErr(string payload, QueryReplyErrOptions options, out ZResult result)
        {
            return TryReplyErrCore(BytesFactory.FromString(payload), options, out result);
        }

        private bool TryReplyErrCore(ZOwnedBytes movingPayload, QueryReplyErrOptions options, out ZResult result)
        {
            ZQueryReplyErrOptions nativeOpts = ZQueryReplyErrOptions.Default();
            try
            {
                if (options?.Encoding is EncodingPreset encoding)
                    nativeOpts.SetEncoding(encoding.ToLoanedEncoding());

                result = loaned.ReplyErr(movingPayload, nativeOpts);
                return result.IsOk;
            }
            finally
            {
                nativeOpts.Dispose();
            }
        }

        public void ReplyDel(string keyExpr, QueryReplyDelOptions options = null)
        {
            TryReplyDelCore(keyExpr, options, out ZResult result);
            result.ThrowIfErr("QueryView.ReplyDel failed");
        }

        public bool TryReplyDel(string keyExpr, QueryReplyDelOptions options, out ZResult result)
        {
            return TryReplyDelCore(keyExpr, options, out result);
        }

        private bool TryReplyDelCore(string keyExpr, QueryReplyDelOptions options, out ZResult result)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            byte[] keyExprBuffer = Utf8Helper.EncodeNullTerminated(keyExpr);
            result = ZOwnedKeyExpr.TryCreateFromStr(keyExprBuffer, out ZOwnedKeyExpr ownedKe);
            if (!result.IsOk)
                return false;

            ZQueryReplyDelOptions nativeOpts = ZQueryReplyDelOptions.Default();
            try
            {
                if (options?.CongestionControl is ZCongestionControl cc)
                    nativeOpts.SetCongestionControl(cc);
                if (options?.Priority is ZPriority priority)
                    nativeOpts.SetPriority(priority);
                if (options?.IsExpress is bool express)
                    nativeOpts.SetIsExpress(express);
                if (options?.Attachment is byte[] attachment)
                    nativeOpts.SetAttachment(BytesFactory.FromBuffer(attachment));

                result = loaned.ReplyDel(ownedKe.Loan(), nativeOpts);
                return result.IsOk;
            }
            finally
            {
                nativeOpts.Dispose();
                ownedKe.Dispose();
            }
        }
    }
}

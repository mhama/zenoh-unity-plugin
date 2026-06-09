using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    /// <summary>
    /// A query retained beyond its queryable callback (cloned via z_query_clone),
    /// so it can be inspected and replied to after the callback returns — including
    /// from another thread.
    ///
    /// Obtain one with <see cref="QueryView.Defer"/> from inside the onQuery
    /// callback, while the <see cref="QueryView"/> is still valid. The clone keeps
    /// the key expression, parameters, payload and attachment, so the deferred work
    /// can read them later without capturing them separately.
    ///
    /// You MUST finalize it exactly once with <see cref="Dispose"/> (or its alias
    /// <see cref="Complete"/>): this drops the native query and signals the querier
    /// that no more replies are coming. Forgetting to finalize keeps the querier
    /// waiting until its own timeout (and the native query alive until GC).
    ///
    /// Replies may be sent from any thread, but the instance is not safe for
    /// concurrent use: serialize your own <c>Reply</c>/<c>Dispose</c> calls. The
    /// reply key expression defaults to the query's own key expression; pass an
    /// explicit one only when replying on a different key.
    /// </summary>
    public sealed class DeferredQuery : IDisposable
    {
        private readonly object gate = new object();
        private readonly string keyExpr;
        private ZOwnedQuery owned;

        internal DeferredQuery(ZOwnedQuery owned, string keyExpr)
        {
            this.owned = owned ?? throw new ArgumentNullException(nameof(owned));
            this.keyExpr = keyExpr;
        }

        /// <summary>
        /// The query's key expression, captured when the query was deferred.
        /// Remains readable after <see cref="Dispose"/> (it is a managed copy).
        /// </summary>
        public string KeyExpr => keyExpr;

        /// <summary>True until the query has been finalized via <see cref="Dispose"/>.</summary>
        public bool IsValid
        {
            get { lock (gate) { return owned != null; } }
        }

        public string Parameters
        {
            get { lock (gate) { ThrowIfDisposed(); return owned.Loan().Parameters(); } }
        }

        public int PayloadLength
        {
            get
            {
                lock (gate)
                {
                    ThrowIfDisposed();
                    ZLoanedBytes payload = owned.Loan().Payload();
                    return payload == null ? 0 : checked((int)payload.Len());
                }
            }
        }

        public byte[] PayloadAsBytes()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                ZLoanedBytes payload = owned.Loan().Payload();
                return payload == null ? null : payload.ToByteArray();
            }
        }

        public string PayloadAsString()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                ZLoanedBytes payload = owned.Loan().Payload();
                return payload == null ? null : payload.ToStringUtf8();
            }
        }

        public string EncodingMime
        {
            get
            {
                lock (gate)
                {
                    ThrowIfDisposed();
                    ZLoanedEncoding encoding = owned.Loan().Encoding();
                    return encoding == null ? null : encoding.AsString();
                }
            }
        }

        public byte[] AttachmentAsBytes()
        {
            lock (gate)
            {
                ThrowIfDisposed();
                ZLoanedBytes attachment = owned.Loan().Attachment();
                return attachment == null ? null : attachment.ToByteArray();
            }
        }

        // Reply on the query's own key expression (the common deferred case).

        public void Reply(byte[] payload, QueryReplyOptions options = null)
        {
            Reply(keyExpr, payload, options);
        }

        public void Reply(string payload, QueryReplyOptions options = null)
        {
            Reply(keyExpr, payload, options);
        }

        public bool TryReply(byte[] payload, QueryReplyOptions options, out ZResult result)
        {
            return TryReplyCore(keyExpr, BytesFactory.FromBuffer(payload), options, out result);
        }

        // Reply on an explicit key expression.

        public void Reply(string keyExpr, byte[] payload, QueryReplyOptions options = null)
        {
            TryReplyCore(keyExpr, BytesFactory.FromBuffer(payload), options, out ZResult result);
            result.ThrowIfErr("DeferredQuery.Reply failed");
        }

        public void Reply(string keyExpr, string payload, QueryReplyOptions options = null)
        {
            TryReplyCore(keyExpr, BytesFactory.FromString(payload), options, out ZResult result);
            result.ThrowIfErr("DeferredQuery.Reply failed");
        }

        public bool TryReply(string keyExpr, byte[] payload, QueryReplyOptions options, out ZResult result)
        {
            return TryReplyCore(keyExpr, BytesFactory.FromBuffer(payload), options, out result);
        }

        private bool TryReplyCore(string keyExpr, ZOwnedBytes movingPayload, QueryReplyOptions options, out ZResult result)
        {
            if (keyExpr == null)
            {
                movingPayload.Dispose();
                throw new ArgumentNullException(nameof(keyExpr));
            }

            lock (gate)
            {
                if (owned == null)
                {
                    movingPayload.Dispose();
                    throw new ObjectDisposedException(nameof(DeferredQuery));
                }

                byte[] keyExprBuffer = Utf8Helper.EncodeNullTerminated(keyExpr);
                result = ZOwnedKeyExpr.TryCreateFromStr(keyExprBuffer, out ZOwnedKeyExpr ownedKe);
                if (!result.IsOk)
                {
                    movingPayload.Dispose();
                    return false;
                }

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

                    result = owned.Loan().Reply(ownedKe.Loan(), movingPayload, nativeOpts);
                    return result.IsOk;
                }
                finally
                {
                    nativeOpts.Dispose();
                    ownedKe.Dispose();
                }
            }
        }

        public void ReplyErr(byte[] payload, QueryReplyErrOptions options = null)
        {
            TryReplyErrCore(BytesFactory.FromBuffer(payload), options, out ZResult result);
            result.ThrowIfErr("DeferredQuery.ReplyErr failed");
        }

        public void ReplyErr(string payload, QueryReplyErrOptions options = null)
        {
            TryReplyErrCore(BytesFactory.FromString(payload), options, out ZResult result);
            result.ThrowIfErr("DeferredQuery.ReplyErr failed");
        }

        public bool TryReplyErr(byte[] payload, QueryReplyErrOptions options, out ZResult result)
        {
            return TryReplyErrCore(BytesFactory.FromBuffer(payload), options, out result);
        }

        private bool TryReplyErrCore(ZOwnedBytes movingPayload, QueryReplyErrOptions options, out ZResult result)
        {
            lock (gate)
            {
                if (owned == null)
                {
                    movingPayload.Dispose();
                    throw new ObjectDisposedException(nameof(DeferredQuery));
                }

                ZQueryReplyErrOptions nativeOpts = ZQueryReplyErrOptions.Default();
                try
                {
                    if (options?.Encoding is EncodingPreset encoding)
                        nativeOpts.SetEncoding(encoding.ToLoanedEncoding());

                    result = owned.Loan().ReplyErr(movingPayload, nativeOpts);
                    return result.IsOk;
                }
                finally
                {
                    nativeOpts.Dispose();
                }
            }
        }

        public void ReplyDel(QueryReplyDelOptions options = null)
        {
            ReplyDel(keyExpr, options);
        }

        public void ReplyDel(string keyExpr, QueryReplyDelOptions options = null)
        {
            TryReplyDelCore(keyExpr, options, out ZResult result);
            result.ThrowIfErr("DeferredQuery.ReplyDel failed");
        }

        public bool TryReplyDel(string keyExpr, QueryReplyDelOptions options, out ZResult result)
        {
            return TryReplyDelCore(keyExpr, options, out result);
        }

        private bool TryReplyDelCore(string keyExpr, QueryReplyDelOptions options, out ZResult result)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            lock (gate)
            {
                ThrowIfDisposed();

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

                    result = owned.Loan().ReplyDel(ownedKe.Loan(), nativeOpts);
                    return result.IsOk;
                }
                finally
                {
                    nativeOpts.Dispose();
                    ownedKe.Dispose();
                }
            }
        }

        /// <summary>
        /// Finalizes the query without sending another reply (alias for
        /// <see cref="Dispose"/>). Use after the last <c>Reply</c>, or to abandon
        /// the query so the querier completes instead of waiting for its timeout.
        /// </summary>
        public void Complete()
        {
            Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (owned == null)
                throw new ObjectDisposedException(nameof(DeferredQuery));
        }

        public void Dispose()
        {
            lock (gate)
            {
                owned?.Dispose();   // z_query_drop — finalizes the query (no more replies)
                owned = null;
            }
        }
    }
}

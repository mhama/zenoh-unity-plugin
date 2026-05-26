using System;
using System.Threading;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public sealed class Session : IDisposable
    {
        private ZOwnedSession owned;

        private Session(ZOwnedSession owned)
        {
            this.owned = owned;
        }

        public static Session Open(string configJson = null)
        {
            Config config = configJson == null ? Config.Default() : Config.FromJsonString(configJson);
            try
            {
                return Open(config);
            }
            catch
            {
                config.Dispose();
                throw;
            }
        }

        public static bool TryOpen(string configJson, out Session session, out ZResult result)
        {
            Config config;
            if (configJson == null)
            {
                if (!Config.TryDefault(out config, out result))
                {
                    session = null;
                    return false;
                }
            }
            else if (!Config.TryFromJsonString(configJson, out config, out result))
            {
                session = null;
                return false;
            }

            try
            {
                return TryOpen(config, out session, out result);
            }
            catch
            {
                config.Dispose();
                throw;
            }
        }

        public static Session Open(Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            ZOwnedConfig movingConfig = config.ConsumeForOpen();
            using (ZOpenOptions options = ZOpenOptions.Default())
            {
                ZResult result = ZOwnedSession.TryCreateOpen(movingConfig, options, out ZOwnedSession owned);
                result.ThrowIfErr("Session.Open failed");
                return new Session(owned);
            }
        }

        public static bool TryOpen(Config config, out Session session, out ZResult result)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            ZOwnedConfig movingConfig = config.ConsumeForOpen();
            using (ZOpenOptions options = ZOpenOptions.Default())
            {
                result = ZOwnedSession.TryCreateOpen(movingConfig, options, out ZOwnedSession owned);
                session = result.IsOk ? new Session(owned) : null;
                return result.IsOk;
            }
        }

        public void Close()
        {
            ThrowIfDisposed();
            using (ZCloseOptions options = ZCloseOptions.Default())
            {
                ZResult result = owned.Loan().Close(options);
                result.ThrowIfErr("Session.Close failed");
            }
        }

        public bool TryClose(out ZResult result)
        {
            ThrowIfDisposed();
            using (ZCloseOptions options = ZCloseOptions.Default())
            {
                result = owned.Loan().Close(options);
                return result.IsOk;
            }
        }

        public bool IsClosed
        {
            get
            {
                ThrowIfDisposed();
                return owned.Loan().IsClosed();
            }
        }

        public void Get(KeyExpr keyExpr, Action<ReplyView> onReply, string parameters = null, GetOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!TryGetCore(keyExpr, onReply, parameters, options, cancellationToken, out ZResult result))
                result.ThrowIfErr("Session.Get failed");
        }

        public void Get(string keyExprString, Action<ReplyView> onReply, string parameters = null, GetOptions options = null, CancellationToken cancellationToken = default)
        {
            if (keyExprString == null)
                throw new ArgumentNullException(nameof(keyExprString));

            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                Get(keyExpr, onReply, parameters, options, cancellationToken);
            }
        }

        public bool TryGet(KeyExpr keyExpr, Action<ReplyView> onReply, string parameters, GetOptions options, CancellationToken cancellationToken, out ZResult result)
        {
            return TryGetCore(keyExpr, onReply, parameters, options, cancellationToken, out result);
        }

        private bool TryGetCore(KeyExpr keyExpr, Action<ReplyView> onReply, string parameters, GetOptions options, CancellationToken cancellationToken, out ZResult result)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (onReply == null)
                throw new ArgumentNullException(nameof(onReply));
            ThrowIfDisposed();

            ZOwnedClosureReply closure = new ZOwnedClosureReply();
            ZOwnedCancellationToken zenohToken = null;
            ZOwnedCancellationToken zenohTokenClone = null;
            CancellationTokenRegistration reg = default;

            try
            {
                if (cancellationToken.CanBeCanceled)
                {
                    result = ZOwnedCancellationToken.TryCreateNew(out zenohToken);
                    if (!result.IsOk)
                        return false;
                    zenohTokenClone = ZOwnedCancellationToken.CreateClone(zenohToken.Loan());
                    var cloneForCallback = zenohTokenClone;
                    // z_cancellation_token_cancel blocks (SyncGroup::wait) until the Get
                    // fully completes. Running it on the calling thread would deadlock
                    // because the closure drop callback calls reg.Dispose(), which waits
                    // for this registration callback to finish.
                    reg = cancellationToken.Register(() =>
                        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                            cloneForCallback.Loan().Cancel()));
                }

                ZClosureReplyCall call = (loanedReply, _) => onReply(new ReplyView(loanedReply));

                var regForDrop = reg;
                var cloneForDrop = zenohTokenClone;
                ZClosureReplyDrop drop = cancellationToken.CanBeCanceled
                    ? (IntPtr _) => { regForDrop.Dispose(); cloneForDrop?.Dispose(); }
                    : (ZClosureReplyDrop)null;

                closure.Set(call, drop, IntPtr.Zero);

                byte[] parametersBytes = parameters == null
                    ? Utf8Helper.EncodeNullTerminated("")
                    : Utf8Helper.EncodeNullTerminated(parameters);

                ZGetOptions nativeOpts = ZGetOptions.Default();
                try
                {
                    if (options?.Target is ZQueryTarget target)
                        nativeOpts.SetTarget(target);
                    if (options?.Consolidation is ZConsolidationMode mode)
                    {
                        ZQueryConsolidation consolidation;
                        switch (mode)
                        {
                            case ZConsolidationMode.Auto: consolidation = ZQueryConsolidation.Auto(); break;
                            case ZConsolidationMode.None: consolidation = ZQueryConsolidation.None(); break;
                            case ZConsolidationMode.Monotonic: consolidation = ZQueryConsolidation.Monotonic(); break;
                            case ZConsolidationMode.Latest: consolidation = ZQueryConsolidation.Latest(); break;
                            default: consolidation = ZQueryConsolidation.Default(); break;
                        }
                        nativeOpts.SetConsolidation(consolidation);
                    }
                    if (options?.Payload is byte[] payload)
                        nativeOpts.SetPayload(BytesFactory.FromBuffer(payload));
                    if (options?.Encoding is EncodingPreset encoding)
                        nativeOpts.SetEncoding(encoding.ToLoanedEncoding());
                    if (options?.CongestionControl is ZCongestionControl cc)
                        nativeOpts.SetCongestionControl(cc);
                    if (options?.IsExpress is bool express)
                        nativeOpts.SetIsExpress(express);
                    if (options?.AllowedDestination is ZLocality dest)
                        nativeOpts.SetAllowedDestination(dest);
                    if (options?.AcceptReplies is ZReplyKeyExpr acceptReplies)
                        nativeOpts.SetAcceptReplies(acceptReplies);
                    if (options?.Priority is ZPriority priority)
                        nativeOpts.SetPriority(priority);
                    if (options?.Attachment is byte[] attachment)
                        nativeOpts.SetAttachment(BytesFactory.FromBuffer(attachment));
                    if (options?.TimeoutMs is ulong timeoutMs)
                        nativeOpts.SetTimeoutMs(timeoutMs);
                    if (zenohToken != null)
                        nativeOpts.SetCancellationToken(zenohToken);

                    result = owned.Loan().Get(keyExpr.LoanInternal(), parametersBytes, closure, nativeOpts);
                    return result.IsOk;
                }
                finally
                {
                    nativeOpts.Dispose();
                }
            }
            catch
            {
                reg.Dispose();
                zenohTokenClone?.Dispose();
                zenohToken?.Dispose();
                closure.Dispose();
                throw;
            }
        }

        internal ZLoanedSession LoanInternal()
        {
            ThrowIfDisposed();
            return owned.Loan();
        }

        private void ThrowIfDisposed()
        {
            if (owned == null)
                throw new ObjectDisposedException(nameof(Session));
        }

        public void Dispose()
        {
            owned?.Dispose();
            owned = null;
        }
    }
}

using System;
using Zenoh.Bindings;

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

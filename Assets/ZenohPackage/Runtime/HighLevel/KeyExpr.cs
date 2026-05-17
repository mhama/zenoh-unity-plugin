using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public sealed class KeyExpr : IDisposable
    {
        private ZOwnedKeyExpr owned;
        private readonly string source;

        private KeyExpr(ZOwnedKeyExpr owned, string source)
        {
            this.owned = owned;
            this.source = source;
        }

        public static KeyExpr Parse(string keyExprString)
        {
            if (keyExprString == null)
                throw new ArgumentNullException(nameof(keyExprString));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(keyExprString);
            ZResult result = ZOwnedKeyExpr.TryCreateFromStr(buffer, out ZOwnedKeyExpr owned);
            result.ThrowIfErr($"KeyExpr.Parse failed for '{keyExprString}'");
            return new KeyExpr(owned, keyExprString);
        }

        public static bool TryParse(string keyExprString, out KeyExpr keyExpr, out ZResult result)
        {
            if (keyExprString == null)
                throw new ArgumentNullException(nameof(keyExprString));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(keyExprString);
            result = ZOwnedKeyExpr.TryCreateFromStr(buffer, out ZOwnedKeyExpr owned);
            keyExpr = result.IsOk ? new KeyExpr(owned, keyExprString) : null;
            return result.IsOk;
        }

        public override string ToString()
        {
            return source ?? string.Empty;
        }

        internal ZLoanedKeyExpr LoanInternal()
        {
            ThrowIfDisposed();
            return owned.Loan();
        }

        private void ThrowIfDisposed()
        {
            if (owned == null)
                throw new ObjectDisposedException(nameof(KeyExpr));
        }

        public void Dispose()
        {
            owned?.Dispose();
            owned = null;
        }
    }
}

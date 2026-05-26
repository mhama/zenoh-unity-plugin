using System;
using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public sealed class QueryableDeclareOptions
    {
        public bool? Complete { get; set; }
        public ZLocality? AllowedOrigin { get; set; }
    }

    public sealed class Queryable : IDisposable
    {
        private ZOwnedQueryable owned;

        private Queryable(ZOwnedQueryable owned)
        {
            this.owned = owned;
        }

        public static Queryable Declare(Session session, KeyExpr keyExpr, Action<QueryView> onQuery, QueryableDeclareOptions options = null)
        {
            if (TryDeclareCore(session, keyExpr, onQuery, options, out Queryable queryable, out ZResult result))
                return queryable;

            throw new ZenohException(result, "Queryable.Declare failed");
        }

        public static bool TryDeclare(Session session, KeyExpr keyExpr, Action<QueryView> onQuery, QueryableDeclareOptions options, out Queryable queryable, out ZResult result)
        {
            return TryDeclareCore(session, keyExpr, onQuery, options, out queryable, out result);
        }

        private static bool TryDeclareCore(Session session, KeyExpr keyExpr, Action<QueryView> onQuery, QueryableDeclareOptions options, out Queryable queryable, out ZResult result)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (onQuery == null)
                throw new ArgumentNullException(nameof(onQuery));

            using (ZOwnedClosureQuery closure = new ZOwnedClosureQuery())
            {
                ZOwnedQueryable owned = new ZOwnedQueryable();
                try
                {
                    ZClosureQueryCall call = (loanedQuery, _) => onQuery(new QueryView(loanedQuery));
                    closure.Set(call, null, IntPtr.Zero);

                    using (ZQueryableOptions nativeOpts = ZQueryableOptions.Default())
                    {
                        if (options?.Complete is bool complete)
                            nativeOpts.SetComplete(complete);
                        if (options?.AllowedOrigin is ZLocality locality)
                            nativeOpts.SetAllowedOrigin(locality);

                        result = owned.Declare(session.LoanInternal(), keyExpr.LoanInternal(), closure, nativeOpts);
                    }

                    if (!result.IsOk)
                    {
                        owned.Dispose();
                        queryable = null;
                        return false;
                    }

                    queryable = new Queryable(owned);
                    return true;
                }
                catch
                {
                    owned.Dispose();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            owned?.Dispose();
            owned = null;
        }
    }
}

using System;
using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public sealed class Subscriber : IDisposable
    {
        private ZOwnedSubscriber owned;

        private Subscriber(ZOwnedSubscriber owned)
        {
            this.owned = owned;
        }

        public static Subscriber Declare(Session session, KeyExpr keyExpr, Action<SampleView> onSample)
        {
            if (TryDeclareCore(session, keyExpr, onSample, out Subscriber subscriber, out ZResult result))
                return subscriber;

            throw new ZenohException(result, "Subscriber.Declare failed");
        }

        public static bool TryDeclare(Session session, KeyExpr keyExpr, Action<SampleView> onSample, out Subscriber subscriber, out ZResult result)
        {
            return TryDeclareCore(session, keyExpr, onSample, out subscriber, out result);
        }

        private static bool TryDeclareCore(Session session, KeyExpr keyExpr, Action<SampleView> onSample, out Subscriber subscriber, out ZResult result)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (onSample == null)
                throw new ArgumentNullException(nameof(onSample));

            ZOwnedClosureSample closure = new ZOwnedClosureSample();
            ZOwnedSubscriber owned = new ZOwnedSubscriber();

            try
            {
                ZClosureSampleCall call = (loanedSample, _) => onSample(new SampleView(loanedSample));
                closure.Set(call, null, IntPtr.Zero);

                using (ZSubscriberOptions options = ZSubscriberOptions.Default())
                {
                    // Declare consumes the closure (Take) regardless of success/failure,
                    // so closure.Dispose() afterwards is a no-op.
                    result = owned.Declare(session.LoanInternal(), keyExpr.LoanInternal(), closure, options);

                    if (!result.IsOk)
                    {
                        owned.Dispose();
                        subscriber = null;
                        return false;
                    }

                    subscriber = new Subscriber(owned);
                    return true;
                }
            }
            catch
            {
                closure.Dispose();
                owned.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            owned?.Dispose();
            owned = null;
        }
    }
}

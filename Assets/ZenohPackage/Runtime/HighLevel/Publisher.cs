using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public sealed class PublisherDeclareOptions
    {
        public EncodingPreset? DefaultEncoding { get; set; }
    }

    public sealed class Publisher : IDisposable
    {
        private ZOwnedPublisher owned;

        private Publisher(ZOwnedPublisher owned)
        {
            this.owned = owned;
        }

        public static Publisher Declare(Session session, KeyExpr keyExpr, PublisherDeclareOptions options = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            ZPublisherOptions nativeOptions = ZPublisherOptions.Default();
            try
            {
                if (options?.DefaultEncoding is EncodingPreset preset)
                    nativeOptions.SetEncoding(preset.ToOwnedEncoding());

                ZOwnedPublisher owned = new ZOwnedPublisher();
                try
                {
                    ZResult result = owned.Declare(session.LoanInternal(), keyExpr.LoanInternal(), nativeOptions);
                    result.ThrowIfErr("Publisher.Declare failed");
                    return new Publisher(owned);
                }
                catch
                {
                    owned.Dispose();
                    throw;
                }
            }
            finally
            {
                nativeOptions.Dispose();
            }
        }

        public static bool TryDeclare(Session session, KeyExpr keyExpr, PublisherDeclareOptions options, out Publisher publisher, out ZResult result)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            ZPublisherOptions nativeOptions = ZPublisherOptions.Default();
            try
            {
                if (options?.DefaultEncoding is EncodingPreset preset)
                    nativeOptions.SetEncoding(preset.ToOwnedEncoding());

                ZOwnedPublisher owned = new ZOwnedPublisher();
                result = owned.Declare(session.LoanInternal(), keyExpr.LoanInternal(), nativeOptions);
                if (!result.IsOk)
                {
                    owned.Dispose();
                    publisher = null;
                    return false;
                }

                publisher = new Publisher(owned);
                return true;
            }
            finally
            {
                nativeOptions.Dispose();
            }
        }

        public void Put(string payload, EncodingPreset? encoding = null)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            PutCore(BytesFactory.FromString(payload), encoding);
        }

        public void Put(byte[] payload, EncodingPreset? encoding = null)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            PutCore(BytesFactory.FromBuffer(payload), encoding);
        }

        public void Put(ReadOnlySpan<byte> payload, EncodingPreset? encoding = null)
        {
            PutCore(BytesFactory.FromBuffer(payload), encoding);
        }

        public bool TryPut(string payload, EncodingPreset? encoding, out ZResult result)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            return TryPutCore(BytesFactory.FromString(payload), encoding, out result);
        }

        public bool TryPut(byte[] payload, EncodingPreset? encoding, out ZResult result)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            return TryPutCore(BytesFactory.FromBuffer(payload), encoding, out result);
        }

        private void PutCore(ZOwnedBytes movingPayload, EncodingPreset? encoding)
        {
            ThrowIfDisposed();

            ZPublisherPutOptions options = ZPublisherPutOptions.Default();
            try
            {
                if (encoding.HasValue)
                    options.SetEncoding(encoding.Value.ToOwnedEncoding());

                ZResult result = owned.Loan().Put(movingPayload, options);
                result.ThrowIfErr("Publisher.Put failed");
            }
            finally
            {
                options.Dispose();
            }
        }

        private bool TryPutCore(ZOwnedBytes movingPayload, EncodingPreset? encoding, out ZResult result)
        {
            ThrowIfDisposed();

            ZPublisherPutOptions options = ZPublisherPutOptions.Default();
            try
            {
                if (encoding.HasValue)
                    options.SetEncoding(encoding.Value.ToOwnedEncoding());

                result = owned.Loan().Put(movingPayload, options);
                return result.IsOk;
            }
            finally
            {
                options.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (owned == null)
                throw new ObjectDisposedException(nameof(Publisher));
        }

        public void Dispose()
        {
            owned?.Dispose();
            owned = null;
        }
    }
}

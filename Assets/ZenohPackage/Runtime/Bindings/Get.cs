using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    /// <summary>
    /// Public projection of <c>z_query_target_t</c>.
    /// </summary>
    public enum ZQueryTarget : uint
    {
        BestMatching = 0,
        All = 1,
        AllComplete = 2,
    }

    /// <summary>
    /// Public projection of <c>z_consolidation_mode_t</c>.
    /// </summary>
    public enum ZConsolidationMode : int
    {
        Auto = -1,
        None = 0,
        Monotonic = 1,
        Latest = 2,
    }

    /// <summary>
    /// Wraps a <c>z_query_consolidation_t</c> value. Construct via the static
    /// factory methods mirroring the native helpers (<c>Default</c>, <c>Auto</c>,
    /// <c>Latest</c>, <c>Monotonic</c>, <c>None</c>).
    /// </summary>
    public unsafe readonly struct ZQueryConsolidation
    {
        internal readonly z_query_consolidation_t Native;

        internal ZQueryConsolidation(z_query_consolidation_t native)
        {
            Native = native;
        }

        public ZConsolidationMode Mode => (ZConsolidationMode)Native.mode;

        public static ZQueryConsolidation Default()
        {
            return new ZQueryConsolidation(ZenohNative.z_query_consolidation_default());
        }

        public static ZQueryConsolidation Auto()
        {
            return new ZQueryConsolidation(ZenohNative.z_query_consolidation_auto());
        }

        public static ZQueryConsolidation Latest()
        {
            return new ZQueryConsolidation(ZenohNative.z_query_consolidation_latest());
        }

        public static ZQueryConsolidation Monotonic()
        {
            return new ZQueryConsolidation(ZenohNative.z_query_consolidation_monotonic());
        }

        public static ZQueryConsolidation None()
        {
            return new ZQueryConsolidation(ZenohNative.z_query_consolidation_none());
        }
    }

    public unsafe sealed class ZGetOptions : IDisposable
    {
        internal z_get_options_t Native;
        private OwnedStorage<z_owned_bytes_t> adoptedPayload;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private OwnedStorage<z_owned_bytes_t> adoptedAttachment;
        private bool disposed;

        public static ZGetOptions Default()
        {
            ZGetOptions options = new ZGetOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ZenohNative.z_get_options_default(ptr);
            }
        }

        public void SetTarget(ZQueryTarget target)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->target = (z_query_target_t)target;
            }
        }

        public void SetConsolidation(ZQueryConsolidation consolidation)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->consolidation = consolidation.Native;
            }
        }

        public void SetIsExpress(bool isExpress)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->is_express = isExpress;
            }
        }

        public void SetAcceptReplies(ZReplyKeyExpr acceptReplies)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->accept_replies = (z_reply_keyexpr_t)acceptReplies;
            }
        }

        public void SetAllowedDestination(ZLocality locality)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->allowed_destination = (z_locality_t)locality;
            }
        }

        public void SetTimeoutMs(ulong timeoutMs)
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->timeout_ms = timeoutMs;
            }
        }

        public void SetPayload(ZOwnedBytes movingPayload)
        {
            if (movingPayload == null)
                throw new ArgumentNullException(nameof(movingPayload));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingPayload.Take();
            ReleasePayload();
            adoptedPayload = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->payload = (z_moved_bytes_t*)adoptedPayload.Pointer;
            }
        }

        public void SetEncoding(ZOwnedEncoding movingEncoding)
        {
            if (movingEncoding == null)
                throw new ArgumentNullException(nameof(movingEncoding));
            ThrowIfDisposed();

            OwnedStorage<z_owned_encoding_t> storage = movingEncoding.Take();
            ReleaseEncoding();
            adoptedEncoding = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        public void SetEncoding(ZLoanedEncoding loanedEncoding)
        {
            if (loanedEncoding == null)
                throw new ArgumentNullException(nameof(loanedEncoding));
            ThrowIfDisposed();

            SetEncoding(ZOwnedEncoding.CreateClone(loanedEncoding));
        }

        public void SetAttachment(ZOwnedBytes movingAttachment)
        {
            if (movingAttachment == null)
                throw new ArgumentNullException(nameof(movingAttachment));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingAttachment.Take();
            ReleaseAttachment();
            adoptedAttachment = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)adoptedAttachment.Pointer;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZGetOptions));
        }

        private void ReleasePayload()
        {
            if (adoptedPayload == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedPayload.Pointer);
            adoptedPayload.Dispose();
            adoptedPayload = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->payload = null;
            }
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->encoding = null;
            }
        }

        private void ReleaseAttachment()
        {
            if (adoptedAttachment == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedAttachment.Pointer);
            adoptedAttachment.Dispose();
            adoptedAttachment = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->attachment = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleasePayload();
            ReleaseEncoding();
            ReleaseAttachment();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

}

using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZPublisherOptions : IDisposable
    {
        internal z_publisher_options_t Native;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private bool disposed;

        public static ZPublisherOptions Default()
        {
            ZPublisherOptions options = new ZPublisherOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_publisher_options_t* ptr = &Native)
            {
                ZenohNative.z_publisher_options_default(ptr);
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

            fixed (z_publisher_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZPublisherOptions));
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_publisher_options_t* ptr = &Native)
            {
                ptr->encoding = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleaseEncoding();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe sealed class ZPublisherPutOptions : IDisposable
    {
        internal z_publisher_put_options_t Native;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private OwnedStorage<z_owned_bytes_t> adoptedAttachment;
        private bool disposed;

        public static ZPublisherPutOptions Default()
        {
            ZPublisherPutOptions options = new ZPublisherPutOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ZenohNative.z_publisher_put_options_default(ptr);
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

            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        public void SetAttachment(ZOwnedBytes movingAttachment)
        {
            if (movingAttachment == null)
                throw new ArgumentNullException(nameof(movingAttachment));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingAttachment.Take();
            ReleaseAttachment();
            adoptedAttachment = storage;

            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)adoptedAttachment.Pointer;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZPublisherPutOptions));
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_publisher_put_options_t* ptr = &Native)
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

            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ptr->attachment = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleaseEncoding();
            ReleaseAttachment();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public readonly struct ZMatchingStatus
    {
        private readonly bool matching;

        internal ZMatchingStatus(z_matching_status_t native)
        {
            matching = native.matching;
        }

        public bool Matching => matching;
    }

    public unsafe sealed class ZOwnedPublisher : IDisposable
    {
        private z_owned_publisher_t* nativePtr;
        private bool disposed;
        private bool moved;

        public ZOwnedPublisher()
        {
            nativePtr = NativeMemory.Alloc<z_owned_publisher_t>();
            ZenohNative.z_internal_publisher_null(nativePtr);
        }

        public ZResult Declare(ZLoanedSession session, ZLoanedKeyExpr keyExpr, ZPublisherOptions options)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            ThrowIfDisposedOrMoved();
            options.ThrowIfDisposed();

            fixed (z_publisher_options_t* optionsPtr = &options.Native)
            {
                return new ZResult(ZenohNative.z_declare_publisher(session.NativePointer, nativePtr, keyExpr.NativePointer, optionsPtr));
            }
        }

        public ZLoanedPublisher Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedPublisher(ZenohNative.z_publisher_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_publisher_check(nativePtr);
        }

        internal OwnedStorage<z_owned_publisher_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_publisher_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_publisher_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedPublisher));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedPublisher));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_publisher_check(nativePtr))
                    ZenohNative.z_publisher_drop((z_moved_publisher_t*)nativePtr);

                NativeMemory.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedPublisher()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedPublisher
    {
        private readonly ZOwnedPublisher owner;
        internal readonly z_loaned_publisher_t* NativePointer;

        internal ZLoanedPublisher(z_loaned_publisher_t* nativePtr, ZOwnedPublisher owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZResult Put(ZOwnedBytes movingPayload, ZPublisherPutOptions options)
        {
            if (movingPayload == null)
                throw new ArgumentNullException(nameof(movingPayload));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            options.ThrowIfDisposed();

            using (OwnedStorage<z_owned_bytes_t> payloadStorage = movingPayload.Take())
            {
                fixed (z_publisher_put_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_publisher_put(NativePointer, (z_moved_bytes_t*)payloadStorage.Pointer, optionsPtr));
                }
            }
        }

        public ZResult TryGetMatchingStatus(out ZMatchingStatus matchingStatus)
        {
            z_matching_status_t native = default;
            ZResult result = new ZResult(ZenohNative.z_publisher_get_matching_status(NativePointer, &native));
            matchingStatus = result.IsOk ? new ZMatchingStatus(native) : default;
            return result;
        }
    }
}

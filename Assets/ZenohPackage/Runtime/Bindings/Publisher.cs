using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZPublisherOptions
    {
        internal z_publisher_options_t Native;

        public static ZPublisherOptions Default()
        {
            ZPublisherOptions options = new ZPublisherOptions();
            options.InitDefault();
            return options;
        }

        public void InitDefault()
        {
            fixed (z_publisher_options_t* ptr = &Native)
            {
                ZenohNative.z_publisher_options_default(ptr);
            }
        }

        public void MoveEncoding(ZOwnedEncoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (z_publisher_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)encoding.MoveToNative();
            }
        }
    }

    public unsafe sealed class ZPublisherPutOptions
    {
        internal z_publisher_put_options_t Native;

        public static ZPublisherPutOptions Default()
        {
            ZPublisherPutOptions options = new ZPublisherPutOptions();
            options.InitDefault();
            return options;
        }

        public void InitDefault()
        {
            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ZenohNative.z_publisher_put_options_default(ptr);
            }
        }

        public void MoveEncoding(ZOwnedEncoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)encoding.MoveToNative();
            }
        }

        public void MoveAttachment(ZOwnedBytes attachment)
        {
            if (attachment == null)
                throw new ArgumentNullException(nameof(attachment));

            fixed (z_publisher_put_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)attachment.MoveToNative();
            }
        }
    }

    public unsafe sealed class ZMatchingStatus
    {
        internal z_matching_status_t Native;

        public bool Matching => Native.matching;
    }

    public unsafe sealed class ZOwnedPublisher : IDisposable
    {
        private z_owned_publisher_t* nativePtr;
        private bool disposed;

        public ZOwnedPublisher()
        {
            nativePtr = (z_owned_publisher_t*)Marshal.AllocHGlobal(sizeof(z_owned_publisher_t));
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
            ThrowIfDisposed();
            return ZenohNative.z_internal_publisher_check(nativePtr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedPublisher));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (ZenohNative.z_internal_publisher_check(nativePtr))
                    ZenohNative.z_publisher_drop((z_moved_publisher_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
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

        public ZResult Put(ZOwnedBytes payload, ZPublisherPutOptions options)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            fixed (z_publisher_put_options_t* optionsPtr = &options.Native)
            {
                ZResult result = new ZResult(ZenohNative.z_publisher_put(NativePointer, (z_moved_bytes_t*)payload.MoveToNative(), optionsPtr));
                payload.Dispose();
                return result;
            }
        }

        public ZResult GetMatchingStatus(ZMatchingStatus matchingStatus)
        {
            if (matchingStatus == null)
                throw new ArgumentNullException(nameof(matchingStatus));

            fixed (z_matching_status_t* matchingStatusPtr = &matchingStatus.Native)
            {
                return new ZResult(ZenohNative.z_publisher_get_matching_status(NativePointer, matchingStatusPtr));
            }
        }
    }
}

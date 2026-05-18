using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedReply : IDisposable
    {
        private z_owned_reply_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedReply()
        {
            nativePtr = NativeStorage.Alloc<z_owned_reply_t>();
            ZenohNative.z_internal_reply_null(nativePtr);
        }

        public static ZOwnedReply CreateClone(ZLoanedReply source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedReply value = new ZOwnedReply();
            ZenohNative.z_reply_clone(value.nativePtr, source.NativePointer);
            return value;
        }

        public static ZOwnedReply CreateTakeFromLoaned(ZLoanedReply source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedReply value = new ZOwnedReply();
            ZenohNative.z_reply_take_from_loaned(value.nativePtr, source.NativePointer);
            return value;
        }

        public ZLoanedReply Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedReply(ZenohNative.z_reply_loan(nativePtr), this);
        }

        public ZLoanedReply LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedReply(ZenohNative.z_reply_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_reply_check(nativePtr);
        }

        internal OwnedStorage<z_owned_reply_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_reply_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_reply_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedReply));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedReply));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_reply_check(nativePtr))
                    ZenohNative.z_reply_drop((z_moved_reply_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedReply()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedReply
    {
        private readonly object owner;
        internal readonly z_loaned_reply_t* NativePointer;

        internal ZLoanedReply(z_loaned_reply_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public bool IsOk()
        {
            return ZenohNative.z_reply_is_ok(NativePointer);
        }

        public ZLoanedSample Ok()
        {
            z_loaned_sample_t* ptr = ZenohNative.z_reply_ok(NativePointer);
            return ptr == null ? null : new ZLoanedSample(ptr, this);
        }

        public ZLoanedSample OkMut()
        {
            z_loaned_sample_t* ptr = ZenohNative.z_reply_ok_mut(NativePointer);
            return ptr == null ? null : new ZLoanedSample(ptr, this);
        }

        public ZLoanedReplyErr Err()
        {
            z_loaned_reply_err_t* ptr = ZenohNative.z_reply_err(NativePointer);
            return ptr == null ? null : new ZLoanedReplyErr(ptr, this);
        }

        public ZLoanedReplyErr ErrMut()
        {
            z_loaned_reply_err_t* ptr = ZenohNative.z_reply_err_mut(NativePointer);
            return ptr == null ? null : new ZLoanedReplyErr(ptr, this);
        }
    }

    public unsafe sealed class ZOwnedReplyErr : IDisposable
    {
        private z_owned_reply_err_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedReplyErr()
        {
            nativePtr = NativeStorage.Alloc<z_owned_reply_err_t>();
            ZenohNative.z_internal_reply_err_null(nativePtr);
        }

        public static ZOwnedReplyErr CreateClone(ZLoanedReplyErr source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedReplyErr value = new ZOwnedReplyErr();
            ZenohNative.z_reply_err_clone(value.nativePtr, source.NativePointer);
            return value;
        }

        public ZLoanedReplyErr Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedReplyErr(ZenohNative.z_reply_err_loan(nativePtr), this);
        }

        public ZLoanedReplyErr LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedReplyErr(ZenohNative.z_reply_err_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_reply_err_check(nativePtr);
        }

        internal OwnedStorage<z_owned_reply_err_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_reply_err_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_reply_err_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedReplyErr));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedReplyErr));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_reply_err_check(nativePtr))
                    ZenohNative.z_reply_err_drop((z_moved_reply_err_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedReplyErr()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedReplyErr
    {
        private readonly object owner;
        internal readonly z_loaned_reply_err_t* NativePointer;

        internal ZLoanedReplyErr(z_loaned_reply_err_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedBytes Payload()
        {
            return new ZLoanedBytes(ZenohNative.z_reply_err_payload(NativePointer), this);
        }

        public ZLoanedBytes PayloadMut()
        {
            return new ZLoanedBytes(ZenohNative.z_reply_err_payload_mut(NativePointer), this);
        }

        public ZLoanedEncoding Encoding()
        {
            return new ZLoanedEncoding(ZenohNative.z_reply_err_encoding(NativePointer), this);
        }
    }
}

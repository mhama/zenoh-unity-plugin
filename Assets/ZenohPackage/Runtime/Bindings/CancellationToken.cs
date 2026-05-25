using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedCancellationToken : IDisposable
    {
        private z_owned_cancellation_token_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedCancellationToken()
        {
            nativePtr = NativeStorage.Alloc<z_owned_cancellation_token_t>();
            ZenohNative.z_internal_cancellation_token_null(nativePtr);
        }

        public static ZResult TryCreateNew(out ZOwnedCancellationToken value)
        {
            value = new ZOwnedCancellationToken();
            ZResult result = new ZResult(ZenohNative.z_cancellation_token_new(value.nativePtr));
            if (!result.IsOk)
            {
                value.Dispose();
                value = null;
            }

            return result;
        }

        public static ZOwnedCancellationToken CreateClone(ZLoanedCancellationToken source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedCancellationToken value = new ZOwnedCancellationToken();
            ZenohNative.z_cancellation_token_clone(value.nativePtr, source.NativePointer);
            return value;
        }

        public ZLoanedCancellationToken Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedCancellationToken(ZenohNative.z_cancellation_token_loan(nativePtr), this);
        }

        public ZLoanedCancellationToken LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedCancellationToken(ZenohNative.z_cancellation_token_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_cancellation_token_check(nativePtr);
        }

        internal OwnedStorage<z_owned_cancellation_token_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_cancellation_token_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_cancellation_token_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedCancellationToken));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedCancellationToken));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_cancellation_token_check(nativePtr))
                    ZenohNative.z_cancellation_token_drop((z_moved_cancellation_token_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedCancellationToken()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedCancellationToken
    {
        private readonly ZOwnedCancellationToken owner;
        internal readonly z_loaned_cancellation_token_t* NativePointer;

        internal ZLoanedCancellationToken(z_loaned_cancellation_token_t* nativePtr, ZOwnedCancellationToken owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZResult Cancel()
        {
            return new ZResult(ZenohNative.z_cancellation_token_cancel(NativePointer));
        }

        public bool IsCancelled()
        {
            return ZenohNative.z_cancellation_token_is_cancelled(NativePointer);
        }
    }
}

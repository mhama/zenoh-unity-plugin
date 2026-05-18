using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    /// <summary>
    /// Public projection of <c>z_locality_t</c>.
    /// </summary>
    public enum ZLocality : uint
    {
        Any = 0,
        SessionLocal = 1,
        Remote = 2,
    }

    public unsafe sealed class ZQueryableOptions
    {
        internal z_queryable_options_t Native;

        public static ZQueryableOptions Default()
        {
            ZQueryableOptions options = new ZQueryableOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            fixed (z_queryable_options_t* ptr = &Native)
            {
                ZenohNative.z_queryable_options_default(ptr);
            }
        }

        public void SetComplete(bool complete)
        {
            fixed (z_queryable_options_t* ptr = &Native)
            {
                ptr->complete = complete;
            }
        }

        public void SetAllowedOrigin(ZLocality locality)
        {
            fixed (z_queryable_options_t* ptr = &Native)
            {
                ptr->allowed_origin = (z_locality_t)locality;
            }
        }
    }

    public unsafe sealed class ZOwnedQueryable : IDisposable
    {
        private z_owned_queryable_t* nativePtr;
        private bool disposed;
        private bool moved;

        public ZOwnedQueryable()
        {
            nativePtr = NativeStorage.Alloc<z_owned_queryable_t>();
            ZenohNative.z_internal_queryable_null(nativePtr);
        }

        public ZResult Declare(
            ZLoanedSession session,
            ZLoanedKeyExpr keyExpr,
            ZOwnedClosureQuery movingCallback,
            ZQueryableOptions options)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (movingCallback == null)
                throw new ArgumentNullException(nameof(movingCallback));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            ThrowIfDisposedOrMoved();

            using (OwnedStorage<z_owned_closure_query_t> callbackStorage = movingCallback.Take())
            {
                fixed (z_queryable_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_declare_queryable(
                        session.NativePointer,
                        nativePtr,
                        keyExpr.NativePointer,
                        (z_moved_closure_query_t*)callbackStorage.Pointer,
                        optionsPtr));
                }
            }
        }

        public ZLoanedQueryable Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedQueryable(ZenohNative.z_queryable_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_queryable_check(nativePtr);
        }

        internal OwnedStorage<z_owned_queryable_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_queryable_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_queryable_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedQueryable));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedQueryable));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_queryable_check(nativePtr))
                    ZenohNative.z_queryable_drop((z_moved_queryable_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedQueryable()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedQueryable
    {
        private readonly ZOwnedQueryable owner;
        internal readonly z_loaned_queryable_t* NativePointer;

        internal ZLoanedQueryable(z_loaned_queryable_t* nativePtr, ZOwnedQueryable owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedKeyExpr KeyExpr()
        {
            return new ZLoanedKeyExpr(ZenohNative.z_queryable_keyexpr(NativePointer), this);
        }
    }
}

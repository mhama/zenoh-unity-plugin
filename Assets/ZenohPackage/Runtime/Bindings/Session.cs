using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOpenOptions
    {
        internal z_open_options_t Native;

        public static ZOpenOptions Default()
        {
            ZOpenOptions options = new ZOpenOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            fixed (z_open_options_t* ptr = &Native)
            {
                ZenohNative.z_open_options_default(ptr);
            }
        }
    }

    public unsafe sealed class ZCloseOptions
    {
        internal z_close_options_t Native;

        public static ZCloseOptions Default()
        {
            ZCloseOptions options = new ZCloseOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            fixed (z_close_options_t* ptr = &Native)
            {
                ZenohNative.z_close_options_default(ptr);
            }
        }
    }

    public unsafe sealed class ZOwnedSession : IDisposable
    {
        private z_owned_session_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedSession()
        {
            nativePtr = NativeStorage.Alloc<z_owned_session_t>();
            ZenohNative.z_internal_session_null(nativePtr);
        }

        public static ZResult TryCreateOpen(ZOwnedConfig movingConfig, ZOpenOptions options, out ZOwnedSession session)
        {
            if (movingConfig == null)
                throw new ArgumentNullException(nameof(movingConfig));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            using (OwnedStorage<z_owned_config_t> configStorage = movingConfig.Take())
            {
                session = new ZOwnedSession();
                fixed (z_open_options_t* optionsPtr = &options.Native)
                {
                    ZResult result = new ZResult(ZenohNative.z_open(session.nativePtr, (z_moved_config_t*)configStorage.Pointer, optionsPtr));
                    if (!result.IsOk)
                    {
                        session.Dispose();
                        session = null;
                    }

                    return result;
                }
            }
        }

        public ZLoanedSession Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedSession(ZenohNative.z_session_loan(nativePtr), this);
        }

        public ZLoanedSession LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedSession(ZenohNative.z_session_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_session_check(nativePtr);
        }

        internal OwnedStorage<z_owned_session_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_session_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_session_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedSession));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedSession));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_session_check(nativePtr))
                    ZenohNative.z_session_drop((z_moved_session_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedSession()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedSession
    {
        private readonly ZOwnedSession owner;
        internal readonly z_loaned_session_t* NativePointer;

        internal ZLoanedSession(z_loaned_session_t* nativePtr, ZOwnedSession owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZResult Close(ZCloseOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            fixed (z_close_options_t* optionsPtr = &options.Native)
            {
                return new ZResult(ZenohNative.z_close(NativePointer, optionsPtr));
            }
        }

        public bool IsClosed()
        {
            return ZenohNative.z_session_is_closed(NativePointer);
        }

        public ZResult Get(
            ZLoanedKeyExpr keyExpr,
            byte[] nullTerminatedParameters,
            ZOwnedClosureReply movingCallback,
            ZGetOptions options)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (nullTerminatedParameters == null)
                throw new ArgumentNullException(nameof(nullTerminatedParameters));
            if (movingCallback == null)
                throw new ArgumentNullException(nameof(movingCallback));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            options.ThrowIfDisposed();

            using (OwnedStorage<z_owned_closure_reply_t> callbackStorage = movingCallback.Take())
            {
                fixed (byte* parametersPtr = nullTerminatedParameters)
                fixed (z_get_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_get(
                        NativePointer,
                        keyExpr.NativePointer,
                        parametersPtr,
                        (z_moved_closure_reply_t*)callbackStorage.Pointer,
                        optionsPtr));
                }
            }
        }

        public ZResult DeclareBackgroundQueryable(
            ZLoanedKeyExpr keyExpr,
            ZOwnedClosureQuery movingCallback,
            ZQueryableOptions options)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (movingCallback == null)
                throw new ArgumentNullException(nameof(movingCallback));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            using (OwnedStorage<z_owned_closure_query_t> callbackStorage = movingCallback.Take())
            {
                fixed (z_queryable_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_declare_background_queryable(
                        NativePointer,
                        keyExpr.NativePointer,
                        (z_moved_closure_query_t*)callbackStorage.Pointer,
                        optionsPtr));
                }
            }
        }
    }
}

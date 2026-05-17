using System;
using System.Runtime.InteropServices;
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
            nativePtr = (z_owned_session_t*)Marshal.AllocHGlobal(sizeof(z_owned_session_t));
            ZenohNative.z_internal_session_null(nativePtr);
        }

        public static ZResult TryCreateOpen(ZOwnedConfig movingConfig, ZOpenOptions options, out ZOwnedSession session)
        {
            if (movingConfig == null)
                throw new ArgumentNullException(nameof(movingConfig));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            session = new ZOwnedSession();
            fixed (z_open_options_t* optionsPtr = &options.Native)
            {
                ZResult result = new ZResult(ZenohNative.z_open(session.nativePtr, (z_moved_config_t*)movingConfig.MoveToNative(), optionsPtr));
                movingConfig.Dispose();
                if (!result.IsOk)
                {
                    session.Dispose();
                    session = null;
                }

                return result;
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

        internal z_owned_session_t* MoveToNative()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            return nativePtr;
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

                Marshal.FreeHGlobal((IntPtr)nativePtr);
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
    }
}

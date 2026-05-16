using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedConfig : IDisposable
    {
        private z_owned_config_t* nativePtr;
        private bool disposed;
        private bool moved;

        public ZOwnedConfig()
        {
            nativePtr = (z_owned_config_t*)Marshal.AllocHGlobal(sizeof(z_owned_config_t));
            ZenohNative.z_internal_config_null(nativePtr);
        }

        public static ZOwnedConfig Default()
        {
            ZOwnedConfig config = new ZOwnedConfig();
            ZResult result = config.InitDefault();
            if (result.IsError)
            {
                config.Dispose();
                throw new InvalidOperationException(result.ToString());
            }

            return config;
        }

        public ZResult InitDefault()
        {
            ThrowIfDisposedOrMoved();
            return new ZResult(ZenohNative.z_config_default(nativePtr));
        }

        public ZResult FromStr(byte[] nullTerminatedUtf8)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));
            ThrowIfDisposedOrMoved();

            fixed (byte* ptr = nullTerminatedUtf8)
            {
                return new ZResult(ZenohNative.zc_config_from_str(nativePtr, ptr));
            }
        }

        public ZResult FromSubstr(byte[] utf8, nuint len)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));
            ThrowIfDisposedOrMoved();

            fixed (byte* ptr = utf8)
            {
                return new ZResult(ZenohNative.zc_config_from_substr(nativePtr, ptr, len));
            }
        }

        public ZResult FromFile(byte[] nullTerminatedUtf8Path)
        {
            if (nullTerminatedUtf8Path == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8Path));
            ThrowIfDisposedOrMoved();

            fixed (byte* ptr = nullTerminatedUtf8Path)
            {
                return new ZResult(ZenohNative.zc_config_from_file(nativePtr, ptr));
            }
        }

        public ZResult FromEnv()
        {
            ThrowIfDisposedOrMoved();
            return new ZResult(ZenohNative.zc_config_from_env(nativePtr));
        }

        public ZLoanedConfig Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedConfig(ZenohNative.z_config_loan(nativePtr), this);
        }

        public ZLoanedConfig LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedConfig(ZenohNative.z_config_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposed();
            return !moved && ZenohNative.z_internal_config_check(nativePtr);
        }

        internal z_owned_config_t* MoveToNative()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            return nativePtr;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedConfig));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedConfig));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_config_check(nativePtr))
                    ZenohNative.z_config_drop((z_moved_config_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedConfig()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedConfig
    {
        private readonly ZOwnedConfig owner;
        internal readonly z_loaned_config_t* NativePointer;

        internal ZLoanedConfig(z_loaned_config_t* nativePtr, ZOwnedConfig owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }
    }
}

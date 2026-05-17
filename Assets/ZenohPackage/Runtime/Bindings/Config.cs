using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedConfig : IDisposable
    {
        private z_owned_config_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedConfig()
        {
            nativePtr = NativeStorage.Alloc<z_owned_config_t>();
            ZenohNative.z_internal_config_null(nativePtr);
        }

        public static ZResult TryCreateDefault(out ZOwnedConfig value)
        {
            value = new ZOwnedConfig();
            return CompleteCreate(new ZResult(ZenohNative.z_config_default(value.nativePtr)), ref value);
        }

        public static ZResult TryCreateFromStr(byte[] nullTerminatedUtf8, out ZOwnedConfig value)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));

            value = new ZOwnedConfig();
            fixed (byte* ptr = nullTerminatedUtf8)
            {
                return CompleteCreate(new ZResult(ZenohNative.zc_config_from_str(value.nativePtr, ptr)), ref value);
            }
        }

        public static ZResult TryCreateFromSubstr(byte[] utf8, nuint len, out ZOwnedConfig value)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));

            value = new ZOwnedConfig();
            fixed (byte* ptr = utf8)
            {
                return CompleteCreate(new ZResult(ZenohNative.zc_config_from_substr(value.nativePtr, ptr, len)), ref value);
            }
        }

        public static ZResult TryCreateFromFile(byte[] nullTerminatedUtf8Path, out ZOwnedConfig value)
        {
            if (nullTerminatedUtf8Path == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8Path));

            value = new ZOwnedConfig();
            fixed (byte* ptr = nullTerminatedUtf8Path)
            {
                return CompleteCreate(new ZResult(ZenohNative.zc_config_from_file(value.nativePtr, ptr)), ref value);
            }
        }

        public static ZResult TryCreateFromFileSubstr(byte[] utf8Path, nuint len, out ZOwnedConfig value)
        {
            if (utf8Path == null)
                throw new ArgumentNullException(nameof(utf8Path));

            value = new ZOwnedConfig();
            fixed (byte* ptr = utf8Path)
            {
                return CompleteCreate(new ZResult(ZenohNative.zc_config_from_file_substr(value.nativePtr, ptr, len)), ref value);
            }
        }

        public static ZResult TryCreateFromEnv(out ZOwnedConfig value)
        {
            value = new ZOwnedConfig();
            return CompleteCreate(new ZResult(ZenohNative.zc_config_from_env(value.nativePtr)), ref value);
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
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_config_check(nativePtr);
        }

        internal OwnedStorage<z_owned_config_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_config_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_config_t>(ptr);
        }

        private static ZResult CompleteCreate(ZResult result, ref ZOwnedConfig value)
        {
            if (!result.IsOk)
            {
                value.Dispose();
                value = null;
            }

            return result;
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

                NativeStorage.Free(nativePtr);
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

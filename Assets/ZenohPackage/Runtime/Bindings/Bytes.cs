using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedBytes : IDisposable
    {
        private z_owned_bytes_t* nativePtr;
        private bool disposed;
        private bool moved;
        private bool initialized;

        private ZOwnedBytes()
        {
            nativePtr = (z_owned_bytes_t*)Marshal.AllocHGlobal(sizeof(z_owned_bytes_t));
            ZenohNative.z_internal_bytes_null(nativePtr);
        }

        public static ZOwnedBytes CreateEmpty()
        {
            ZOwnedBytes bytes = new ZOwnedBytes();
            ZenohNative.z_bytes_empty(bytes.nativePtr);
            bytes.initialized = true;
            return bytes;
        }

        public static ZResult TryCreateCopyFromBuf(byte[] data, nuint len, out ZOwnedBytes value)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            value = new ZOwnedBytes();

            fixed (byte* ptr = data)
            {
                ZResult result = new ZResult(ZenohNative.z_bytes_copy_from_buf(value.nativePtr, ptr, len));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }
                else
                {
                    value.initialized = true;
                }

                return result;
            }
        }

        public static ZResult TryCreateCopyFromStr(byte[] nullTerminatedUtf8, out ZOwnedBytes value)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));

            value = new ZOwnedBytes();

            fixed (byte* ptr = nullTerminatedUtf8)
            {
                ZResult result = new ZResult(ZenohNative.z_bytes_copy_from_str(value.nativePtr, ptr));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }
                else
                {
                    value.initialized = true;
                }

                return result;
            }
        }

        public ZLoanedBytes Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedBytes(ZenohNative.z_bytes_loan(nativePtr), this);
        }

        public ZLoanedBytes LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedBytes(ZenohNative.z_bytes_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_bytes_check(nativePtr);
        }

        internal z_owned_bytes_t* MoveToNative()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            return nativePtr;
        }

        internal z_owned_bytes_t* MoveStorageToNative()
        {
            z_owned_bytes_t* ptr = MoveToNative();
            nativePtr = null;
            initialized = false;
            return ptr;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedBytes));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedBytes));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && initialized)
                    ZenohNative.z_bytes_drop((z_moved_bytes_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedBytes()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedBytes
    {
        private readonly object owner;
        internal readonly z_loaned_bytes_t* NativePointer;

        internal ZLoanedBytes(z_loaned_bytes_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public bool IsEmpty()
        {
            return ZenohNative.z_bytes_is_empty(NativePointer);
        }

        public nuint Len()
        {
            return ZenohNative.z_bytes_len(NativePointer);
        }
    }
}

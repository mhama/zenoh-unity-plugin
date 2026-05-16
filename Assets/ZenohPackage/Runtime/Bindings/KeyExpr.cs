using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedKeyExpr : IDisposable
    {
        private z_owned_keyexpr_t* nativePtr;
        private bool disposed;
        private bool moved;

        public ZOwnedKeyExpr()
        {
            nativePtr = (z_owned_keyexpr_t*)Marshal.AllocHGlobal(sizeof(z_owned_keyexpr_t));
            ZenohNative.z_internal_keyexpr_null(nativePtr);
        }

        public ZResult FromStr(byte[] nullTerminatedUtf8)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));
            ThrowIfDisposedOrMoved();

            fixed (byte* ptr = nullTerminatedUtf8)
            {
                return new ZResult(ZenohNative.z_keyexpr_from_str(nativePtr, ptr));
            }
        }

        public ZResult FromSubstr(byte[] utf8, nuint len)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));
            ThrowIfDisposedOrMoved();

            fixed (byte* ptr = utf8)
            {
                return new ZResult(ZenohNative.z_keyexpr_from_substr(nativePtr, ptr, len));
            }
        }

        public ZLoanedKeyExpr Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedKeyExpr(ZenohNative.z_keyexpr_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposed();
            return !moved && ZenohNative.z_internal_keyexpr_check(nativePtr);
        }

        internal z_owned_keyexpr_t* MoveToNative()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            return nativePtr;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedKeyExpr));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedKeyExpr));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_keyexpr_check(nativePtr))
                    ZenohNative.z_keyexpr_drop((z_moved_keyexpr_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedKeyExpr()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedKeyExpr
    {
        private readonly object owner;
        internal readonly z_loaned_keyexpr_t* NativePointer;

        internal ZLoanedKeyExpr(z_loaned_keyexpr_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public bool Equals(ZLoanedKeyExpr other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            return ZenohNative.z_keyexpr_equals(NativePointer, other.NativePointer);
        }
    }
}

using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedKeyExpr : IDisposable
    {
        private z_owned_keyexpr_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedKeyExpr()
        {
            nativePtr = NativeStorage.Alloc<z_owned_keyexpr_t>();
            ZenohNative.z_internal_keyexpr_null(nativePtr);
        }

        public static ZResult TryCreateFromStr(byte[] nullTerminatedUtf8, out ZOwnedKeyExpr value)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));

            value = new ZOwnedKeyExpr();
            fixed (byte* ptr = nullTerminatedUtf8)
            {
                ZResult result = new ZResult(ZenohNative.z_keyexpr_from_str(value.nativePtr, ptr));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }

                return result;
            }
        }

        public static ZResult TryCreateFromSubstr(byte[] utf8, nuint len, out ZOwnedKeyExpr value)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));

            value = new ZOwnedKeyExpr();
            fixed (byte* ptr = utf8)
            {
                ZResult result = new ZResult(ZenohNative.z_keyexpr_from_substr(value.nativePtr, ptr, len));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }

                return result;
            }
        }

        public ZLoanedKeyExpr Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedKeyExpr(ZenohNative.z_keyexpr_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_keyexpr_check(nativePtr);
        }

        internal OwnedStorage<z_owned_keyexpr_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_keyexpr_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_keyexpr_t>(ptr);
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

                NativeStorage.Free(nativePtr);
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

    public unsafe sealed class ZLoanedKeyExpr : IEquatable<ZLoanedKeyExpr>
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
                return false;

            return ZenohNative.z_keyexpr_equals(NativePointer, other.NativePointer);
        }

        public override bool Equals(object obj)
        {
            return obj is ZLoanedKeyExpr other && Equals(other);
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}

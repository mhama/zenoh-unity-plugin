using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedSample : IDisposable
    {
        private z_owned_sample_t* nativePtr;
        private bool disposed;

        public ZOwnedSample()
        {
            nativePtr = (z_owned_sample_t*)Marshal.AllocHGlobal(sizeof(z_owned_sample_t));
            ZenohNative.z_internal_sample_null(nativePtr);
        }

        public ZLoanedSample Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedSample(ZenohNative.z_sample_loan(nativePtr), this);
        }

        public ZLoanedSample LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedSample(ZenohNative.z_sample_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposed();
            return ZenohNative.z_internal_sample_check(nativePtr);
        }

        internal z_owned_sample_t* NativePointer
        {
            get
            {
                ThrowIfDisposedOrMoved();
                return nativePtr;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedSample));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (ZenohNative.z_internal_sample_check(nativePtr))
                    ZenohNative.z_sample_drop((z_moved_sample_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedSample()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedSample
    {
        private readonly object owner;
        internal readonly z_loaned_sample_t* NativePointer;

        internal ZLoanedSample(z_loaned_sample_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedKeyExpr KeyExpr()
        {
            return new ZLoanedKeyExpr(ZenohNative.z_sample_keyexpr(NativePointer), this);
        }

        public ZLoanedEncoding Encoding()
        {
            return new ZLoanedEncoding(ZenohNative.z_sample_encoding(NativePointer), this);
        }

        public ZLoanedBytes Payload()
        {
            return new ZLoanedBytes(ZenohNative.z_sample_payload(NativePointer), this);
        }

        public ZLoanedBytes PayloadMut()
        {
            return new ZLoanedBytes(ZenohNative.z_sample_payload_mut(NativePointer), this);
        }

        public uint Kind()
        {
            return (uint)ZenohNative.z_sample_kind(NativePointer);
        }

        public IntPtr Timestamp()
        {
            return (IntPtr)ZenohNative.z_sample_timestamp(NativePointer);
        }

        public ZLoanedBytes Attachment()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_sample_attachment(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public IntPtr SourceInfo()
        {
            return (IntPtr)ZenohNative.z_sample_source_info(NativePointer);
        }

        public bool Express()
        {
            return ZenohNative.z_sample_express(NativePointer);
        }
    }
}

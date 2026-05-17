using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public enum ZSampleKind : uint
    {
        Put = 0,
        Delete = 1,
    }

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

        public ZSampleKind Kind()
        {
            return (ZSampleKind)ZenohNative.z_sample_kind(NativePointer);
        }

        public ZLoanedTimestamp Timestamp()
        {
            z_timestamp_t* ptr = ZenohNative.z_sample_timestamp(NativePointer);
            return ptr == null ? null : new ZLoanedTimestamp(ptr, this);
        }

        public ZLoanedBytes Attachment()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_sample_attachment(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public ZLoanedSourceInfo SourceInfo()
        {
            z_source_info_t* ptr = ZenohNative.z_sample_source_info(NativePointer);
            return ptr == null ? null : new ZLoanedSourceInfo(ptr, this);
        }

        public bool Express()
        {
            return ZenohNative.z_sample_express(NativePointer);
        }
    }

    public unsafe sealed class ZLoanedTimestamp
    {
        private readonly object owner;
        internal readonly z_timestamp_t* NativePointer;

        internal ZLoanedTimestamp(z_timestamp_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ulong Ntp64Time()
        {
            return ZenohNative.z_timestamp_ntp64_time(NativePointer);
        }
    }

    public unsafe sealed class ZLoanedSourceInfo
    {
        private readonly object owner;
        internal readonly z_source_info_t* NativePointer;

        internal ZLoanedSourceInfo(z_source_info_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public uint Sn()
        {
            return ZenohNative.z_source_info_sn(NativePointer);
        }
    }
}

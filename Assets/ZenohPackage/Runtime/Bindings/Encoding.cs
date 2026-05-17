using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public unsafe sealed class ZOwnedEncoding : IDisposable
    {
        private z_owned_encoding_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedEncoding()
        {
            nativePtr = NativeStorage.Alloc<z_owned_encoding_t>();
            ZenohNative.z_internal_encoding_null(nativePtr);
        }

        public static ZResult TryCreateFromStr(byte[] nullTerminatedUtf8, out ZOwnedEncoding value)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));

            value = new ZOwnedEncoding();
            fixed (byte* ptr = nullTerminatedUtf8)
            {
                ZResult result = new ZResult(ZenohNative.z_encoding_from_str(value.nativePtr, ptr));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }

                return result;
            }
        }

        public static ZResult TryCreateFromSubstr(byte[] utf8, nuint len, out ZOwnedEncoding value)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));

            value = new ZOwnedEncoding();
            fixed (byte* ptr = utf8)
            {
                ZResult result = new ZResult(ZenohNative.z_encoding_from_substr(value.nativePtr, ptr, len));
                if (!result.IsOk)
                {
                    value.Dispose();
                    value = null;
                }

                return result;
            }
        }

        public ZLoanedEncoding Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedEncoding(ZenohNative.z_encoding_loan(nativePtr), this);
        }

        public ZLoanedEncoding LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedEncoding(ZenohNative.z_encoding_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_encoding_check(nativePtr);
        }

        internal OwnedStorage<z_owned_encoding_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_encoding_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_encoding_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedEncoding));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedEncoding));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_encoding_check(nativePtr))
                    ZenohNative.z_encoding_drop((z_moved_encoding_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedEncoding()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedEncoding : IEquatable<ZLoanedEncoding>
    {
        private readonly object owner;
        internal readonly z_loaned_encoding_t* NativePointer;

        internal ZLoanedEncoding(z_loaned_encoding_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZResult SetSchemaFromStr(byte[] nullTerminatedUtf8)
        {
            if (nullTerminatedUtf8 == null)
                throw new ArgumentNullException(nameof(nullTerminatedUtf8));

            fixed (byte* ptr = nullTerminatedUtf8)
            {
                return new ZResult(ZenohNative.z_encoding_set_schema_from_str(NativePointer, ptr));
            }
        }

        public ZResult SetSchemaFromSubstr(byte[] utf8, nuint len)
        {
            if (utf8 == null)
                throw new ArgumentNullException(nameof(utf8));

            fixed (byte* ptr = utf8)
            {
                return new ZResult(ZenohNative.z_encoding_set_schema_from_substr(NativePointer, ptr, len));
            }
        }

        public bool Equals(ZLoanedEncoding other)
        {
            if (other == null)
                return false;

            return ZenohNative.z_encoding_equals(NativePointer, other.NativePointer);
        }

        public override bool Equals(object obj)
        {
            return obj is ZLoanedEncoding other && Equals(other);
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    public unsafe static class ZEncodingConstants
    {
        public static ZLoanedEncoding LoanDefault() => new ZLoanedEncoding(ZenohNative.z_encoding_loan_default(), null);
        public static ZLoanedEncoding ZenohBytes() => new ZLoanedEncoding(ZenohNative.z_encoding_zenoh_bytes(), null);
        public static ZLoanedEncoding ZenohString() => new ZLoanedEncoding(ZenohNative.z_encoding_zenoh_string(), null);
        public static ZLoanedEncoding ZenohSerialized() => new ZLoanedEncoding(ZenohNative.z_encoding_zenoh_serialized(), null);
        public static ZLoanedEncoding ApplicationOctetStream() => new ZLoanedEncoding(ZenohNative.z_encoding_application_octet_stream(), null);
        public static ZLoanedEncoding TextPlain() => new ZLoanedEncoding(ZenohNative.z_encoding_text_plain(), null);
        public static ZLoanedEncoding ApplicationJson() => new ZLoanedEncoding(ZenohNative.z_encoding_application_json(), null);
        public static ZLoanedEncoding TextJson() => new ZLoanedEncoding(ZenohNative.z_encoding_text_json(), null);
        public static ZLoanedEncoding TextJson5() => new ZLoanedEncoding(ZenohNative.z_encoding_text_json5(), null);
        public static ZLoanedEncoding ImagePng() => new ZLoanedEncoding(ZenohNative.z_encoding_image_png(), null);
        public static ZLoanedEncoding ImageJpeg() => new ZLoanedEncoding(ZenohNative.z_encoding_image_jpeg(), null);
        public static ZLoanedEncoding ImageGif() => new ZLoanedEncoding(ZenohNative.z_encoding_image_gif(), null);
        public static ZLoanedEncoding ImageBmp() => new ZLoanedEncoding(ZenohNative.z_encoding_image_bmp(), null);
        public static ZLoanedEncoding ImageWebp() => new ZLoanedEncoding(ZenohNative.z_encoding_image_webp(), null);
    }
}

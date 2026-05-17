using System;
using System.Runtime.InteropServices;
using System.Text;
using Zenoh.Bindings;
using Zenoh.Plugins;

namespace Zenoh.HighLevel.Internal
{
    internal static unsafe class BindingsInterop
    {
        public static string KeyExprAsString(ZLoanedKeyExpr keyExpr)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));

            z_view_string_t viewString;
            ZenohNative.z_keyexpr_as_view_string(keyExpr.NativePointer, &viewString);
            z_loaned_string_t* loaned = ZenohNative.z_view_string_loan(&viewString);
            return LoanedStringToManaged(loaned);
        }

        public static byte[] BytesToByteArray(ZLoanedBytes bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            int len = checked((int)ZenohNative.z_bytes_len(bytes.NativePointer));
            byte[] managed = new byte[len];
            if (len > 0)
                BytesCopyToInternal(bytes, managed, 0, len);
            return managed;
        }

        public static int BytesCopyTo(ZLoanedBytes bytes, byte[] dst, int dstOffset)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            int len = checked((int)ZenohNative.z_bytes_len(bytes.NativePointer));
            if (dstOffset < 0 || dstOffset > dst.Length)
                throw new ArgumentOutOfRangeException(nameof(dstOffset));
            if (len > dst.Length - dstOffset)
                throw new ArgumentException("Destination buffer is too small", nameof(dst));

            if (len == 0)
                return 0;

            return BytesCopyToInternal(bytes, dst, dstOffset, len);
        }

        private static int BytesCopyToInternal(ZLoanedBytes bytes, byte[] dst, int dstOffset, int len)
        {
            // Fast path: bytes are contiguous in native memory, copy directly without
            // an intermediate native heap allocation.
            z_view_slice_t view = default;
            ZResult viewResult = new ZResult(ZenohNative.z_bytes_get_contiguous_view(bytes.NativePointer, &view));
            if (viewResult.IsOk)
            {
                z_loaned_slice_t* loaned = ZenohNative.z_view_slice_loan(&view);
                byte* src = ZenohNative.z_slice_data(loaned);
                Marshal.Copy((IntPtr)src, dst, dstOffset, len);
                return len;
            }

            // Fallback for non-contiguous payloads: stream via reader directly into dst.
            z_bytes_reader_t reader = ZenohNative.z_bytes_get_reader(bytes.NativePointer);
            fixed (byte* dstPtr = dst)
            {
                nuint read = ZenohNative.z_bytes_reader_read(&reader, dstPtr + dstOffset, (nuint)len);
                return checked((int)read);
            }
        }

        public static string BytesToStringUtf8(ZLoanedBytes bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            z_owned_string_t str = default;
            ZResult result = new ZResult(ZenohNative.z_bytes_to_string(bytes.NativePointer, &str));
            if (!result.IsOk)
                throw new ZenohException(result, "Failed to convert bytes to string");

            try
            {
                z_loaned_string_t* loaned = ZenohNative.z_string_loan(&str);
                return LoanedStringToManaged(loaned);
            }
            finally
            {
                ZenohNative.z_string_drop((z_moved_string_t*)&str);
            }
        }

        public static string EncodingToString(ZLoanedEncoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            z_owned_string_t str = default;
            ZenohNative.z_encoding_to_string(encoding.NativePointer, &str);
            try
            {
                z_loaned_string_t* loaned = ZenohNative.z_string_loan(&str);
                return LoanedStringToManaged(loaned);
            }
            finally
            {
                ZenohNative.z_string_drop((z_moved_string_t*)&str);
            }
        }

        private static string LoanedStringToManaged(z_loaned_string_t* loaned)
        {
            byte* data = ZenohNative.z_string_data(loaned);
            nuint len = ZenohNative.z_string_len(loaned);
            if (data == null || len == 0)
                return string.Empty;

            int length = checked((int)len);
            return Encoding.UTF8.GetString(data, length);
        }
    }
}

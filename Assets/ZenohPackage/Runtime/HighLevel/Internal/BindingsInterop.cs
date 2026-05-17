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

            z_owned_slice_t slice = default;
            ZResult result = new ZResult(ZenohNative.z_bytes_to_slice(bytes.NativePointer, &slice));
            if (!result.IsOk)
                throw new ZenohException(result, "Failed to convert bytes to slice");

            try
            {
                z_loaned_slice_t* loaned = ZenohNative.z_slice_loan(&slice);
                byte* data = ZenohNative.z_slice_data(loaned);
                nuint len = ZenohNative.z_slice_len(loaned);
                int length = checked((int)len);
                byte[] managed = new byte[length];
                if (length > 0)
                    Marshal.Copy((IntPtr)data, managed, 0, length);
                return managed;
            }
            finally
            {
                ZenohNative.z_slice_drop((z_moved_slice_t*)&slice);
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

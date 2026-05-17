using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public static class LoanedExtensions
    {
        public static byte[] ToByteArray(this ZLoanedBytes bytes)
        {
            return BindingsInterop.BytesToByteArray(bytes);
        }

        public static string ToStringUtf8(this ZLoanedBytes bytes)
        {
            return BindingsInterop.BytesToStringUtf8(bytes);
        }

        public static string AsString(this ZLoanedKeyExpr keyExpr)
        {
            return BindingsInterop.KeyExprAsString(keyExpr);
        }

        public static string AsString(this ZLoanedEncoding encoding)
        {
            return BindingsInterop.EncodingToString(encoding);
        }
    }
}

using System;
using System.Text;

namespace Zenoh.HighLevel.Internal
{
    internal static class Utf8Helper
    {
        public static byte[] EncodeNullTerminated(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            int byteCount = Encoding.UTF8.GetByteCount(value);
            byte[] buffer = new byte[byteCount + 1];
            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
            return buffer;
        }
    }
}

using System;
using Zenoh.Bindings;

namespace Zenoh.HighLevel.Internal
{
    internal static class BytesFactory
    {
        public static ZOwnedBytes FromString(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(value);
            ZResult result = ZOwnedBytes.TryCreateCopyFromStr(buffer, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from string");
            return owned;
        }

        public static ZOwnedBytes FromBuffer(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ZResult result = ZOwnedBytes.TryCreateCopyFromBuf(buffer, (nuint)buffer.Length, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from buffer");
            return owned;
        }

        public static ZOwnedBytes FromBuffer(ReadOnlySpan<byte> buffer)
        {
            byte[] copy = buffer.ToArray();
            return FromBuffer(copy);
        }
    }
}

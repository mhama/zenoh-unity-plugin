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

            // Zero-copy: pin the managed array and hand ownership to Zenoh, which
            // releases the pin via the registered deleter when the bytes are dropped.
            ZResult result = ZOwnedBytes.TryCreateFromManagedBuf(buffer, 0, buffer.Length, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from buffer");
            return owned;
        }

        public static ZOwnedBytes FromBuffer(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ZResult result = ZOwnedBytes.TryCreateFromManagedBuf(buffer, offset, length, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from buffer slice");
            return owned;
        }

        public static ZOwnedBytes FromBuffer(ReadOnlySpan<byte> buffer)
        {
            // ReadOnlySpan can't be safely pinned across the lifetime of the bytes,
            // so we fall back to the copy path.
            byte[] copy = buffer.ToArray();
            return FromBuffer(copy);
        }
    }
}

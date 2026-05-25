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

        /// <summary>
        /// Copies the span contents into Zenoh-owned memory.
        ///
        /// Internally allocates a temporary array, then pins it into Zenoh
        /// to avoid a second copy.
        /// </summary>
        public static ZOwnedBytes FromBuffer(ReadOnlySpan<byte> buffer)
        {
            byte[] copy = buffer.ToArray();
            return FromBufferZeroCopy(copy);
        }

        /// <summary>
        /// Pins the managed array and hands it to Zenoh without copying.
        ///
        /// The caller must NOT mutate <paramref name="buffer"/> until Zenoh drops
        /// the bytes (i.e. until the internal deleter fires).
        /// </summary>
        public static ZOwnedBytes FromBufferZeroCopy(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ZResult result = ZOwnedBytes.TryCreateFromManagedBuf(buffer, 0, buffer.Length, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from buffer (zero-copy)");
            return owned;
        }

        /// <summary>
        /// Pins a slice of the managed array and hands it to Zenoh without copying.
        ///
        /// The caller must NOT mutate the pinned region until Zenoh drops the bytes.
        /// </summary>
        public static ZOwnedBytes FromBufferZeroCopy(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            ZResult result = ZOwnedBytes.TryCreateFromManagedBuf(buffer, offset, length, out ZOwnedBytes owned);
            result.ThrowIfErr("Failed to create bytes from buffer slice (zero-copy)");
            return owned;
        }
    }
}

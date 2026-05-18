using System;
using System.Runtime.InteropServices;
using AOT;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    // Hand-written, non-mechanical factories for ZOwnedBytes that hand a borrowed
    // managed/external buffer to Zenoh without copying. Lifetime is controlled via
    // GCHandle pinning and a native deleter callback, so these need careful review
    // separate from the mechanical Owned/Loaned scaffolding in Bytes.cs.
    public unsafe sealed partial class ZOwnedBytes
    {
        // Static delegate field keeps the unmanaged-callable thunk alive for AOT.
        private static readonly ZenohNative.z_bytes_from_buf_deleter_delegate ManagedBufDeleter = OnManagedBufFreed;

        /// <summary>
        /// Constructs a <see cref="ZOwnedBytes"/> that aliases a pinned managed byte[]
        /// without copying. The array is pinned via <see cref="GCHandle"/> and released
        /// when Zenoh drops the bytes (i.e. when this wrapper is disposed or moved into
        /// an API that drops it).
        /// </summary>
        public static ZResult TryCreateFromManagedBuf(byte[] buffer, int offset, int length, out ZOwnedBytes value)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length < 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            value = new ZOwnedBytes();
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                byte* basePtr = (byte*)handle.AddrOfPinnedObject();
                byte* dataPtr = basePtr + offset;
                ZResult result = new ZResult(ZenohNative.z_bytes_from_buf(
                    value.nativePtr,
                    dataPtr,
                    (nuint)length,
                    ManagedBufDeleter,
                    GCHandle.ToIntPtr(handle).ToPointer()));

                if (!result.IsOk)
                {
                    // z_bytes_from_buf did not take ownership; free pin ourselves.
                    handle.Free();
                    value.Dispose();
                    value = null;
                }
                else
                {
                    value.initialized = true;
                }

                return result;
            }
            catch
            {
                if (handle.IsAllocated)
                    handle.Free();
                value?.Dispose();
                value = null;
                throw;
            }
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_bytes_from_buf_deleter_delegate))]
        private static void OnManagedBufFreed(void* data, void* context)
        {
            if (context == null)
                return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}

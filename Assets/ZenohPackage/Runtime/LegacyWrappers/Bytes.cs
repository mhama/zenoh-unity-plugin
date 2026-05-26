using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Wrapper for z_owned_bytes_t native type
    public unsafe class Bytes : IDisposable
    {
        private z_owned_bytes_t* nativePtr;
        private bool disposed = false;

        public Bytes()
        {
            // Allocate memory for the native bytes
            nativePtr = (z_owned_bytes_t*)Marshal.AllocHGlobal(sizeof(z_owned_bytes_t));
        }

        // Create from string
        public Bytes(string text) : this()
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            z_result_t result = ZenohNative.z_bytes_copy_from_str(nativePtr, text);
            if (result != z_result_t.Z_OK)
            {
                Dispose();
                throw new Exception("Failed to create bytes from string");
            }
        }

        // Create from byte array
        public Bytes(byte[] buffer) : this()
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length == 0)
            {
                // For empty arrays, just create an empty bytes object
                ZenohNative.z_bytes_empty(nativePtr);
                return;
            }

            fixed(byte* bufferPtr = &buffer[0])
            {
                // create from buffer
                z_result_t result = ZenohNative.z_bytes_copy_from_buf(
                    nativePtr,
                    (byte*)bufferPtr,
                    (nuint)buffer.Length
                );
                if (result != z_result_t.Z_OK)
                {
                    // Clean up resources on error
                    Dispose();
                    throw new Exception("Failed to create bytes from buffer");
                }
            }
        }

        public BytesRef Loan()
        {
            return new BytesRef(ZenohNative.z_bytes_loan(nativePtr));
        }

        // Convert to byte array
        public byte[] ToByteArray()
        {
            z_owned_slice_t slice = new z_owned_slice_t();
            ZenohNative.z_bytes_to_slice(Loan().NativePointer, &slice);
            
            z_loaned_slice_t* loanedSlice = ZenohNative.z_slice_loan(&slice);
            byte* buf = ZenohNative.z_slice_data(loanedSlice);
            long len = (long)ZenohNative.z_slice_len(loanedSlice);
            
            byte[] result = new byte[len];
            if (len > 0)
            {
                Marshal.Copy((IntPtr)buf, result, 0, (int)len);
            }
            
            ZenohNative.z_slice_drop((z_moved_slice_t*)&slice);
            
            return result;
        }

        // Convert to string
        public override string ToString()
        {
            byte[] bytes = ToByteArray();
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        ~Bytes()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (nativePtr != null)
                {
                    ZenohNative.z_bytes_drop((z_moved_bytes_t*)nativePtr);
                    Marshal.FreeHGlobal((IntPtr)nativePtr);
                    nativePtr = null;
                }
                disposed = true;
            }
        }

        // Expose native pointer if needed for advanced operations
        internal z_owned_bytes_t* NativePointer => nativePtr;
    }

    public unsafe class BytesRef
    {
        private readonly z_loaned_bytes_t* nativePtr;

        internal BytesRef(z_loaned_bytes_t* keyExpr)
        {
            nativePtr = keyExpr;
        }

        internal z_loaned_bytes_t* NativePointer => nativePtr;

        public unsafe byte[] ToByteArray()
        {
            // Convert bytes to slice
            z_owned_slice_t slice = new z_owned_slice_t();
            ZenohNative.z_bytes_to_slice(nativePtr, &slice);
            
            // Loan the slice to access its data
            var loanedSlice = ZenohNative.z_slice_loan(&slice);
            
            // Get the data pointer and length
            byte* buf = ZenohNative.z_slice_data(loanedSlice);
            long len = (long)ZenohNative.z_slice_len(loanedSlice);
            
            // Create a new byte array and copy the data
            byte[] result = new byte[len];
            if (len > 0)
            {
                Marshal.Copy((IntPtr)buf, result, 0, (int)len);
            }
            
            // Clean up the slice
            ZenohNative.z_slice_drop((z_moved_slice_t*)&slice);
            
            return result;
        }
    }
}
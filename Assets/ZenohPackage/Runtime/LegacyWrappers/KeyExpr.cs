using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Wrapper for z_owned_keyexpr_t native type
    public unsafe class KeyExpr : IDisposable
    {
        private z_owned_keyexpr_t* nativePtr;

        public KeyExpr(string keyExprStr)
        {
            // Allocate memory for the native key expression
            nativePtr = (z_owned_keyexpr_t*)Marshal.AllocHGlobal(sizeof(z_owned_keyexpr_t));
            
            z_result_t result = ZenohNative.z_keyexpr_from_str(nativePtr, keyExprStr);
            if(result != z_result_t.Z_OK)
            {
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
                throw new Exception("Failed to create key expression");
            }
        }

        public KeyExprRef Loan()
        {
            return new KeyExprRef(ZenohNative.z_keyexpr_loan(nativePtr));
        }

        ~KeyExpr()
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
            if (nativePtr != null)
            {
                ZenohNative.z_keyexpr_drop((z_moved_keyexpr_t*)nativePtr);
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }
        }

        override public string ToString()
        {
            return Loan().ToString();
        }

        // Expose native pointer if needed for advanced operations
        internal z_owned_keyexpr_t* NativePointer => nativePtr;
    }

    public unsafe class KeyExprRef
    {
        private readonly z_loaned_keyexpr_t* nativePtr;

        internal KeyExprRef(z_loaned_keyexpr_t* keyExpr)
        {
            nativePtr = keyExpr;
        }

        override public string ToString()
        {
            z_view_string_t viewString = new z_view_string_t();
            ZenohNative.z_keyexpr_as_view_string(nativePtr, &viewString);
            z_loaned_string_t* loanedString = ZenohNative.z_view_string_loan(&viewString);
            return Marshal.PtrToStringAnsi((IntPtr)ZenohNative.z_string_data(loanedString));
        }

        internal z_loaned_keyexpr_t* NativePointer => nativePtr;
    }
}
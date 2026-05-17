using System;
using System.Runtime.InteropServices;

namespace Zenoh.Bindings.Internal
{
    internal static unsafe class NativeMemory
    {
        public static T* Alloc<T>() where T : unmanaged
        {
            return (T*)Marshal.AllocHGlobal(sizeof(T));
        }

        public static void Free<T>(T* ptr) where T : unmanaged
        {
            if (ptr != null)
                Marshal.FreeHGlobal((IntPtr)ptr);
        }
    }

    internal sealed unsafe class OwnedStorage<T> : IDisposable where T : unmanaged
    {
        private T* pointer;

        public T* Pointer => pointer;
        public bool IsValid => pointer != null;

        internal OwnedStorage(T* pointer)
        {
            this.pointer = pointer;
        }

        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        ~OwnedStorage()
        {
            Release();
        }

        private void Release()
        {
            if (pointer == null)
                return;

            NativeMemory.Free(pointer);
            pointer = null;
        }
    }
}

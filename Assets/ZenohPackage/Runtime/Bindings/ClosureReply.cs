using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public delegate void ZClosureReplyCall(ZLoanedReply reply, IntPtr context);
    public delegate void ZClosureReplyDrop(IntPtr context);

    public unsafe sealed class ZOwnedClosureReply : IDisposable
    {
        private static readonly ZenohNative.z_closure_reply_call_delegate NativeCall = DispatchCall;
        private static readonly ZenohNative.z_closure_reply_drop_delegate NativeDrop = DispatchDrop;

        private z_owned_closure_reply_t* nativePtr;
        private bool disposed;
        private bool moved;
        private GCHandle stateHandle;

        public ZOwnedClosureReply()
        {
            nativePtr = NativeStorage.Alloc<z_owned_closure_reply_t>();
            ZenohNative.z_internal_closure_reply_null(nativePtr);
        }

        public void Set(ZClosureReplyCall call, ZClosureReplyDrop drop, IntPtr context)
        {
            ThrowIfDisposedOrMoved();
            if (stateHandle.IsAllocated)
                stateHandle.Free();

            ClosureState state = new ClosureState(call, drop, context);
            stateHandle = GCHandle.Alloc(state);
            ZenohNative.z_closure_reply(nativePtr, NativeCall, NativeDrop, GCHandle.ToIntPtr(stateHandle).ToPointer());
        }

        public ZLoanedClosureReply Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureReply(ZenohNative.z_closure_reply_loan(nativePtr), this);
        }

        public ZLoanedClosureReply LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureReply(ZenohNative.z_closure_reply_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_closure_reply_check(nativePtr);
        }

        internal OwnedStorage<z_owned_closure_reply_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_closure_reply_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_closure_reply_t>(ptr);
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_reply_call_delegate))]
        private static void DispatchCall(z_loaned_reply_t* reply, void* context)
        {
            if (context == null)
                return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            if (!(handle.Target is ClosureState state) || state.Call == null)
                return;

            try
            {
                state.Call(new ZLoanedReply(reply, state), state.Context);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_reply_drop_delegate))]
        private static void DispatchDrop(void* context)
        {
            if (context == null)
                return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            if (handle.Target is ClosureState state)
                state.Drop?.Invoke(state.Context);

            handle.Free();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedClosureReply));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedClosureReply));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_closure_reply_check(nativePtr))
                    ZenohNative.z_closure_reply_drop((z_moved_closure_reply_t*)nativePtr);
                else if (!moved && stateHandle.IsAllocated)
                    stateHandle.Free();

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedClosureReply()
        {
            Dispose();
        }

        private sealed class ClosureState
        {
            public readonly ZClosureReplyCall Call;
            public readonly ZClosureReplyDrop Drop;
            public readonly IntPtr Context;

            public ClosureState(ZClosureReplyCall call, ZClosureReplyDrop drop, IntPtr context)
            {
                Call = call;
                Drop = drop;
                Context = context;
            }
        }
    }

    public unsafe sealed class ZLoanedClosureReply
    {
        private readonly ZOwnedClosureReply owner;
        internal readonly z_loaned_closure_reply_t* NativePointer;

        internal ZLoanedClosureReply(z_loaned_closure_reply_t* nativePtr, ZOwnedClosureReply owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public void Call(ZLoanedReply reply)
        {
            if (reply == null)
                throw new ArgumentNullException(nameof(reply));

            ZenohNative.z_closure_reply_call(NativePointer, reply.NativePointer);
        }
    }
}

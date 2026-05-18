using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public delegate void ZClosureQueryCall(ZLoanedQuery query, IntPtr context);
    public delegate void ZClosureQueryDrop(IntPtr context);

    public unsafe sealed class ZOwnedClosureQuery : IDisposable
    {
        private static readonly ZenohNative.z_closure_query_call_delegate NativeCall = DispatchCall;
        private static readonly ZenohNative.z_closure_query_drop_delegate NativeDrop = DispatchDrop;

        private z_owned_closure_query_t* nativePtr;
        private bool disposed;
        private bool moved;
        private GCHandle stateHandle;

        public ZOwnedClosureQuery()
        {
            nativePtr = NativeStorage.Alloc<z_owned_closure_query_t>();
            ZenohNative.z_internal_closure_query_null(nativePtr);
        }

        public void Set(ZClosureQueryCall call, ZClosureQueryDrop drop, IntPtr context)
        {
            ThrowIfDisposedOrMoved();
            if (stateHandle.IsAllocated)
                stateHandle.Free();

            ClosureState state = new ClosureState(call, drop, context);
            stateHandle = GCHandle.Alloc(state);
            ZenohNative.z_closure_query(nativePtr, NativeCall, NativeDrop, GCHandle.ToIntPtr(stateHandle).ToPointer());
        }

        public ZLoanedClosureQuery Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureQuery(ZenohNative.z_closure_query_loan(nativePtr), this);
        }

        public ZLoanedClosureQuery LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureQuery(ZenohNative.z_closure_query_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_closure_query_check(nativePtr);
        }

        internal OwnedStorage<z_owned_closure_query_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_closure_query_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_closure_query_t>(ptr);
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_query_call_delegate))]
        private static void DispatchCall(z_loaned_query_t* query, void* context)
        {
            if (context == null)
                return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            if (!(handle.Target is ClosureState state) || state.Call == null)
                return;

            try
            {
                state.Call(new ZLoanedQuery(query, state), state.Context);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_query_drop_delegate))]
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
                throw new ObjectDisposedException(nameof(ZOwnedClosureQuery));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedClosureQuery));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_closure_query_check(nativePtr))
                    ZenohNative.z_closure_query_drop((z_moved_closure_query_t*)nativePtr);
                else if (!moved && stateHandle.IsAllocated)
                    stateHandle.Free();

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedClosureQuery()
        {
            Dispose();
        }

        private sealed class ClosureState
        {
            public readonly ZClosureQueryCall Call;
            public readonly ZClosureQueryDrop Drop;
            public readonly IntPtr Context;

            public ClosureState(ZClosureQueryCall call, ZClosureQueryDrop drop, IntPtr context)
            {
                Call = call;
                Drop = drop;
                Context = context;
            }
        }
    }

    public unsafe sealed class ZLoanedClosureQuery
    {
        private readonly ZOwnedClosureQuery owner;
        internal readonly z_loaned_closure_query_t* NativePointer;

        internal ZLoanedClosureQuery(z_loaned_closure_query_t* nativePtr, ZOwnedClosureQuery owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public void Call(ZLoanedQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            ZenohNative.z_closure_query_call(NativePointer, query.NativePointer);
        }
    }
}

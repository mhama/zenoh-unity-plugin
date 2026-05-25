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

        /// <summary>
        /// Dispatches a native query callback to the managed delegate.
        ///
        /// UNSAFE CALLBACK LIFETIME: The <see cref="ZLoanedQuery"/> passed to
        /// the user callback is valid ONLY during this call. Storing it and
        /// using it after the callback returns causes a native crash
        /// (use-after-free). To retain, call <see cref="ZOwnedQuery.CreateClone"/>
        /// inside the callback.
        /// </summary>
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

    public unsafe sealed class ZQueryableOptions : IDisposable
    {
        internal z_queryable_options_t Native;
        private bool disposed;

        public static ZQueryableOptions Default()
        {
            ZQueryableOptions options = new ZQueryableOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            fixed (z_queryable_options_t* ptr = &Native)
            {
                ZenohNative.z_queryable_options_default(ptr);
            }
        }

        public void SetComplete(bool value)
        {
            ThrowIfDisposed();
            Native.complete = value;
        }

        public void SetAllowedOrigin(ZLocality value)
        {
            ThrowIfDisposed();
            Native.allowed_origin = (z_locality_t)value;
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZQueryableOptions));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
        }
    }

    public unsafe sealed class ZOwnedQueryable : IDisposable
    {
        private z_owned_queryable_t* nativePtr;
        private bool disposed;
        private bool moved;

        public ZOwnedQueryable()
        {
            nativePtr = NativeStorage.Alloc<z_owned_queryable_t>();
            ZenohNative.z_internal_queryable_null(nativePtr);
        }

        public ZResult Declare(
            ZLoanedSession session,
            ZLoanedKeyExpr keyExpr,
            ZOwnedClosureQuery movingCallback,
            ZQueryableOptions options)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (movingCallback == null)
                throw new ArgumentNullException(nameof(movingCallback));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            ThrowIfDisposedOrMoved();

            using (OwnedStorage<z_owned_closure_query_t> callbackStorage = movingCallback.Take())
            {
                fixed (z_queryable_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_declare_queryable(
                        session.NativePointer,
                        nativePtr,
                        keyExpr.NativePointer,
                        (z_moved_closure_query_t*)callbackStorage.Pointer,
                        optionsPtr));
                }
            }
        }

        public ZLoanedQueryable Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedQueryable(ZenohNative.z_queryable_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_queryable_check(nativePtr);
        }

        internal OwnedStorage<z_owned_queryable_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_queryable_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_queryable_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedQueryable));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedQueryable));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_queryable_check(nativePtr))
                    ZenohNative.z_queryable_drop((z_moved_queryable_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedQueryable()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedQueryable
    {
        private readonly ZOwnedQueryable owner;
        internal readonly z_loaned_queryable_t* NativePointer;

        internal ZLoanedQueryable(z_loaned_queryable_t* nativePtr, ZOwnedQueryable owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedKeyExpr KeyExpr()
        {
            return new ZLoanedKeyExpr(ZenohNative.z_queryable_keyexpr(NativePointer), this);
        }

        internal z_entity_global_id_t Id()
        {
            return ZenohNative.z_queryable_id(NativePointer);
        }
    }
}

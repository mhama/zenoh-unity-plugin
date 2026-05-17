using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public delegate void ZClosureSampleCall(ZLoanedSample sample, IntPtr context);
    public delegate void ZClosureSampleDrop(IntPtr context);

    public unsafe sealed class ZSubscriberOptions
    {
        internal z_subscriber_options_t Native;

        public static ZSubscriberOptions Default()
        {
            ZSubscriberOptions options = new ZSubscriberOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            fixed (z_subscriber_options_t* ptr = &Native)
            {
                ZenohNative.z_subscriber_options_default(ptr);
            }
        }
    }

    public unsafe sealed class ZOwnedClosureSample : IDisposable
    {
        private static readonly ZenohNative.z_closure_sample_call_delegate NativeCall = DispatchCall;
        private static readonly ZenohNative.z_closure_sample_drop_delegate NativeDrop = DispatchDrop;

        private z_owned_closure_sample_t* nativePtr;
        private bool disposed;
        private bool moved;
        private GCHandle stateHandle;

        public ZOwnedClosureSample()
        {
            nativePtr = (z_owned_closure_sample_t*)Marshal.AllocHGlobal(sizeof(z_owned_closure_sample_t));
            ZenohNative.z_internal_closure_sample_null(nativePtr);
        }

        public void Set(ZClosureSampleCall call, ZClosureSampleDrop drop, IntPtr context)
        {
            ThrowIfDisposedOrMoved();
            // Replacing an unconsumed closure releases the managed state without
            // invoking the previous native drop callback.
            if (stateHandle.IsAllocated)
                stateHandle.Free();

            ClosureState state = new ClosureState(call, drop, context);
            stateHandle = GCHandle.Alloc(state);
            ZenohNative.z_closure_sample(nativePtr, NativeCall, NativeDrop, GCHandle.ToIntPtr(stateHandle).ToPointer());
        }

        public ZLoanedClosureSample Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureSample(ZenohNative.z_closure_sample_loan(nativePtr), this);
        }

        public ZLoanedClosureSample LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedClosureSample(ZenohNative.z_closure_sample_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_closure_sample_check(nativePtr);
        }

        internal z_owned_closure_sample_t* MoveToNative()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            return nativePtr;
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_sample_call_delegate))]
        private static void DispatchCall(z_loaned_sample_t* sample, void* context)
        {
            if (context == null)
                return;

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            if (!(handle.Target is ClosureState state) || state.Call == null)
                return;

            try
            {
                state.Call(new ZLoanedSample(sample, state), state.Context);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_sample_drop_delegate))]
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
                throw new ObjectDisposedException(nameof(ZOwnedClosureSample));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedClosureSample));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_closure_sample_check(nativePtr))
                    ZenohNative.z_closure_sample_drop((z_moved_closure_sample_t*)nativePtr);
                // If native storage was invalidated before it was moved, release
                // any managed state that native drop can no longer reach.
                else if (!moved && stateHandle.IsAllocated)
                    stateHandle.Free();

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedClosureSample()
        {
            Dispose();
        }

        private sealed class ClosureState
        {
            public readonly ZClosureSampleCall Call;
            public readonly ZClosureSampleDrop Drop;
            public readonly IntPtr Context;

            public ClosureState(ZClosureSampleCall call, ZClosureSampleDrop drop, IntPtr context)
            {
                Call = call;
                Drop = drop;
                Context = context;
            }
        }
    }

    public unsafe sealed class ZLoanedClosureSample
    {
        private readonly ZOwnedClosureSample owner;
        internal readonly z_loaned_closure_sample_t* NativePointer;

        internal ZLoanedClosureSample(z_loaned_closure_sample_t* nativePtr, ZOwnedClosureSample owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public void Call(ZLoanedSample sample)
        {
            if (sample == null)
                throw new ArgumentNullException(nameof(sample));

            ZenohNative.z_closure_sample_call(NativePointer, sample.NativePointer);
        }
    }

    public unsafe sealed class ZOwnedSubscriber : IDisposable
    {
        private z_owned_subscriber_t* nativePtr;
        private bool disposed;

        public ZOwnedSubscriber()
        {
            nativePtr = (z_owned_subscriber_t*)Marshal.AllocHGlobal(sizeof(z_owned_subscriber_t));
            ZenohNative.z_internal_subscriber_null(nativePtr);
        }

        public ZResult Declare(
            ZLoanedSession session,
            ZLoanedKeyExpr keyExpr,
            ZOwnedClosureSample callback,
            ZSubscriberOptions options)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            ThrowIfDisposedOrMoved();

            fixed (z_subscriber_options_t* optionsPtr = &options.Native)
            {
                ZResult result = new ZResult(ZenohNative.z_declare_subscriber(
                    session.NativePointer,
                    nativePtr,
                    keyExpr.NativePointer,
                    (z_moved_closure_sample_t*)callback.MoveToNative(),
                    optionsPtr));
                callback.Dispose();
                return result;
            }
        }

        public ZLoanedSubscriber Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedSubscriber(ZenohNative.z_subscriber_loan(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposed();
            return ZenohNative.z_internal_subscriber_check(nativePtr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedSubscriber));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (ZenohNative.z_internal_subscriber_check(nativePtr))
                    ZenohNative.z_subscriber_drop((z_moved_subscriber_t*)nativePtr);

                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedSubscriber()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedSubscriber
    {
        private readonly ZOwnedSubscriber owner;
        internal readonly z_loaned_subscriber_t* NativePointer;

        internal ZLoanedSubscriber(z_loaned_subscriber_t* nativePtr, ZOwnedSubscriber owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedKeyExpr KeyExpr()
        {
            return new ZLoanedKeyExpr(ZenohNative.z_subscriber_keyexpr(NativePointer), this);
        }
    }
}

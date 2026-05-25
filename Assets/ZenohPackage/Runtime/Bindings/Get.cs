using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    public enum ZQueryTarget : uint
    {
        BestMatching = 0,
        All = 1,
        AllComplete = 2,
    }

    public enum ZConsolidationMode : int
    {
        Auto = -1,
        None = 0,
        Monotonic = 1,
        Latest = 2,
    }

    public readonly unsafe struct ZQueryConsolidation
    {
        private readonly z_query_consolidation_t native;

        internal ZQueryConsolidation(z_query_consolidation_t native) { this.native = native; }
        internal z_query_consolidation_t Native => native;

        public ZConsolidationMode Mode => (ZConsolidationMode)native.mode;

        public static ZQueryConsolidation Default() => new ZQueryConsolidation(ZenohNative.z_query_consolidation_default());
        public static ZQueryConsolidation Auto() => new ZQueryConsolidation(ZenohNative.z_query_consolidation_auto());
        public static ZQueryConsolidation Latest() => new ZQueryConsolidation(ZenohNative.z_query_consolidation_latest());
        public static ZQueryConsolidation Monotonic() => new ZQueryConsolidation(ZenohNative.z_query_consolidation_monotonic());
        public static ZQueryConsolidation None() => new ZQueryConsolidation(ZenohNative.z_query_consolidation_none());
    }

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

        /// <summary>
        /// Dispatches a native reply callback to the managed delegate.
        ///
        /// UNSAFE CALLBACK LIFETIME: The <see cref="ZLoanedReply"/> passed to
        /// the user callback is valid ONLY during this call. Storing it and
        /// using it after the callback returns causes a native crash
        /// (use-after-free). To retain, call <see cref="ZOwnedReply.CreateClone"/>
        /// inside the callback.
        /// </summary>
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

    public unsafe sealed class ZGetOptions : IDisposable
    {
        internal z_get_options_t Native;
        private OwnedStorage<z_owned_bytes_t> adoptedPayload;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private OwnedStorage<z_owned_bytes_t> adoptedAttachment;
        private OwnedStorage<z_owned_cancellation_token_t> adoptedCancellationToken;
        private bool disposed;

        public static ZGetOptions Default()
        {
            ZGetOptions options = new ZGetOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_get_options_t* ptr = &Native)
            {
                ZenohNative.z_get_options_default(ptr);
            }
        }

        public void SetTarget(ZQueryTarget target)
        {
            ThrowIfDisposed();
            Native.target = (z_query_target_t)target;
        }

        public void SetConsolidation(ZQueryConsolidation consolidation)
        {
            ThrowIfDisposed();
            Native.consolidation = consolidation.Native;
        }

        public void SetPayload(ZOwnedBytes movingPayload)
        {
            if (movingPayload == null)
                throw new ArgumentNullException(nameof(movingPayload));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingPayload.Take();
            ReleasePayload();
            adoptedPayload = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->payload = (z_moved_bytes_t*)adoptedPayload.Pointer;
            }
        }

        public void SetEncoding(ZOwnedEncoding movingEncoding)
        {
            if (movingEncoding == null)
                throw new ArgumentNullException(nameof(movingEncoding));
            ThrowIfDisposed();

            OwnedStorage<z_owned_encoding_t> storage = movingEncoding.Take();
            ReleaseEncoding();
            adoptedEncoding = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        public void SetEncoding(ZLoanedEncoding loanedEncoding)
        {
            if (loanedEncoding == null)
                throw new ArgumentNullException(nameof(loanedEncoding));
            ThrowIfDisposed();

            SetEncoding(ZOwnedEncoding.CreateClone(loanedEncoding));
        }

        public void SetCongestionControl(ZCongestionControl value)
        {
            ThrowIfDisposed();
            Native.congestion_control = (z_congestion_control_t)value;
        }

        public void SetIsExpress(bool value)
        {
            ThrowIfDisposed();
            Native.is_express = value;
        }

        public void SetAllowedDestination(ZLocality value)
        {
            ThrowIfDisposed();
            Native.allowed_destination = (z_locality_t)value;
        }

        public void SetAcceptReplies(ZReplyKeyExpr value)
        {
            ThrowIfDisposed();
            Native.accept_replies = (z_reply_keyexpr_t)value;
        }

        public void SetPriority(ZPriority value)
        {
            ThrowIfDisposed();
            Native.priority = (z_priority_t)value;
        }

        /// <summary>
        /// Sets the source info for the query.
        ///
        /// UNSAFE: Stores a borrowed pointer without taking ownership.
        /// The caller must keep the source alive until the native call completes;
        /// disposing the source beforehand causes a native crash (dangling pointer).
        /// </summary>
        public void SetSourceInfo(ZLoanedSourceInfo sourceInfo)
        {
            if (sourceInfo == null)
                throw new ArgumentNullException(nameof(sourceInfo));
            ThrowIfDisposed();

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->source_info = sourceInfo.NativePointer;
            }
        }

        public void SetAttachment(ZOwnedBytes movingAttachment)
        {
            if (movingAttachment == null)
                throw new ArgumentNullException(nameof(movingAttachment));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingAttachment.Take();
            ReleaseAttachment();
            adoptedAttachment = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)adoptedAttachment.Pointer;
            }
        }

        public void SetTimeoutMs(ulong timeoutMs)
        {
            ThrowIfDisposed();
            Native.timeout_ms = timeoutMs;
        }

        public void SetCancellationToken(ZOwnedCancellationToken movingCancellationToken)
        {
            if (movingCancellationToken == null)
                throw new ArgumentNullException(nameof(movingCancellationToken));
            ThrowIfDisposed();

            OwnedStorage<z_owned_cancellation_token_t> storage = movingCancellationToken.Take();
            ReleaseCancellationToken();
            adoptedCancellationToken = storage;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->cancellation_token = (z_moved_cancellation_token_t*)adoptedCancellationToken.Pointer;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZGetOptions));
        }

        private void ReleasePayload()
        {
            if (adoptedPayload == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedPayload.Pointer);
            adoptedPayload.Dispose();
            adoptedPayload = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->payload = null;
            }
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->encoding = null;
            }
        }

        private void ReleaseAttachment()
        {
            if (adoptedAttachment == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedAttachment.Pointer);
            adoptedAttachment.Dispose();
            adoptedAttachment = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->attachment = null;
            }
        }

        private void ReleaseCancellationToken()
        {
            if (adoptedCancellationToken == null)
                return;

            ZenohNative.z_cancellation_token_drop((z_moved_cancellation_token_t*)adoptedCancellationToken.Pointer);
            adoptedCancellationToken.Dispose();
            adoptedCancellationToken = null;

            fixed (z_get_options_t* ptr = &Native)
            {
                ptr->cancellation_token = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleasePayload();
            ReleaseEncoding();
            ReleaseAttachment();
            ReleaseCancellationToken();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

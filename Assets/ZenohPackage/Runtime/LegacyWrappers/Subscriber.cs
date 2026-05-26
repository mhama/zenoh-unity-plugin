using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;
using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Delegate for C# callback
    public delegate void SampleReceivedCallback(SampleRef sample);

    // Wrapper for z_owned_subscriber_t native type
    public unsafe class Subscriber : IDisposable
    {
        private z_owned_subscriber_t* nativePtr;
        private SampleReceivedCallback callback;
        private GCHandle callbackHandle; // To prevent garbage collection
        private ClosureSample closure; // Keep a reference to prevent garbage collection
        private static readonly ZenohNative.z_closure_sample_call_delegate StaticCallbackHandler = HandleSampleCallback;

        public Subscriber()
        {
            // Allocate memory for the native subscriber
            nativePtr = (z_owned_subscriber_t*)Marshal.AllocHGlobal(sizeof(z_owned_subscriber_t));
        }

        // Creates a subscriber using the provided Session and KeyExpr.
        // Loans of objects are handled transparently.
        public ZResult CreateSubscriber(Session session, KeyExpr keyExpr, SampleReceivedCallback callback = null)
        {
            this.callback = callback;
            
            // If we have a callback, create a GCHandle to prevent it from being garbage collected
            if (callback != null && !callbackHandle.IsAllocated)
            {
                callbackHandle = GCHandle.Alloc(this);
            }
            
            KeyExprRef loanedKeyExpr = keyExpr.Loan();
            
            // Create a closure with our static callback handler if a callback was provided
            this.closure = callback != null 
                ? ClosureSample.Create(StaticCallbackHandler, null, GCHandle.ToIntPtr(callbackHandle).ToPointer())
                : ClosureSample.CreateDefault();
            
            z_subscriber_options_t options = new z_subscriber_options_t();
            ZenohNative.z_subscriber_options_default(&options);
            
            z_result_t result = ZenohNative.z_declare_subscriber(
                session.Loan().NativePointer,
                nativePtr,
                loanedKeyExpr.NativePointer,
                (z_moved_closure_sample_t*)this.closure.NativePointer,
                &options);
            return new ZResult(result);
        }

        // Static method to handle the native callback
        [MonoPInvokeCallback(typeof(ZenohNative.z_closure_sample_call_delegate))]
        private static void HandleSampleCallback(z_loaned_sample_t* sample, void* context)
        {
            if (context == null) return;
            
            // Convert context pointer back to GCHandle and get the subscriber instance
            GCHandle handle = GCHandle.FromIntPtr((IntPtr)context);
            Subscriber subscriber = (Subscriber)handle.Target;
            
            // Call the C# callback if it exists
            if (subscriber != null && subscriber.callback != null)
            {
                SampleRef sampleRef = new SampleRef(sample);
                subscriber.callback(sampleRef);
            }
        }

        public void Dispose()
        {
            if (nativePtr != null)
            {
                ZenohNative.z_subscriber_drop((z_moved_subscriber_t*)nativePtr);
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
            }
            
            if (callbackHandle.IsAllocated)
            {
                callbackHandle.Free();
            }
            
            if (closure != null)
            {
                closure.Dispose();
                closure = null;
            }
        }

        // Expose native pointer if needed for advanced operations
        internal z_owned_subscriber_t* NativePointer => nativePtr;
    }

    // Wrapper for z_owned_closure_sample_t native type
    public unsafe class ClosureSample : IDisposable
    {
        private z_owned_closure_sample_t* nativePtr;
        private bool disposed = false;

        private ClosureSample()
        {
            // Allocate memory for the native closure sample
            nativePtr = (z_owned_closure_sample_t*)Marshal.AllocHGlobal(sizeof(z_owned_closure_sample_t));
        }

        // Create a closure sample with explicit callbacks and context.
        internal static ClosureSample Create(ZenohNative.z_closure_sample_call_delegate sampleCall, ZenohNative.z_closure_sample_drop_delegate dropCall, void* context)
        {
            ClosureSample instance = new ClosureSample();
            ZenohNative.z_closure_sample(instance.nativePtr, sampleCall, dropCall, context);
            return instance;
        }
        
        // Create a default closure sample with null callbacks.
        public static ClosureSample CreateDefault()
        {
            return Create(null, null, null);
        }

        // Expose the native closure for use in native calls.
        internal z_owned_closure_sample_t* NativePointer => nativePtr;

        ~ClosureSample()
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
            if (!disposed && nativePtr != null)
            {
                ZenohNative.z_closure_sample_drop((z_moved_closure_sample_t*)nativePtr);
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
                disposed = true;
            }
        }
    }
}
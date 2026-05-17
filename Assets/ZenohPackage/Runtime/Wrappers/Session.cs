using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Wrapper for z_owned_session_t* native type
    public unsafe class Session : IDisposable
    {
        private z_owned_session_t* nativePtr;
        private bool disposed = false;

        public Session()
        {
            // Allocate memory for the native session
            nativePtr = (z_owned_session_t*)Marshal.AllocHGlobal(sizeof(z_owned_session_t));
        }

        public ZResult Open(string conf)
        {
            Debug.Log("Starting Zenoh open session test...");
            
            try
            {
                // 1. まずConfigurationを作成（所有権を持つ）
                z_owned_config_t ownedConfig = new z_owned_config_t();
                z_result_t configResult = ZenohNative.z_config_default(&ownedConfig);
                if (configResult != z_result_t.Z_OK)
                {
                    Debug.LogError("Failed to create Zenoh config");
                    return new ZResult(configResult);
                }
                
                // 3. OpenOptionsを準備
                z_open_options_t openOptions = new z_open_options_t();
                ZenohNative.z_open_options_default(&openOptions);
                
                // 4. 設定を使用してセッションを開く
                if (conf == null)
                {
                    ZenohNative.z_config_default(&ownedConfig);
                }
                else 
                {
                    ZenohNative.zc_config_from_str(&ownedConfig, conf);
                }
                
                configResult = ZenohNative.z_open(nativePtr, (z_moved_config_t *)&ownedConfig, &openOptions);

                if (configResult != z_result_t.Z_OK)
                {
                    Debug.LogError("Failed to open Zenoh session");
                    return new ZResult(configResult);
                }
                
                Debug.Log("Zenoh session opened successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in Zenoh open test: {ex.Message}\n{ex.StackTrace}");
            }

            return ZResult.Ok;
        }

        public ZResult Close()
        {
            var result = Loan().Close();
            // drop
            ZenohNative.z_session_drop((z_moved_session_t*)nativePtr);
            return result;
        }

        public SessionRef Loan()
        {
            return new SessionRef(ZenohNative.z_session_loan(nativePtr));
        }

        ~Session()
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
                // Free the memory allocated for the pointer
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
                disposed = true;
            }
        }

        // Expose native pointer if needed for advanced operations
        internal z_owned_session_t* NativePointer => nativePtr;
    }


    public unsafe class SessionRef
    {
        private readonly z_loaned_session_t* nativePtr;

        internal SessionRef(z_loaned_session_t* keyExpr)
        {
            nativePtr = keyExpr;
        }

        internal z_loaned_session_t* NativePointer => nativePtr;

        public ZResult Close()
        {
            z_close_options_t closeOptions = new z_close_options_t();
            ZenohNative.z_close_options_default(&closeOptions);
            
            // close the session
            z_result_t closeResult = ZenohNative.z_close(nativePtr, &closeOptions);
            return new ZResult(closeResult);
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using Zenoh.Legacy;

public class CameraReceiverTest : MonoBehaviour
{
    private Session session;
    private Subscriber subscriber;
    private KeyExpr keyExpr;
    private bool initialized = false;
    
    private byte[] managedBuffer;
    private object obj = new object();
    private Texture2D texture;
    private SynchronizationContext syncContext;

    [SerializeField]
    private string keyExprStr = "rpi/camera/image_jpeg/left";

    [SerializeField]
    private TextAsset zenohConfigText;
    
    // Reference to the renderer
    [SerializeField] 
    private Renderer targetRenderer;
    
    // Flag indicating if the texture has been updated
    private bool textureUpdated = false;
    
    void Start()
    {
        syncContext = SynchronizationContext.Current;
        // Initialize the lock object
        if (obj == null) obj = new object();
        
        // If no target renderer is set, use this object's renderer
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();
            
        // Create and open Zenoh session
        session = new Session();
        string conf = zenohConfigText == null ? null : zenohConfigText?.text;
        var result = session.Open(conf);

        if (!result.IsOk)
        {
            Debug.LogError("Failed to open session");
            return;
        }
        
        // Create subscriber
        StartCoroutine(TestSubscriber());

        texture = new Texture2D(1, 1);
    }

    void Update()
    {
        // Only update the material if the texture has been updated
        if (textureUpdated && texture != null && targetRenderer != null)
        {
            targetRenderer.material.mainTexture = texture;
            textureUpdated = false;
            Debug.Log("Material texture updated");
        }
    }

    void OnDestroy()
    {
        if (initialized)
        {
            // Close the session
            session.Close();
            initialized = false;
        }
        
        // Release resources
        if (keyExpr != null)
        {
            keyExpr.Dispose();
            keyExpr = null;
        }
        
        if (subscriber != null)
        {
            subscriber.Dispose();
            subscriber = null;
        }
        
        if (session != null)
        {
            session.Dispose();
            session = null;
        }
    }
     
    // Callback for when a sample is received
    private void OnSampleReceived(SampleRef sample)
    {
        // Get data from the sample
        byte[] data = sample.GetPayload().ToByteArray();
        string keyExpr = sample.GetKeyExprRef().ToString();
        
        Debug.Log($"received: keyexpr: {keyExpr}");
        
        // Update the managed buffer inside a lock
        lock(obj)
        {
            if (managedBuffer == null || managedBuffer.Length < data.Length)
            {
                managedBuffer = new byte[data.Length];
            }
            Array.Copy(data, managedBuffer, data.Length);
        }

        // Execute on the main thread using SynchronizationContext
        try
        {
            // Delegate to the main thread
            syncContext.Post(_ => {
                try
                {
                    // Copy JPEG data to handle it thread-safely within the callback
                    byte[] textureCopy;
                    lock(obj)
                    {
                        textureCopy = new byte[managedBuffer.Length];
                        Array.Copy(managedBuffer, textureCopy, managedBuffer.Length);
                    }
                    
                    // Load JPEG image data into the texture
                    if (texture != null && textureCopy != null && textureCopy.Length > 0)
                    {
                        texture.LoadImage(textureCopy);
                        textureUpdated = true; // Set the texture update flag
                        Debug.Log($"Texture updated: {texture.width}x{texture.height}");
                    }
                    else
                    {
                        Debug.LogWarning("Failed to update texture: texture or buffer is null/empty");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error updating texture: {e.Message}\n{e.StackTrace}");
                }
            }, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to post to main thread: {ex.Message}\n{ex.StackTrace}");
            
            // Fallback if SynchronizationContext is not available
            // (In this case, we expect to check the textureUpdated flag in Update)
            lock(obj)
            {
                textureUpdated = true;
            }
        }
    }

    private IEnumerator TestSubscriber()
    {
        CreateSubscriber(keyExprStr);
        initialized = true;
        yield return new WaitForSeconds(5.0f);
    }
    
    // Example of creating a subscriber
    private void CreateSubscriber(string keyExprStr)
    {
        keyExpr = new KeyExpr(keyExprStr);
        subscriber = new Subscriber();
        
        // Register subscriber with callback
        var result = subscriber.CreateSubscriber(session, keyExpr, OnSampleReceived);
        
        Debug.Log($"Subscriber created for key expression: {keyExprStr}");
    }
}

using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using Zenoh.HighLevel;

public class CameraReceiverHighLevelTest : MonoBehaviour
{
    private Session session;
    private Subscriber subscriber;
    private bool initialized;

    private byte[] managedBuffer;
    private readonly object bufferLock = new object();
    private Texture2D texture;
    private SynchronizationContext syncContext;

    [SerializeField]
    private string keyExprStr = "rpi/camera/image_jpeg/left";

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private Renderer targetRenderer;

    private bool textureUpdated;

    void Start()
    {
        syncContext = SynchronizationContext.Current;

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        string conf = zenohConfigText == null ? null : zenohConfigText.text;
        try
        {
            session = Session.Open(conf);
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Failed to open session: {ex.Message}");
            return;
        }

        StartCoroutine(TestSubscriber());

        texture = new Texture2D(1, 1);
    }

    void Update()
    {
        if (textureUpdated && texture != null && targetRenderer != null)
        {
            targetRenderer.material.mainTexture = texture;
            textureUpdated = false;
            Debug.Log("Material texture updated");
        }
    }

    void OnDestroy()
    {
        if (initialized && session != null)
        {
            try { session.Close(); } catch (ZenohException) { }
            initialized = false;
        }

        subscriber?.Dispose();
        subscriber = null;

        session?.Dispose();
        session = null;
    }

    private void OnSampleReceived(SampleView sample)
    {
        byte[] data = sample.PayloadAsBytes();
        Debug.Log($"received: keyexpr: {sample.KeyExpr}");

        if (data == null)
            return;

        lock (bufferLock)
        {
            if (managedBuffer == null || managedBuffer.Length < data.Length)
            {
                managedBuffer = new byte[data.Length];
            }
            Array.Copy(data, managedBuffer, data.Length);
        }

        try
        {
            syncContext.Post(_ =>
            {
                try
                {
                    byte[] textureCopy;
                    lock (bufferLock)
                    {
                        if (managedBuffer == null)
                            return;
                        textureCopy = new byte[managedBuffer.Length];
                        Array.Copy(managedBuffer, textureCopy, managedBuffer.Length);
                    }

                    if (texture != null && textureCopy.Length > 0)
                    {
                        texture.LoadImage(textureCopy);
                        textureUpdated = true;
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
            lock (bufferLock)
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

    private void CreateSubscriber(string keyExprString)
    {
        try
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                subscriber = Subscriber.Declare(session, keyExpr, OnSampleReceived);
            }
            Debug.Log($"Subscriber created for key expression: {keyExprString}");
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Failed to create subscriber: {ex.Message}");
        }
    }
}

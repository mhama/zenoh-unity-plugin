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

    private Texture2D texture;
    private SynchronizationContext syncContext;

    [SerializeField]
    private string keyExprStr = "rpi/camera/image_jpeg/left";

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private Renderer targetRenderer;

    // Holds the latest decoded frame for the renderer; written on main thread only.
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
        // Derived resources first, then the session they depend on. Disposing the
        // session ahead of the subscriber would leave us trying to drop a stopped
        // native subscriber handle.
        subscriber?.Dispose();
        subscriber = null;

        if (initialized && session != null)
        {
            try { session.Close(); } catch (ZenohException) { }
            initialized = false;
        }

        session?.Dispose();
        session = null;
    }

    private void OnSampleReceived(SampleView sample)
    {
        int payloadLength = sample.PayloadLength;
        if (payloadLength == 0)
            return;

        // One allocation + one native→managed copy per frame. Ownership of `frame`
        // transfers to the main-thread closure; no shared buffer / lock needed.
        byte[] frame = new byte[payloadLength];
        sample.PayloadCopyTo(frame, 0);

        Debug.Log($"received: keyexpr: {sample.KeyExpr} ({payloadLength} bytes)");

        try
        {
            syncContext.Post(state =>
            {
                byte[] data = (byte[])state;
                try
                {
                    if (texture == null || data.Length == 0)
                        return;

                    // LoadImage internally decodes JPEG and copies pixels into the
                    // texture; the byte[] is eligible for GC after this call.
                    texture.LoadImage(data);
                    textureUpdated = true;
                    Debug.Log($"Texture updated: {texture.width}x{texture.height}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error updating texture: {e.Message}\n{e.StackTrace}");
                }
            }, frame);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to post to main thread: {ex.Message}\n{ex.StackTrace}");
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

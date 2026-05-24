using System.Collections;
using UnityEngine;
using Zenoh.HighLevel;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Zenoh Publisher class for publishing webcam images using the HighLevel wrapper.
/// </summary>
class CameraPublisherHighLevelTest : MonoBehaviour
{
    private Session session;
    private Publisher publisher;

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprStr = "rpi/camera/image_jpeg/left";

    [SerializeField]
    private int captureWidth = 1280;

    [SerializeField]
    private int captureHeight = 720;

    [SerializeField]
    private int fps = 30;

    [SerializeField]
    private int jpegQuality = 75;

    [SerializeField]
    private Renderer renderer;

    private WebCamTexture webCamTexture;
    private Texture2D texture2D;

    void OnEnable()
    {
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

        StartCoroutine(InitializeWebcamCoroutine());
    }

    private IEnumerator InitializeWebcamCoroutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogError("Camera is not permitted.");
            yield break;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("Webcam is not permitted.");
            yield break;
        }
#endif

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam found");
            yield break;
        }

        Debug.Log($"Found {devices.Length} webcam devices");
        foreach (var device in devices)
        {
            Debug.Log($"Webcam: {device.name}");
        }

        webCamTexture = new WebCamTexture(devices[0].name, captureWidth, captureHeight, fps);
        webCamTexture.Play();

        while (!webCamTexture.isPlaying || webCamTexture.width < 100)
        {
            yield return null;
        }

        if (texture2D != null)
        {
            Destroy(texture2D);
        }
        texture2D = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

        Debug.Log($"Webcam initialized with dimensions: {webCamTexture.width}x{webCamTexture.height}");

        if (renderer != null)
        {
            renderer.material.mainTexture = webCamTexture;
        }

        StartCoroutine(TestPublisher());
    }

    private byte[] CaptureJpegFromWebcam()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            Debug.LogWarning("Webcam not initialized or not playing");
            return null;
        }

        texture2D.SetPixels32(webCamTexture.GetPixels32());
        texture2D.Apply();

        return texture2D.EncodeToJPG(jpegQuality);
    }

    void OnDisable()
    {
        Debug.Log("OnDisable called.");

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }

        if (texture2D != null)
        {
            Destroy(texture2D);
            texture2D = null;
        }

        publisher?.Dispose();
        publisher = null;

        if (session != null)
        {
            try { session.Close(); } catch (ZenohException) { }
            session.Dispose();
            session = null;
        }
    }

    private IEnumerator TestPublisher()
    {
        if (!CreatePublisher(keyExprStr))
        {
            yield break;
        }
        StartCoroutine(SendLoop());
    }

    private bool CreatePublisher(string keyExprString)
    {
        try
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                publisher = Publisher.Declare(
                    session,
                    keyExpr,
                    new PublisherDeclareOptions { DefaultEncoding = EncodingPreset.ImageJpeg });
            }
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Failed to declare publisher: {ex.Message}");
            return false;
        }

        Debug.Log($"Publisher created for key expression: {keyExprString}");
        return true;
    }

    IEnumerator SendLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(1.0f / fps);
        while (enabled)
        {
            // EncodeToJPG allocates a fresh managed byte[]. Publisher.Put pins it via
            // GCHandle and hands ownership to Zenoh (zero-copy), so the array is released
            // back to GC after Zenoh drops the payload.
            byte[] jpegData = CaptureJpegFromWebcam();

            if (jpegData != null && jpegData.Length > 0)
            {
                try
                {
                    publisher.Put(jpegData);
                    Debug.Log($"Published webcam frame: {jpegData.Length} bytes");
                }
                catch (ZenohException ex)
                {
                    Debug.LogError($"Failed to publish frame: {ex.Message}");
                }
            }

            yield return wait;
        }
    }
}

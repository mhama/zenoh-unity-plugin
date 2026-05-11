using System.Collections;
using UnityEngine;
using Zenoh;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Zenoh Publisher class for publishing webcam images.
/// </summary>
class CameraPublisherTest : MonoBehaviour
{
    private Session session;
    private Publisher publisher;
    private KeyExpr keyExpr;

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprStr = "rpi/camera/image_jpeg/left";

    // Added webcam settings
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
        // Create and open Zenoh session
        session = new Session();
        string conf = zenohConfigText == null ? null : zenohConfigText?.text;
        var result = session.Open(conf);
        if (!result.IsOk)
        {
            Debug.LogError("Failed to open session");
            return;
        }

        // Setup webcam using coroutine
        StartCoroutine(InitializeWebcamCoroutine());
    }

    private IEnumerator InitializeWebcamCoroutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            // 許可されていない場合、リクエストを送信
            Permission.RequestUserPermission(Permission.Camera);
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogError("Camera is not permitted.");
            yield break; // Coroutineなのでyield breakを使用
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            Debug.LogError("Webcam is not permitted.");
            yield break; // Coroutineなのでyield breakを使用
        }
#endif

        // Get available webcam devices
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam found");
            yield break; // Coroutineなのでyield breakを使用
        }

        // Log available webcams
        Debug.Log($"Found {devices.Length} webcam devices");
        foreach (var device in devices)
        {
            Debug.Log($"Webcam: {device.name}");
        }

        // Create webcam texture using first available camera
        webCamTexture = new WebCamTexture(devices[0].name, captureWidth, captureHeight, fps);
        webCamTexture.Play();

        // Wait for the webcam texture to be ready
        while (!webCamTexture.isPlaying || webCamTexture.width < 100)
        {
            yield return null;
        }

        // Create texture2D for frame processing using actual webcam dimensions
        if (texture2D != null)
        {
            Destroy(texture2D);
        }
        texture2D = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

        Debug.Log($"Webcam initialized with dimensions: {webCamTexture.width}x{webCamTexture.height}");

        if (renderer != null) {
            renderer.material.mainTexture = webCamTexture; // Set the webcam texture to the renderer's material
        }

        // Create publisher
        StartCoroutine(TestPublisher());
    }

    private byte[] CaptureJpegFromWebcam()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            Debug.LogWarning("Webcam not initialized or not playing");
            return null;
        }

        // Create texture and apply webcam image
        texture2D.SetPixels32(webCamTexture.GetPixels32());
        texture2D.Apply();

        // Convert to JPEG byte array
        byte[] jpegBytes = texture2D.EncodeToJPG(jpegQuality);
        return jpegBytes;
    }

    void OnDisable()
    {
        Debug.Log("OnDisable called.");

        // Stop and release webcam
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

        // Release resources
        if (keyExpr != null)
        {
            keyExpr.Dispose();
            keyExpr = null;
        }

        if (publisher != null)
        {
            publisher.Dispose();
            publisher = null;
        }

        if (session != null)
        {
            session.Close();
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

    // Example of creating a publisher
    private bool CreatePublisher(string keyExprStr)
    {
        keyExpr = new KeyExpr(keyExprStr);
        publisher = new Publisher();

        // Register publisher
        var result = publisher.Declare(session, keyExpr);
        if (!result.IsOk)
        {
            Debug.LogError($"Failed to declare publisher: {result}");
            return false;
        }
        Debug.Log($"Publisher created for key expression: {keyExprStr}");
        return true;
    }

    IEnumerator SendLoop()
    {
        while (enabled)
        {
            // Capture JPEG from webcam and publish it
            byte[] jpegData = CaptureJpegFromWebcam();

            if (jpegData != null && jpegData.Length > 0)
            {
                using (Encoding encoding = Encoding.ImageJpeg())
                using (PublisherPutOptions options = new PublisherPutOptions())
                using (Bytes bytes = new Bytes(jpegData))
                {
                    options.SetMovedEncoding(encoding);
                    publisher.Put(bytes, options);
                }
                Debug.Log($"Published webcam frame: {jpegData.Length} bytes");
            }

            yield return new WaitForSeconds(1.0f / fps);
        }
    }
}

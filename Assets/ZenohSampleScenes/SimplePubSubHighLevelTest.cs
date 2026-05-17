using System;
using System.Collections;
using UnityEngine;
using Zenoh.HighLevel;

public class SimplePubSubHighLevelTest : MonoBehaviour
{
    private Session session;
    private Subscriber subscriber;
    private Publisher publisher;
    private bool initialized;

    [SerializeField]
    private TextAsset zenohConfigText;

    void Start()
    {
        string conf = zenohConfigText == null ? null : zenohConfigText.text;
        try
        {
            session = Session.Open(conf);
            initialized = true;
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Failed to open session: {ex.Message}");
            return;
        }

        const string keyExpr = "myhome/kitchen/temp";
        StartCoroutine(TestPublisher(keyExpr));
        StartCoroutine(TestSubscriber(keyExpr));
    }

    void OnDestroy()
    {
        if (!initialized)
            return;

        publisher?.Dispose();
        publisher = null;

        subscriber?.Dispose();
        subscriber = null;

        try { session?.Close(); } catch (ZenohException) { }
        session?.Dispose();
        session = null;
    }

    IEnumerator TestPublisher(string keyExprString)
    {
        if (!StartPublisher(keyExprString))
            yield break;

        for (int i = 0; i < 100; i++)
        {
            PublishMessage(i, keyExprString);
            yield return new WaitForSeconds(0.1f);
        }
    }

    bool StartPublisher(string keyExprString)
    {
        Debug.Log("Starting Zenoh publisher test (HighLevel)...");

        try
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                publisher = Publisher.Declare(session, keyExpr);
            }
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception declaring publisher: {ex.Message}");
            return false;
        }

        Debug.Log($"Publisher declared on '{keyExprString}'");
        return true;
    }

    void PublishMessage(int idx, string keyExprString)
    {
        try
        {
            string message = $"[{idx:D4}] Unity Zenoh HighLevel Message";
            Debug.Log($"Putting Data ('{keyExprString}': '{message}')...");
            publisher.Put(message, EncodingPreset.TextPlain);
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception publishing: {ex.Message}");
        }
    }

    IEnumerator TestSubscriber(string keyExprString)
    {
        CreateSubscriber(keyExprString);
        yield return new WaitForSeconds(5.0f);
    }

    void CreateSubscriber(string keyExprString)
    {
        try
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                subscriber = Subscriber.Declare(session, keyExpr, HandleSample);
            }
            Debug.Log($"Subscriber created for '{keyExprString}'");
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception creating subscriber: {ex.Message}");
        }
    }

    void HandleSample(SampleView sample)
    {
        Debug.Log($"Received: keyexpr: {sample.KeyExpr}");
        Debug.Log($"Payload: {sample.PayloadAsString()}");
    }
}

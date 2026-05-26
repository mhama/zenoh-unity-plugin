using System.Collections;
using System.Threading;
using UnityEngine;
using Zenoh.HighLevel;

public class QueryGetHighLevelTest : MonoBehaviour
{
    private Session session;
    private bool initialized;

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprString = "demo/query";

    [SerializeField]
    private float queryInterval = 2.0f;

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

        StartCoroutine(QueryLoop());
    }

    void OnDestroy()
    {
        if (!initialized)
            return;

        try { session?.Close(); } catch (ZenohException) { }
        session?.Dispose();
        session = null;
    }

    IEnumerator QueryLoop()
    {
        for (int i = 0; i < 50; i++)
        {
            PerformGet(i);
            yield return new WaitForSeconds(queryInterval);
        }
    }

    void PerformGet(int index)
    {
        Debug.Log($"[{index:D4}] Sending Get to '{keyExprString}'...");

        try
        {
            using (KeyExpr ke = KeyExpr.Parse(keyExprString))
            {
                session.Get(ke, HandleReply,
                    parameters: $"index={index}",
                    options: new GetOptions { TimeoutMs = 5000 });
            }
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception performing Get: {ex.Message}");
        }
    }

    void HandleReply(ReplyView reply)
    {
        if (reply.IsOk)
        {
            SampleView sample = reply.OkSample.Value;
            Debug.Log($"Reply OK: keyexpr={sample.KeyExpr}, payload={sample.PayloadAsString()}");
        }
        else
        {
            Debug.LogWarning($"Reply Error: {reply.ErrorPayloadAsString()}");
        }
    }
}

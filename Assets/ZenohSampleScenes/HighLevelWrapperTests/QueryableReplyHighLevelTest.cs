using UnityEngine;
using Zenoh.HighLevel;

public class QueryableReplyHighLevelTest : MonoBehaviour
{
    private Session session;
    private Queryable queryable;
    private bool initialized;

    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprString = "demo/query";

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

        DeclareQueryable();
    }

    void OnDestroy()
    {
        if (!initialized)
            return;

        queryable?.Dispose();
        queryable = null;

        try { session?.Close(); } catch (ZenohException) { }
        session?.Dispose();
        session = null;
    }

    void DeclareQueryable()
    {
        try
        {
            using (KeyExpr ke = KeyExpr.Parse(keyExprString))
            {
                queryable = Queryable.Declare(session, ke, HandleQuery);
            }
            Debug.Log($"Queryable declared on '{keyExprString}'");
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception declaring queryable: {ex.Message}");
        }
    }

    void HandleQuery(QueryView query)
    {
        Debug.Log($"Query received: keyexpr={query.KeyExpr}, params={query.Parameters}");

        string requestPayload = query.PayloadAsString();
        if (requestPayload != null)
            Debug.Log($"Query payload: {requestPayload}");

        string responsePayload = $"Hello! You queried: {query.KeyExpr}";
        try
        {
            query.Reply(query.KeyExpr, responsePayload, new QueryReplyOptions { Encoding = EncodingPreset.TextPlain });
            Debug.Log($"Reply sent: {responsePayload}");
        }
        catch (ZenohException ex)
        {
            Debug.LogError($"Exception replying to query: {ex.Message}");
        }
    }
}

using System;
using UnityEngine;
using Zenoh.Bindings;

public class QueryableReplyBindingTest : MonoBehaviour
{
    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprString = "unity/test/query";

    private ZOwnedSession session;
    private ZOwnedQueryable queryable;
    private ZOwnedKeyExpr keyExpr;

    void Start()
    {
        session = OpenSession();
        if (session == null) return;

        byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes(keyExprString);
        ZResult keyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out keyExpr);
        if (!keyResult.IsOk)
        {
            Debug.LogError($"[Queryable] Failed to create key expression: {keyResult}");
            return;
        }

        queryable = new ZOwnedQueryable();

        using (ZOwnedClosureQuery closure = new ZOwnedClosureQuery())
        {
            closure.Set(OnQuery, null, IntPtr.Zero);

            ZQueryableOptions options = ZQueryableOptions.Default();
            ZResult declareResult = queryable.Declare(session.Loan(), keyExpr.Loan(), closure, options);
            if (!declareResult.IsOk)
            {
                Debug.LogError($"[Queryable] Failed to declare queryable: {declareResult}");
                return;
            }
        }

        Debug.Log($"[Queryable] Listening on '{keyExprString}'");
    }

    void OnDestroy()
    {
        queryable?.Dispose();
        keyExpr?.Dispose();

        if (session != null)
        {
            ZCloseOptions closeOptions = ZCloseOptions.Default();
            session.Loan().Close(closeOptions);
            session.Dispose();
        }
    }

    private void OnQuery(ZLoanedQuery query, IntPtr context)
    {
        string parameters = query.Parameters();
        Debug.Log($"[Queryable] Received query: parameters='{parameters}'");

        ZLoanedBytes payload = query.Payload();
        if (payload != null && !payload.IsEmpty())
        {
            Debug.Log($"[Queryable]   payload length={payload.Len()}");
        }

        byte[] replyData = System.Text.Encoding.UTF8.GetBytes($"Hello from Unity Queryable! params={parameters}");
        ZResult bytesResult = ZOwnedBytes.TryCreateCopyFromBuf(replyData, (nuint)replyData.Length, out ZOwnedBytes replyBytes);
        if (!bytesResult.IsOk)
        {
            Debug.LogError($"[Queryable] Failed to create reply bytes: {bytesResult}");
            return;
        }

        using (ZQueryReplyOptions replyOptions = ZQueryReplyOptions.Default())
        {
            ZResult replyResult = query.Reply(query.KeyExpr(), replyBytes, replyOptions);
            if (!replyResult.IsOk)
                Debug.LogError($"[Queryable] Failed to send reply: {replyResult}");
            else
                Debug.Log("[Queryable] Reply sent");
        }
    }

    private ZOwnedSession OpenSession()
    {
        ZOwnedConfig config;
        if (zenohConfigText != null)
        {
            byte[] configBytes = NullTerminatedUtf8(zenohConfigText.text);
            ZResult configResult = ZOwnedConfig.TryCreateFromStr(configBytes, out config);
            if (!configResult.IsOk)
            {
                Debug.LogError($"[Queryable] Failed to create config: {configResult}");
                return null;
            }
        }
        else
        {
            ZResult configResult = ZOwnedConfig.TryCreateDefault(out config);
            if (!configResult.IsOk)
            {
                Debug.LogError($"[Queryable] Failed to create default config: {configResult}");
                return null;
            }
        }

        using (config)
        {
            ZOpenOptions openOptions = ZOpenOptions.Default();
            ZResult openResult = ZOwnedSession.TryCreateOpen(config, openOptions, out ZOwnedSession s);
            if (!openResult.IsOk)
            {
                Debug.LogError($"[Queryable] Failed to open session: {openResult}");
                return null;
            }

            Debug.Log("[Queryable] Session opened");
            return s;
        }
    }

    private static byte[] NullTerminatedUtf8(string value)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
        byte[] result = new byte[utf8.Length + 1];
        Buffer.BlockCopy(utf8, 0, result, 0, utf8.Length);
        return result;
    }
}

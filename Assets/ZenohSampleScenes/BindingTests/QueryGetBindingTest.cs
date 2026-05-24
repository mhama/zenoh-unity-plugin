using System;
using System.Collections;
using UnityEngine;
using Zenoh.Bindings;

public class QueryGetBindingTest : MonoBehaviour
{
    [SerializeField]
    private TextAsset zenohConfigText;

    [SerializeField]
    private string keyExprString = "unity/test/query";

    [SerializeField]
    private float queryIntervalSeconds = 2.0f;

    [SerializeField]
    private int queryCount = 5;

    private ZOwnedSession session;

    void Start()
    {
        session = OpenSession();
        if (session == null) return;

        StartCoroutine(QueryLoop());
    }

    void OnDestroy()
    {
        if (session != null)
        {
            ZCloseOptions closeOptions = ZCloseOptions.Default();
            session.Loan().Close(closeOptions);
            session.Dispose();
        }
    }

    IEnumerator QueryLoop()
    {
        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < queryCount; i++)
        {
            SendQuery(i);
            yield return new WaitForSeconds(queryIntervalSeconds);
        }

        Debug.Log("[Getter] Query loop finished");
    }

    private void SendQuery(int index)
    {
        byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes(keyExprString);
        ZResult keyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr keyExpr);
        if (!keyResult.IsOk)
        {
            Debug.LogError($"[Getter] Failed to create key expression: {keyResult}");
            return;
        }

        using (keyExpr)
        using (ZOwnedClosureReply closure = new ZOwnedClosureReply())
        {
            int queryIndex = index;
            closure.Set((reply, context) => OnReply(reply, queryIndex), null, IntPtr.Zero);

            using (ZGetOptions options = ZGetOptions.Default())
            {
                options.SetTimeoutMs(5000);

                string parametersStr = $"index={index}";
                byte[] parameters = NullTerminatedUtf8(parametersStr);

                Debug.Log($"[Getter] Sending query #{index} on '{keyExprString}' parameters='{parametersStr}'");
                ZResult getResult = session.Loan().Get(keyExpr.Loan(), parameters, closure, options);
                if (!getResult.IsOk)
                    Debug.LogError($"[Getter] Get failed: {getResult}");
            }
        }
    }

    private void OnReply(ZLoanedReply reply, int queryIndex)
    {
        if (reply.IsOk())
        {
            ZLoanedSample sample = reply.Ok();
            if (sample == null)
            {
                Debug.Log($"[Getter] Reply #{queryIndex}: ok but null sample");
                return;
            }

            ZLoanedBytes payload = sample.Payload();
            byte[] buf = payload.CopyToByteArray();
            string payloadStr = System.Text.Encoding.UTF8.GetString(buf);
            Debug.Log($"[Getter] Reply #{queryIndex}: '{payloadStr}'");
        }
        else
        {
            ZLoanedReplyErr err = reply.Err();
            if (err != null)
            {
                nuint errLen = err.Payload().Len();
                Debug.LogWarning($"[Getter] Reply #{queryIndex}: error, payload length={errLen}");
            }
            else
            {
                Debug.LogWarning($"[Getter] Reply #{queryIndex}: error (no details)");
            }
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
                Debug.LogError($"[Getter] Failed to create config: {configResult}");
                return null;
            }
        }
        else
        {
            ZResult configResult = ZOwnedConfig.TryCreateDefault(out config);
            if (!configResult.IsOk)
            {
                Debug.LogError($"[Getter] Failed to create default config: {configResult}");
                return null;
            }
        }

        using (config)
        {
            ZOpenOptions openOptions = ZOpenOptions.Default();
            ZResult openResult = ZOwnedSession.TryCreateOpen(config, openOptions, out ZOwnedSession s);
            if (!openResult.IsOk)
            {
                Debug.LogError($"[Getter] Failed to open session: {openResult}");
                return null;
            }

            Debug.Log("[Getter] Session opened");
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

using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Zenoh.Bindings;

namespace Zenoh.Tests
{
    [TestFixture]
    [Category("ZenohIntegration")]
    public sealed class BindingsZenohdIntegrationTests
    {
        private ZenohdTestServer zenohd;

        [SetUp]
        public void SetUp()
        {
            zenohd = ZenohdTestServer.Start();
        }

        [TearDown]
        public void TearDown()
        {
            zenohd?.Dispose();
            zenohd = null;
        }

        [Test]
        public void OpenPublishSubscribe_RoundTripsSample()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/pubsub");
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("hello from Unity TestRunner");

            ZResult subscriberKeyExprResult = ZOwnedKeyExpr.TryCreateFromSubstr(
                keyExprBytes,
                (nuint)keyExprBytes.Length,
                out ZOwnedKeyExpr subscriberKeyExpr);
            Assert.That(subscriberKeyExprResult.IsOk, Is.True, subscriberKeyExprResult.ToString());

            ZResult publisherKeyExprResult = ZOwnedKeyExpr.TryCreateFromSubstr(
                keyExprBytes,
                (nuint)keyExprBytes.Length,
                out ZOwnedKeyExpr publisherKeyExpr);
            Assert.That(publisherKeyExprResult.IsOk, Is.True, publisherKeyExprResult.ToString());

            using (ZOwnedSession subscriberSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession publisherSession = OpenLocalhostZenohdSession())
            using (subscriberKeyExpr)
            using (publisherKeyExpr)
            using (ZOwnedSubscriber subscriber = new ZOwnedSubscriber())
            using (ZOwnedPublisher publisher = new ZOwnedPublisher())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                int receivedPayloadLength = -1;

                using (ZOwnedClosureSample callback = new ZOwnedClosureSample())
                {
                    callback.Set((sample, context) =>
                    {
                        receivedPayloadLength = checked((int)sample.Payload().Len());
                        received.Set();
                    }, null, IntPtr.Zero);

                    ZSubscriberOptions subscriberOptions = ZSubscriberOptions.Default();

                    ZResult declareSubscriber = subscriber.Declare(
                        subscriberSession.Loan(),
                        subscriberKeyExpr.Loan(),
                        callback,
                        subscriberOptions);
                    Assert.That(declareSubscriber.IsOk, Is.True, declareSubscriber.ToString());
                }

                using (ZPublisherOptions publisherOptions = ZPublisherOptions.Default())
                {
                    ZResult declarePublisher = publisher.Declare(
                        publisherSession.Loan(),
                        publisherKeyExpr.Loan(),
                        publisherOptions);
                    Assert.That(declarePublisher.IsOk, Is.True, declarePublisher.ToString());
                }

                ZLoanedPublisher loanedPublisher = publisher.Loan();
                WaitForMatchingSubscriber(loanedPublisher);

                ZResult bytesResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes message);
                using (message)
                {
                    Assert.That(bytesResult.IsOk, Is.True, bytesResult.ToString());
                    Assert.That(message, Is.Not.Null);

                    using (ZPublisherPutOptions putOptions = ZPublisherPutOptions.Default())
                    {
                        byte[] encodingBytes = System.Text.Encoding.UTF8.GetBytes("text/plain");
                        ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                            encodingBytes,
                            (nuint)encodingBytes.Length,
                            out ZOwnedEncoding putEncoding);
                        Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

                        putOptions.SetEncoding(putEncoding);
                        Assert.DoesNotThrow(() => putEncoding.Dispose());

                        ZResult putResult = loanedPublisher.Put(message, putOptions);
                        Assert.That(putResult.IsOk, Is.True, putResult.ToString());
                    }
                }

                Assert.That(received.Wait(2000), Is.True, "No sample received via local zenohd after a single publish.");
                Assert.That(receivedPayloadLength, Is.EqualTo(payload.Length));

                CloseSession(subscriberSession);
                CloseSession(publisherSession);
            }
        }

        [Test]
        public void GetQueryable_RoundTripsReply()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/getqueryable");
            byte[] replyPayload = System.Text.Encoding.UTF8.GetBytes("pong");
            byte[] queryPayload = System.Text.Encoding.UTF8.GetBytes("ping");

            ZResult queryableKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(
                keyExprBytes,
                (nuint)keyExprBytes.Length,
                out ZOwnedKeyExpr queryableKey);
            Assert.That(queryableKeyResult.IsOk, Is.True, queryableKeyResult.ToString());

            ZResult getKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(
                keyExprBytes,
                (nuint)keyExprBytes.Length,
                out ZOwnedKeyExpr getKey);
            Assert.That(getKeyResult.IsOk, Is.True, getKeyResult.ToString());

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession querierSession = OpenLocalhostZenohdSession())
            using (queryableKey)
            using (getKey)
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim replyReceived = new ManualResetEventSlim(false))
            {
                Exception queryableCallbackError = null;
                int observedQueryPayloadLength = -1;
                byte[] observedQueryParameters = null;

                int replyPayloadLength = -1;
                bool replyWasOk = false;

                using (ZOwnedClosureQuery queryableCallback = new ZOwnedClosureQuery())
                {
                    queryableCallback.Set((query, context) =>
                    {
                        try
                        {
                            ZLoanedBytes payload = query.Payload();
                            observedQueryPayloadLength = payload == null ? -1 : checked((int)payload.Len());
                            observedQueryParameters = query.Parameters();

                            ZResult bytesResult = ZOwnedBytes.TryCreateCopyFromBuf(
                                replyPayload,
                                (nuint)replyPayload.Length,
                                out ZOwnedBytes payloadOut);
                            if (!bytesResult.IsOk)
                            {
                                payloadOut?.Dispose();
                                queryableCallbackError = new InvalidOperationException("reply payload create failed: " + bytesResult);
                                return;
                            }

                            using (payloadOut)
                            using (ZQueryReplyOptions replyOptions = ZQueryReplyOptions.Default())
                            {
                                ZResult replyResult = query.Reply(query.KeyExpr(), payloadOut, replyOptions);
                                if (!replyResult.IsOk)
                                {
                                    queryableCallbackError = new InvalidOperationException("z_query_reply failed: " + replyResult);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            queryableCallbackError = ex;
                        }
                    }, null, IntPtr.Zero);

                    ZQueryableOptions queryableOptions = ZQueryableOptions.Default();
                    queryableOptions.SetComplete(true);

                    ZResult declareResult = queryable.Declare(
                        queryableSession.Loan(),
                        queryableKey.Loan(),
                        queryableCallback,
                        queryableOptions);
                    Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                }

                WaitForSessionDiscovery();

                using (ZOwnedClosureReply replyCallback = new ZOwnedClosureReply())
                {
                    replyCallback.Set((reply, context) =>
                    {
                        try
                        {
                            replyWasOk = reply.IsOk();
                            if (replyWasOk)
                            {
                                ZLoanedSample sample = reply.Ok();
                                if (sample != null)
                                    replyPayloadLength = checked((int)sample.Payload().Len());
                            }
                        }
                        finally
                        {
                            replyReceived.Set();
                        }
                    }, null, IntPtr.Zero);

                    ZResult queryPayloadResult = ZOwnedBytes.TryCreateCopyFromBuf(
                        queryPayload,
                        (nuint)queryPayload.Length,
                        out ZOwnedBytes queryPayloadBytes);
                    Assert.That(queryPayloadResult.IsOk, Is.True, queryPayloadResult.ToString());

                    using (queryPayloadBytes)
                    using (ZGetOptions getOptions = ZGetOptions.Default())
                    {
                        getOptions.SetTarget(ZQueryTarget.All);
                        getOptions.SetConsolidation(ZQueryConsolidation.None());
                        getOptions.SetTimeoutMs(5000);
                        getOptions.SetPayload(queryPayloadBytes);

                        ZResult getResult = querierSession.Loan().Get(
                            getKey.Loan(),
                            NullTerminatedUtf8(string.Empty),
                            replyCallback,
                            getOptions);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(replyReceived.Wait(5000), Is.True, "No reply received via local zenohd.");
                Assert.That(queryableCallbackError, Is.Null, queryableCallbackError?.ToString());
                Assert.That(observedQueryPayloadLength, Is.EqualTo(queryPayload.Length));
                Assert.That(observedQueryParameters, Is.Not.Null);
                Assert.That(observedQueryParameters.Length, Is.EqualTo(0));
                Assert.That(replyWasOk, Is.True);
                Assert.That(replyPayloadLength, Is.EqualTo(replyPayload.Length));

                CloseSession(queryableSession);
                CloseSession(querierSession);
            }
        }

        private static void WaitForSessionDiscovery()
        {
            // Allow a brief moment for the queryable declaration to propagate through
            // the router before the querier issues z_get. zenohd routes queries by
            // intersection of declared key expressions, so a tiny grace period is
            // sufficient in practice for an in-process router on loopback.
            Thread.Sleep(200);
        }

        private static void WaitForMatchingSubscriber(ZLoanedPublisher publisher)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                ZResult result = publisher.TryGetMatchingStatus(out ZMatchingStatus matchingStatus);
                Assert.That(result.IsOk, Is.True, result.ToString());

                if (matchingStatus.Matching)
                {
                    return;
                }

                Thread.Sleep(50);
            }

            Assert.Fail("Publisher did not report a matching subscriber within 5 seconds.");
        }

        private static ZOwnedSession OpenLocalhostZenohdSession()
        {
            ZResult configResult = ZOwnedConfig.TryCreateFromStr(
                NullTerminatedUtf8(LocalhostZenohdClientConfig),
                out ZOwnedConfig config);
            using (config)
            {
                Assert.That(configResult.IsOk, Is.True, configResult.ToString());

                ZOpenOptions options = ZOpenOptions.Default();

                ZResult openResult = ZOwnedSession.TryCreateOpen(config, options, out ZOwnedSession session);
                if (!openResult.IsOk)
                {
                    session?.Dispose();
                    Assert.Fail($"z_open failed. Ensure zenohd is listening on {ZenohdTestServer.Endpoint}. Result: {openResult}");
                }

                return session;
            }
        }

        private static void CloseSession(ZOwnedSession session)
        {
            ZCloseOptions closeOptions = ZCloseOptions.Default();
            session.Loan().Close(closeOptions);
        }

        private static byte[] NullTerminatedUtf8(string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] result = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, result, 0, utf8.Length);
            return result;
        }

        private const string LocalhostZenohdClientConfig = @"
{
  mode: ""client"",

  listen: {
    endpoints: []
  },

  connect: {
    endpoints: [""tcp/127.0.0.1:17447""]
  },

  scouting: {
    multicast: {
      enabled: false,
      autoconnect: [],
      listen: false
    },
    gossip: {
      enabled: false,
      autoconnect: []
    }
  }
}";
    }
}

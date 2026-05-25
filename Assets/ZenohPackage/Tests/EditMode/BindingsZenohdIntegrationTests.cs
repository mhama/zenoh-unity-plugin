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

                    using (ZSubscriberOptions subscriberOptions = ZSubscriberOptions.Default())
                    {
                        ZResult declareSubscriber = subscriber.Declare(
                            subscriberSession.Loan(),
                            subscriberKeyExpr.Loan(),
                            callback,
                            subscriberOptions);
                        Assert.That(declareSubscriber.IsOk, Is.True, declareSubscriber.ToString());
                    }
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

                using (ZOpenOptions options = ZOpenOptions.Default())
                {
                    ZResult openResult = ZOwnedSession.TryCreateOpen(config, options, out ZOwnedSession session);
                    if (!openResult.IsOk)
                    {
                        session?.Dispose();
                        Assert.Fail($"z_open failed. Ensure zenohd is listening on {ZenohdTestServer.Endpoint}. Result: {openResult}");
                    }

                    return session;
                }
            }
        }

        private static void CloseSession(ZOwnedSession session)
        {
            using (ZCloseOptions closeOptions = ZCloseOptions.Default())
            {
                session.Loan().Close(closeOptions);
            }
        }

        private static byte[] NullTerminatedUtf8(string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] result = new byte[utf8.Length + 1];
            Buffer.BlockCopy(utf8, 0, result, 0, utf8.Length);
            return result;
        }

        [Test]
        public void QueryReply_RoundTripsPayload()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/queryreply");
            byte[] replyPayload = System.Text.Encoding.UTF8.GetBytes("reply-payload");

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession getterSession = OpenLocalhostZenohdSession())
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                ZResult qKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr qKey);
                Assert.That(qKeyResult.IsOk, Is.True, qKeyResult.ToString());

                using (ZOwnedClosureQuery closureQuery = new ZOwnedClosureQuery())
                using (qKey)
                {
                    closureQuery.Set((query, context) =>
                    {
                        ZResult payloadResult = ZOwnedBytes.TryCreateCopyFromBuf(replyPayload, (nuint)replyPayload.Length, out ZOwnedBytes payload);
                        if (!payloadResult.IsOk) return;

                        using (ZQueryReplyOptions opts = ZQueryReplyOptions.Default())
                        {
                            query.Reply(query.KeyExpr(), payload, opts);
                        }
                    }, null, IntPtr.Zero);

                    using (ZQueryableOptions qOpts = ZQueryableOptions.Default())
                    {
                        ZResult declareResult = queryable.Declare(queryableSession.Loan(), qKey.Loan(), closureQuery, qOpts);
                        Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                    }
                }

                Thread.Sleep(200);

                bool replyIsOk = false;
                int replyPayloadLen = -1;

                ZResult gKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr gKey);
                Assert.That(gKeyResult.IsOk, Is.True, gKeyResult.ToString());

                using (ZOwnedClosureReply closureReply = new ZOwnedClosureReply())
                using (gKey)
                {
                    closureReply.Set((reply, context) =>
                    {
                        replyIsOk = reply.IsOk();
                        if (replyIsOk)
                        {
                            ZLoanedSample sample = reply.Ok();
                            if (sample != null)
                                replyPayloadLen = checked((int)sample.Payload().Len());
                        }
                        received.Set();
                    }, null, IntPtr.Zero);

                    using (ZGetOptions getOpts = ZGetOptions.Default())
                    {
                        byte[] parameters = NullTerminatedUtf8("");
                        ZResult getResult = getterSession.Loan().Get(gKey.Loan(), parameters, closureReply, getOpts);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(received.Wait(5000), Is.True, "No reply received within 5 seconds.");
                Assert.That(replyIsOk, Is.True);
                Assert.That(replyPayloadLen, Is.EqualTo(replyPayload.Length));

                CloseSession(queryableSession);
                CloseSession(getterSession);
            }
        }

        [Test]
        public void QueryReplyErr_RoundTripsError()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/queryreplyerr");
            byte[] errPayload = System.Text.Encoding.UTF8.GetBytes("error-info");

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession getterSession = OpenLocalhostZenohdSession())
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                ZResult qKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr qKey);
                Assert.That(qKeyResult.IsOk, Is.True, qKeyResult.ToString());

                using (ZOwnedClosureQuery closureQuery = new ZOwnedClosureQuery())
                using (qKey)
                {
                    closureQuery.Set((query, context) =>
                    {
                        ZResult payloadResult = ZOwnedBytes.TryCreateCopyFromBuf(errPayload, (nuint)errPayload.Length, out ZOwnedBytes payload);
                        if (!payloadResult.IsOk) return;

                        using (ZQueryReplyErrOptions opts = ZQueryReplyErrOptions.Default())
                        {
                            query.ReplyErr(payload, opts);
                        }
                    }, null, IntPtr.Zero);

                    using (ZQueryableOptions qOpts = ZQueryableOptions.Default())
                    {
                        ZResult declareResult = queryable.Declare(queryableSession.Loan(), qKey.Loan(), closureQuery, qOpts);
                        Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                    }
                }

                Thread.Sleep(200);

                bool replyIsOk = true;
                int errPayloadLen = -1;

                ZResult gKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr gKey);
                Assert.That(gKeyResult.IsOk, Is.True, gKeyResult.ToString());

                using (ZOwnedClosureReply closureReply = new ZOwnedClosureReply())
                using (gKey)
                {
                    closureReply.Set((reply, context) =>
                    {
                        replyIsOk = reply.IsOk();
                        if (!replyIsOk)
                        {
                            ZLoanedReplyErr err = reply.Err();
                            if (err != null)
                                errPayloadLen = checked((int)err.Payload().Len());
                        }
                        received.Set();
                    }, null, IntPtr.Zero);

                    using (ZGetOptions getOpts = ZGetOptions.Default())
                    {
                        byte[] parameters = NullTerminatedUtf8("");
                        ZResult getResult = getterSession.Loan().Get(gKey.Loan(), parameters, closureReply, getOpts);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(received.Wait(5000), Is.True, "No reply received within 5 seconds.");
                Assert.That(replyIsOk, Is.False);
                Assert.That(errPayloadLen, Is.EqualTo(errPayload.Length));

                CloseSession(queryableSession);
                CloseSession(getterSession);
            }
        }

        [Test]
        public void QueryReplyDel_RoundTripsDeleteSample()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/queryreplydel");

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession getterSession = OpenLocalhostZenohdSession())
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                ZResult qKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr qKey);
                Assert.That(qKeyResult.IsOk, Is.True, qKeyResult.ToString());

                using (ZOwnedClosureQuery closureQuery = new ZOwnedClosureQuery())
                using (qKey)
                {
                    closureQuery.Set((query, context) =>
                    {
                        using (ZQueryReplyDelOptions opts = ZQueryReplyDelOptions.Default())
                        {
                            query.ReplyDel(query.KeyExpr(), opts);
                        }
                    }, null, IntPtr.Zero);

                    using (ZQueryableOptions qOpts = ZQueryableOptions.Default())
                    {
                        ZResult declareResult = queryable.Declare(queryableSession.Loan(), qKey.Loan(), closureQuery, qOpts);
                        Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                    }
                }

                Thread.Sleep(200);

                bool replyIsOk = false;
                ZSampleKind sampleKind = ZSampleKind.Put;

                ZResult gKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr gKey);
                Assert.That(gKeyResult.IsOk, Is.True, gKeyResult.ToString());

                using (ZOwnedClosureReply closureReply = new ZOwnedClosureReply())
                using (gKey)
                {
                    closureReply.Set((reply, context) =>
                    {
                        replyIsOk = reply.IsOk();
                        if (replyIsOk)
                        {
                            ZLoanedSample sample = reply.Ok();
                            if (sample != null)
                                sampleKind = sample.Kind();
                        }
                        received.Set();
                    }, null, IntPtr.Zero);

                    using (ZGetOptions getOpts = ZGetOptions.Default())
                    {
                        byte[] parameters = NullTerminatedUtf8("");
                        ZResult getResult = getterSession.Loan().Get(gKey.Loan(), parameters, closureReply, getOpts);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(received.Wait(5000), Is.True, "No reply received within 5 seconds.");
                Assert.That(replyIsOk, Is.True);
                Assert.That(sampleKind, Is.EqualTo(ZSampleKind.Delete));

                CloseSession(queryableSession);
                CloseSession(getterSession);
            }
        }

        [Test]
        public void QueryFields_MatchGetParameters()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/queryfields");
            byte[] queryPayload = System.Text.Encoding.UTF8.GetBytes("query-body");
            byte[] attachmentPayload = System.Text.Encoding.UTF8.GetBytes("attach");
            string parametersStr = "a=1&b=2";

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession getterSession = OpenLocalhostZenohdSession())
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                string receivedParameters = null;
                int receivedPayloadLen = -1;
                int receivedAttachmentLen = -1;

                ZResult qKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr qKey);
                Assert.That(qKeyResult.IsOk, Is.True, qKeyResult.ToString());

                using (ZOwnedClosureQuery closureQuery = new ZOwnedClosureQuery())
                using (qKey)
                {
                    closureQuery.Set((query, context) =>
                    {
                        receivedParameters = query.Parameters();

                        ZLoanedBytes payload = query.Payload();
                        if (payload != null)
                            receivedPayloadLen = checked((int)payload.Len());

                        ZLoanedBytes attachment = query.Attachment();
                        if (attachment != null)
                            receivedAttachmentLen = checked((int)attachment.Len());

                        byte[] replyBytes = System.Text.Encoding.UTF8.GetBytes("ok");
                        ZResult r = ZOwnedBytes.TryCreateCopyFromBuf(replyBytes, (nuint)replyBytes.Length, out ZOwnedBytes replyPayload);
                        if (r.IsOk)
                        {
                            using (ZQueryReplyOptions opts = ZQueryReplyOptions.Default())
                            {
                                query.Reply(query.KeyExpr(), replyPayload, opts);
                            }
                        }

                        received.Set();
                    }, null, IntPtr.Zero);

                    using (ZQueryableOptions qOpts = ZQueryableOptions.Default())
                    {
                        ZResult declareResult = queryable.Declare(queryableSession.Loan(), qKey.Loan(), closureQuery, qOpts);
                        Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                    }
                }

                Thread.Sleep(200);

                ZResult gKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr gKey);
                Assert.That(gKeyResult.IsOk, Is.True, gKeyResult.ToString());

                ZResult payloadBytesResult = ZOwnedBytes.TryCreateCopyFromBuf(queryPayload, (nuint)queryPayload.Length, out ZOwnedBytes payloadBytes);
                Assert.That(payloadBytesResult.IsOk, Is.True, payloadBytesResult.ToString());

                ZResult attachmentBytesResult = ZOwnedBytes.TryCreateCopyFromBuf(attachmentPayload, (nuint)attachmentPayload.Length, out ZOwnedBytes attachmentBytes);
                Assert.That(attachmentBytesResult.IsOk, Is.True, attachmentBytesResult.ToString());

                using (ZOwnedClosureReply closureReply = new ZOwnedClosureReply())
                using (gKey)
                using (payloadBytes)
                using (attachmentBytes)
                {
                    closureReply.Set((reply, context) => { }, null, IntPtr.Zero);

                    using (ZGetOptions getOpts = ZGetOptions.Default())
                    {
                        getOpts.SetPayload(payloadBytes);
                        getOpts.SetAttachment(attachmentBytes);

                        byte[] parameters = NullTerminatedUtf8(parametersStr);
                        ZResult getResult = getterSession.Loan().Get(gKey.Loan(), parameters, closureReply, getOpts);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(received.Wait(5000), Is.True, "Queryable callback not invoked within 5 seconds.");
                Assert.That(receivedParameters, Is.EqualTo(parametersStr));
                Assert.That(receivedPayloadLen, Is.EqualTo(queryPayload.Length));
                Assert.That(receivedAttachmentLen, Is.EqualTo(attachmentPayload.Length));

                CloseSession(queryableSession);
                CloseSession(getterSession);
            }
        }

        [Test]
        public void QueryClone_CanBeUsedOutsideCallback()
        {
            byte[] keyExprBytes = System.Text.Encoding.UTF8.GetBytes("unity/test/bindings/queryclone");

            using (ZOwnedSession queryableSession = OpenLocalhostZenohdSession())
            using (ZOwnedSession getterSession = OpenLocalhostZenohdSession())
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                ZOwnedQuery clonedQuery = null;

                ZResult qKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr qKey);
                Assert.That(qKeyResult.IsOk, Is.True, qKeyResult.ToString());

                using (ZOwnedClosureQuery closureQuery = new ZOwnedClosureQuery())
                using (qKey)
                {
                    closureQuery.Set((query, context) =>
                    {
                        clonedQuery = ZOwnedQuery.CreateClone(query);

                        byte[] replyBytes = System.Text.Encoding.UTF8.GetBytes("ok");
                        ZResult r = ZOwnedBytes.TryCreateCopyFromBuf(replyBytes, (nuint)replyBytes.Length, out ZOwnedBytes replyPayload);
                        if (r.IsOk)
                        {
                            using (ZQueryReplyOptions opts = ZQueryReplyOptions.Default())
                            {
                                query.Reply(query.KeyExpr(), replyPayload, opts);
                            }
                        }

                        received.Set();
                    }, null, IntPtr.Zero);

                    using (ZQueryableOptions qOpts = ZQueryableOptions.Default())
                    {
                        ZResult declareResult = queryable.Declare(queryableSession.Loan(), qKey.Loan(), closureQuery, qOpts);
                        Assert.That(declareResult.IsOk, Is.True, declareResult.ToString());
                    }
                }

                Thread.Sleep(200);

                ZResult gKeyResult = ZOwnedKeyExpr.TryCreateFromSubstr(keyExprBytes, (nuint)keyExprBytes.Length, out ZOwnedKeyExpr gKey);
                Assert.That(gKeyResult.IsOk, Is.True, gKeyResult.ToString());

                using (ZOwnedClosureReply closureReply = new ZOwnedClosureReply())
                using (gKey)
                {
                    closureReply.Set((reply, context) => { }, null, IntPtr.Zero);

                    using (ZGetOptions getOpts = ZGetOptions.Default())
                    {
                        byte[] parameters = NullTerminatedUtf8("");
                        ZResult getResult = getterSession.Loan().Get(gKey.Loan(), parameters, closureReply, getOpts);
                        Assert.That(getResult.IsOk, Is.True, getResult.ToString());
                    }
                }

                Assert.That(received.Wait(5000), Is.True, "Queryable callback not invoked within 5 seconds.");

                using (clonedQuery)
                {
                    Assert.That(clonedQuery, Is.Not.Null);
                    Assert.That(clonedQuery.Check(), Is.True);
                    ZLoanedQuery loaned = clonedQuery.Loan();
                    Assert.DoesNotThrow(() => loaned.KeyExpr());
                }

                CloseSession(queryableSession);
                CloseSession(getterSession);
            }
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

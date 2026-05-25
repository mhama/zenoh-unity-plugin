using System;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Zenoh.Bindings;
using Zenoh.HighLevel;

namespace Zenoh.Tests
{
    [TestFixture]
    [Category("ZenohIntegration")]
    public sealed class HighLevelZenohdIntegrationTests
    {
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
        public void Session_OpenAndClose_WithClientConfig()
        {
            using (Session session = Session.Open(LocalhostZenohdClientConfig))
            {
                Assert.That(session, Is.Not.Null);
                Assert.That(session.IsClosed, Is.False);

                session.Close();
                Assert.That(session.IsClosed, Is.True);
            }
        }

        [Test]
        public void Session_TryOpenWithInvalidConfig_ReturnsFalse()
        {
            bool ok = Session.TryOpen("{ malformed ---", out Session session, out ZResult result);

            Assert.That(ok, Is.False);
            Assert.That(session, Is.Null);
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        public void Publisher_PutString_DeliveredToSubscriber()
        {
            const string keyExpr = "unity/test/highlevel/string";
            const string payload = "hello from HighLevel";

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                string receivedKey = null;
                string receivedPayload = null;
                string receivedEncoding = null;

                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, sample =>
                {
                    receivedKey = sample.KeyExpr;
                    receivedPayload = sample.PayloadAsString();
                    receivedEncoding = sample.EncodingMime;
                    received.Set();
                });

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, EncodingPreset.TextPlain);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);
                        publisher.Put(payload);

                        Assert.That(received.Wait(2000), Is.True,
                            "No sample received via local zenohd after a single Put.");
                        Assert.That(receivedKey, Is.EqualTo(keyExpr));
                        Assert.That(receivedPayload, Is.EqualTo(payload));
                        Assert.That(receivedEncoding, Does.Contain("text/plain"));
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void Publisher_PutByteArray_DeliversCopiedPayload()
        {
            const string keyExpr = "unity/test/highlevel/bytes";
            byte[] payload = MakeByteSequence(0, 4096);

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                byte[] receivedPayload = null;
                int receivedLength = -1;

                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, sample =>
                {
                    receivedLength = sample.PayloadLength;
                    receivedPayload = sample.PayloadAsBytes();
                    received.Set();
                });

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, null);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);
                        publisher.Put(payload);

                        Assert.That(received.Wait(2000), Is.True);
                        Assert.That(receivedLength, Is.EqualTo(payload.Length));
                        Assert.That(receivedPayload, Is.EqualTo(payload));
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void Publisher_PutZeroCopy_DeliversPinnedPayload()
        {
            const string keyExpr = "unity/test/highlevel/zerocopy";
            byte[] payload = MakeByteSequence(0, 4096);

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                byte[] receivedPayload = null;
                int receivedLength = -1;

                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, sample =>
                {
                    receivedLength = sample.PayloadLength;
                    receivedPayload = sample.PayloadAsBytes();
                    received.Set();
                });

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, null);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);
                        publisher.PutZeroCopy(payload);

                        Assert.That(received.Wait(2000), Is.True);
                        Assert.That(receivedLength, Is.EqualTo(payload.Length));
                        Assert.That(receivedPayload, Is.EqualTo(payload));
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void SampleView_PayloadCopyTo_FillsPreallocatedBuffer()
        {
            const string keyExpr = "unity/test/highlevel/copyto";
            byte[] payload = MakeByteSequence(0, 1024);

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                byte[] destination = new byte[2048];
                int copiedLength = -1;

                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, sample =>
                {
                    copiedLength = sample.PayloadCopyTo(destination, 0);
                    received.Set();
                });

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, null);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);
                        publisher.Put(payload);

                        Assert.That(received.Wait(2000), Is.True);
                        Assert.That(copiedLength, Is.EqualTo(payload.Length));

                        byte[] truncated = new byte[payload.Length];
                        Buffer.BlockCopy(destination, 0, truncated, 0, payload.Length);
                        Assert.That(truncated, Is.EqualTo(payload));
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void Publisher_DefaultEncoding_AppliesToSubsequentPut()
        {
            const string keyExpr = "unity/test/highlevel/defaultencoding";

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                string receivedEncoding = null;

                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, sample =>
                {
                    receivedEncoding = sample.EncodingMime;
                    received.Set();
                });

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, EncodingPreset.ApplicationJson);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);
                        // No encoding argument here: should inherit DefaultEncoding from declare.
                        publisher.Put(Encoding.UTF8.GetBytes("{}"));

                        Assert.That(received.Wait(2000), Is.True);
                        Assert.That(receivedEncoding, Does.Contain("application/json"));
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void Publisher_TryPut_ReturnsTrueOnSuccess()
        {
            const string keyExpr = "unity/test/highlevel/tryput";

            using (Session subSession = OpenClientSession())
            using (Session pubSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                Subscriber subscriber = DeclareSubscriber(subSession, keyExpr, _ => received.Set());

                try
                {
                    Publisher publisher = DeclarePublisher(pubSession, keyExpr, null);
                    try
                    {
                        WaitForMatchingSubscriber(publisher);

                        bool ok = publisher.TryPut("hi", EncodingPreset.TextPlain, out ZResult result);
                        Assert.That(ok, Is.True, result.ToString());
                        Assert.That(received.Wait(2000), Is.True);
                    }
                    finally
                    {
                        publisher.Dispose();
                    }
                }
                finally
                {
                    subscriber.Dispose();
                }
            }
        }

        [Test]
        public void Subscriber_TryDeclare_WithValidArgsReturnsTrue()
        {
            using (Session session = OpenClientSession())
            using (KeyExpr keyExpr = KeyExpr.Parse("unity/test/highlevel/trydeclare"))
            {
                bool ok = Subscriber.TryDeclare(session, keyExpr, _ => { }, out Subscriber subscriber, out ZResult result);
                try
                {
                    Assert.That(ok, Is.True, result.ToString());
                    Assert.That(subscriber, Is.Not.Null);
                }
                finally
                {
                    subscriber?.Dispose();
                }
            }
        }

        private static Session OpenClientSession()
        {
            try
            {
                return Session.Open(LocalhostZenohdClientConfig);
            }
            catch (ZenohException ex)
            {
                Assert.Fail($"Session.Open failed; ensure zenohd is listening on {ZenohdTestServer.Endpoint}. Result: {ex.Result}");
                return null;
            }
        }

        private static Publisher DeclarePublisher(Session session, string keyExprString, EncodingPreset? defaultEncoding)
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                PublisherDeclareOptions options = defaultEncoding.HasValue
                    ? new PublisherDeclareOptions { DefaultEncoding = defaultEncoding.Value }
                    : null;
                return Publisher.Declare(session, keyExpr, options);
            }
        }

        private static Subscriber DeclareSubscriber(Session session, string keyExprString, Action<SampleView> onSample)
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                return Subscriber.Declare(session, keyExpr, onSample);
            }
        }

        private static void WaitForMatchingSubscriber(Publisher publisher)
        {
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (publisher.IsMatchingSubscribers)
                    return;
                Thread.Sleep(50);
            }

            Assert.Fail("Publisher did not report a matching subscriber within 5 seconds.");
        }

        [Test]
        public void Queryable_ReplyString_DeliveredToGet()
        {
            const string keyExpr = "unity/test/highlevel/queryreply/string";
            const string replyPayload = "hello from queryable";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                bool receivedIsOk = false;
                string receivedPayload = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.Reply(query.KeyExpr, replyPayload);
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            receivedIsOk = reply.IsOk;
                            if (reply.IsOk)
                                receivedPayload = reply.OkSample.Value.PayloadAsString();
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(receivedIsOk, Is.True);
                    Assert.That(receivedPayload, Is.EqualTo(replyPayload));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Queryable_ReplyBytes_DeliveredToGet()
        {
            const string keyExpr = "unity/test/highlevel/queryreply/bytes";
            byte[] replyPayload = MakeByteSequence(42, 256);

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                bool receivedIsOk = false;
                byte[] receivedPayload = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.Reply(query.KeyExpr, replyPayload);
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            receivedIsOk = reply.IsOk;
                            if (reply.IsOk)
                                receivedPayload = reply.OkSample.Value.PayloadAsBytes();
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(receivedIsOk, Is.True);
                    Assert.That(receivedPayload, Is.EqualTo(replyPayload));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Queryable_ReplyErr_DeliveredToGet()
        {
            const string keyExpr = "unity/test/highlevel/queryreply/err";
            const string errorPayload = "something went wrong";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                bool receivedIsOk = true;
                string receivedError = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.ReplyErr(errorPayload);
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            receivedIsOk = reply.IsOk;
                            if (!reply.IsOk)
                                receivedError = reply.ErrorPayloadAsString();
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(receivedIsOk, Is.False);
                    Assert.That(receivedError, Is.EqualTo(errorPayload));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Queryable_ReplyDel_DeliveredToGet()
        {
            const string keyExpr = "unity/test/highlevel/queryreply/del";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                bool receivedIsOk = false;
                ZSampleKind receivedKind = ZSampleKind.Put;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.ReplyDel(query.KeyExpr);
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            receivedIsOk = reply.IsOk;
                            if (reply.IsOk)
                                receivedKind = reply.OkSample.Value.Kind;
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(receivedIsOk, Is.True);
                    Assert.That(receivedKind, Is.EqualTo(ZSampleKind.Delete));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Queryable_TryDeclare_WithValidArgsReturnsTrue()
        {
            using (Session session = OpenClientSession())
            using (KeyExpr keyExpr = KeyExpr.Parse("unity/test/highlevel/queryable/trydeclare"))
            {
                bool ok = Queryable.TryDeclare(session, keyExpr, _ => { }, null, out Queryable queryable, out ZResult result);
                try
                {
                    Assert.That(ok, Is.True, result.ToString());
                    Assert.That(queryable, Is.Not.Null);
                }
                finally
                {
                    queryable?.Dispose();
                }
            }
        }

        [Test]
        public void Session_Get_WithParameters_QueryReceivesParameters()
        {
            const string keyExpr = "unity/test/highlevel/get/parameters";
            const string expectedParams = "key=value&foo=bar";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                string receivedParams = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    receivedParams = query.Parameters;
                    query.Reply(query.KeyExpr, "ok");
                    received.Set();
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, _ => { }, parameters: expectedParams, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "Queryable did not receive query within 5 seconds.");
                    Assert.That(receivedParams, Is.EqualTo(expectedParams));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Session_Get_WithPayload_QueryReceivesPayload()
        {
            const string keyExpr = "unity/test/highlevel/get/payload";
            byte[] expectedPayload = MakeByteSequence(10, 128);

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                byte[] receivedPayload = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    receivedPayload = query.PayloadAsBytes();
                    query.Reply(query.KeyExpr, "ok");
                    received.Set();
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, _ => { }, options: new GetOptions { Payload = expectedPayload, TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "Queryable did not receive query within 5 seconds.");
                    Assert.That(receivedPayload, Is.EqualTo(expectedPayload));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Session_Get_WithEncoding_QueryReceivesEncoding()
        {
            const string keyExpr = "unity/test/highlevel/get/encoding";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.Reply(query.KeyExpr, "ok", new QueryReplyOptions { Encoding = EncodingPreset.TextPlain });
                });

                try
                {
                    Thread.Sleep(200);

                    string replyEncoding = null;
                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            if (reply.IsOk)
                                replyEncoding = reply.OkSample.Value.EncodingMime;
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(replyEncoding, Does.Contain("text/plain"));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Session_Get_WithAttachment_QueryReceivesAttachment()
        {
            const string keyExpr = "unity/test/highlevel/get/attachment";
            byte[] expectedAttachment = MakeByteSequence(99, 64);

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                byte[] replyAttachment = null;

                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.Reply(query.KeyExpr, "ok", new QueryReplyOptions { Attachment = expectedAttachment });
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply =>
                        {
                            if (reply.IsOk)
                                replyAttachment = reply.OkSample.Value.AttachmentAsBytes();
                            received.Set();
                        }, options: new GetOptions { TimeoutMs = 5000 });
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                    Assert.That(replyAttachment, Is.EqualTo(expectedAttachment));
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Session_TryGet_ReturnsTrueOnSuccess()
        {
            const string keyExpr = "unity/test/highlevel/get/tryget";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (ManualResetEventSlim received = new ManualResetEventSlim(false))
            {
                Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
                {
                    query.Reply(query.KeyExpr, "pong");
                });

                try
                {
                    Thread.Sleep(200);

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        bool ok = gSession.TryGet(ke, reply =>
                        {
                            received.Set();
                        }, null, new GetOptions { TimeoutMs = 5000 }, default, out ZResult result);

                        Assert.That(ok, Is.True, result.ToString());
                    }

                    Assert.That(received.Wait(5000), Is.True,
                        "No reply received within 5 seconds.");
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        [Test]
        public void Session_Get_CancellationToken_CancelsGet()
        {
            const string keyExpr = "unity/test/highlevel/get/cancel";

            using (Session qSession = OpenClientSession())
            using (Session gSession = OpenClientSession())
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Queryable queryable = DeclareQueryable(qSession, keyExpr, query => { });

                try
                {
                    Thread.Sleep(200);

                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                    using (KeyExpr ke = KeyExpr.Parse(keyExpr))
                    {
                        gSession.Get(ke, reply => { },
                            options: new GetOptions { TimeoutMs = 30000 },
                            cancellationToken: cts.Token);
                    }

                    cts.Cancel();

                    Thread.Sleep(2000);
                    Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10000),
                        "Get should have been cancelled well before the 30s timeout.");
                }
                finally
                {
                    queryable.Dispose();
                }
            }
        }

        private static Queryable DeclareQueryable(Session session, string keyExprString, Action<QueryView> onQuery)
        {
            using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
            {
                return Queryable.Declare(session, keyExpr, onQuery);
            }
        }

        private static byte[] MakeByteSequence(int start, int count)
        {
            byte[] buffer = new byte[count];
            for (int i = 0; i < count; i++)
                buffer[i] = (byte)((start + i) & 0xFF);
            return buffer;
        }
    }
}

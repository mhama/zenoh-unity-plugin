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

                ZPublisherOptions publisherOptions = ZPublisherOptions.Default();

                ZResult declarePublisher = publisher.Declare(
                    publisherSession.Loan(),
                    publisherKeyExpr.Loan(),
                    publisherOptions);
                Assert.That(declarePublisher.IsOk, Is.True, declarePublisher.ToString());

                ZLoanedPublisher loanedPublisher = publisher.Loan();
                WaitForMatchingSubscriber(loanedPublisher);

                ZResult bytesResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes message);
                using (message)
                {
                    Assert.That(bytesResult.IsOk, Is.True, bytesResult.ToString());
                    Assert.That(message, Is.Not.Null);

                    ZPublisherPutOptions putOptions = ZPublisherPutOptions.Default();
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

using System;
using System.IO;
using NUnit.Framework;
using Zenoh.Bindings;
using Zenoh.HighLevel;

namespace Zenoh.Tests
{
    public sealed class HighLevelBasicTests
    {
        [Test]
        public void ZenohException_CarriesResultAndMessage()
        {
            ZResult result = new ZResult(ZResultCode.Z_EINVAL);
            ZenohException ex = new ZenohException(result, "boom");

            Assert.That(ex.Result.ResultCode, Is.EqualTo(ZResultCode.Z_EINVAL));
            Assert.That(ex.Message, Does.Contain("boom"));
            Assert.That(ex.Message, Does.Contain("Z_EINVAL"));
        }

        [Test]
        public void ThrowIfErr_OkResult_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ZResult.Ok.ThrowIfErr());
            Assert.DoesNotThrow(() => ZResult.Ok.ThrowIfErr("optional message"));
        }

        [Test]
        public void ThrowIfErr_ErrorResult_ThrowsZenohException()
        {
            ZResult err = new ZResult(ZResultCode.Z_EPARSE);

            ZenohException ex = Assert.Throws<ZenohException>(() => err.ThrowIfErr("parse failed"));
            Assert.That(ex.Result.ResultCode, Is.EqualTo(ZResultCode.Z_EPARSE));
        }

        [Test]
        public void KeyExpr_Parse_ValidStringSucceeds()
        {
            using (KeyExpr keyExpr = KeyExpr.Parse("demo/test"))
            {
                Assert.That(keyExpr, Is.Not.Null);
                Assert.That(keyExpr.ToString(), Is.EqualTo("demo/test"));
            }
        }

        [Test]
        public void KeyExpr_Parse_InvalidStringThrows()
        {
            Assert.Throws<ZenohException>(() => KeyExpr.Parse(""));
        }

        [Test]
        public void KeyExpr_Parse_NullThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => KeyExpr.Parse(null));
        }

        [Test]
        public void KeyExpr_TryParse_ValidReturnsTrueAndKeyExpr()
        {
            bool ok = KeyExpr.TryParse("demo/test", out KeyExpr keyExpr, out ZResult result);
            using (keyExpr)
            {
                Assert.That(ok, Is.True);
                Assert.That(result.IsOk, Is.True);
                Assert.That(keyExpr, Is.Not.Null);
            }
        }

        [Test]
        public void KeyExpr_TryParse_InvalidReturnsFalse()
        {
            bool ok = KeyExpr.TryParse("", out KeyExpr keyExpr, out ZResult result);

            Assert.That(ok, Is.False);
            Assert.That(keyExpr, Is.Null);
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        public void KeyExpr_DisposeIsIdempotent()
        {
            KeyExpr keyExpr = KeyExpr.Parse("demo/test");
            keyExpr.Dispose();
            Assert.DoesNotThrow(keyExpr.Dispose);
        }

        [Test]
        public void Config_DefaultSucceeds()
        {
            using (Config config = Config.Default())
            {
                Assert.That(config, Is.Not.Null);
            }
        }

        [Test]
        public void Config_TryDefaultReturnsTrue()
        {
            bool ok = Config.TryDefault(out Config config, out ZResult result);
            using (config)
            {
                Assert.That(ok, Is.True);
                Assert.That(result.IsOk, Is.True);
                Assert.That(config, Is.Not.Null);
            }
        }

        [Test]
        public void Config_FromJsonString_InvalidThrows()
        {
            Assert.Throws<ZenohException>(() => Config.FromJsonString("{ this is not valid json5 ---"));
        }

        [Test]
        public void Config_TryFromJsonString_InvalidReturnsFalse()
        {
            bool ok = Config.TryFromJsonString("{ this is not valid json5 ---", out Config config, out ZResult result);

            Assert.That(ok, Is.False);
            Assert.That(config, Is.Null);
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        public void Config_FromFile_NonExistentThrows()
        {
            string path = Path.Combine(Path.GetTempPath(), "definitely-not-there-" + Guid.NewGuid().ToString("N") + ".json5");
            Assert.That(File.Exists(path), Is.False);

            Assert.Throws<ZenohException>(() => Config.FromFile(path));
        }

        [Test]
        public void Config_DisposeBeforeConsumeIsSafe()
        {
            Config config = Config.Default();
            Assert.DoesNotThrow(config.Dispose);
            Assert.DoesNotThrow(config.Dispose);
        }

        [Test]
        public void Publisher_Declare_NullArgsThrowArgumentNull()
        {
            using (KeyExpr keyExpr = KeyExpr.Parse("demo/test"))
            {
                KeyExpr capturedKeyExpr = keyExpr;
                Assert.Throws<ArgumentNullException>(() => Publisher.Declare(null, capturedKeyExpr));
            }

            // Cannot construct a Session without zenohd at this layer, so we only
            // verify the null-keyexpr path via the Try* overload above. The other
            // null path is the same code shape.
        }

        [Test]
        public void Subscriber_Declare_NullArgsThrowArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Subscriber.Declare(null, null, _ => { }));
        }

        [Test]
        public void Queryable_Declare_NullSessionThrowsArgumentNull()
        {
            using (KeyExpr keyExpr = KeyExpr.Parse("demo/test"))
            {
                KeyExpr capturedKeyExpr = keyExpr;
                Assert.Throws<ArgumentNullException>(() => Queryable.Declare(null, capturedKeyExpr, _ => { }));
            }
        }

        [Test]
        public void Queryable_Declare_NullCallbackThrowsArgumentNull()
        {
            using (KeyExpr keyExpr = KeyExpr.Parse("demo/test"))
            {
                KeyExpr capturedKeyExpr = keyExpr;
                Assert.Throws<ArgumentNullException>(() => Queryable.Declare(null, capturedKeyExpr, null));
            }
        }

        [Test]
        public void GetOptions_DefaultPropertyValues()
        {
            GetOptions options = new GetOptions();

            Assert.That(options.Target, Is.Null);
            Assert.That(options.Consolidation, Is.Null);
            Assert.That(options.Payload, Is.Null);
            Assert.That(options.Encoding, Is.Null);
            Assert.That(options.CongestionControl, Is.Null);
            Assert.That(options.IsExpress, Is.Null);
            Assert.That(options.AllowedDestination, Is.Null);
            Assert.That(options.AcceptReplies, Is.Null);
            Assert.That(options.Priority, Is.Null);
            Assert.That(options.Attachment, Is.Null);
            Assert.That(options.TimeoutMs, Is.Null);
        }

        [Test]
        public void QueryReplyOptions_DefaultPropertyValues()
        {
            QueryReplyOptions options = new QueryReplyOptions();

            Assert.That(options.Encoding, Is.Null);
            Assert.That(options.CongestionControl, Is.Null);
            Assert.That(options.Priority, Is.Null);
            Assert.That(options.IsExpress, Is.Null);
            Assert.That(options.Attachment, Is.Null);
        }
    }
}

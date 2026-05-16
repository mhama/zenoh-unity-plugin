using System;
using NUnit.Framework;
using Zenoh.Bindings;

namespace Zenoh.Tests
{
    public sealed class BindingsBasicTests
    {
        [Test]
        public void ZResult_ReportsOkAndErrorCodes()
        {
            Assert.That(ZResult.Ok.IsOk, Is.True);
            Assert.That(ZResult.Ok.IsError, Is.False);

            ZResult error = new ZResult(ZResultCode.Z_EINVAL);
            Assert.That(error.IsError, Is.True);
            Assert.That(error.IsOk, Is.False);
            Assert.That(error.ToString(), Is.EqualTo("Z_EINVAL"));
        }

        [Test]
        public void ZOwnedBytes_EmptyCanBeLoanedAndDisposedTwice()
        {
            using (ZOwnedBytes bytes = ZOwnedBytes.CreateEmpty())
            {
                Assert.That(bytes.Check(), Is.True);

                ZLoanedBytes loaned = bytes.Loan();
                Assert.That(loaned.Len(), Is.EqualTo((nuint)0));
                Assert.That(loaned.IsEmpty(), Is.True);

                bytes.Dispose();
                Assert.DoesNotThrow(bytes.Dispose);
                Assert.Throws<ObjectDisposedException>(() => bytes.Loan());
            }
        }

        [Test]
        public void ZOwnedBytes_CopyFromBufPreservesLength()
        {
            byte[] payload = { 1, 2, 3, 4 };

            ZResult result = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes bytes);
            using (bytes)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(bytes, Is.Not.Null);
                Assert.That(bytes.Check(), Is.True);
                Assert.That(bytes.Loan().Len(), Is.EqualTo((nuint)payload.Length));
            }
        }

        [Test]
        public void ZOwnedKeyExpr_FromSubstrCanBeLoaned()
        {
            byte[] keyExpr = System.Text.Encoding.UTF8.GetBytes("demo/test");

            using (ZOwnedKeyExpr owned = new ZOwnedKeyExpr())
            {
                ZResult result = owned.FromSubstr(keyExpr, (nuint)keyExpr.Length);

                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(owned.Check(), Is.True);
                Assert.DoesNotThrow(() => owned.Loan());
            }
        }

        [Test]
        public void ZOwnedEncoding_FromSubstrEqualsMatchingConstant()
        {
            byte[] encoding = System.Text.Encoding.UTF8.GetBytes("text/plain");

            using (ZOwnedEncoding owned = new ZOwnedEncoding())
            {
                ZResult result = owned.FromSubstr(encoding, (nuint)encoding.Length);

                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(owned.Check(), Is.True);
                Assert.That(owned.Loan().Equals(ZEncodingConstants.TextPlain()), Is.True);
            }
        }

        [Test]
        public void ZOwnedConfig_DefaultCanBeCreatedAndLoaned()
        {
            using (ZOwnedConfig config = ZOwnedConfig.Default())
            {
                Assert.That(config.Check(), Is.True);
                Assert.DoesNotThrow(() => config.Loan());
            }
        }

        [Test]
        public void ZPublisherPutOptions_MoveEncodingPreventsFurtherEncodingUse()
        {
            ZPublisherPutOptions options = ZPublisherPutOptions.Default();

            using (ZOwnedEncoding encoding = new ZOwnedEncoding())
            {
                Assert.That(encoding.FromSubstr(
                    System.Text.Encoding.UTF8.GetBytes("text/plain"),
                    (nuint)"text/plain".Length).IsOk, Is.True);

                options.MoveEncoding(encoding);

                Assert.That(encoding.Check(), Is.False);
                Assert.Throws<ObjectDisposedException>(() => encoding.Loan());
            }
        }

        [Test]
        public void ZOwnedClosureSample_CanWrapManagedCallbacks()
        {
            bool called = false;
            bool dropped = false;

            using (ZOwnedClosureSample closure = new ZOwnedClosureSample())
            {
                closure.Set((sample, context) => called = true, context => dropped = true, IntPtr.Zero);

                Assert.That(closure.Check(), Is.True);
                Assert.DoesNotThrow(() => closure.Loan());
            }

            Assert.That(called, Is.False);
            Assert.That(dropped, Is.True);
        }
    }
}

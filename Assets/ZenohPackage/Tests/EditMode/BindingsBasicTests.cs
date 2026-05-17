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

            ZResult channelStatus = new ZResult(ZResultCode.Z_CHANNEL_NODATA);
            Assert.That(channelStatus.IsChannelStatus, Is.True);
            Assert.That(channelStatus.IsError, Is.False);
        }

        [Test]
        public void ZOwnedBytes_EmptyCanBeLoanedAndDisposedTwice()
        {
            using (ZOwnedBytes bytes = ZOwnedBytes.CreateEmpty())
            {
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

            ZResult result = ZOwnedKeyExpr.TryCreateFromSubstr(keyExpr, (nuint)keyExpr.Length, out ZOwnedKeyExpr owned);
            using (owned)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(owned.Check(), Is.True);
                Assert.DoesNotThrow(() => owned.Loan());
            }
        }

        [Test]
        public void ZOwnedEncoding_FromSubstrEqualsMatchingConstant()
        {
            byte[] encoding = System.Text.Encoding.UTF8.GetBytes("text/plain");

            ZResult result = ZOwnedEncoding.TryCreateFromSubstr(encoding, (nuint)encoding.Length, out ZOwnedEncoding owned);
            using (owned)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(owned.Check(), Is.True);
                Assert.That(owned.Loan().Equals(ZEncodingConstants.TextPlain()), Is.True);
            }
        }

        [Test]
        public void ZOwnedConfig_DefaultCanBeCreatedAndLoaned()
        {
            ZResult result = ZOwnedConfig.TryCreateDefault(out ZOwnedConfig config);
            using (config)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(config.Check(), Is.True);
                Assert.DoesNotThrow(() => config.Loan());
            }
        }

        [Test]
        public void ZPublisherPutOptions_MoveEncodingPreventsFurtherEncodingUse()
        {
            ZResult result = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            using (ZPublisherPutOptions options = ZPublisherPutOptions.Default())
            using (encoding)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());

                options.MoveEncoding(encoding);

                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => encoding.Loan());
                Assert.DoesNotThrow(() => encoding.Dispose());
            }
        }

        [Test]
        public void ZPublisherPutOptions_AdoptsMovedFieldsUntilDisposed()
        {
            byte[] payload = { 1, 2, 3 };
            ZResult attachmentResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes attachment);
            Assert.That(attachmentResult.IsOk, Is.True, attachmentResult.ToString());

            ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

            using (ZPublisherPutOptions options = ZPublisherPutOptions.Default())
            {
                options.MoveAttachment(attachment);
                options.MoveEncoding(encoding);

                Assert.Throws<ObjectDisposedException>(() => attachment.Check());
                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => attachment.Loan());
                Assert.Throws<ObjectDisposedException>(() => encoding.Loan());

                Assert.DoesNotThrow(() => attachment.Dispose());
                Assert.DoesNotThrow(() => encoding.Dispose());
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

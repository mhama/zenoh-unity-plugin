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
        public void ZPublisherPutOptions_SetEncodingPreventsFurtherEncodingUse()
        {
            ZResult result = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            using (ZPublisherPutOptions options = ZPublisherPutOptions.Default())
            using (encoding)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());

                options.SetEncoding(encoding);

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
                options.SetAttachment(attachment);
                options.SetEncoding(encoding);

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

        [Test]
        public void ZOwnedClosureQuery_DropsManagedStateWhenDisposed()
        {
            bool dropped = false;

            using (ZOwnedClosureQuery closure = new ZOwnedClosureQuery())
            {
                closure.Set((query, context) => { }, context => dropped = true, IntPtr.Zero);

                Assert.That(closure.Check(), Is.True);
                Assert.DoesNotThrow(() => closure.Loan());
            }

            Assert.That(dropped, Is.True);
        }

        [Test]
        public void ZOwnedClosureReply_DropsManagedStateWhenDisposed()
        {
            bool dropped = false;

            using (ZOwnedClosureReply closure = new ZOwnedClosureReply())
            {
                closure.Set((reply, context) => { }, context => dropped = true, IntPtr.Zero);

                Assert.That(closure.Check(), Is.True);
                Assert.DoesNotThrow(() => closure.Loan());
            }

            Assert.That(dropped, Is.True);
        }

        [Test]
        public void ZOwnedQueryable_GravestoneCheckIsFalse()
        {
            using (ZOwnedQueryable queryable = new ZOwnedQueryable())
            {
                Assert.That(queryable.Check(), Is.False);
            }
        }

        [Test]
        public void ZQueryableOptions_AcceptsCompleteAndAllowedOrigin()
        {
            ZQueryableOptions options = ZQueryableOptions.Default();

            Assert.DoesNotThrow(() => options.SetComplete(true));
            Assert.DoesNotThrow(() => options.SetAllowedOrigin(ZLocality.Remote));
        }

        [Test]
        public void ZGetOptions_DefaultExposesQueryShape()
        {
            using (ZGetOptions options = ZGetOptions.Default())
            {
                Assert.DoesNotThrow(() => options.SetTarget(ZQueryTarget.AllComplete));
                Assert.DoesNotThrow(() => options.SetConsolidation(ZQueryConsolidation.Latest()));
                Assert.DoesNotThrow(() => options.SetAcceptReplies(ZReplyKeyExpr.Any));
                Assert.DoesNotThrow(() => options.SetAllowedDestination(ZLocality.SessionLocal));
                Assert.DoesNotThrow(() => options.SetIsExpress(true));
                Assert.DoesNotThrow(() => options.SetTimeoutMs(1000));
            }
        }

        [Test]
        public void ZGetOptions_AdoptsMovedFieldsUntilDisposed()
        {
            byte[] payloadBytes = { 9, 8, 7 };
            ZResult payloadResult = ZOwnedBytes.TryCreateCopyFromBuf(payloadBytes, (nuint)payloadBytes.Length, out ZOwnedBytes payload);
            Assert.That(payloadResult.IsOk, Is.True, payloadResult.ToString());

            ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

            ZResult attachmentResult = ZOwnedBytes.TryCreateCopyFromBuf(payloadBytes, (nuint)payloadBytes.Length, out ZOwnedBytes attachment);
            Assert.That(attachmentResult.IsOk, Is.True, attachmentResult.ToString());

            using (ZGetOptions options = ZGetOptions.Default())
            {
                options.SetPayload(payload);
                options.SetEncoding(encoding);
                options.SetAttachment(attachment);

                Assert.Throws<ObjectDisposedException>(() => payload.Check());
                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => attachment.Check());

                Assert.DoesNotThrow(() => payload.Dispose());
                Assert.DoesNotThrow(() => encoding.Dispose());
                Assert.DoesNotThrow(() => attachment.Dispose());
            }
        }

        [Test]
        public void ZQueryConsolidation_FactoriesReportCorrespondingModes()
        {
            Assert.That(ZQueryConsolidation.None().Mode, Is.EqualTo(ZConsolidationMode.None));
            Assert.That(ZQueryConsolidation.Latest().Mode, Is.EqualTo(ZConsolidationMode.Latest));
            Assert.That(ZQueryConsolidation.Monotonic().Mode, Is.EqualTo(ZConsolidationMode.Monotonic));
            Assert.That(ZQueryConsolidation.Auto().Mode, Is.EqualTo(ZConsolidationMode.Auto));
            Assert.That(ZQueryConsolidation.Default().Mode, Is.EqualTo(ZConsolidationMode.Auto));
        }

        [Test]
        public void ZQueryReplyOptions_SetEncodingPreventsFurtherEncodingUse()
        {
            ZResult result = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            using (ZQueryReplyOptions options = ZQueryReplyOptions.Default())
            using (encoding)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());

                options.SetEncoding(encoding);

                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => encoding.Loan());
                Assert.DoesNotThrow(() => encoding.Dispose());
            }
        }

        [Test]
        public void ZQueryReplyErrOptions_SetEncodingAdoptsMovedEncoding()
        {
            ZResult result = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            using (ZQueryReplyErrOptions options = ZQueryReplyErrOptions.Default())
            using (encoding)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());

                options.SetEncoding(encoding);

                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
            }
        }

        [Test]
        public void ZQueryReplyDelOptions_SetAttachmentAdoptsMovedBytes()
        {
            byte[] payload = { 1, 2, 3 };
            ZResult result = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes attachment);
            using (ZQueryReplyDelOptions options = ZQueryReplyDelOptions.Default())
            using (attachment)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());

                options.SetIsExpress(true);
                options.SetAttachment(attachment);

                Assert.Throws<ObjectDisposedException>(() => attachment.Check());
            }
        }
    }
}

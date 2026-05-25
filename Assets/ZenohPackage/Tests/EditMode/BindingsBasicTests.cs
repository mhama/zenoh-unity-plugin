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
        public void ZReplyKeyExpr_DefaultMatchesNative()
        {
            Zenoh.Plugins.z_reply_keyexpr_t nativeDefault = Zenoh.Plugins.ZenohNative.z_reply_keyexpr_default();
            Assert.That((uint)nativeDefault, Is.EqualTo((uint)ZReplyKeyExpr.MatchingQuery));
        }

        [Test]
        public void ZQueryTarget_DefaultMatchesNative()
        {
            Zenoh.Plugins.z_query_target_t nativeDefault = Zenoh.Plugins.ZenohNative.z_query_target_default();
            Assert.That((uint)nativeDefault, Is.EqualTo((uint)ZQueryTarget.BestMatching));
        }

        [Test]
        public void ZQueryConsolidation_FactoriesReturnCorrectModes()
        {
            Assert.That(ZQueryConsolidation.Default().Mode, Is.EqualTo(ZConsolidationMode.Auto));
            Assert.That(ZQueryConsolidation.Auto().Mode, Is.EqualTo(ZConsolidationMode.Auto));
            Assert.That(ZQueryConsolidation.Latest().Mode, Is.EqualTo(ZConsolidationMode.Latest));
            Assert.That(ZQueryConsolidation.Monotonic().Mode, Is.EqualTo(ZConsolidationMode.Monotonic));
            Assert.That(ZQueryConsolidation.None().Mode, Is.EqualTo(ZConsolidationMode.None));
        }

        [Test]
        public void ZQueryReplyOptions_SetEncodingAndAttachmentMakeSourceInert()
        {
            ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

            byte[] payload = { 1, 2, 3 };
            ZResult attachmentResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes attachment);
            Assert.That(attachmentResult.IsOk, Is.True, attachmentResult.ToString());

            using (ZQueryReplyOptions options = ZQueryReplyOptions.Default())
            {
                options.SetEncoding(encoding);
                options.SetAttachment(attachment);

                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => attachment.Check());

                Assert.DoesNotThrow(() => encoding.Dispose());
                Assert.DoesNotThrow(() => attachment.Dispose());
            }

            Assert.DoesNotThrow(() => encoding.Dispose());
            Assert.DoesNotThrow(() => attachment.Dispose());
        }

        [Test]
        public void ZQueryReplyErrOptions_SetEncodingMakesSourceInert()
        {
            ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("text/plain"),
                (nuint)"text/plain".Length,
                out ZOwnedEncoding encoding);
            Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

            using (ZQueryReplyErrOptions options = ZQueryReplyErrOptions.Default())
            {
                options.SetEncoding(encoding);
                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.DoesNotThrow(() => encoding.Dispose());
            }
        }

        [Test]
        public void ZQueryReplyDelOptions_SetAttachmentMakesSourceInert()
        {
            byte[] payload = { 1, 2, 3 };
            ZResult attachmentResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes attachment);
            Assert.That(attachmentResult.IsOk, Is.True, attachmentResult.ToString());

            using (ZQueryReplyDelOptions options = ZQueryReplyDelOptions.Default())
            {
                options.SetAttachment(attachment);
                Assert.Throws<ObjectDisposedException>(() => attachment.Check());
                Assert.DoesNotThrow(() => attachment.Dispose());
            }
        }

        [Test]
        public void ZGetOptions_AllSettersSmokeTest()
        {
            byte[] payload = { 10, 20 };
            ZResult payloadResult = ZOwnedBytes.TryCreateCopyFromBuf(payload, (nuint)payload.Length, out ZOwnedBytes payloadBytes);
            Assert.That(payloadResult.IsOk, Is.True, payloadResult.ToString());

            ZResult encodingResult = ZOwnedEncoding.TryCreateFromSubstr(
                System.Text.Encoding.UTF8.GetBytes("application/json"),
                (nuint)"application/json".Length,
                out ZOwnedEncoding encoding);
            Assert.That(encodingResult.IsOk, Is.True, encodingResult.ToString());

            byte[] attachmentPayload = { 30 };
            ZResult attachmentResult = ZOwnedBytes.TryCreateCopyFromBuf(attachmentPayload, (nuint)attachmentPayload.Length, out ZOwnedBytes attachment);
            Assert.That(attachmentResult.IsOk, Is.True, attachmentResult.ToString());

            ZResult tokenResult = ZOwnedCancellationToken.TryCreateNew(out ZOwnedCancellationToken token);
            Assert.That(tokenResult.IsOk, Is.True, tokenResult.ToString());

            using (ZGetOptions options = ZGetOptions.Default())
            using (payloadBytes)
            using (encoding)
            using (attachment)
            using (token)
            {
                options.SetTarget(ZQueryTarget.All);
                options.SetConsolidation(ZQueryConsolidation.Latest());
                options.SetPayload(payloadBytes);
                options.SetEncoding(encoding);
                options.SetCongestionControl(ZCongestionControl.Block);
                options.SetIsExpress(true);
                options.SetAllowedDestination(ZLocality.Remote);
                options.SetAcceptReplies(ZReplyKeyExpr.Any);
                options.SetPriority(ZPriority.RealTime);
                options.SetAttachment(attachment);
                options.SetTimeoutMs(5000);
                options.SetCancellationToken(token);

                Assert.Throws<ObjectDisposedException>(() => payloadBytes.Check());
                Assert.Throws<ObjectDisposedException>(() => encoding.Check());
                Assert.Throws<ObjectDisposedException>(() => attachment.Check());
                Assert.Throws<ObjectDisposedException>(() => token.Check());
            }
        }

        [Test]
        public void ZOwnedCancellationToken_LifecycleAndCancel()
        {
            ZResult result = ZOwnedCancellationToken.TryCreateNew(out ZOwnedCancellationToken token);
            using (token)
            {
                Assert.That(result.IsOk, Is.True, result.ToString());
                Assert.That(token.Check(), Is.True);

                ZLoanedCancellationToken loaned = token.Loan();
                Assert.That(loaned.IsCancelled(), Is.False);

                ZResult cancelResult = loaned.Cancel();
                Assert.That(cancelResult.IsOk, Is.True, cancelResult.ToString());
                Assert.That(loaned.IsCancelled(), Is.True);
            }
        }

        [Test]
        public void ZQueryableOptions_DefaultDoesNotRequireDispose()
        {
            using (ZQueryableOptions options = ZQueryableOptions.Default())
            {
                Assert.That(options, Is.Not.Null);
            }
        }

        [Test]
        public void ZOwnedClosureQuery_CanWrapManagedCallbacks()
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
        public void ZOwnedClosureReply_CanWrapManagedCallbacks()
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
    }
}

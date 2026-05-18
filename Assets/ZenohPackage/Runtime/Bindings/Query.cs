using System;
using Zenoh.Bindings.Internal;
using Zenoh.Plugins;

namespace Zenoh.Bindings
{
    /// <summary>
    /// Public projection of <c>z_reply_keyexpr_t</c>.
    /// </summary>
    public enum ZReplyKeyExpr : uint
    {
        Any = 0,
        MatchingQuery = 1,
    }

    public unsafe sealed class ZQueryReplyOptions : IDisposable
    {
        internal z_query_reply_options_t Native;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private OwnedStorage<z_owned_bytes_t> adoptedAttachment;
        private bool disposed;

        public static ZQueryReplyOptions Default()
        {
            ZQueryReplyOptions options = new ZQueryReplyOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ZenohNative.z_query_reply_options_default(ptr);
            }
        }

        public void SetEncoding(ZOwnedEncoding movingEncoding)
        {
            if (movingEncoding == null)
                throw new ArgumentNullException(nameof(movingEncoding));
            ThrowIfDisposed();

            OwnedStorage<z_owned_encoding_t> storage = movingEncoding.Take();
            ReleaseEncoding();
            adoptedEncoding = storage;

            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        public void SetEncoding(ZLoanedEncoding loanedEncoding)
        {
            if (loanedEncoding == null)
                throw new ArgumentNullException(nameof(loanedEncoding));
            ThrowIfDisposed();

            SetEncoding(ZOwnedEncoding.CreateClone(loanedEncoding));
        }

        public void SetAttachment(ZOwnedBytes movingAttachment)
        {
            if (movingAttachment == null)
                throw new ArgumentNullException(nameof(movingAttachment));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingAttachment.Take();
            ReleaseAttachment();
            adoptedAttachment = storage;

            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)adoptedAttachment.Pointer;
            }
        }

        public void SetIsExpress(bool isExpress)
        {
            ThrowIfDisposed();
            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ptr->is_express = isExpress;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZQueryReplyOptions));
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ptr->encoding = null;
            }
        }

        private void ReleaseAttachment()
        {
            if (adoptedAttachment == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedAttachment.Pointer);
            adoptedAttachment.Dispose();
            adoptedAttachment = null;

            fixed (z_query_reply_options_t* ptr = &Native)
            {
                ptr->attachment = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleaseEncoding();
            ReleaseAttachment();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe sealed class ZQueryReplyErrOptions : IDisposable
    {
        internal z_query_reply_err_options_t Native;
        private OwnedStorage<z_owned_encoding_t> adoptedEncoding;
        private bool disposed;

        public static ZQueryReplyErrOptions Default()
        {
            ZQueryReplyErrOptions options = new ZQueryReplyErrOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_query_reply_err_options_t* ptr = &Native)
            {
                ZenohNative.z_query_reply_err_options_default(ptr);
            }
        }

        public void SetEncoding(ZOwnedEncoding movingEncoding)
        {
            if (movingEncoding == null)
                throw new ArgumentNullException(nameof(movingEncoding));
            ThrowIfDisposed();

            OwnedStorage<z_owned_encoding_t> storage = movingEncoding.Take();
            ReleaseEncoding();
            adoptedEncoding = storage;

            fixed (z_query_reply_err_options_t* ptr = &Native)
            {
                ptr->encoding = (z_moved_encoding_t*)adoptedEncoding.Pointer;
            }
        }

        public void SetEncoding(ZLoanedEncoding loanedEncoding)
        {
            if (loanedEncoding == null)
                throw new ArgumentNullException(nameof(loanedEncoding));
            ThrowIfDisposed();

            SetEncoding(ZOwnedEncoding.CreateClone(loanedEncoding));
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZQueryReplyErrOptions));
        }

        private void ReleaseEncoding()
        {
            if (adoptedEncoding == null)
                return;

            ZenohNative.z_encoding_drop((z_moved_encoding_t*)adoptedEncoding.Pointer);
            adoptedEncoding.Dispose();
            adoptedEncoding = null;

            fixed (z_query_reply_err_options_t* ptr = &Native)
            {
                ptr->encoding = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleaseEncoding();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe sealed class ZQueryReplyDelOptions : IDisposable
    {
        internal z_query_reply_del_options_t Native;
        private OwnedStorage<z_owned_bytes_t> adoptedAttachment;
        private bool disposed;

        public static ZQueryReplyDelOptions Default()
        {
            ZQueryReplyDelOptions options = new ZQueryReplyDelOptions();
            options.InitDefault();
            return options;
        }

        private void InitDefault()
        {
            ThrowIfDisposed();
            fixed (z_query_reply_del_options_t* ptr = &Native)
            {
                ZenohNative.z_query_reply_del_options_default(ptr);
            }
        }

        public void SetIsExpress(bool isExpress)
        {
            ThrowIfDisposed();
            fixed (z_query_reply_del_options_t* ptr = &Native)
            {
                ptr->is_express = isExpress;
            }
        }

        public void SetAttachment(ZOwnedBytes movingAttachment)
        {
            if (movingAttachment == null)
                throw new ArgumentNullException(nameof(movingAttachment));
            ThrowIfDisposed();

            OwnedStorage<z_owned_bytes_t> storage = movingAttachment.Take();
            ReleaseAttachment();
            adoptedAttachment = storage;

            fixed (z_query_reply_del_options_t* ptr = &Native)
            {
                ptr->attachment = (z_moved_bytes_t*)adoptedAttachment.Pointer;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZQueryReplyDelOptions));
        }

        private void ReleaseAttachment()
        {
            if (adoptedAttachment == null)
                return;

            ZenohNative.z_bytes_drop((z_moved_bytes_t*)adoptedAttachment.Pointer);
            adoptedAttachment.Dispose();
            adoptedAttachment = null;

            fixed (z_query_reply_del_options_t* ptr = &Native)
            {
                ptr->attachment = null;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            ReleaseAttachment();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public unsafe sealed class ZOwnedQuery : IDisposable
    {
        private z_owned_query_t* nativePtr;
        private bool disposed;
        private bool moved;

        private ZOwnedQuery()
        {
            nativePtr = NativeStorage.Alloc<z_owned_query_t>();
            ZenohNative.z_internal_query_null(nativePtr);
        }

        public static ZOwnedQuery CreateClone(ZLoanedQuery source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedQuery value = new ZOwnedQuery();
            ZenohNative.z_query_clone(value.nativePtr, source.NativePointer);
            return value;
        }

        public static ZOwnedQuery CreateTakeFromLoaned(ZLoanedQuery source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ZOwnedQuery value = new ZOwnedQuery();
            ZenohNative.z_query_take_from_loaned(value.nativePtr, source.NativePointer);
            return value;
        }

        public ZLoanedQuery Loan()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedQuery(ZenohNative.z_query_loan(nativePtr), this);
        }

        public ZLoanedQuery LoanMut()
        {
            ThrowIfDisposedOrMoved();
            return new ZLoanedQuery(ZenohNative.z_query_loan_mut(nativePtr), this);
        }

        public bool Check()
        {
            ThrowIfDisposedOrMoved();
            return ZenohNative.z_internal_query_check(nativePtr);
        }

        internal OwnedStorage<z_owned_query_t> Take()
        {
            ThrowIfDisposedOrMoved();
            moved = true;
            z_owned_query_t* ptr = nativePtr;
            nativePtr = null;
            return new OwnedStorage<z_owned_query_t>(ptr);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ZOwnedQuery));
        }

        private void ThrowIfDisposedOrMoved()
        {
            ThrowIfDisposed();
            if (moved)
                throw new ObjectDisposedException(nameof(ZOwnedQuery));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (nativePtr != null)
            {
                if (!moved && ZenohNative.z_internal_query_check(nativePtr))
                    ZenohNative.z_query_drop((z_moved_query_t*)nativePtr);

                NativeStorage.Free(nativePtr);
                nativePtr = null;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ZOwnedQuery()
        {
            Dispose();
        }
    }

    public unsafe sealed class ZLoanedQuery
    {
        private readonly object owner;
        internal readonly z_loaned_query_t* NativePointer;

        internal ZLoanedQuery(z_loaned_query_t* nativePtr, object owner)
        {
            NativePointer = nativePtr;
            this.owner = owner;
        }

        public ZLoanedKeyExpr KeyExpr()
        {
            return new ZLoanedKeyExpr(ZenohNative.z_query_keyexpr(NativePointer), this);
        }

        /// <summary>
        /// Returns the query's parameter selector segment as raw UTF-8 bytes (without
        /// any null terminator). Returns an empty array when the query has no parameters.
        /// </summary>
        public byte[] Parameters()
        {
            z_view_string_t view = default;
            ZenohNative.z_query_parameters(NativePointer, &view);
            z_loaned_string_t* loaned = ZenohNative.z_view_string_loan(&view);
            nuint len = ZenohNative.z_string_len(loaned);
            if (len == 0)
                return Array.Empty<byte>();

            byte* data = ZenohNative.z_string_data(loaned);
            byte[] copy = new byte[checked((int)len)];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = data[i];
            return copy;
        }

        public ZLoanedBytes Payload()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_query_payload(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public ZLoanedBytes PayloadMut()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_query_payload_mut(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public ZLoanedEncoding Encoding()
        {
            z_loaned_encoding_t* ptr = ZenohNative.z_query_encoding(NativePointer);
            return ptr == null ? null : new ZLoanedEncoding(ptr, this);
        }

        public ZLoanedBytes Attachment()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_query_attachment(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public ZLoanedBytes AttachmentMut()
        {
            z_loaned_bytes_t* ptr = ZenohNative.z_query_attachment_mut(NativePointer);
            return ptr == null ? null : new ZLoanedBytes(ptr, this);
        }

        public ZLoanedSourceInfo SourceInfo()
        {
            z_source_info_t* ptr = ZenohNative.z_query_source_info(NativePointer);
            return ptr == null ? null : new ZLoanedSourceInfo(ptr, this);
        }

        public ZReplyKeyExpr AcceptsReplies()
        {
            return (ZReplyKeyExpr)ZenohNative.z_query_accepts_replies(NativePointer);
        }

        public ZResult Reply(ZLoanedKeyExpr keyExpr, ZOwnedBytes movingPayload, ZQueryReplyOptions options)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (movingPayload == null)
                throw new ArgumentNullException(nameof(movingPayload));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            options.ThrowIfDisposed();

            using (OwnedStorage<z_owned_bytes_t> payloadStorage = movingPayload.Take())
            {
                fixed (z_query_reply_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_query_reply(
                        NativePointer,
                        keyExpr.NativePointer,
                        (z_moved_bytes_t*)payloadStorage.Pointer,
                        optionsPtr));
                }
            }
        }

        public ZResult ReplyErr(ZOwnedBytes movingPayload, ZQueryReplyErrOptions options)
        {
            if (movingPayload == null)
                throw new ArgumentNullException(nameof(movingPayload));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            options.ThrowIfDisposed();

            using (OwnedStorage<z_owned_bytes_t> payloadStorage = movingPayload.Take())
            {
                fixed (z_query_reply_err_options_t* optionsPtr = &options.Native)
                {
                    return new ZResult(ZenohNative.z_query_reply_err(
                        NativePointer,
                        (z_moved_bytes_t*)payloadStorage.Pointer,
                        optionsPtr));
                }
            }
        }

        public ZResult ReplyDel(ZLoanedKeyExpr keyExpr, ZQueryReplyDelOptions options)
        {
            if (keyExpr == null)
                throw new ArgumentNullException(nameof(keyExpr));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            options.ThrowIfDisposed();

            fixed (z_query_reply_del_options_t* optionsPtr = &options.Native)
            {
                return new ZResult(ZenohNative.z_query_reply_del(
                    NativePointer,
                    keyExpr.NativePointer,
                    optionsPtr));
            }
        }
    }
}

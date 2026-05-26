using System;
using System.Runtime.InteropServices;
using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Wrapper for z_owned_encoding_t native type

    public unsafe class Encoding : IDisposable
    {
        private z_owned_encoding_t* nativePtr;
        private bool disposed = false;

        public Encoding()
        {
            // Allocate memory for the native encoding
            nativePtr = (z_owned_encoding_t*)Marshal.AllocHGlobal(sizeof(z_owned_encoding_t));
        }

        // Constructor that takes ownership of an existing native pointer
        internal Encoding(z_owned_encoding_t* ptr)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
                
            nativePtr = ptr;
            disposed = false;
        }

        // Create from loaned encoding
        internal static Encoding FromLoaned(z_loaned_encoding_t* loanedEncoding)
        {
            if (loanedEncoding == null)
                throw new ArgumentNullException(nameof(loanedEncoding));

            Encoding encoding = new Encoding();
            ZenohNative.z_encoding_clone(encoding.nativePtr, loanedEncoding);
            return encoding;
        }

        #region MIME Types

        // Zenoh-specific encodings
        public static Encoding ZenohBytes()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_zenoh_bytes();
            return FromLoaned(encoding);
        }

        public static Encoding ZenohString()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_zenoh_string();
            return FromLoaned(encoding);
        }

        public static Encoding ZenohSerialized()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_zenoh_serialized();
            return FromLoaned(encoding);
        }

        // Application types
        public static Encoding ApplicationOctetStream()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_octet_stream();
            return FromLoaned(encoding);
        }

        public static Encoding TextPlain()
        {
            z_loaned_encoding_t* textPlainEncoding = ZenohNative.z_encoding_text_plain();
            return FromLoaned(textPlainEncoding);
        }

        public static Encoding ApplicationJson()
        {
            z_loaned_encoding_t* jsonEncoding = ZenohNative.z_encoding_application_json();
            return FromLoaned(jsonEncoding);
        }

        public static Encoding TextJson()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_json();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationCdr()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_cdr();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationCbor()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_cbor();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationYaml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_yaml();
            return FromLoaned(encoding);
        }

        public static Encoding TextYaml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_yaml();
            return FromLoaned(encoding);
        }

        public static Encoding TextJson5()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_json5();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationPythonSerializedObject()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_python_serialized_object();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationProtobuf()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_protobuf();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationJavaSerializedObject()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_java_serialized_object();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationOpenMetricsText()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_openmetrics_text();
            return FromLoaned(encoding);
        }

        // Image types
        public static Encoding ImagePng()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_image_png();
            return FromLoaned(encoding);
        }

        public static Encoding ImageJpeg()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_image_jpeg();
            return FromLoaned(encoding);
        }

        public static Encoding ImageGif()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_image_gif();
            return FromLoaned(encoding);
        }

        public static Encoding ImageBmp()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_image_bmp();
            return FromLoaned(encoding);
        }

        public static Encoding ImageWebp()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_image_webp();
            return FromLoaned(encoding);
        }

        // Application XML formats
        public static Encoding ApplicationXml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_xml();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationFormUrlEncoded()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_x_www_form_urlencoded();
            return FromLoaned(encoding);
        }

        // Text formats
        public static Encoding TextHtml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_html();
            return FromLoaned(encoding);
        }

        public static Encoding TextXml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_xml();
            return FromLoaned(encoding);
        }

        public static Encoding TextCss()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_css();
            return FromLoaned(encoding);
        }

        public static Encoding TextJavaScript()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_javascript();
            return FromLoaned(encoding);
        }

        public static Encoding TextMarkdown()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_markdown();
            return FromLoaned(encoding);
        }

        public static Encoding TextCsv()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_text_csv();
            return FromLoaned(encoding);
        }

        // Database and API formats
        public static Encoding ApplicationSql()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_sql();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationCoapPayload()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_coap_payload();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationJsonPatchJson()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_json_patch_json();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationJsonSeq()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_json_seq();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationJsonPath()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_jsonpath();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationJwt()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_jwt();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationMp4()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_mp4();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationSoapXml()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_soap_xml();
            return FromLoaned(encoding);
        }

        public static Encoding ApplicationYang()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_application_yang();
            return FromLoaned(encoding);
        }

        // Audio formats
        public static Encoding AudioAac()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_audio_aac();
            return FromLoaned(encoding);
        }

        public static Encoding AudioFlac()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_audio_flac();
            return FromLoaned(encoding);
        }

        public static Encoding AudioMp4()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_audio_mp4();
            return FromLoaned(encoding);
        }

        public static Encoding AudioOgg()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_audio_ogg();
            return FromLoaned(encoding);
        }

        public static Encoding AudioVorbis()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_audio_vorbis();
            return FromLoaned(encoding);
        }

        // Video formats
        public static Encoding VideoH261()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_h261();
            return FromLoaned(encoding);
        }

        public static Encoding VideoH263()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_h263();
            return FromLoaned(encoding);
        }

        public static Encoding VideoH264()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_h264();
            return FromLoaned(encoding);
        }

        public static Encoding VideoH265()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_h265();
            return FromLoaned(encoding);
        }

        public static Encoding VideoH266()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_h266();
            return FromLoaned(encoding);
        }

        public static Encoding VideoMp4()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_mp4();
            return FromLoaned(encoding);
        }

        public static Encoding VideoOgg()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_ogg();
            return FromLoaned(encoding);
        }

        public static Encoding VideoRaw()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_raw();
            return FromLoaned(encoding);
        }

        public static Encoding VideoVp8()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_vp8();
            return FromLoaned(encoding);
        }

        public static Encoding VideoVp9()
        {
            z_loaned_encoding_t* encoding = ZenohNative.z_encoding_video_vp9();
            return FromLoaned(encoding);
        }
        
        #endregion

        // Move ownership of the native pointer to the caller
        internal z_owned_encoding_t* Move()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Encoding));
                
            z_owned_encoding_t* result = nativePtr;
            nativePtr = null; // Relinquish ownership
            disposed = true;  // Mark as disposed
            return result;
        }

        ~Encoding()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && nativePtr != null)
            {
                ZenohNative.z_encoding_drop((z_moved_encoding_t*)nativePtr);
                Marshal.FreeHGlobal((IntPtr)nativePtr);
                nativePtr = null;
                disposed = true;
            }
        }

        // Expose native pointer if needed for advanced operations
        internal z_owned_encoding_t* NativePointer => nativePtr;
    }
}
using System;
using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public enum EncodingPreset
    {
        ZenohBytes,
        ZenohString,
        ZenohSerialized,
        ApplicationOctetStream,
        TextPlain,
        ApplicationJson,
        TextJson,
        TextJson5,
        ImagePng,
        ImageJpeg,
        ImageGif,
        ImageBmp,
        ImageWebp,
    }

    internal static class EncodingPresetExtensions
    {
        /// <summary>
        /// Returns the borrowed Zenoh-side encoding constant for this preset. This is
        /// a zero-allocation lookup; the loaned encoding can be passed to Bindings
        /// option setters that accept <see cref="ZLoanedEncoding"/> (which clone it
        /// into the option's owned storage).
        /// </summary>
        public static ZLoanedEncoding ToLoanedEncoding(this EncodingPreset preset)
        {
            switch (preset)
            {
                case EncodingPreset.ZenohBytes: return ZEncodingConstants.ZenohBytes();
                case EncodingPreset.ZenohString: return ZEncodingConstants.ZenohString();
                case EncodingPreset.ZenohSerialized: return ZEncodingConstants.ZenohSerialized();
                case EncodingPreset.ApplicationOctetStream: return ZEncodingConstants.ApplicationOctetStream();
                case EncodingPreset.TextPlain: return ZEncodingConstants.TextPlain();
                case EncodingPreset.ApplicationJson: return ZEncodingConstants.ApplicationJson();
                case EncodingPreset.TextJson: return ZEncodingConstants.TextJson();
                case EncodingPreset.TextJson5: return ZEncodingConstants.TextJson5();
                case EncodingPreset.ImagePng: return ZEncodingConstants.ImagePng();
                case EncodingPreset.ImageJpeg: return ZEncodingConstants.ImageJpeg();
                case EncodingPreset.ImageGif: return ZEncodingConstants.ImageGif();
                case EncodingPreset.ImageBmp: return ZEncodingConstants.ImageBmp();
                case EncodingPreset.ImageWebp: return ZEncodingConstants.ImageWebp();
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }
    }
}

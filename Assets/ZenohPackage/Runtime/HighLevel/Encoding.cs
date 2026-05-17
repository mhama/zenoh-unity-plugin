using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

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
        public static string ToMimeString(this EncodingPreset preset)
        {
            switch (preset)
            {
                case EncodingPreset.ZenohBytes: return "zenoh/bytes";
                case EncodingPreset.ZenohString: return "zenoh/string";
                case EncodingPreset.ZenohSerialized: return "zenoh/serialized";
                case EncodingPreset.ApplicationOctetStream: return "application/octet-stream";
                case EncodingPreset.TextPlain: return "text/plain";
                case EncodingPreset.ApplicationJson: return "application/json";
                case EncodingPreset.TextJson: return "text/json";
                case EncodingPreset.TextJson5: return "text/json5";
                case EncodingPreset.ImagePng: return "image/png";
                case EncodingPreset.ImageJpeg: return "image/jpeg";
                case EncodingPreset.ImageGif: return "image/gif";
                case EncodingPreset.ImageBmp: return "image/bmp";
                case EncodingPreset.ImageWebp: return "image/webp";
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        public static ZOwnedEncoding ToOwnedEncoding(this EncodingPreset preset)
        {
            byte[] buffer = Utf8Helper.EncodeNullTerminated(preset.ToMimeString());
            ZResult result = ZOwnedEncoding.TryCreateFromStr(buffer, out ZOwnedEncoding owned);
            result.ThrowIfErr($"Failed to build encoding for preset {preset}");
            return owned;
        }
    }
}

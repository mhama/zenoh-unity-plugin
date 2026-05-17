using System;
using Zenoh.Bindings;
using Zenoh.HighLevel.Internal;

namespace Zenoh.HighLevel
{
    public sealed class Config : IDisposable
    {
        private ZOwnedConfig owned;
        private bool consumed;

        private Config(ZOwnedConfig owned)
        {
            this.owned = owned;
        }

        public static Config Default()
        {
            ZResult result = ZOwnedConfig.TryCreateDefault(out ZOwnedConfig owned);
            result.ThrowIfErr("Config.Default failed");
            return new Config(owned);
        }

        public static bool TryDefault(out Config config, out ZResult result)
        {
            result = ZOwnedConfig.TryCreateDefault(out ZOwnedConfig owned);
            config = result.IsOk ? new Config(owned) : null;
            return result.IsOk;
        }

        public static Config FromJsonString(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(json);
            ZResult result = ZOwnedConfig.TryCreateFromStr(buffer, out ZOwnedConfig owned);
            result.ThrowIfErr("Config.FromJsonString failed");
            return new Config(owned);
        }

        public static bool TryFromJsonString(string json, out Config config, out ZResult result)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(json);
            result = ZOwnedConfig.TryCreateFromStr(buffer, out ZOwnedConfig owned);
            config = result.IsOk ? new Config(owned) : null;
            return result.IsOk;
        }

        public static Config FromFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(path);
            ZResult result = ZOwnedConfig.TryCreateFromFile(buffer, out ZOwnedConfig owned);
            result.ThrowIfErr("Config.FromFile failed");
            return new Config(owned);
        }

        public static bool TryFromFile(string path, out Config config, out ZResult result)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            byte[] buffer = Utf8Helper.EncodeNullTerminated(path);
            result = ZOwnedConfig.TryCreateFromFile(buffer, out ZOwnedConfig owned);
            config = result.IsOk ? new Config(owned) : null;
            return result.IsOk;
        }

        public static Config FromEnv()
        {
            ZResult result = ZOwnedConfig.TryCreateFromEnv(out ZOwnedConfig owned);
            result.ThrowIfErr("Config.FromEnv failed");
            return new Config(owned);
        }

        public static bool TryFromEnv(out Config config, out ZResult result)
        {
            result = ZOwnedConfig.TryCreateFromEnv(out ZOwnedConfig owned);
            config = result.IsOk ? new Config(owned) : null;
            return result.IsOk;
        }

        internal ZOwnedConfig ConsumeForOpen()
        {
            if (consumed)
                throw new InvalidOperationException("Config has already been consumed by Session.Open");
            if (owned == null)
                throw new ObjectDisposedException(nameof(Config));

            consumed = true;
            ZOwnedConfig taken = owned;
            owned = null;
            return taken;
        }

        public void Dispose()
        {
            if (consumed)
                return;

            owned?.Dispose();
            owned = null;
        }
    }
}

using System;
using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public class ZenohException : Exception
    {
        public ZResult Result { get; }

        public ZenohException(ZResult result, string message) : base(BuildMessage(result, message))
        {
            Result = result;
        }

        public ZenohException(ZResult result, string message, Exception innerException)
            : base(BuildMessage(result, message), innerException)
        {
            Result = result;
        }

        private static string BuildMessage(ZResult result, string message)
        {
            if (string.IsNullOrEmpty(message))
                return $"Zenoh operation failed: {result.ResultCode}";
            return $"{message} (result: {result.ResultCode})";
        }
    }
}

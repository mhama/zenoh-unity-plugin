using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public static class ZResultExtensions
    {
        public static void ThrowIfErr(this ZResult result, string message = null)
        {
            if (result.IsOk)
                return;
            throw new ZenohException(result, message);
        }
    }
}

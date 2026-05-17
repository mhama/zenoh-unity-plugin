using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    public enum ZResultCode
    {
        Z_XXXXXX = 100,
        Z_CHANNEL_DISCONNECTED = 1,
        Z_CHANNEL_NODATA = 2,
        Z_OK = 0,
        Z_EINVAL = -1,
        Z_EPARSE = -2,
        Z_EIO = -3,
        Z_ENETWORK = -4,
        Z_ENULL = -5,
        Z_EUNAVAILABLE = -6,
        Z_EDESERIALIZE = -7,
        Z_ESESSION_CLOSED = -8,
        Z_EUTF8 = -9,
        Z_EBUSY_MUTEX = -16,
        Z_EINVAL_MUTEX = -22,
        Z_EAGAIN_MUTEX = -11,
        Z_EPOISON_MUTEX = -22,
        Z_EGENERIC = -128,
    }

    public struct ZResult
    {
        public static readonly ZResult Ok = new ZResult();
        public ZResultCode resultCode;

        public bool IsError => resultCode != ZResultCode.Z_OK;
        public bool IsOk => resultCode == ZResultCode.Z_OK;

        internal ZResult(z_result_t result)
        {
            resultCode = (ZResultCode)result;
        }

        public ZResult(ZResultCode resultCode = ZResultCode.Z_OK)
        {
            this.resultCode = resultCode;
        }
    }
}
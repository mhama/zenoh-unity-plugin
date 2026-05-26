using Zenoh.Plugins;

namespace Zenoh.Legacy
{
    // Wrapper for z_loaned_sample_t* native type
    public unsafe class SampleRef
    {
        private readonly z_loaned_sample_t* nativeSample;

        internal SampleRef(z_loaned_sample_t* sample)
        {
            nativeSample = sample;
        }

        public BytesRef GetPayload()
        {
            return new BytesRef(ZenohNative.z_sample_payload(nativeSample));
        }

        public KeyExprRef GetKeyExprRef()
        {
            z_loaned_keyexpr_t* keyExpr = ZenohNative.z_sample_keyexpr(nativeSample);
            return new KeyExprRef(keyExpr);
        }
    }
}
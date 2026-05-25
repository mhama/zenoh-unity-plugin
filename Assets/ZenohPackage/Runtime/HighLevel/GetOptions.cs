using Zenoh.Bindings;

namespace Zenoh.HighLevel
{
    public sealed class GetOptions
    {
        public ZQueryTarget? Target { get; set; }
        public ZConsolidationMode? Consolidation { get; set; }
        public byte[] Payload { get; set; }
        public EncodingPreset? Encoding { get; set; }
        public ZCongestionControl? CongestionControl { get; set; }
        public bool? IsExpress { get; set; }
        public ZLocality? AllowedDestination { get; set; }
        public ZReplyKeyExpr? AcceptReplies { get; set; }
        public ZPriority? Priority { get; set; }
        public byte[] Attachment { get; set; }
        public ulong? TimeoutMs { get; set; }
    }
}

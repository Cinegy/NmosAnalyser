using System.Collections.Generic;

namespace NmosAnalyser
{
    public class SerialisableMetrics
    {
        public SerialisableMetrics()
        {
            Network = new SerialisableNetworkMetric();
            Rtp = new SerialisableRtpMetric();
        }

        public SerialisableNetworkMetric Network { get; set; }

        public SerialisableRtpMetric Rtp { get; set; }

        public class SerialisableNetworkMetric
        {
            public long TotalPacketsRecieved { get; set; }
            public long CurrentBitrate { get; set; }
            public long HighestBitrate { get; set; }
            public long LongestTimeBetweenPackets { get; set; }
            public long LowestBitrate { get; set; }
            public float NetworkBufferUsage { get; set; }
            public int PacketsPerSecond { get; set; }
            public long ShortestTimeBetweenPackets { get; set; }
            public long TimeBetweenLastPacket { get; set; }
            public long AverageBitrate { get; set; }
        }

        public class SerialisableRtpMetric
        {
            public long MinLostPackets { get; set; }
            public long SequenceNumber { get; set; }
            public long Timestamp { get; set; }
            public long SSRC { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NmosAnalyser
{
    public class NmosRtpMetric
    {
        public long TotalNmosHeaders { get; private set; }

        public List<RtpExtensionHeader> LastStartNmosHeaders { get; private set; }

        public void AddPacket(byte[] data)
        {
            TotalNmosHeaders++;

            var rtpPacket = new RtpPacket(data);

            if (rtpPacket.ExtensionHeaders.Count > 1)
            {
                LastStartNmosHeaders = rtpPacket.ExtensionHeaders;
            }
        }
    }
}

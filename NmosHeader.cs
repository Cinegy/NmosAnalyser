using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NmosAnalyser
{
    public struct NmosHeader
    {
        public Dictionary<int, string> NmosHeaders { get; set; }
    }

    public class NmosHeaderFactory
    {
        public static NmosHeader GetNmosHeaderFromData(RtpPacket rtpPacket)
        {
            var retval = new NmosHeader();
            foreach (var rtpExtensionHeader in rtpPacket.ExtensionHeaders)
            {
                
            }
            return retval;
        }
    }
}

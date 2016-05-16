using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NmosAnalyser
{
    public class NmosRtpMetric
    {
        public long TotalNmosHeaders { get; private set; }

        public NmosHeader LastNmosHeader { get; private set; }

        public void AddPacket(byte[] data)
        {
            TotalNmosHeaders++;
            LastNmosHeader = NmosHeaderFactory.GetNmosHeaderFromData(data);

        }
    }
}

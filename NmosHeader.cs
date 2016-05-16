using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NmosAnalyser
{
    public struct NmosHeader
    {
           public int ExtensionLength { get; set; }
     }

    public class NmosHeaderFactory
    {
        public static NmosHeader GetNmosHeaderFromData(byte[] data)
        {
            var nmosHeader = new NmosHeader()
            {
                ExtensionLength = (data[14] << 8) + data[15]
            };

            if (nmosHeader.ExtensionLength > 1)
            {

            }
            return nmosHeader;
        }
    }
}

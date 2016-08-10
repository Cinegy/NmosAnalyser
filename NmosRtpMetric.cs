using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace NmosAnalyser
{
    public class NmosRtpMetric
    {
        private static byte[] _spsData;
        private static byte[] _ppsData;

        public H264SeqParamSet FirstH264SeqParamSet { get; set; }

        public long TotalNmosHeaders { get; private set; }

        public List<RtpExtensionHeader> LastStartNmosHeaders { get; private set; }

        public bool IsAvc { get; set; }

        public int VideoWidth { get; set; }

        public void AddPacket(byte[] data)
        {
            TotalNmosHeaders++;

            var rtpPacket = new RtpPacket(data);

            if (rtpPacket.ExtensionHeaders.Count > 1)
            {
                LastStartNmosHeaders = rtpPacket.ExtensionHeaders;
            }

            if (VideoWidth == 0)
            {
                foreach (var header in LastStartNmosHeaders)
                {
                    var hdrString = Encoding.Default.GetString(header.Data);
                    if (hdrString.ToLowerInvariant().Contains("content-type:"))
                    {
                        if (hdrString.ToLowerInvariant().Contains("video/h264"))
                        {
                            IsAvc = true;
                        }
                    }
                }

                if (IsAvc)
                {
                    ReadH264FromPacket(rtpPacket);
                    if(_spsData!=null)
                    {
                        //read video width from the SPS
                        FirstH264SeqParamSet = new H264SeqParamSet();
                        FirstH264SeqParamSet.Decode(_spsData);
                        VideoWidth = (int)FirstH264SeqParamSet.frame_width_in_mbs;
                        
                    }
                }
            }
        }

        private static void ReadH264FromPacket(RtpPacket bufferedPacket)
        {
            try
            {
                if (bufferedPacket.Padding)
                {
                    Debug.WriteLine("RTP Packet has padding... this needs to be removed - not yet implemented!!");
                }
                else if (bufferedPacket.Payload == null)
                {
                    Debug.WriteLine("RTP Packet with null payload!!");
                }
                else if (bufferedPacket.Payload.Length < 1)
                {
                    Debug.WriteLine($"RTP Packet {bufferedPacket.SequenceNumber} and TS {bufferedPacket.Timestamp} with empty packet payload...", true);
                }
                else if ((bufferedPacket.Payload.Length > 0) && (bufferedPacket.Payload[0] & 0x1C) == 0x1c)
                {
                    Debug.WriteLine("FU-A packed packets not supported for SPS/PPS detection");
                    //ReadFuAPayload(bufferedPacket);
                }
                else if ((bufferedPacket.Payload[0] & 0x18) == 0x18)
                {
                    ReadStapAPayload(bufferedPacket);
                }
                else //not an FU-A or STAP-A packed payload - check if it is just a NAL
                {
                    // ReadNalPayload(bufferedPacket);
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($@"Unhandled exception within network receiver: {ex.Message}");
            }
        }


        private static void ReadStapAPayload(RtpPacket packet)
        {
            Debug.WriteLine(
                           $"STAP-A packet, SeqNum: {packet.SequenceNumber}, LastTS: {packet.Timestamp} Length: {packet.Payload.Length}",
                           true);

            var payloadPos = 1;

            while ((payloadPos + 1) < packet.Payload.Length)
            {
                var nalLen = (ushort)((packet.Payload[payloadPos] << 8) + packet.Payload[payloadPos + 1]);

                NalTypes nal = NalTypes.unspecified;

                if (typeof(NalTypes).IsEnumDefined(packet.Payload[payloadPos + 2] & 0x1F))
                {
                    nal = (NalTypes)(packet.Payload[payloadPos + 2] & 0x1F);
                }

                Debug.WriteLine(
                           $"STAP-A NAL Entry: {nal}, First NAL len: {nalLen}",
                           true);

                if (nal == NalTypes.seq_parameter_set_rbsp)
                {
                    _spsData = new byte[nalLen + 4];
                    _spsData[3] = 0x01; //4-byte start code used
                    Buffer.BlockCopy(packet.Payload, payloadPos + 2, _spsData, 4, nalLen);
                }

                if (nal == NalTypes.pic_parameter_set_rbsp)
                {
                    _ppsData = new byte[nalLen + 4];
                    _ppsData[3] = 0x01; //4-byte start code used
                    Buffer.BlockCopy(packet.Payload, payloadPos + 2, _ppsData, 4, nalLen);
                }

                payloadPos += (2 + nalLen);
            }
        }
    }

    enum NalTypes
    {
        unspecified,
        non_idr_slice_layer_without_partitioning_rbsp,
        slice_data_partition_a_layer_rbsp,
        slice_data_partition_b_layer_rbsp,
        slice_data_partition_c_layer_rbsp,
        idr_slice_layer_without_partitioning_rbsp,
        sei_rbsp,
        seq_parameter_set_rbsp,
        pic_parameter_set_rbsp,
        access_unit_delimiter_rbsp,
        end_of_seq_rbsp,
        end_of_stream_rbsp,
        filler_data_rbsp,
        seq_parameter_set_extension_rbsp,
        prefix_nal_unit_rbsp,
        subset_seq_parameter_set_rbsp,
        reserved_16,
        reserved_17,
        reserved_18,
        aux_slice_layer_without_partitioning_rbsp,
        extension_slice_layer_extension_rbsp,
        depth_extension_slice_layer_extension_rbsp
    }

}
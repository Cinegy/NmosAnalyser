﻿using System;

namespace NmosAnalyser
{
    public class RtpMetric
    {
        private long _totalPackets;
        public long MinLostPackets { get; private set; }
        public int LastSequenceNumber { get; private set; }
        public uint Ssrc { get; private set; }
        public uint LastTimestamp { get; private set; }
        public bool HasExtension { get; private set; }

        public void AddPacket(byte[] data)
        {
            HasExtension = (data[0] & 0x10) != 0;
            var seqNum = (data[2] << 8) + data[3];
            LastTimestamp = (uint) ((data[4] << 24) + (data[5] << 16) + (data[6] << 8) + data[7]);
            Ssrc = (uint) ((data[8] << 24) + (data[9] << 16) + (data[10] << 8) + data[11]);

            if (_totalPackets == 0)
            {
                RegisterFirstPacket(seqNum);
                return;
            }

            _totalPackets++;

            if (seqNum == 0)
            {
                if (LastSequenceNumber != ushort.MaxValue)
                {
                    MinLostPackets += ushort.MaxValue - LastSequenceNumber;
                    OnSequenceDiscontinuityDetected(new SequenceDiscontinuityEventArgs() { LastSequenceNumber = LastSequenceNumber, NewSequenceNumber = seqNum });
                }
            }
            else if (LastSequenceNumber + 1 != seqNum)
            {
                var seqDiff = seqNum - LastSequenceNumber;

                if (seqDiff < 0)
                {
                    seqDiff = seqNum + ushort.MaxValue - LastSequenceNumber;
                }
                MinLostPackets += seqDiff;
                OnSequenceDiscontinuityDetected(new SequenceDiscontinuityEventArgs() { LastSequenceNumber = LastSequenceNumber, NewSequenceNumber = seqNum });

            }

            LastSequenceNumber = seqNum;
        }

        private void RegisterFirstPacket(int seqNum)
        {
            LastSequenceNumber = seqNum;
            _totalPackets++;
        }

        // Sequence Counter Error has been detected
        public event EventHandler<SequenceDiscontinuityEventArgs> SequenceDiscontinuityDetected;
        
        protected virtual void OnSequenceDiscontinuityDetected(SequenceDiscontinuityEventArgs args)
        {  
            var handler = SequenceDiscontinuityDetected;
            handler?.Invoke(this, args);
        }
    }
    public class SequenceDiscontinuityEventArgs : EventArgs
    {
        public int LastSequenceNumber { get; set; }
        public int NewSequenceNumber { get; set; }
    }
}
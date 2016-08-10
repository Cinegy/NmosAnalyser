/*
   Copyright 2016 Cinegy GmbH

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

*/

using System;

namespace NmosAnalyser
{
    //-----------------------------------------------------------------------------
    public class BitWriter
    //-----------------------------------------------------------------------------
    {
        protected byte[] Buffer;
        protected int Bytepos;
        protected int Bitpos;

        //----------------------------------------------------------------
        public BitWriter(byte[] buf)
        //----------------------------------------------------------------
        {
            Buffer = buf;
            Bytepos = 0;
            Bitpos = 0;
        }

        //----------------------------------------------------------------
        public void Put_Bits(uint val, int n)
        //----------------------------------------------------------------
        {
            if (n == 0)
                return;

            var bitsLeft = 8 - Bitpos;

            if (n < bitsLeft)
            {
                val <<= (bitsLeft - n);
                Buffer[Bytepos] |= (byte)val;
                Bitpos += n;
                return;
            }

            Int64 bigval = ((Int64)(Buffer[Bytepos]) << (n - bitsLeft)) | val;

            int nn = n + Bitpos;

            while (nn >= 8)
            {
                Write_Byte((byte)(bigval >> (nn - 8)));
                nn -= 8;
            }

            Bitpos += n;
            Bitpos &= 7;

            if (nn != 0)
            {
                Buffer[Bytepos] = (byte)(bigval << (8 - Bitpos));
            }
        }

        //----------------------------------------------------------------
        protected virtual void Write_Byte(byte val)
        //----------------------------------------------------------------
        {
            Buffer[Bytepos++] = val;
        }

        //----------------------------------------------------------------
        public void Put_Bits32_Aligned(uint val)
        //----------------------------------------------------------------
        {
            Align();
            Write_Byte((byte)(val >> 24));
            Write_Byte((byte)(val >> 16));
            Write_Byte((byte)(val >> 8));
            Write_Byte((byte)(val >> 0));
        }

        //----------------------------------------------------------------
        public bool Put_Bool(bool val)
        //----------------------------------------------------------------
        {
            Put_Bits(val ? 1u : 0u, 1);
            return val;
        }

        //----------------------------------------------------------------
        private int BitsToAlign()
        //----------------------------------------------------------------
        {
            return ((Bitpos - 1) & 7) ^ 7;
        }

        //----------------------------------------------------------------
        public bool IsAligned()
        //----------------------------------------------------------------
        {
            return (Bitpos & 7) == 0;
        }

        //----------------------------------------------------------------
        public void Align()
        //----------------------------------------------------------------
        {
            //if(m_pos&7) m_pos = (m_pos+7)&~7;
            //m_pos += BitsToAlign();
            Put_Bits(0, BitsToAlign());
        }

        //----------------------------------------------------------------
        public int BitPos
        //----------------------------------------------------------------
        {
            get
            {
                return (Bytepos << 3) + Bitpos;
            }
            set
            {
                if (value > Buffer.Length * 8)
                    throw new IndexOutOfRangeException("BitPos is outside the bounds");

                Bytepos = value >> 3;
                Bitpos = value & 7;
            }
        }

        //----------------------------------------------------------------
        public int BytesInBuffer
        //----------------------------------------------------------------
        {
            get
            {
                return (BitPos + 7) >> 3;
            }
        }
    }

    //-----------------------------------------------------------------------------
    public class H264BitWriter : BitWriter
    //-----------------------------------------------------------------------------
    {
        //----------------------------------------------------------------
        public H264BitWriter(byte[] buf) : base(buf)
        //----------------------------------------------------------------
        {
        }

        //----------------------------------------------------------------
        protected override void Write_Byte(byte val)
        //----------------------------------------------------------------
        {
            if (val <= 1 && Bytepos >= 2 && Buffer[Bytepos - 1] == 0 && Buffer[Bytepos - 2] == 0)
                Buffer[Bytepos++] = 03;

            Buffer[Bytepos++] = val;
        }

        //----------------------------------------------------------------
        public void Put_StartCode(int len)
        //----------------------------------------------------------------
        {
            Align();

            for (int i = 0; i < len - 1; i++)
                base.Write_Byte(0);

            base.Write_Byte(1);
        }

        //-----------------------------------------------------------------------------
        public void Put_UE(uint val)
        //-----------------------------------------------------------------------------
        {
            if (val == 0)
            {
                Put_Bits(1, 1);
                return;
            }

            val += 1;
            var lsbPos = __bsr(val);

            Put_Bits(val, lsbPos * 2 + 1);
        }

        //-----------------------------------------------------------------------------
        public void Put_SE(int val)
        //----------------------------------------------------------------
        {
            if (val > 0)
                Put_UE((uint)(val * 2 - 1));
            else
                Put_UE((uint)(-val * 2));
        }


#if true
        private static int __bsr(uint v)
        {
            int r = 0;
            while ((v >>= 1) != 0)
            {
                r++;
            }
            return r;
        }
#else
        private uint LeadingZeros(uint x)
        {
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (sizeof(int) * 8 - Ones(x));
        }
        private uint Ones(uint x)
        {
            x -= ((x >> 1) & 0x55555555);
            x = (((x >> 2) & 0x33333333) + (x & 0x33333333));
            x = (((x >> 4) + x) & 0x0f0f0f0f);
            x += (x >> 8);
            x += (x >> 16);
            return (x & 0x0000003f);
        }
#endif
    }
}

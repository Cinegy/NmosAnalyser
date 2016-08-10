/*   Copyright 2016 Cinegy GmbH

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NmosAnalyser
{
    //-----------------------------------------------------------------------------
    public class BitReader
    //-----------------------------------------------------------------------------
    {
        byte [] m_buffer;
        int m_pos;

        //----------------------------------------------------------------
        public BitReader(byte [] buf)
        //----------------------------------------------------------------
        {
            m_buffer = buf;
            m_pos = 0;
        }

        //----------------------------------------------------------------
        private int get_03_code_bits(int n)
        //----------------------------------------------------------------
        {
            int curr_offs = m_pos >> 3;
            int next_offs = (m_pos + n) >> 3;

            if (next_offs >= m_buffer.Length)
            {
                throw new IndexOutOfRangeException("Reading the bits beyond the buffer");
            }

            if (curr_offs < 2)
                return 0;

            int cnt = 0;

            for (int i = curr_offs; i <= next_offs; i++)
                if (m_buffer[i] == 0x03 && m_buffer[i - 1] == 0 && m_buffer[i - 2] == 0)
                    cnt++;

            return cnt * 8;
        }

        //----------------------------------------------------------------
        public uint Show_Bits(int n)
        //----------------------------------------------------------------
        {
            if (n < 0)
                throw new Exception("ShowBits(): The value on N is negative");

            if (n == 0)
                return 0;

            if (n > 32)
                throw new Exception("ShowBits(): The value on N is too big");

            if (m_pos + n > m_buffer.Length * 8)
            {
                if (m_pos >= m_buffer.Length * 8)
                    throw new Exception("ShowBits(): Reading the bits beyond the buffer");

                int safe_bits = (int)(m_buffer.Length * 8 - m_pos);

                if (safe_bits <= 0)
                {
                    throw new IndexOutOfRangeException("Reading the bits beyond the buffer");
                }

                return Show_Bits(safe_bits) << (n - safe_bits);
            }

            int offs = m_pos >> 3;
            int ppos = m_pos  & 7;

            Int64 val = 0;
            int bits_read = 0;

            for (;;)
            {
                byte x = m_buffer[offs++];

                if (x == 0x03 && offs >= 3 && m_buffer[offs - 2] == 0 && m_buffer[offs - 3] == 0)
                    continue;

                val = (val << 8) | x;

                if ((bits_read += 8) >= n + ppos)
                    break;
            }

            val >>= bits_read - (n + ppos);

            return (uint)(val & (~0U >> (32 - n)));
        }

        //----------------------------------------------------------------
        public void Flush_Bits(int n)
        //----------------------------------------------------------------
        {
            m_pos += (n + get_03_code_bits(n));
        }

        //----------------------------------------------------------------
        public uint Get_Bits(int n)
        //----------------------------------------------------------------
        {
            uint val = Show_Bits(n);
            Flush_Bits(n);
            return val;
        }

        //----------------------------------------------------------------
        public uint Show_Bits32_Aligned()
        //----------------------------------------------------------------
        {
            int offs = (m_pos + 7) >> 3;

            if (offs + 4 > m_buffer.Length)
                throw new Exception("ShowBits32(): Reading the bits beyond the buffer");

            return (uint)((m_buffer[offs + 0] << 24) |
                   (m_buffer[offs + 1] << 16) |
                   (m_buffer[offs + 2] << 8) |
                   (m_buffer[offs + 3] << 0));
        }

        //----------------------------------------------------------------
        public bool Get_Bool()
        //----------------------------------------------------------------
        {
            return Get_Bits(1) != 0;
        }

        //----------------------------------------------------------------
        public void Unget_Bits(int n)
        //----------------------------------------------------------------
        {
            if (m_pos < n)
                throw new Exception("Too many bits to rewind");

            m_pos -= n;
        }

        //----------------------------------------------------------------
        private int BitsToAlign()
        //----------------------------------------------------------------
        {
            return ((m_pos - 1) & 7) ^ 7;
        }

        //----------------------------------------------------------------
        public bool IsAligned()
        //----------------------------------------------------------------
        {
            return (m_pos & 7) == 0;
        }

        //----------------------------------------------------------------
        public void Align()
        //----------------------------------------------------------------
        {
            //if(m_pos&7) m_pos = (m_pos+7)&~7;
            m_pos += BitsToAlign();
        }

        //----------------------------------------------------------------
        public int BitPos
        //----------------------------------------------------------------
        {
            get
            {
                return m_pos;
            }
            set
            {
                if (value > m_buffer.Length * 8)
                    throw new Exception("BitPos is outside the bounds");

                m_pos = value;
            }
        }

        //----------------------------------------------------------------
        public int BitsLeft
        //----------------------------------------------------------------
        {
            get
            {
                return m_buffer.Length * 8 - m_pos;
            }
        }
    }

    //-----------------------------------------------------------------------------
    public class H264BitReader : BitReader
    //-----------------------------------------------------------------------------
    {
        //----------------------------------------------------------------
        public H264BitReader(byte[] buf) : base(buf)
        //----------------------------------------------------------------
        {
        }

        //-----------------------------------------------------------------------------
        public uint Get_UE()
        //-----------------------------------------------------------------------------
        {
            int leadingZeroBits = 0;

            while (Get_Bits(1) == 0)
                leadingZeroBits++;

            return leadingZeroBits == 0 ? 0 : (1u << leadingZeroBits) - 1 + Get_Bits(leadingZeroBits);
        }

        //-----------------------------------------------------------------------------
        public int Get_SE()
        //----------------------------------------------------------------
        {
            uint val = Get_UE();
            //return val == 0 ? 0 : (int)((val + 1) >> 1) * ((val & 1) != 0 ? 1 : -1);
            uint sign = (val & 1) - 1;
            return val == 0 ? 0 : (int)((((val + 1) >> 1) ^ sign) - sign);
        }

        //----------------------------------------------------------------
        public uint FindStartCode()
        //----------------------------------------------------------------
        {
            Align();

            for (;;)
            {
                if (BitsLeft < 32)
                    return 0;

                uint code = Show_Bits32_Aligned();

                if (((code ^ ~(code + 0x7efefeff)) & 0x81010100) != 0)
                {
                    if ((code & 0xFFFFFF00) == 0x00000100)
                        return code;

                    if (code == 0x00000001)
                    {
                        Flush_Bits(8);
                        code = Show_Bits32_Aligned();
                        Unget_Bits(8);
                        return code;
                    }

                    Flush_Bits(8); // going slow
                    continue;
                }

                Flush_Bits(32);
            }
        }

        //----------------------------------------------------------------
        public int SkipStartCode()
        //----------------------------------------------------------------
        {
            if (!IsAligned())
                return 0;

            uint code = Show_Bits32_Aligned();

            if ((code & 0xFFFFFF00) == 0x00000100)
            {
                Flush_Bits(32);
                return 32;
            }

            if (code == 0x00000001)
            {
                Flush_Bits(40);
                return 40;
            }

            return 0;
        }

        //----------------------------------------------------------------
        public bool RBSP_Trailing_Bits()
        //----------------------------------------------------------------
        {
            if (BitsLeft <= 0)
                return false;

            uint rbsp_stop_bit = Get_Bits(1);
            //int rbsp_alignment_zero_bits = Show_Bits(BitsToAlign());
            uint rbsp_zero_bits = Show_Bits(Math.Min(23, BitsLeft)); // aligning_bits + next_start_code_bits

            Unget_Bits(1);

            return rbsp_stop_bit == 1 && rbsp_zero_bits == 0;
        }

        //----------------------------------------------------------------
        public bool More_RBSP_Data()
        //----------------------------------------------------------------
        {
            if (BitsLeft <= 0)
                return false;

            return !RBSP_Trailing_Bits();
        }
    }
}

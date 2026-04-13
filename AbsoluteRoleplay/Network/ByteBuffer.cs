using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Networking
{
    // Serialization buffer used to pack and unpack network packet data.
    // Write methods append typed values; Read methods consume them in the same order.
    // The Peek parameter on Read methods controls whether the read position advances
    // (true = advance, which is the normal case despite the parameter name).
    public class ByteBuffer : IDisposable
    {
        private List<byte> Buff;          // backing store for all written bytes
        private byte[] readBuff;          // snapshot array used during reads for performance
        private int readPos;              // current read cursor position
        private bool buffUpdated = false; // tracks whether Buff changed since the last snapshot

        public ByteBuffer()
        {
            Buff = new List<byte>();
            readPos = 0;
        }
        public int GetReadPos()
        {
            return readPos;
        }
        public void SetReadPos(int pos)
        {
            readPos = pos;
        }
        // Returns the full buffer contents as a byte array (used when sending packets)
        public byte[] ToArray()
        {
            return Buff.ToArray();
        }
        // Total number of bytes written into the buffer
        public int Count()
        {
            return Buff.Count();
        }
        // Number of unread bytes remaining (total minus what has already been consumed)
        public int Length()
        {
            return Count() - readPos;
        }
        // Resets the buffer so it can be reused without allocating a new instance
        public void Clear()
        {
            Buff.Clear();
            readPos = 0;
        }

        // --- Write methods: append typed values to the end of the buffer ---

        public void WriteByte(byte input)
        {
            Buff.Add(input);
            buffUpdated = true;
        }

        // Reads a single byte. If Peek is true (default), the read position advances.
        // Rebuilds the read snapshot array if the buffer was modified since the last read.
        public byte ReadByte(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                // Rebuild snapshot if writes occurred since last read
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = readBuff[readPos];
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 1;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type. You are not trying to read out a 'BYTE'");
            }
        }

        public void WriteBytes(byte[] input)
        {
            Buff.AddRange(input);
            buffUpdated = true;
        }
        public void WriteShort(short input)
        {
            Buff.AddRange(BitConverter.GetBytes(input));
            buffUpdated = true;
        }
        public void WriteInt(int input)
        {
            Buff.AddRange(BitConverter.GetBytes(input));
            buffUpdated = true;
        }
        public void WriteLong(long input)
        {
            Buff.AddRange(BitConverter.GetBytes(input));
            buffUpdated = true;
        }

        public void WriteFloat(float input)
        {
            Buff.AddRange(BitConverter.GetBytes(input));
            buffUpdated = true;
        }
        public void WriteBool(bool input)
        {
            Buff.AddRange(BitConverter.GetBytes(input));
            buffUpdated = true;
        }
        // Strings are length-prefixed: writes the byte count as an int, then the UTF-8 bytes.
        // This lets ReadString know exactly how many bytes to consume on the other end.
        public void WriteString(string input)
        {
            // Defensive: treat null as empty string to avoid Encoding.GetBytes throwing ArgumentNullException
            if (input == null) input = string.Empty;
            byte[] stringBytes = Encoding.UTF8.GetBytes(input);
            Buff.AddRange(BitConverter.GetBytes(stringBytes.Length)); // Store length first
            Buff.AddRange(stringBytes); // Store UTF-8 encoded string
            buffUpdated = true;
        }

        // --- Read methods: consume typed values from the current read position ---

        public byte[] ReadBytes(int length, bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = Buff.GetRange(readPos, length).ToArray();
                if (Peek)
                {
                    readPos += length;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'BYTE[]'");
            }
        }
        public short ReadShort(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = BitConverter.ToInt16(readBuff, readPos);
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 2;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'SHORT'");
            }
        }
        public int ReadInt(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = BitConverter.ToInt32(readBuff, readPos);
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'INT'");
            }
        }
        public long ReadLong(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = BitConverter.ToInt64(readBuff, readPos);
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 8;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'LONG'");
            }
        }
        public float ReadFloat(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = BitConverter.ToSingle(readBuff, readPos);
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 4;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'FLOAT'");
            }
        }
        public bool ReadBool(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }

                var value = BitConverter.ToBoolean(readBuff, readPos);
                if (Peek & Buff.Count > readPos)
                {
                    readPos += 1;
                }
                return value;
            }
            else
            {
                throw new Exception("Wrong Data Type.You are not trying to read out a 'BOOL'");
            }
        }
        // Reads a length-prefixed string: first consumes the int byte-count, then decodes that many UTF-8 bytes.
        public string ReadString(bool Peek = true)
        {
            try
            {
                // Read the byte length of the string (always advances the cursor)
                int length = ReadInt(true);
                if (buffUpdated)
                {
                    readBuff = Buff.ToArray();
                    buffUpdated = false;
                }
                string value = Encoding.UTF8.GetString(readBuff, readPos, length);
                if (Peek & Buff.Count > readPos)
                {
                    if (value.Length > 0)
                        readPos += length; // advance past the string bytes
                }
                return value;
            }
            catch
            {
                throw new Exception("You are not trying to read out a 'STRING'");
            }
        }


        // --- IDisposable implementation ---
        // Note: the inner condition (if disposing) is unreachable because the outer
        // condition checks (!disposing). This means the buffer is never actually cleared
        // on dispose, but GC will collect it. This is a known quirk left as-is.
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                if (disposing)
                {
                    Buff?.Clear();
                    readPos = 0;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

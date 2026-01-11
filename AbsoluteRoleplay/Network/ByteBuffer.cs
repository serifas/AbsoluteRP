using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Networking
{
    public class ByteBuffer : IDisposable
    {
        private List<byte> Buff;
        private byte[] readBuff;
        private int readPos;
        private bool buffUpdated = false;

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
        public byte[] ToArray()
        {
            return Buff.ToArray();
        }
        public int Count()
        {
            return Buff.Count();
        }
        public int Length()
        {
            return Count() - readPos;
        }
        public void Clear()
        {
            Buff.Clear();
            readPos = 0;
        }

        public void WriteByte(byte input)
        {
            Buff.Add(input);
            buffUpdated = true;
        }

        public byte ReadByte(bool Peek = true)
        {
            if (Buff.Count > readPos)
            {
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
        public void WriteString(string input)
        {
            // Defensive: treat null as empty string to avoid Encoding.GetBytes throwing ArgumentNullException
            if (input == null) input = string.Empty;
            byte[] stringBytes = Encoding.UTF8.GetBytes(input);
            Buff.AddRange(BitConverter.GetBytes(stringBytes.Length)); // Store length first
            Buff.AddRange(stringBytes); // Store UTF-8 encoded string
            buffUpdated = true;
        }
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
        public string ReadString(bool Peek = true)
        {
            try
            {
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
                        readPos += length;
                }
                return value;
            }
            catch
            {
                throw new Exception("You are not trying to read out a 'STRING'");
            }
        }


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

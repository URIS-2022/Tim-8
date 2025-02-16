﻿using System;
using System.IO;

namespace NBitcoin.Protocol
{
    public class CompactVarInt : IBitcoinSerializable
    {
        private ulong value = 0;
        private int size;

        public CompactVarInt(int size)
        {
            this.size = size;
        }

        public CompactVarInt(ulong value, int size)
        {
            this.value = value;
            this.size = size;
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                ulong n = this.value;
                var tmp = new byte[(this.size * 8 + 6) / 7];
                int len = 0;
                while (true)
                {
                    byte a = (byte)(n & 0x7F);
                    byte b = (byte)(len != 0 ? 0x80 : 0x00);
                    tmp[len] = (byte)(a | b);
                    if (n <= 0x7F)
                        break;

                    n = (n >> 7) - 1;
                    len++;
                }
                do
                {
                    byte b = tmp[len];
                    stream.ReadWrite(ref b);
                } while (len-- != 0);
            }
            else
            {
                ulong n = 0;
                while (true)
                {
                    byte chData = 0;
                    stream.ReadWrite(ref chData);
                    ulong a = (n << 7);
                    byte b = (byte)(chData & 0x7F);
                    n = (a | b);
                    if ((chData & 0x80) != 0)
                        n++;
                    else
                        break;
                }
                this.value = n;
            }
        }

        #endregion

        public ulong ToLong()
        {
            return this.value;
        }
    }

    // https://en.bitcoin.it/wiki/Protocol_specification#Variable_length_integer
    public class VarInt : IBitcoinSerializable
    {
        private byte prefixByte = 0;
        private ulong value = 0;

        public VarInt()
            : this(0)
        {

        }
        public VarInt(ulong value)
        {
            SetValue(value);
        }

        internal void SetValue(ulong value)
        {
            this.value = value;
            if (this.value < 0xFD)
                this.prefixByte = (byte)(int)this.value;
            else if (this.value <= 0xffff)
                this.prefixByte = 0xFD;
            else if (this.value <= 0xffffffff)
                this.prefixByte = 0xFE;
            else
                this.prefixByte = 0xFF;
        }
        public static void StaticWrite(BitcoinStream bs, ulong length)
        {
            if (!bs.Serializing)
                throw new InvalidOperationException("Stream should be serializing");
            var stream = bs.Inner;
            bs.Counter.AddWritten(1);
            if (length < 0xFD)
            {
                stream.WriteByte((byte)length);
            }
            else if (length <= 0xffff)
            {
                var value = (ushort)length;
                stream.WriteByte((byte)0xFD);
                bs.ReadWrite(ref value);
            }
            else if (length <= 0xffffffff)
            {
                var value = (uint)length;
                stream.WriteByte((byte)0xFE);
                bs.ReadWrite(ref value);
            }
            else
            {
                var value = length;
                stream.WriteByte((byte)0xFF);
                bs.ReadWrite(ref value);
            }
        }

        public static ulong StaticRead(BitcoinStream bs)
        {
            if (bs.Serializing)
                throw new InvalidOperationException("Stream should not be serializing");
            var prefix = bs.Inner.ReadByte();
            bs.Counter.AddRead(1);
            if (prefix == -1)
                throw new EndOfStreamException("No more byte to read");
            if (prefix < 0xFD)
                return (byte)prefix;
            else if (prefix == 0xFD)
            {
                var value = (ushort)0;
                bs.ReadWrite(ref value);
                return value;
            }
            else if (prefix == 0xFE)
            {
                var _value = (uint)0;
                bs.ReadWrite(ref _value);
                return _value;
            }
            else
            {
                var value = (ulong)0;
                bs.ReadWrite(ref value);
                return value;
            }
        }

        public ulong ToLong()
        {
            return this.value;
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.prefixByte);
            if (this.prefixByte < 0xFD)
            {
                this.value = this.prefixByte;
            }
            else if (this.prefixByte == 0xFD)
            {
                ushort val = (ushort)this.value;
                stream.ReadWrite(ref val);
                this.value = val;
            }
            else if (this.prefixByte == 0xFE)
            {
                uint val = (uint)this.value;
                stream.ReadWrite(ref val);
                this.value = val;
            }
            else
            {
                ulong val = (ulong)this.value;
                stream.ReadWrite(ref val);
                this.value = val;
            }
        }
        #endregion
    }
}
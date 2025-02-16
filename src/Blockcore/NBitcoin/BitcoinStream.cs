﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Blockcore.Consensus;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public enum SerializationType
    {
        Disk,
        Network,
        Hash
    }

    public class Scope : IDisposable
    {
        private readonly Action close;

        public Scope(Action open, Action close)
        {
            this.close = close;
            open();
        }

        public void Dispose()
        {
            this.close();
        }

        public static IDisposable Nothing
        {
            get
            {
                return new Scope(() =>
                {
                }, () =>
                {
                });
            }
        }
    }

    /// <summary>
    /// A component that performs byte serialization of objects.
    /// </summary>
    public partial class BitcoinStream
    {
        private static MethodInfo readWriteTyped;

        static BitcoinStream()
        {
            readWriteTyped = typeof(BitcoinStream)
            .GetTypeInfo()
            .DeclaredMethods
            .Where(m => m.Name == "ReadWrite")
            .Where(m => m.IsGenericMethodDefinition)
            .Where(m => m.GetParameters().Length == 1)
            .Where(m => m.GetParameters().Any(p => p.ParameterType.IsByRef && p.ParameterType.HasElementType && !p.ParameterType.GetElementType().IsArray))
            .First();
        }

        private readonly bool isNetworkStream;

        public int MaxArraySize { get; set; }

        public Stream Inner { get; }

        public bool Serializing { get; }

        public long ProcessedBytes => this.Serializing ? this.Counter.WrittenBytes : this.Counter.ReadBytes;

        public uint ProtocolVersion { get; private set; }

        public TransactionOptions TransactionOptions { get; set; }

        public ConsensusFactory ConsensusFactory { get; private set; }

        public bool IsBigEndian { get; set; }

        public SerializationType Type { get; set; }
        public System.Threading.CancellationToken ReadCancellationToken { get; set; }

        public BitcoinStream(Stream inner, bool serializing, ConsensusFactory consensus, uint overrideProtocolVersion)
        {
            this.ConsensusFactory = consensus;
            this.ProtocolVersion = overrideProtocolVersion;
            this.TransactionOptions = TransactionOptions.All;
            this.Serializing = serializing;

            this.isNetworkStream = inner is NetworkStream;
            this.Inner = inner;
            this.MaxArraySize = 1024 * 1024;
        }

        public BitcoinStream(Stream inner, bool serializing, ConsensusFactory consensus)
            : this(inner, serializing, consensus, consensus.Protocol.ProtocolVersion)
        {
        }

        public BitcoinStream(Stream inner, bool serializing, uint overrideProtocolVersion)
            : this(inner, serializing, new DefaultConsensusFactory(), overrideProtocolVersion)
        {
        }

        public BitcoinStream(Stream inner, bool serializing)
            : this(inner, serializing, new DefaultConsensusFactory())
        {
        }

        public Script ReadWrite(Script data)
        {
            if (this.Serializing)
            {
                byte[] bytes = data == null ? Script.Empty.ToBytes(true) : data.ToBytes(true);
                ReadWriteAsVarString(ref bytes);
                return data;
            }
            else
            {
                var varString = new VarString();
                varString.ReadWrite(this);
                return Script.FromBytesUnsafe(varString.GetString(true));
            }
        }

        public void ReadWrite(ref Script script)
        {
            if (this.Serializing)
                ReadWrite(script);
            else
                script = ReadWrite(script);
        }

        public T ReadWrite<T>(T data) where T : IBitcoinSerializable
        {
            ReadWrite<T>(ref data);
            return data;
        }

        public void ReadWriteAsVarString(ref byte[] bytes)
        {
            if (this.Serializing)
            {
                var str = new VarString(bytes);
                str.ReadWrite(this);
            }
            else
            {
                var str = new VarString();
                str.ReadWrite(this);
                bytes = str.GetString(true);
            }
        }

        public void ReadWrite(Type type, ref object obj)
        {
            try
            {
                var parameters = new object[] { obj };
                readWriteTyped.MakeGenericMethod(type).Invoke(this, parameters);
                obj = parameters[0];
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        public void ReadWrite(ref byte data)
        {
            ReadWriteByte(ref data);
        }

        public byte ReadWrite(byte data)
        {
            ReadWrite(ref data);
            return data;
        }

        public void ReadWrite(ref bool data)
        {
            byte d = data ? (byte)1 : (byte)0;
            ReadWriteByte(ref d);
            data = (d == 0 ? false : true);
        }

        public void ReadWriteStruct<T>(ref T data) where T : struct, IBitcoinSerializable
        {
            data.ReadWrite(this);
        }

        public void ReadWriteStruct<T>(T data) where T : struct, IBitcoinSerializable
        {
            data.ReadWrite(this);
        }

        public void ReadWrite<T>(ref T data) where T : IBitcoinSerializable
        {
            T obj = data;
            if (obj == null)
            {
                obj = this.ConsensusFactory.TryCreateNew<T>();
                if (obj == null)
                    obj = Activator.CreateInstance<T>();
            }

            obj.ReadWrite(this);
            if (!this.Serializing)
                data = obj;
        }

        public void ReadWrite<T>(ref List<T> list) where T : IBitcoinSerializable
        {
            ReadWriteList<List<T>, T>(ref list);
        }

        public void ReadWrite<TList, TItem>(ref TList list)
            where TList : List<TItem>, new()
            where TItem : IBitcoinSerializable, new()
        {
            ReadWriteList<TList, TItem>(ref list);
        }

        private void ReadWriteList<TList, TItem>(ref TList data)
            where TList : List<TItem>, new()
            where TItem : IBitcoinSerializable
        {
            TItem[] dataArray = data == null ? null : data.ToArray();

            if (this.Serializing && dataArray == null)
            {
                dataArray = new TItem[0];
            }

            ReadWriteArray(ref dataArray);

            if (!this.Serializing)
            {
                if (data == null)
                    data = new TList();
                else
                    data.Clear();
                data.AddRange(dataArray);
            }
        }

        public void ReadWriteListBytes(ref List<byte[]> data)
        {
            var dataArray = data?.ToArray();
            if (this.Serializing && dataArray == null)
            {
                dataArray = new byte[0][];
            }
            ReadWriteArray(ref dataArray);
            if (!this.Serializing)
            {
                if (data == null)
                    data = new List<byte[]>();
                else
                    data.Clear();
                data.AddRange(dataArray);
            }
        }

        public void ReadWrite(ref byte[] arr)
        {
            ReadWriteBytes(ref arr);
        }

        public void ReadWrite(ref Span<byte> arr)
        {
            ReadWriteBytes(arr);
        }

        public void ReadWrite(ref string str)
        {
            if (this.Serializing)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(str);

                this._VarInt.SetValue((ulong)str.Length);
                ReadWrite(ref this._VarInt);

                this.ReadWriteBytes(ref bytes);
            }
            else
            {
                this._VarInt.SetValue(0);
                ReadWrite(ref this._VarInt);

                ulong length = this._VarInt.ToLong();

                byte[] bytes = new byte[length];

                this.ReadWriteBytes(ref bytes, 0, bytes.Length);

                str = Encoding.ASCII.GetString(bytes);
            }
        }

        public void ReadWrite(ref byte[] arr, int offset, int count)
        {
            ReadWriteBytes(ref arr, offset, count);
        }

        public void ReadWrite<T>(ref T[] arr) where T : IBitcoinSerializable, new()
        {
            ReadWriteArray<T>(ref arr);
        }

        private void ReadWriteNumber(ref long value, int size)
        {
            ulong uvalue = unchecked((ulong)value);
            ReadWriteNumber(ref uvalue, size);
            value = unchecked((long)uvalue);
        }

        private void ReadWriteNumber(ref ulong value, int size)
        {
            if (this.isNetworkStream && this.ReadCancellationToken.CanBeCanceled)
            {
                ReadWriteNumberInefficient(ref value, size);
                return;
            }

            Span<byte> bytes = stackalloc byte[size];
            for (int i = 0; i < size; i++)
            {
                bytes[i] = (byte)(value >> i * 8);
            }

            if (this.IsBigEndian)
            {
                bytes.Reverse();
            }

            ReadWriteBytes(bytes);
            if (this.IsBigEndian)
            {
                bytes.Reverse();
            }

            ulong valueTemp = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                var v = (ulong)bytes[i];
                valueTemp += v << (i * 8);
            }

            value = valueTemp;
        }

        private void ReadWriteNumberInefficient(ref ulong value, int size)
        {
            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = (byte)(value >> i * 8);
            }
            if (this.IsBigEndian)
                Array.Reverse(bytes);
            ReadWriteBytes(ref bytes);
            if (this.IsBigEndian)
                Array.Reverse(bytes);
            ulong valueTemp = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                ulong v = (ulong)bytes[i];
                valueTemp += v << (i * 8);
            }
            value = valueTemp;
        }

        private void ReadWriteBytes(ref byte[] data, int offset = 0, int count = -1)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length == 0)
                return;
            count = count == -1 ? data.Length : count;
            if (count == 0)
                return;
            ReadWriteBytes(new Span<byte>(data, offset, count));
        }

        private void ReadWriteBytes(Span<byte> data)
        {
            if (this.Serializing)
            {
                this.Inner.Write(data);
                this.Counter.AddWritten(data.Length);
            }
            else
            {
                var read = this.Inner.ReadEx(data, this.ReadCancellationToken);
                if (read == 0)
                    throw new EndOfStreamException("No more byte to read");
                this.Counter.AddRead(read);
            }
        }

        private PerformanceCounter counter;

        public PerformanceCounter Counter
        {
            get
            {
                if (this.counter == null)
                    this.counter = new PerformanceCounter();
                return this.counter;
            }
        }

        private void ReadWriteByte(ref byte data)
        {
            if (this.Serializing)
            {
                this.Inner.WriteByte(data);
                this.Counter.AddWritten(1);
            }
            else
            {
                int readen = this.Inner.ReadByte();
                if (readen == -1)
                    throw new EndOfStreamException("No more byte to read");
                data = (byte)readen;
                this.Counter.AddRead(1);
            }
        }

        public IDisposable BigEndianScope()
        {
            bool old = this.IsBigEndian;
            return new Scope(() =>
            {
                this.IsBigEndian = true;
            },
            () =>
            {
                this.IsBigEndian = old;
            });
        }

        public IDisposable ProtocolVersionScope(uint version)
        {
            uint old = this.ProtocolVersion;
            return new Scope(() =>
            {
                this.ProtocolVersion = version;
            },
            () =>
            {
                this.ProtocolVersion = old;
            });
        }

        public void CopyParameters(BitcoinStream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            this.ConsensusFactory = stream.ConsensusFactory;
            this.ProtocolVersion = stream.ProtocolVersion;
            this.TransactionOptions = stream.TransactionOptions;
            this.IsBigEndian = stream.IsBigEndian;
            this.MaxArraySize = stream.MaxArraySize;
            this.Type = stream.Type;
        }

        public IDisposable SerializationTypeScope(SerializationType value)
        {
            SerializationType old = this.Type;
            return new Scope(() =>
            {
                this.Type = value;
            }, () =>
            {
                this.Type = old;
            });
        }

        public void ReadWriteAsVarInt(ref uint val)
        {
            ulong vallong = val;
            ReadWriteAsVarInt(ref vallong);
            if (!this.Serializing)
                val = (uint)vallong;
        }

        public void ReadWriteAsVarInt(ref ulong val)
        {
            var value = new VarInt(val);
            ReadWrite(ref value);
            if (!this.Serializing)
                val = value.ToLong();
        }

        public void ReadWriteAsCompactVarInt(ref uint val)
        {
            var value = new CompactVarInt(val, sizeof(uint));
            ReadWrite(ref value);
            if (!this.Serializing)
                val = (uint)value.ToLong();
        }

        public void ReadWriteAsCompactVarInt(ref ulong val)
        {
            var value = new CompactVarInt(val, sizeof(ulong));
            ReadWrite(ref value);
            if (!this.Serializing)
                val = value.ToLong();
        }
    }
}
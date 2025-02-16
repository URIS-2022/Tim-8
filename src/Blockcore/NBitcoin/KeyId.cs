﻿using System;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Networks;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    public abstract class TxDestination : IDestination
    {
        internal byte[] _DestBytes;

        public TxDestination()
        {
            this._DestBytes = new byte[] { 0 };
        }

        public TxDestination(byte[] value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            this._DestBytes = value;
        }

        public TxDestination(string value)
        {
            this._DestBytes = Encoders.Hex.DecodeData(value);
            this._Str = value;
        }

        public abstract BitcoinAddress GetAddress(Network network);

        #region IDestination Members

        public abstract Script ScriptPubKey
        {
            get;
        }

        #endregion


        public byte[] ToBytes()
        {
            return ToBytes(false);
        }
        public byte[] ToBytes(bool @unsafe)
        {
            if (@unsafe)
                return this._DestBytes;
            var array = new byte[this._DestBytes.Length];
            Array.Copy(this._DestBytes, array, this._DestBytes.Length);
            return array;
        }

        public override bool Equals(object obj)
        {
            var item = obj as TxDestination;
            if (item == null)
                return false;
            return Utils.ArrayEqual(this._DestBytes, item._DestBytes) && item.GetType() == GetType();
        }
        public static bool operator ==(TxDestination a, TxDestination b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return Utils.ArrayEqual(a._DestBytes, b._DestBytes) && a.GetType() == b.GetType();
        }

        public static bool operator !=(TxDestination a, TxDestination b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Utils.GetHashCode(this._DestBytes);
        }

        private string _Str;
        public override string ToString()
        {
            if (this._Str == null) this._Str = Encoders.Hex.EncodeData(this._DestBytes);
            return this._Str;
        }
    }
    public class KeyId : TxDestination
    {
        public KeyId()
            : this(0)
        {

        }

        public KeyId(byte[] value)
            : base(value)
        {
            if (value.Length != 20)
                throw new ArgumentException("value should be 20 bytes", "value");
        }
        public KeyId(uint160 value)
            : base(value.ToBytes())
        {

        }

        public KeyId(string value)
            : base(value)
        {
        }

        public override Script ScriptPubKey
        {
            get
            {
                return PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(this);
            }
        }

        public override BitcoinAddress GetAddress(Network network)
        {
            return new BitcoinPubKeyAddress(this, network);
        }
    }
    public class WitKeyId : TxDestination
    {
        public WitKeyId()
            : this(0)
        {

        }

        public WitKeyId(byte[] value)
            : base(value)
        {
            if (value.Length != 20)
                throw new ArgumentException("value should be 20 bytes", "value");
        }
        public WitKeyId(uint160 value)
            : base(value.ToBytes())
        {

        }

        public WitKeyId(string value)
            : base(value)
        {
        }

        public WitKeyId(KeyId keyId)
            : base(keyId.ToBytes())
        {

        }


        public override Script ScriptPubKey
        {
            get
            {
                return PayToWitTemplate.Instance.GenerateScriptPubKey(OpcodeType.OP_0, this._DestBytes);
            }
        }

        [Obsolete("Use AsKeyId().ScriptPubKey instead")]
        public Script WitScriptPubKey
        {
            get
            {
                return new KeyId(this._DestBytes).ScriptPubKey;
            }
        }

        public KeyId AsKeyId()
        {
            return new KeyId(this._DestBytes);
        }

        public override BitcoinAddress GetAddress(Network network)
        {
            return new BitcoinWitPubKeyAddress(this, network);
        }
    }

    public class WitScriptId : TxDestination
    {
        public WitScriptId()
            : this(0)
        {

        }

        public WitScriptId(byte[] value)
            : base(value)
        {
            if (value.Length != 32)
                throw new ArgumentException("value should be 32 bytes", "value");
        }
        public WitScriptId(uint256 value)
            : base(value.ToBytes())
        {

        }

        public WitScriptId(string value)
            : base(value)
        {
        }

        public WitScriptId(Script script)
            : this(Hashes.SHA256(script._Script))
        {
        }

        public override Script ScriptPubKey
        {
            get
            {
                return PayToWitTemplate.Instance.GenerateScriptPubKey(OpcodeType.OP_0, this._DestBytes);
            }
        }

        public override BitcoinAddress GetAddress(Network network)
        {
            return new BitcoinWitScriptAddress(this, network);
        }
    }

    public class ScriptId : TxDestination
    {
        public ScriptId()
            : this(0)
        {

        }

        public ScriptId(byte[] value)
            : base(value)
        {
            if (value.Length != 20)
                throw new ArgumentException("value should be 20 bytes", "value");
        }
        public ScriptId(uint160 value)
            : base(value.ToBytes())
        {

        }

        public ScriptId(string value)
            : base(value)
        {
        }

        public ScriptId(Script script)
            : this(Hashes.Hash160(script._Script))
        {
        }

        public override Script ScriptPubKey
        {
            get
            {
                return PayToScriptHashTemplate.Instance.GenerateScriptPubKey(this);
            }
        }

        public override BitcoinAddress GetAddress(Network network)
        {
            return new BitcoinScriptAddress(this, network);
        }
    }
}

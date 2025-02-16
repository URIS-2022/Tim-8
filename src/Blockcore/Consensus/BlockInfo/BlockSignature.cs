﻿using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Blockcore.Consensus.BlockInfo
{
    /// <summary>
    /// A representation of a block signature.
    /// </summary>
    public sealed class BlockSignature : IBitcoinSerializable
    {
        protected bool Equals(BlockSignature other)
        {
            return Equals(this.signature, other.signature);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return this.Equals((BlockSignature)obj);
        }

        public override int GetHashCode()
        {
            return this.signature?.GetHashCode() ?? 0;
        }

        public BlockSignature()
        {
            this.signature = System.Array.Empty<byte>();
        }

        private byte[] signature;

        public byte[] Signature
        {
            get
            {
                return this.signature;
            }

            set
            {
                this.signature = value;
            }
        }

        internal void SetNull()
        {
            this.signature = new byte[0];
        }

        public bool IsEmpty()
        {
            return !this.signature.Any();
        }

        public static bool operator ==(BlockSignature a, BlockSignature b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if ((a is null) || (b is null))
                return false;

            return a.signature.SequenceEqual(b.signature);
        }

        public static bool operator !=(BlockSignature a, BlockSignature b)
        {
            return !(a == b);
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWriteAsVarString(ref this.signature);
        }

        #endregion IBitcoinSerializable Members

        public override string ToString()
        {
            return Encoders.Hex.EncodeData(this.signature);
        }
    }
}
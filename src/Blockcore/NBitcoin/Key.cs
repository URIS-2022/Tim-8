﻿using System;
using System.Linq;
using System.Text;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Networks;
using NBitcoin.BouncyCastle.Asn1.X9;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;

namespace NBitcoin
{
    public class Key : IBitcoinSerializable, IDestination
    {
        private const int KEY_SIZE = 32;
        private readonly static uint256 N = uint256.Parse("fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141");

        public static Key Parse(string wif, Network network = null)
        {
            return Network.Parse<BitcoinSecret>(wif, network).PrivateKey;
        }

        public static Key Parse(string wif, string password, Network network = null)
        {
            return Network.Parse<BitcoinEncryptedSecret>(wif, network).GetKey(password);
        }

        private byte[] vch = new byte[0];
        internal ECKey _ECKey;
        public bool IsCompressed
        {
            get;
            internal set;
        }

        public Key()
            : this(true)
        {

        }

        public Key(bool fCompressedIn)
        {
            var data = new byte[KEY_SIZE];
            do
            {
                RandomUtils.GetBytes(data);
            } while (!Check(data));

            SetBytes(data, data.Length, fCompressedIn);
        }
        public Key(byte[] data, int count = -1, bool fCompressedIn = true)
        {
            if (count == -1)
                count = data.Length;
            if (count != KEY_SIZE)
            {
                throw new FormatException("The size of an EC key should be 32");
            }
            if (Check(data))
            {
                SetBytes(data, count, fCompressedIn);
            }
            else
                throw new FormatException("Invalid EC key");
        }

        private void SetBytes(byte[] data, int count, bool fCompressedIn)
        {
            this.vch = data.SafeSubarray(0, count);
            this.IsCompressed = fCompressedIn;
            this._ECKey = new ECKey(this.vch, true);
        }

        private static bool Check(byte[] vch)
        {
            var candidateKey = new uint256(vch.SafeSubarray(0, KEY_SIZE));
            return candidateKey > 0 && candidateKey < N;
        }

        private PubKey _PubKey;

        public PubKey PubKey
        {
            get
            {
                if (this._PubKey == null)
                {
                    var key = new ECKey(this.vch, true);
                    this._PubKey = key.GetPubKey(this.IsCompressed);
                }
                return this._PubKey;
            }
        }

        public ECDSASignature Sign(uint256 hash)
        {
            return this._ECKey.Sign(hash);
        }

        public SchnorrSignature SignSchnorr(uint256 hash)
        {
            var signer = new SchnorrSigner();
            return signer.Sign(hash, this);

        }

        /// <summary>
        /// Hashes and signs a message, returning the signature.
        /// </summary>
        /// <param name="messageBytes">The message to hash then sign.</param>
        /// <returns>The signature of the hashed and signed message.</returns>
        public ECDSASignature SignMessageBytes(byte[] messageBytes)
        {
            byte[] data = Utils.FormatMessageForSigning(messageBytes);
            uint256 hash = Hashes.Hash256(data);
            return this._ECKey.Sign(hash);
        }

        public string SignMessage(String message)
        {
            return SignMessage(Encoding.UTF8.GetBytes(message));
        }

        public string SignMessage(byte[] messageBytes)
        {
            byte[] data = Utils.FormatMessageForSigning(messageBytes);
            uint256 hash = Hashes.Hash256(data);
            return Convert.ToBase64String(SignCompact(hash));
        }


        public byte[] SignCompact(uint256 hash)
        {
            ECDSASignature sig = this._ECKey.Sign(hash);
            // Now we have to work backwards to figure out the recId needed to recover the signature.
            int recId = -1;
            for (int i = 0; i < 4; i++)
            {
                ECKey k = ECKey.RecoverFromSignature(i, sig, hash, this.IsCompressed);
                if (k != null && k.GetPubKey(this.IsCompressed).ToHex() == this.PubKey.ToHex())
                {
                    recId = i;
                    break;
                }
            }

            if (recId == -1)
                throw new InvalidOperationException("Could not construct a recoverable key. This should never happen.");

            int headerByte = recId + 27 + (this.IsCompressed ? 4 : 0);

            var sigData = new byte[65];  // 1 header + 32 bytes for R + 32 bytes for S

            sigData[0] = (byte)headerByte;

            Array.Copy(Utils.BigIntegerToBytes(sig.R, 32), 0, sigData, 1, 32);
            Array.Copy(Utils.BigIntegerToBytes(sig.S, 32), 0, sigData, 33, 32);
            return sigData;
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.vch);
            if (!stream.Serializing)
            {
                this._ECKey = new ECKey(this.vch, true);
            }
        }

        #endregion

        public Key Derivate(byte[] cc, uint nChild, out byte[] ccChild)
        {
            byte[] l = null;
            if ((nChild >> 31) == 0)
            {
                byte[] pubKey = this.PubKey.ToBytes();
                l = Hashes.BIP32Hash(cc, nChild, pubKey[0], pubKey.SafeSubarray(1));
            }
            else
            {
                l = Hashes.BIP32Hash(cc, nChild, 0, this.ToBytes());
            }
            byte[] ll = l.SafeSubarray(0, 32);
            byte[] lr = l.SafeSubarray(32, 32);

            ccChild = lr;

            var parse256LL = new BigInteger(1, ll);
            var kPar = new BigInteger(1, this.vch);
            BigInteger N = ECKey.CURVE.N;

            if (parse256LL.CompareTo(N) >= 0)
                throw new InvalidOperationException("You won a prize ! this should happen very rarely. Take a screenshot, and roll the dice again.");
            BigInteger key = parse256LL.Add(kPar).Mod(N);
            if (key == BigInteger.Zero)
                throw new InvalidOperationException("You won the big prize ! this has probability lower than 1 in 2^127. Take a screenshot, and roll the dice again.");

            byte[] keyBytes = key.ToByteArrayUnsigned();
            if (keyBytes.Length < 32)
                keyBytes = new byte[32 - keyBytes.Length].Concat(keyBytes).ToArray();
            return new Key(keyBytes);
        }

        public Key Uncover(Key scan, PubKey ephem)
        {
            X9ECParameters curve = ECKey.Secp256k1;
            byte[] priv = new BigInteger(1, PubKey.GetStealthSharedSecret(scan, ephem))
                            .Add(new BigInteger(1, this.ToBytes()))
                            .Mod(curve.N)
                            .ToByteArrayUnsigned();

            if (priv.Length < 32)
                priv = new byte[32 - priv.Length].Concat(priv).ToArray();

            var key = new Key(priv, fCompressedIn: this.IsCompressed);
            return key;
        }

        public BitcoinSecret GetBitcoinSecret(Network network)
        {
            return new BitcoinSecret(this, network);
        }

        /// <summary>
        /// Same than GetBitcoinSecret
        /// </summary>
        /// <param name="network"></param>
        /// <returns></returns>
        public BitcoinSecret GetWif(Network network)
        {
            return new BitcoinSecret(this, network);
        }

        public BitcoinEncryptedSecretNoEC GetEncryptedBitcoinSecret(string password, Network network)
        {
            return new BitcoinEncryptedSecretNoEC(this, password, network);
        }

        public string ToString(Network network)
        {
            return new BitcoinSecret(this, network).ToString();
        }

        #region IDestination Members

        public Script ScriptPubKey
        {
            get
            {
                return this.PubKey.Hash.ScriptPubKey;
            }
        }

        #endregion

        public TransactionSignature Sign(uint256 hash, SigHash sigHash)
        {
            return new TransactionSignature(Sign(hash), sigHash);
        }


        public override bool Equals(object obj)
        {
            var item = obj as Key;
            if (item == null)
                return false;
            return this.PubKey.Equals(item.PubKey);
        }
        public static bool operator ==(Key a, Key b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            return a.PubKey == b.PubKey;
        }

        public static bool operator !=(Key a, Key b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.PubKey.GetHashCode();
        }
    }
}

﻿using System;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Networks;
using NBitcoin.OpenAsset;

namespace NBitcoin
{
    public interface IColoredCoin : ICoin
    {
        AssetId AssetId
        {
            get;
        }

        Coin Bearer
        {
            get;
        }
    }

    public interface ICoin
    {
        IMoney Amount
        {
            get;
        }

        OutPoint Outpoint
        {
            get;
        }

        TxOut TxOut
        {
            get;
        }

        /// <summary>
        /// Returns the script actually signed and executed
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Additional information needed to get the ScriptCode</exception>
        /// <returns>The executed script</returns>
        Script GetScriptCode(Network network);

        void OverrideScriptCode(Script scriptCode);

        bool CanGetScriptCode(Network network);

        HashVersion GetHashVersion(Network network);
    }

    public class IssuanceCoin : IColoredCoin
    {
        public IssuanceCoin()
        {
        }

        public IssuanceCoin(Coin bearer)
        {
            this.Bearer = bearer;
        }

        public IssuanceCoin(OutPoint outpoint, TxOut txout)
        {
            this.Bearer = new Coin(outpoint, txout);
        }

        public AssetId AssetId
        {
            get
            {
                return this.Bearer.TxOut.ScriptPubKey.Hash.ToAssetId();
            }
        }

        public Uri DefinitionUrl
        {
            get;
            set;
        }

        #region ICoin Members

        public Money Amount
        {
            get
            {
                return this.Bearer.TxOut.Value;
            }
            set
            {
                this.Bearer.TxOut.Value = value;
            }
        }

        public TxOut TxOut
        {
            get
            {
                return this.Bearer.TxOut;
            }
        }

        #endregion ICoin Members

        public Script ScriptPubKey
        {
            get
            {
                return this.Bearer.TxOut.ScriptPubKey;
            }
        }

        #region IColoredCoin Members

        public Coin Bearer
        {
            get;
            set;
        }

        public OutPoint Outpoint
        {
            get
            {
                return this.Bearer.Outpoint;
            }
        }

        #endregion IColoredCoin Members

        #region IColoredCoin Members

        AssetId IColoredCoin.AssetId
        {
            get
            {
                return this.AssetId;
            }
        }

        Coin IColoredCoin.Bearer
        {
            get
            {
                return this.Bearer;
            }
        }

        #endregion IColoredCoin Members

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return this.Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return this.Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return this.TxOut;
            }
        }

        #endregion ICoin Members

        #region ICoin Members

        public Script GetScriptCode(Network network)
        {
            return this.Bearer.GetScriptCode(network);
        }

        public bool CanGetScriptCode(Network network)
        {
            return this.Bearer.CanGetScriptCode(network);
        }

        public HashVersion GetHashVersion(Network network)
        {
            return this.Bearer.GetHashVersion(network);
        }

        public void OverrideScriptCode(Script scriptCode)
        {
            this.Bearer.OverrideScriptCode(scriptCode);
        }

        #endregion ICoin Members
    }

    public class ColoredCoin : IColoredCoin
    {
        public ColoredCoin()
        {
        }

        public ColoredCoin(AssetMoney asset, Coin bearer)
        {
            this.Amount = asset;
            this.Bearer = bearer;
        }

        public AssetId AssetId
        {
            get
            {
                return this.Amount.Id;
            }
        }

        public AssetMoney Amount
        {
            get;
            set;
        }

        [Obsolete("Use Amount instead")]
        public AssetMoney Asset
        {
            get
            {
                return this.Amount;
            }
            set
            {
                this.Amount = value;
            }
        }

        public Coin Bearer
        {
            get;
            set;
        }

        public TxOut TxOut
        {
            get
            {
                return this.Bearer.TxOut;
            }
        }

        #region ICoin Members

        public OutPoint Outpoint
        {
            get
            {
                return this.Bearer.Outpoint;
            }
        }

        public Script ScriptPubKey
        {
            get
            {
                return this.Bearer.ScriptPubKey;
            }
        }

        #endregion ICoin Members

        #region IColoredCoin Members

        AssetId IColoredCoin.AssetId
        {
            get
            {
                return this.AssetId;
            }
        }

        Coin IColoredCoin.Bearer
        {
            get
            {
                return this.Bearer;
            }
        }

        #endregion IColoredCoin Members

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return this.Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return this.Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return this.TxOut;
            }
        }

        public Script GetScriptCode(Network network)
        {
            return this.Bearer.GetScriptCode(network);
        }

        public bool CanGetScriptCode(Network network)
        {
            return this.Bearer.CanGetScriptCode(network);
        }

        public HashVersion GetHashVersion(Network network)
        {
            return this.Bearer.GetHashVersion(network);
        }

        public void OverrideScriptCode(Script scriptCode)
        {
            this.Bearer.OverrideScriptCode(scriptCode);
        }

        #endregion ICoin Members
    }

    public class Coin : ICoin
    {
        public Coin()
        {
        }

        public Coin(OutPoint fromOutpoint, TxOut fromTxOut)
        {
            this.Outpoint = fromOutpoint;
            this.TxOut = fromTxOut;
        }

        public Coin(Transaction fromTx, uint fromOutputIndex)
        {
            if (fromTx == null)
                throw new ArgumentNullException("fromTx");
            this.Outpoint = new OutPoint(fromTx, fromOutputIndex);
            this.TxOut = fromTx.Outputs[fromOutputIndex];
        }

        public Coin(Transaction fromTx, TxOut fromOutput)
        {
            if (fromTx == null)
                throw new ArgumentNullException("fromTx");
            if (fromOutput == null)
                throw new ArgumentNullException("fromOutput");
            uint outputIndex = (uint)fromTx.Outputs.FindIndex(r => ReferenceEquals(fromOutput, r));
            this.Outpoint = new OutPoint(fromTx, outputIndex);
            this.TxOut = fromOutput;
        }

        public Coin(IndexedTxOut txOut)
        {
            this.Outpoint = new OutPoint(txOut.Transaction.GetHash(), txOut.N);
            this.TxOut = txOut.TxOut;
        }

        public Coin(uint256 fromTxHash, uint fromOutputIndex, Money amount, Script scriptPubKey)
        {
            this.Outpoint = new OutPoint(fromTxHash, fromOutputIndex);
            this.TxOut = new TxOut(amount, scriptPubKey);
        }

        public virtual Script GetScriptCode(Network network)
        {
            if (!CanGetScriptCode(network))
                throw new InvalidOperationException("You need to provide P2WSH or P2SH redeem script with Coin.ToScriptCoin()");
            if (this._OverrideScriptCode != null)
                return this._OverrideScriptCode;
            WitKeyId key = PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(network, this.ScriptPubKey);
            if (key != null)
                return key.AsKeyId().ScriptPubKey;
            return this.ScriptPubKey;
        }

        public virtual bool CanGetScriptCode(Network network)
        {
            return this._OverrideScriptCode != null || !this.ScriptPubKey.IsScriptType(ScriptType.P2SH) && !PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(this.ScriptPubKey);
        }

        public virtual HashVersion GetHashVersion(Network network)
        {
            if (PayToWitTemplate.Instance.CheckScriptPubKey(this.ScriptPubKey))
                return HashVersion.Witness;
            return HashVersion.Original;
        }

        public ScriptCoin ToScriptCoin(Script redeemScript)
        {
            if (redeemScript == null)
                throw new ArgumentNullException("redeemScript");
            var scriptCoin = this as ScriptCoin;
            if (scriptCoin != null)
                return scriptCoin;
            return new ScriptCoin(this, redeemScript);
        }

        public ColoredCoin ToColoredCoin(AssetId asset, ulong quantity)
        {
            return ToColoredCoin(new AssetMoney(asset, quantity));
        }

        public ColoredCoin ToColoredCoin(BitcoinAssetId asset, ulong quantity)
        {
            return ToColoredCoin(new AssetMoney(asset, quantity));
        }

        public ColoredCoin ToColoredCoin(AssetMoney asset)
        {
            return new ColoredCoin(asset, this);
        }

        public OutPoint Outpoint
        {
            get;
            set;
        }

        public TxOut TxOut
        {
            get;
            set;
        }

        #region ICoin Members

        public Money Amount
        {
            get
            {
                if (this.TxOut == null)
                    return Money.Zero;
                return this.TxOut.Value;
            }
            set
            {
                EnsureTxOut();
                this.TxOut.Value = value;
            }
        }

        private void EnsureTxOut()
        {
            if (this.TxOut == null) this.TxOut = new TxOut();
        }

        protected Script _OverrideScriptCode;

        public void OverrideScriptCode(Script scriptCode)
        {
            this._OverrideScriptCode = scriptCode;
        }

        #endregion ICoin Members

        public Script ScriptPubKey
        {
            get
            {
                if (this.TxOut == null)
                    return Script.Empty;
                return this.TxOut.ScriptPubKey;
            }
            set
            {
                EnsureTxOut();
                this.TxOut.ScriptPubKey = value;
            }
        }

        #region ICoin Members

        IMoney ICoin.Amount
        {
            get
            {
                return this.Amount;
            }
        }

        OutPoint ICoin.Outpoint
        {
            get
            {
                return this.Outpoint;
            }
        }

        TxOut ICoin.TxOut
        {
            get
            {
                return this.TxOut;
            }
        }

        #endregion ICoin Members
    }

    public enum RedeemType
    {
        P2SH,
        WitnessV0
    }

    /// <summary>
    /// Represent a coin which need a redeem script to be spent (P2SH or P2WSH)
    /// </summary>
    public class ScriptCoin : Coin
    {
        public ScriptCoin()
        {
        }

        public ScriptCoin(OutPoint fromOutpoint, TxOut fromTxOut, Script redeem)
            : base(fromOutpoint, fromTxOut)
        {
            this.Redeem = redeem;
        }

        internal ScriptCoin(Transaction fromTx, uint fromOutputIndex, Script redeem)
            : base(fromTx, fromOutputIndex)
        {
            this.Redeem = redeem;
        }

        internal ScriptCoin(Transaction fromTx, TxOut fromOutput, Script redeem)
            : base(fromTx, fromOutput)
        {
            this.Redeem = redeem;
        }

        internal ScriptCoin(ICoin coin, Script redeem)
            : base(coin.Outpoint, coin.TxOut)
        {
            this.Redeem = redeem;
        }

        internal ScriptCoin(IndexedTxOut txOut, Script redeem)
            : base(txOut)
        {
            this.Redeem = redeem;
        }

        internal ScriptCoin(uint256 txHash, uint outputIndex, Money amount, Script scriptPubKey, Script redeem)
            : base(txHash, outputIndex, amount, scriptPubKey)
        {
            this.Redeem = redeem;
        }

        public static ScriptCoin Create(Network network, OutPoint fromOutpoint, TxOut fromTxOut, Script redeem)
        {
            return new ScriptCoin(fromOutpoint, fromTxOut, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, Transaction fromTx, uint fromOutputIndex, Script redeem)
        {
            return new ScriptCoin(fromTx, fromOutputIndex, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, Transaction fromTx, TxOut fromOutput, Script redeem)
        {
            return new ScriptCoin(fromTx, fromOutput, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, ICoin coin, Script redeem)
        {
            return new ScriptCoin(coin, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, IndexedTxOut txOut, Script redeem)
        {
            return new ScriptCoin(txOut, redeem).AssertCoherent(network);
        }

        public static ScriptCoin Create(Network network, uint256 txHash, uint outputIndex, Money amount, Script scriptPubKey, Script redeem)
        {
            return new ScriptCoin(txHash, outputIndex, amount, scriptPubKey, redeem).AssertCoherent(network);
        }

        public bool IsP2SH
        {
            get
            {
                return this.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_HASH160;
            }
        }

        public Script GetP2SHRedeem()
        {
            if (!this.IsP2SH)
                return null;
            Script p2shRedeem;
            if (this.RedeemType == RedeemType.P2SH)
            {
                p2shRedeem = this.Redeem;
            }
            else
            {
                p2shRedeem = this.RedeemType == RedeemType.WitnessV0 ? this.Redeem.WitHash.ScriptPubKey :
                            null;
            }

            if (p2shRedeem == null)
                throw new NotSupportedException("RedeemType not supported for getting the P2SH script, contact the library author");
            return p2shRedeem;
        }

        public RedeemType RedeemType
        {
            get
            {
                return this.Redeem.Hash.ScriptPubKey == this.TxOut.ScriptPubKey ?
                    RedeemType.P2SH :
                    RedeemType.WitnessV0;
            }
        }

        public ScriptCoin AssertCoherent(Network network)
        {
            if (this.Redeem == null)
                throw new ArgumentException("redeem cannot be null", "redeem");

            TxDestination expectedDestination = GetRedeemHash(network, this.TxOut.ScriptPubKey);
            if (expectedDestination == null)
            {
                throw new ArgumentException("the provided scriptPubKey is not P2SH or P2WSH");
            }
            if (expectedDestination is ScriptId)
            {
                if (PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(this.Redeem))
                {
                    throw new ArgumentException("The redeem script provided must be the witness one, not the P2SH one");
                }

                if (expectedDestination.ScriptPubKey != this.Redeem.Hash.ScriptPubKey)
                {
                    if (this.Redeem.WitHash.ScriptPubKey.Hash.ScriptPubKey != expectedDestination.ScriptPubKey)
                        throw new ArgumentException("The redeem provided does not match the scriptPubKey of the coin");
                }
            }
            else if (expectedDestination is WitScriptId)
            {
                if (expectedDestination.ScriptPubKey != this.Redeem.WitHash.ScriptPubKey)
                    throw new ArgumentException("The redeem provided does not match the scriptPubKey of the coin");
            }
            else
                throw new NotSupportedException("Not supported redeemed scriptPubkey");

            return this;
        }

        public Script Redeem
        {
            get;
            set;
        }

        public override Script GetScriptCode(Network network)
        {
            if (!CanGetScriptCode(network))
                throw new InvalidOperationException("You need to provide the P2WSH redeem script with ScriptCoin.ToScriptCoin()");
            if (this._OverrideScriptCode != null)
                return this._OverrideScriptCode;
            WitKeyId key = PayToWitPubKeyHashTemplate.Instance.ExtractScriptPubKeyParameters(network, this.Redeem);
            if (key != null)
                return key.AsKeyId().ScriptPubKey;
            return this.Redeem;
        }

        public override bool CanGetScriptCode(Network network)
        {
            return this._OverrideScriptCode != null || !this.IsP2SH || !PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(this.Redeem);
        }

        public override HashVersion GetHashVersion(Network network)
        {
            bool isWitness = PayToWitTemplate.Instance.CheckScriptPubKey(this.ScriptPubKey) ||
                            PayToWitTemplate.Instance.CheckScriptPubKey(this.Redeem) || this.RedeemType == RedeemType.WitnessV0;
            return isWitness ? HashVersion.Witness : HashVersion.Original;
        }

        /// <summary>
        /// Returns the hash contained in the scriptPubKey (P2SH or P2WSH)
        /// </summary>
        /// <param name="scriptPubKey">The scriptPubKey</param>
        /// <returns>The hash of the scriptPubkey</returns>
        public static TxDestination GetRedeemHash(Network network, Script scriptPubKey)
        {
            if (scriptPubKey == null)
                throw new ArgumentNullException("scriptPubKey");
            return PayToScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(scriptPubKey) as TxDestination
                    ??
                    PayToWitScriptHashTemplate.Instance.ExtractScriptPubKeyParameters(network, scriptPubKey);
        }
    }
}
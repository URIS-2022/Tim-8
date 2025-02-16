﻿using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.Consensus.Rules.CommonRules;
using Blockcore.Networks.Stratis.Rules;
using NBitcoin;
using Xunit;

namespace Blockcore.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosTimeMaskRuleTest : PosConsensusRuleUnitTestBase
    {
        private const int MaxFutureDriftBeforeHardFork = 128 * 60 * 60;
        private const int MaxFutureDriftAfterHardFork = 15;

        public PosTimeMaskRuleTest()
        {
            AddBlocksToChain(this.ChainIndexer, 5);
        }

        [Fact]
        public void RunAsync_HeaderVersionBelowMinimalHeaderVersion_ThrowsBadVersionConsensusError()
        {
            var rule = CreateRule<StratisHeaderVersionRule>();

            int MinimalHeaderVersion = 7;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(1);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Version = MinimalHeaderVersion - 1;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => rule.Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadVersion, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkTooHigh_ThrowsProofOfWorkTooHighConsensusErrorAsync()
        {
            var rule = CreateRule<PosTimeMaskRule>();

            SetBlockStake();
            this.network.Consensus.LastPOWBlock = 2;
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = (uint)StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(3);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.ProofOfWorkTooHigh, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_BlockTimeNotTransactionTime_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            var rule = CreateRule<PosTimeMaskRule>();

            SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = (uint)StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp;

            // create a stake trx
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Inputs.Add(new TxIn(new OutPoint(uint256.One, 0)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Outputs.Add(new TxOut(Money.Zero, new Script()));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Outputs.Add(new TxOut(Money.Zero, new Script()));

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(3);

            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = this.ruleContext.ValidationContext.BlockToValidate.Header.Time + MaxFutureDriftAfterHardFork + 1;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = this.ruleContext.ValidationContext.BlockToValidate.Header.Time + MaxFutureDriftAfterHardFork;

            rule.FutureDriftRule = new StratisBugFixPosFutureDriftRule();

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_StakeTimestampInvalid_TransactionTimeDoesNotIncludeStakeTimestampMask_ThrowsStakeTimeViolationConsensusErrorAsync()
        {
            var rule = CreateRule<PosTimeMaskRule>();

            SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = (uint)StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp;

            // create a stake trx
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Inputs.Add(new TxIn(new OutPoint(uint256.One, 0)));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Outputs.Add(new TxOut(Money.Zero, new Script()));
            this.ruleContext.ValidationContext.BlockToValidate.Transactions[1].Outputs.Add(new TxOut(Money.Zero, new Script()));

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(3);
            this.network.Consensus.LastPOWBlock = 12500;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = this.ruleContext.ValidationContext.BlockToValidate.Header.Time + MaxFutureDriftAfterHardFork;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = this.ruleContext.ValidationContext.BlockToValidate.Header.Time + MaxFutureDriftAfterHardFork;

            rule.FutureDriftRule = new StratisBugFixPosFutureDriftRule();

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => rule.RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.StakeTimeViolation, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_BlockTimestampSameAsPrevious_ThrowsBlockTimestampTooEarlyConsensusError()
        {
            var rule = CreateRule<HeaderTimeChecksPosRule>();

            SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(3);
            this.network.Consensus.LastPOWBlock = 12500;

            // time same as previous block.
            uint previousBlockHeaderTime = this.ruleContext.ValidationContext.ChainedHeaderToValidate.Previous.Header.Time;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = previousBlockHeaderTime;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = previousBlockHeaderTime;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = previousBlockHeaderTime;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => rule.Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooEarly, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ValidRuleContext_DoesNotThrowExceptionAsync()
        {
            var rule = CreateRule<PosTimeMaskRule>();

            SetBlockStake(BlockFlag.BLOCK_PROOF_OF_STAKE);
            this.ruleContext.ValidationContext = new ValidationContext();
            this.ruleContext.ValidationContext.BlockToValidate = this.network.Consensus.ConsensusFactory.CreateBlock();
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());
            this.ruleContext.ValidationContext.BlockToValidate.Transactions.Add(this.network.CreateTransaction());

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = this.ChainIndexer.GetHeader(3);
            this.network.Consensus.LastPOWBlock = 12500;

            // time after previous block.
            uint previousBlockHeaderTime = this.ruleContext.ValidationContext.ChainedHeaderToValidate.Previous.Header.Time;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = previousBlockHeaderTime + 62;
            this.ruleContext.ValidationContext.BlockToValidate.Header.Time = previousBlockHeaderTime + 64;
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = previousBlockHeaderTime + 64;

            rule.FutureDriftRule = new StratisBugFixPosFutureDriftRule();

            await rule.RunAsync(this.ruleContext);
        }

        private void SetBlockStake(BlockFlag flg)
        {
            (this.ruleContext as PosRuleContext).BlockStake = new BlockStake()
            {
                Flags = flg
            };
        }

        private void SetBlockStake()
        {
            (this.ruleContext as PosRuleContext).BlockStake = new BlockStake();
        }

    }
}

﻿using System.IO;
using System.Linq;
using Blockcore.Features.Wallet;
using Blockcore.Features.Wallet.Api.Controllers;
using Blockcore.Features.Wallet.Api.Models;
using Blockcore.Features.Wallet.Helpers;
using Blockcore.Features.Wallet.Types;
using Blockcore.IntegrationTests.Common;
using Blockcore.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Blockcore.IntegrationTests.Common.Extensions;
using Blockcore.IntegrationTests.Common.TestNetworks;
using Blockcore.Networks;
using Blockcore.Tests.Common;
using Blockcore.Tests.Common.TestFramework;
using FluentAssertions;
using NBitcoin;
using Xunit.Abstractions;

namespace Blockcore.IntegrationTests.Wallet
{
    public partial class Wallet_address_generation_and_funds_visibility : BddSpecification
    {
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "passphrase";

        public const string AccountZero = "account 0";
        private const string ReceivingNodeName = "receiving";
        private const string SendingNodeName = "sending";

        private CoreNode sendingStratisBitcoinNode;
        private CoreNode receivingStratisBitcoinNode;
        private long walletBalance;
        private NodeBuilder nodeBuilder;
        private Network network;

        protected override void BeforeTest()
        {
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(GetType().Name, this.CurrentTest.DisplayName));
            this.network = new BitcoinRegTestOverrideCoinbaseMaturity(1, "regtest1");
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        public Wallet_address_generation_and_funds_visibility(ITestOutputHelper output) : base(output)
        {
        }

        private void MineSpendableCoins()
        {
            this.sendingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity.Should().Be(this.receivingStratisBitcoinNode.FullNode.Network.Consensus.CoinbaseMaturity);

            TestHelper.MineBlocks(this.sendingStratisBitcoinNode, 2);
        }

        private void a_default_gap_limit_of_20()
        {
            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();

            TestHelper.ConnectAndSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);

            MineSpendableCoins();
        }

        private void a_gap_limit_of_21()
        {
            int customUnusedAddressBuffer = 21;
            var configParameters = new NodeConfigParameters { { "walletaddressbuffer", customUnusedAddressBuffer.ToString() } };

            this.sendingStratisBitcoinNode = this.nodeBuilder.CreateStratisPowNode(this.network).WithWallet().Start();
            this.receivingStratisBitcoinNode = this.nodeBuilder.CreateStratisCustomPowNode(this.network, configParameters).WithWallet().Start();

            TestHelper.ConnectAndSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);
            MineSpendableCoins();
        }

        private void a_wallet_with_funds_at_index_20_which_is_beyond_default_gap_limit()
        {
            ExtPubKey xPublicKey = GetExtendedPublicKey(this.receivingStratisBitcoinNode);
            var recipientAddressBeyondGapLimit = xPublicKey.Derive(new KeyPath("0/20")).PubKey.GetAddress(KnownNetworks.RegTest);

            TransactionBuildContext transactionBuildContext = TestHelper.CreateTransactionBuildContext(
                this.sendingStratisBitcoinNode.FullNode.Network,
                WalletName,
                AccountZero,
                WalletPassword,
                new[] {
                        new Recipient {
                            Amount = Money.COIN * 1,
                            ScriptPubKey = recipientAddressBeyondGapLimit.ScriptPubKey
                        }
                },
                FeeType.Medium, 0);

            var transaction = this.sendingStratisBitcoinNode.FullNode.WalletTransactionHandler()
                 .BuildTransaction(transactionBuildContext);

            this.sendingStratisBitcoinNode.FullNode.NodeController<WalletController>()
                .SendTransaction(new SendTransactionRequest(transaction.ToHex()));

            TestHelper.MineBlocks(this.sendingStratisBitcoinNode, 1);
        }

        private ExtPubKey GetExtendedPublicKey(CoreNode node)
        {
            ExtKey xPrivKey = node.Mnemonic.DeriveExtKey(WalletPassphrase);
            Key privateKey = xPrivKey.PrivateKey;
            ExtPubKey xPublicKey = HdOperations.GetExtendedPublicKey(privateKey, xPrivKey.ChainCode, 44, KnownCoinTypes.Bitcoin, 0);
            return xPublicKey;
        }

        private void getting_wallet_balance()
        {
            TestHelper.WaitForNodeToSync(this.sendingStratisBitcoinNode, this.receivingStratisBitcoinNode);

            this.walletBalance = this.receivingStratisBitcoinNode.FullNode.WalletManager()
               .GetSpendableTransactionsInWallet(WalletName)
               .Sum(s => s.Transaction.Amount);
        }

        private void the_balance_is_zero()
        {
            this.walletBalance.Should().Be(0);
        }

        private void _21_new_addresses_are_requested()
        {
            this.receivingStratisBitcoinNode.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletName, AccountZero), 21);
        }

        private void the_balance_is_NOT_zero()
        {
            this.walletBalance.Should().Be(1 * Money.COIN);
        }
    }
}

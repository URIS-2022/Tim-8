﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Blockcore.AsyncWork;
using Blockcore.Configuration;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Features.BlockStore;
using Blockcore.Features.Wallet.Exceptions;
using Blockcore.Features.Wallet.Interfaces;
using Blockcore.Interfaces;
using Blockcore.Networks;
using Blockcore.Signals;
using Blockcore.Tests.Common;
using Blockcore.Tests.Common.Logging;
using Blockcore.Tests.Wallet.Common;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Xunit;

namespace Blockcore.Features.Wallet.Tests
{
    public class WalletSyncManagerTest : LogsTestBase
    {
        private ChainIndexer chainIndexer;
        private readonly Mock<IWalletManager> walletManager;
        private readonly Mock<IBlockStore> blockStore;
        private readonly Mock<INodeLifetime> nodeLifetime;
        private readonly ILoggerFactory loggerFactory;
        private readonly StoreSettings storeSettings;
        private readonly ISignals signals;
        private readonly IAsyncProvider asyncProvider;

        public WalletSyncManagerTest()
        {
            this.storeSettings = new StoreSettings(new NodeSettings(KnownNetworks.StratisMain));
            this.chainIndexer = new ChainIndexer(KnownNetworks.StratisMain);
            this.walletManager = new Mock<IWalletManager>();
            this.blockStore = new Mock<IBlockStore>();
            this.nodeLifetime = new Mock<INodeLifetime>();
            this.loggerFactory = new LoggerFactory();
            this.signals = new Signals.Signals(new LoggerFactory(), null);
            this.asyncProvider = new AsyncProvider(new LoggerFactory(), this.signals, this.nodeLifetime.Object);

            this.walletManager.Setup(w => w.ContainsWallets).Returns(true);
        }

        [Fact(Skip = "Enables this when wallet can support pruning")]
        public void Start_HavingPrunedStoreSetting_ThrowsWalletException()
        {
            this.storeSettings.AmountOfBlocksToKeep = 1;
            this.storeSettings.PruningEnabled = true;

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.Start();
            });
        }

        [Fact]
        public void Start_BlockOnChain_DoesNotReorgWalletManager()
        {
            this.storeSettings.AmountOfBlocksToKeep = 0;
            this.chainIndexer = WalletTestsHelpers.PrepareChainWithBlock();
            this.walletManager.Setup(w => w.WalletTipHash)
                .Returns(this.chainIndexer.Tip.Header.GetHash());

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.Start();

            this.walletManager.Verify(w => w.GetFirstWalletBlockLocator(), Times.Exactly(0));
            this.walletManager.Verify(w => w.RemoveBlocks(It.IsAny<ChainedHeader>()), Times.Exactly(0));
        }

        [Fact]
        public void Start_BlockNotChain_ReorgsWalletManagerUsingWallet()
        {
            this.storeSettings.AmountOfBlocksToKeep = 0;
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(5, KnownNetworks.StratisMain);
            this.walletManager.SetupGet(w => w.WalletTipHash)
                .Returns(new uint256(125)); // try to load non-existing block to get chain to return null.

            ChainedHeader forkBlock = this.chainIndexer.GetHeader(3); // use a block as the fork to recover to.
            uint256 forkBlockHash = forkBlock.Header.GetHash();
            this.walletManager.Setup(w => w.GetFirstWalletBlockLocator())
                .Returns(new Collection<uint256> { forkBlockHash });

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.Start();

            // verify the walletmanager is reorged using the fork block and it's tip is set to it.
            this.walletManager.Verify(w => w.RemoveBlocks(It.Is<ChainedHeader>(c => c.Header.GetHash() == forkBlockHash)));
            this.walletManager.VerifySet(w => w.WalletTipHash = forkBlockHash);
            Assert.Equal(walletSyncManager.WalletTip.HashBlock.ToString(), forkBlock.HashBlock.ToString());
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is the same as the <see cref="WalletSyncManager.WalletTip"/> pass it directly to the <see cref="WalletManager"/>
        /// and set it as the new WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_PreviousHashSameAsWalletTip_PassesBlockToManagerWithoutReorg()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chainIndexer = result.Chain;
            List<Block> blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);
            walletSyncManager.SetWalletTip(this.chainIndexer.GetHeader(3));

            Block blockToProcess = blocks[3];
            blockToProcess.SetPrivatePropertyValue("BlockSize", 1L);

            walletSyncManager.ProcessBlock(blockToProcess); //4th block in the list has same prevhash as which is loaded

            uint256 expectedBlockHash = AssertTipBlockHash(walletSyncManager, 4);

            AssertTipBlockHash(walletSyncManager, 4);

            this.walletManager.Verify(w => w.ProcessBlock(It.Is<Block>(b => b.GetHash() == blockToProcess.GetHash()), It.Is<ChainedHeader>(c => c.Header.GetHash() == expectedBlockHash)));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is not on the best chain
        /// look for the point at which the chain forked and remove blocks after that fork point from the <see cref="WalletManager"/>.
        /// After removing those blocks use the <see cref="BlockStore"/> to retrieve blocks on the best chain and use those to catchup the WalletManager.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockNotOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer LeftChain, ChainIndexer RightChain, List<Block> LeftForkBlocks, List<Block> RightForkBlocks) result = WalletTestsHelpers.GenerateForkedChainAndBlocksWithHeight(5, KnownNetworks.StratisMain, 2);
            // left side chain containing the 'old' fork.
            ChainIndexer leftChainIndexer = result.LeftChain;
            // right side chain containing the 'new' fork. Work on this.
            this.chainIndexer = result.RightChain;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);
            // setup blockstore to return blocks on the chain.
            this.blockStore.Setup(b => b.GetBlock(It.IsAny<uint256>()))
                .Returns((uint256 hashblock) =>
                {
                    return result.LeftForkBlocks.Union(result.RightForkBlocks).Single(b => b.GetHash() == hashblock);
                });

            // set 4th block of the old chain as tip. 2 ahead of the fork thus not being on the right chain.
            walletSyncManager.SetWalletTip(leftChainIndexer.GetHeader(result.LeftForkBlocks[3].Header.GetHash()));
            //process 5th block from the right side of the fork in the list does not have same prevhash as which is loaded.
            Block blockToProcess = result.RightForkBlocks[4];
            blockToProcess.SetPrivatePropertyValue("BlockSize", 1L);

            walletSyncManager.ProcessBlock(blockToProcess);

            AssertTipBlockHash(walletSyncManager, 5);

            // walletmanager removes all blocks up to the fork.
            this.walletManager.Verify(w => w.RemoveBlocks(ExpectChainedBlock(this.chainIndexer.GetHeader(2))));

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[2]), ExpectChainedBlock(this.chainIndexer.GetHeader(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[3]), ExpectChainedBlock(this.chainIndexer.GetHeader(4))));
            // height 5
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(result.RightForkBlocks[4]), ExpectChainedBlock(this.chainIndexer.GetHeader(5))), Times.Exactly(2));
        }

        /// <summary>
        /// When processing a new <see cref="Block"/> that has a previous hash that is not the same as the <see cref="WalletSyncManager.WalletTip"/> and is on the best chain
        /// see which blocks are missing and retrieve blocks from the <see cref="BlockStore"/> to catchup the <see cref="WalletManager"/>.
        /// Then set the incoming block as the WalletTip.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock__BlockOnBestChain_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chainIndexer = result.Chain;
            List<Block> blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);
            // setup blockstore to return blocks on the chain.
            this.blockStore.Setup(b => b.GetBlock(It.IsAny<uint256>()))
                .Returns((uint256 hashblock) =>
                {
                    return blocks.Single(b => b.GetHash() == hashblock);
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chainIndexer.GetHeader(2));
            //process 4th block in the list does not have same prevhash as which is loaded
            Block blockToProcess = blocks[3];
            blockToProcess.SetPrivatePropertyValue("BlockSize", 1L);

            walletSyncManager.ProcessBlock(blockToProcess);

            AssertTipBlockHash(walletSyncManager, 4);

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chainIndexer.GetHeader(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chainIndexer.GetHeader(4))), Times.Exactly(2));
        }

        /// <summary>
        /// When using the <see cref="BlockStore"/> to catchup on the <see cref="WalletManager"/> and the <see cref="Block"/> is not in the BlockStore yet try to wait until it arrives.
        /// If it does use it to catchup the WalletManager.
        /// </summary>
        [Fact]
        public void ProcessBlock_NewBlock_BlockArrivesLateInBlockStoreCache_ReOrgWalletManagerUsingBlockStoreCache()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(5, KnownNetworks.StratisMain);
            this.chainIndexer = result.Chain;
            List<Block> blocks = result.Blocks;
            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);
            var blockEmptyCounters = new Dictionary<uint256, int>();
            // setup blockstore to return blocks on the chain but postpone by 3 rounds for each block.
            this.blockStore.Setup(b => b.GetBlock(It.IsAny<uint256>()))
                .Returns((uint256 hashblock) =>
                {
                    if (!blockEmptyCounters.ContainsKey(hashblock))
                    {
                        blockEmptyCounters.Add(hashblock, 0);
                    }

                    if (blockEmptyCounters[hashblock] < 3)
                    {
                        blockEmptyCounters[hashblock] += 1;
                        return null;
                    }
                    else
                    {
                        return blocks.Single(b => b.GetHash() == hashblock);
                    }
                });

            // set 2nd block as tip
            walletSyncManager.SetWalletTip(this.chainIndexer.GetHeader(2));
            //process 4th block in the list  does not have same prevhash as which is loaded
            Block blockToProcess = blocks[3];
            blockToProcess.SetPrivatePropertyValue("BlockSize", 1L);

            walletSyncManager.ProcessBlock(blockToProcess);

            AssertTipBlockHash(walletSyncManager, 4);

            //verify manager processes each missing block until caught up.
            // height 3
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[2]), ExpectChainedBlock(this.chainIndexer.GetHeader(3))));
            // height 4
            this.walletManager.Verify(w => w.ProcessBlock(ExpectBlock(blocks[3]), ExpectChainedBlock(this.chainIndexer.GetHeader(4))), Times.Exactly(2));
        }

        [Fact]
        public void ProcessTransaction_CallsWalletManager()
        {
            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
               this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            var transaction = new Transaction
            {
                Version = 15
            };

            walletSyncManager.ProcessTransaction(transaction);

            this.walletManager.Verify(w => w.ProcessTransaction(transaction, null, null, true));
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the closest <see cref="Block"/> to the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateMatchingBlocksOnChain_UpdatesUsingClosestBlock()
        {
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.SyncFromDate(this.chainIndexer.GetHeader(3).Header.BlockTime.DateTime.AddDays(2));

            uint256 expectedHash = this.chainIndexer.GetHeader(3).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the first <see cref="Block"/> if there is no block near the provided date.
        /// </summary>
        [Fact]
        public void SyncFromDate_GivenDateNotMatchingBlocksOnChain_UpdatesUsingFirstBlock()
        {
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.SyncFromDate(new DateTime(1900, 1, 1)); // date before any block.

            uint256 expectedHash = this.chainIndexer.GetHeader(1).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        /// <summary>
        /// Updates the <see cref="WalletSyncManager.WalletTip"/> and the <see cref="WalletManager.WalletTipHash"/> using the genesis <see cref="Block"/> if there is no block on the chain.
        /// </summary>
        [Fact]
        public void SyncFromDate_EmptyChain_UpdateUsingGenesisBlock()
        {
            this.chainIndexer = new ChainIndexer(KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.SyncFromDate(new DateTime(1900, 1, 1)); // date before any block.

            uint256 expectedHash = this.chainIndexer.Genesis.HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        [Fact]
        public void SyncFromHeight_BlockWithHeightOnChain_UpdatesWalletTipOnWalletAndWalletSyncManagers()
        {
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(3, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.SyncFromHeight(2);

            uint256 expectedHash = this.chainIndexer.GetHeader(2).HashBlock;
            Assert.Equal(walletSyncManager.WalletTip.HashBlock, expectedHash);
            this.walletManager.VerifySet(w => w.WalletTipHash = expectedHash);
        }

        [Fact]
        public void SyncFromHeight_NoBlockWithGivenHeightOnChain_ThrowsWalletException()
        {
            this.chainIndexer = WalletTestsHelpers.GenerateChainWithHeight(1, KnownNetworks.StratisMain);

            var walletSyncManager = new WalletSyncManager(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
             this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            Assert.Throws<WalletException>(() =>
            {
                walletSyncManager.SyncFromHeight(2);
            });
        }

        /// <summary>
        /// Don't enqueue new <see cref="Block"/>s - to be processed by <see cref="WalletSyncManager"/> - when there is no Wallet.
        /// </summary>c
        [Fact]
        public void ProcessBlock_With_No_Wallet_Processing_Is_Ignored()
        {
            (ChainIndexer Chain, List<Block> Blocks) result = WalletTestsHelpers.GenerateChainAndBlocksWithHeight(1, KnownNetworks.StratisMain);

            this.chainIndexer = result.Chain;

            this.walletManager.Setup(w => w.ContainsWallets).Returns(false);

            var walletSyncManager = new WalletSyncManagerOverride(this.LoggerFactory.Object, this.walletManager.Object, this.chainIndexer, KnownNetworks.StratisMain,
                this.blockStore.Object, this.storeSettings, this.signals, this.asyncProvider);

            walletSyncManager.SetWalletTip(this.chainIndexer.GetHeader(1));

            walletSyncManager.ProcessBlock(result.Blocks[0]);

            this.walletManager.Verify(w => w.ProcessBlock(It.IsAny<Block>(), It.IsAny<ChainedHeader>()), Times.Never);
        }

        private static ChainedHeader ExpectChainedBlock(ChainedHeader block)
        {
            return It.Is<ChainedHeader>(c => c.Header.GetHash() == block.Header.GetHash());
        }

        private static Block ExpectBlock(Block block)
        {
            return It.Is<Block>(b => b.GetHash() == block.GetHash());
        }

        private class WalletSyncManagerOverride : WalletSyncManager
        {
            public WalletSyncManagerOverride(ILoggerFactory loggerFactory, IWalletManager walletManager, ChainIndexer chainIndexer,
                Network network, IBlockStore blockStore, StoreSettings storeSettings, ISignals signals, IAsyncProvider asyncProvider)
                : base(loggerFactory, walletManager, chainIndexer, network, blockStore, storeSettings, signals, asyncProvider)
            {
            }

            public void SetWalletTip(ChainedHeader tip)
            {
                this.walletTip = tip;
            }
        }

        private static void WaitLoop(Func<bool> act, string failureReason, int millisecondsTimeout = 50)
        {
            if (failureReason == null)
                throw new ArgumentNullException(nameof(failureReason));

            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while (!act())
            {
                try
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(millisecondsTimeout);
                }
                catch (OperationCanceledException e)
                {
                    Assert.False(true, $"{failureReason}{Environment.NewLine}{e.Message}");
                }
            }
        }

        private uint256 AssertTipBlockHash(IWalletSyncManager walletSyncManager, int blockHeight)
        {
            uint256 expectedBlockHash = this.chainIndexer.GetHeader(blockHeight).Header.GetHash();

            WaitLoop(() => expectedBlockHash == walletSyncManager.WalletTip.Header.GetHash(),
                $"Expected block {expectedBlockHash} does not match tip {walletSyncManager.WalletTip.Header.GetHash()}.");

            return expectedBlockHash;
        }
    }
}
﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.AsyncWork;
using Blockcore.Base;
using Blockcore.Base.Deployments;
using Blockcore.Configuration;
using Blockcore.Configuration.Settings;
using Blockcore.Connection;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.Checkpoints;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Consensus.Validators;
using Blockcore.Features.BlockStore;
using Blockcore.Features.BlockStore.Persistence.LevelDb;
using Blockcore.Features.Consensus.CoinViews;
using Blockcore.Features.Consensus.Rules;
using Blockcore.Features.Consensus.Rules.CommonRules;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.MemoryPool.Fee;
using Blockcore.Features.Miner;
using Blockcore.Interfaces;
using Blockcore.Mining;
using Blockcore.Networks;
using Blockcore.P2P;
using Blockcore.P2P.Peer;
using Blockcore.Signals;
using Blockcore.Tests.Common;
using Blockcore.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;

using Xunit;
using Xunit.Sdk;

namespace Blockcore.Features.Consensus.Tests
{
    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestChainContext
    {
        public List<Block> Blocks { get; set; }

        public ConsensusManager Consensus { get; set; }

        public ConsensusRuleEngine ConsensusRules { get; set; }

        public PeerBanning PeerBanning { get; set; }

        public IDateTimeProvider DateTimeProvider { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ConnectionManagerSettings ConnectionSettings { get; set; }

        public ChainIndexer ChainIndexer { get; set; }

        public Network Network { get; set; }

        public IConnectionManager ConnectionManager { get; set; }

        public Mock<IConnectionManager> MockConnectionManager { get; set; }

        public Mock<IReadOnlyNetworkPeerCollection> MockReadOnlyNodesCollection { get; set; }

        public Checkpoints Checkpoints { get; set; }

        public IPeerAddressManager PeerAddressManager { get; set; }

        public IChainState ChainState { get; set; }

        public IFinalizedBlockInfoRepository FinalizedBlockInfo { get; set; }

        public IInitialBlockDownloadState InitialBlockDownloadState { get; set; }

        public HeaderValidator HeaderValidator { get; set; }

        public IntegrityValidator IntegrityValidator { get; set; }

        public PartialValidator PartialValidator { get; set; }

        public FullValidator FullValidator { get; set; }

        public ISignals Signals { get; set; }

        public IAsyncProvider AsyncProvider { get; set; }
    }

    /// <summary>
    /// Factory for creating the test chain.
    /// Much of this logic was taken directly from the embedded TestContext class in MinerTest.cs in the integration tests.
    /// </summary>
    internal class TestChainFactory
    {
        /// <summary>
        /// Creates test chain with a consensus loop.
        /// </summary>
        public static async Task<TestChainContext> CreateAsync(Network network, string dataDir, Mock<IPeerAddressManager> mockPeerAddressManager = null)
        {
            var testChainContext = new TestChainContext() { Network = network };

            testChainContext.NodeSettings = new NodeSettings(network, args: new string[] { $"-datadir={dataDir}" });
            testChainContext.ConnectionSettings = new ConnectionManagerSettings(testChainContext.NodeSettings);
            testChainContext.LoggerFactory = testChainContext.NodeSettings.LoggerFactory;
            testChainContext.DateTimeProvider = DateTimeProvider.Default;

            testChainContext.Signals = new Signals.Signals(testChainContext.NodeSettings.LoggerFactory, null);
            testChainContext.AsyncProvider = new AsyncProvider(testChainContext.NodeSettings.LoggerFactory, testChainContext.Signals, new Mock<INodeLifetime>().Object);

            network.Consensus.Options = new ConsensusOptions();
            //new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(network.Consensus);

            var consensusSettings = new ConsensusSettings(testChainContext.NodeSettings);
            testChainContext.Checkpoints = new Checkpoints();
            testChainContext.ChainIndexer = new ChainIndexer(network);
            testChainContext.ChainState = new ChainState();
            testChainContext.InitialBlockDownloadState = new InitialBlockDownloadState(testChainContext.ChainState, testChainContext.Network, consensusSettings, new Checkpoints(), testChainContext.NodeSettings.LoggerFactory, testChainContext.DateTimeProvider);

            var inMemoryCoinView = new InMemoryCoinView(new HashHeightPair(testChainContext.ChainIndexer.Tip));
            var cachedCoinView = new CachedCoinView(network, new Checkpoints(), inMemoryCoinView, DateTimeProvider.Default, testChainContext.LoggerFactory, new NodeStats(testChainContext.DateTimeProvider, testChainContext.LoggerFactory), new ConsensusSettings(testChainContext.NodeSettings));

            var dataFolder = new DataFolder(TestBase.AssureEmptyDir(dataDir));
            testChainContext.PeerAddressManager =
                mockPeerAddressManager == null ?
                    new PeerAddressManager(DateTimeProvider.Default, dataFolder, testChainContext.LoggerFactory, new SelfEndpointTracker(testChainContext.LoggerFactory, testChainContext.ConnectionSettings))
                    : mockPeerAddressManager.Object;

            testChainContext.MockConnectionManager = new Mock<IConnectionManager>();
            testChainContext.MockReadOnlyNodesCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            testChainContext.MockConnectionManager.Setup(s => s.ConnectedPeers).Returns(testChainContext.MockReadOnlyNodesCollection.Object);
            testChainContext.MockConnectionManager.Setup(s => s.NodeSettings).Returns(testChainContext.NodeSettings);
            testChainContext.MockConnectionManager.Setup(s => s.ConnectionSettings).Returns(testChainContext.ConnectionSettings);

            testChainContext.ConnectionManager = testChainContext.MockConnectionManager.Object;
            var dateTimeProvider = new DateTimeProvider();

            testChainContext.PeerBanning = new PeerBanning(testChainContext.ConnectionManager, testChainContext.LoggerFactory, testChainContext.DateTimeProvider, testChainContext.PeerAddressManager);
            var deployments = new NodeDeployments(testChainContext.Network, testChainContext.ChainIndexer);
            testChainContext.ConsensusRules = new PowConsensusRuleEngine(testChainContext.Network, testChainContext.LoggerFactory, testChainContext.DateTimeProvider,
                testChainContext.ChainIndexer, deployments, consensusSettings, testChainContext.Checkpoints, cachedCoinView, testChainContext.ChainState,
                    new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider, testChainContext.LoggerFactory), testChainContext.AsyncProvider, new ConsensusRulesContainer()).SetupRulesEngineParent();

            testChainContext.HeaderValidator = new HeaderValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.IntegrityValidator = new IntegrityValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.PartialValidator = new PartialValidator(testChainContext.AsyncProvider, testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.FullValidator = new FullValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);

            var dBreezeSerializer = new DataStoreSerializer(network.Consensus.ConsensusFactory);

            var blockRepository = new LevelDbBlockRepository(testChainContext.Network, dataFolder, testChainContext.LoggerFactory, dBreezeSerializer);

            var blockStoreFlushCondition = new BlockStoreQueueFlushCondition(testChainContext.ChainState, testChainContext.InitialBlockDownloadState);

            var blockStore = new BlockStoreQueue(testChainContext.ChainIndexer, testChainContext.ChainState, blockStoreFlushCondition, new Mock<StoreSettings>().Object,
                blockRepository, testChainContext.LoggerFactory, new Mock<INodeStats>().Object, testChainContext.AsyncProvider);

            blockStore.Initialize();

            testChainContext.Consensus = ConsensusManagerHelper.CreateConsensusManager(network, dataDir);

            await testChainContext.Consensus.InitializeAsync(testChainContext.ChainIndexer.Tip);

            return testChainContext;
        }

        public static async Task<List<Block>> MineBlocksWithLastBlockMutatedAsync(TestChainContext testChainContext,
            int count, Script receiver)
        {
            return await MineBlocksAsync(testChainContext, count, receiver, true);
        }

        public static async Task<List<Block>> MineBlocksAsync(TestChainContext testChainContext,
            int count, Script receiver)
        {
            return await MineBlocksAsync(testChainContext, count, receiver, false);
        }

        /// <summary>
        /// Mine new blocks in to the consensus database and the chain.
        /// </summary>
        private static async Task<List<Block>> MineBlocksAsync(TestChainContext testChainContext, int count, Script receiver, bool mutateLastBlock)
        {
            var blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(testChainContext.NodeSettings), testChainContext.LoggerFactory, testChainContext.NodeSettings);
            var mempool = new TxMempool(testChainContext.DateTimeProvider, blockPolicyEstimator, testChainContext.LoggerFactory, testChainContext.NodeSettings);
            var mempoolLock = new MempoolSchedulerLock();

            // Simple block creation, nothing special yet:
            var blocks = new List<Block>();
            for (int i = 0; i < count; i++)
            {
                BlockTemplate newBlock = await MineBlockAsync(testChainContext, receiver, mempool, mempoolLock, mutateLastBlock && i == count - 1);

                blocks.Add(newBlock.Block);
            }

            return blocks;
        }

        private static async Task<BlockTemplate> MineBlockAsync(TestChainContext testChainContext, Script scriptPubKey, TxMempool mempool,
            MempoolSchedulerLock mempoolLock, bool getMutatedBlock = false)
        {
            BlockTemplate newBlock = CreateBlockTemplate(testChainContext, scriptPubKey, mempool, mempoolLock);

            if (getMutatedBlock) BuildMutatedBlock(newBlock);

            newBlock.Block.UpdateMerkleRoot();

            TryFindNonceForProofOfWork(testChainContext, newBlock);

            if (!getMutatedBlock) await ValidateBlock(testChainContext, newBlock);
            else CheckBlockIsMutated(newBlock);

            return newBlock;
        }

        private static BlockTemplate CreateBlockTemplate(TestChainContext testChainContext, Script scriptPubKey,
            TxMempool mempool, MempoolSchedulerLock mempoolLock)
        {
            PowBlockDefinition blockAssembler = new PowBlockDefinition(testChainContext.Consensus,
                testChainContext.DateTimeProvider, testChainContext.LoggerFactory as LoggerFactory, mempool, mempoolLock,
                new MinerSettings(testChainContext.NodeSettings), testChainContext.Network, testChainContext.ConsensusRules, new NodeDeployments(testChainContext.Network, testChainContext.ChainIndexer));

            BlockTemplate newBlock = blockAssembler.Build(testChainContext.ChainIndexer.Tip, scriptPubKey);

            int nHeight = testChainContext.ChainIndexer.Tip.Height + 1; // Height first in coinbase required for block.version=2
            Transaction txCoinbase = newBlock.Block.Transactions[0];
            txCoinbase.Inputs[0] = TxIn.CreateCoinbase(nHeight);
            return newBlock;
        }

        private static void BuildMutatedBlock(BlockTemplate newBlock)
        {
            Transaction coinbaseTransaction = newBlock.Block.Transactions[0];
            Transaction outTransaction = TransactionsHelper.BuildNewTransactionFromExistingTransaction(coinbaseTransaction, 0);
            newBlock.Block.Transactions.Add(outTransaction);
            Transaction duplicateTransaction = TransactionsHelper.BuildNewTransactionFromExistingTransaction(coinbaseTransaction, 1);
            newBlock.Block.Transactions.Add(duplicateTransaction);
            newBlock.Block.Transactions.Add(duplicateTransaction);
        }

        private static void TryFindNonceForProofOfWork(TestChainContext testChainContext, BlockTemplate newBlock)
        {
            int maxTries = int.MaxValue;
            while (maxTries > 0 && !newBlock.Block.CheckProofOfWork())
            {
                ++newBlock.Block.Header.Nonce;
                --maxTries;
            }

            if (maxTries == 0)
                throw new XunitException("Test failed no blocks found");
        }

        private static void CheckBlockIsMutated(BlockTemplate newBlock)
        {
            List<uint256> transactionHashes = newBlock.Block.Transactions.Select(t => t.GetHash()).ToList();
            BlockMerkleRootRule.ComputeMerkleRoot(transactionHashes, out bool isMutated);
            isMutated.Should().Be(true);
        }

        private static async Task ValidateBlock(TestChainContext testChainContext, BlockTemplate newBlock)
        {
            var res = await testChainContext.Consensus.BlockMinedAsync(newBlock.Block);
            Assert.NotNull(res);
        }
    }
}
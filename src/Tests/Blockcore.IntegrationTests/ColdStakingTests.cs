﻿using System;
using Blockcore.Base.Deployments;
using Blockcore.Configuration;
using Blockcore.Consensus;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.IntegrationTests.Common;
using Blockcore.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Blockcore.Networks.Stratis;
using Blockcore.Networks.Stratis.Deployments;
using Blockcore.Utilities.Extensions;
using Xunit;

namespace Blockcore.IntegrationTests
{
    /// <summary>
    /// Prevent network being matched by name and replaced with a different network
    /// in the <see cref="NodeSettings" /> constructor.
    /// </summary>
    public class StratisOverrideRegTest : StratisRegTest
    {
        public StratisOverrideRegTest(string name = null) : base()
        {
            this.Name = name ?? Guid.NewGuid().ToString();
        }
    }

    public class ColdStakingTests
    {
        /// <summary>
        /// Tests that cold staking gets activated as expected.
        /// </summary>
        [Fact]
        public void ColdStakingActivatedOnStratisNode()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                // Create separate network parameters for this test.
                var network = new StratisOverrideRegTest();

                // Set the date ranges such that ColdStaking will 'Start' immediately after the initial confirmation window.
                // Also reduce the minimum number of 'votes' required within the confirmation window to reach 'LockedIn' state.
                network.Consensus.BIP9Deployments[StratisBIP9Deployments.ColdStaking] = new BIP9DeploymentsParameters("Test", 1, 0, DateTime.Now.AddDays(50).ToUnixTimestamp(), 8);

                // Set a small confirmation window to reduce time taken by this test.
                network.Consensus.MinerConfirmationWindow = 10;

                CoreNode stratisNode = builder.CreateStratisPosNode(network, "cs-1-stratisNode").WithWallet();
                stratisNode.Start();

                // ColdStaking activation:
                // - Deployment state changes every 'MinerConfirmationWindow' blocks.
                // - Remains in 'Defined' state until 'startedHeight'.
                // - Changes to 'Started' state at 'startedHeight'.
                // - Changes to 'LockedIn' state at 'lockedInHeight' (as coldstaking should already be signaled in blocks).
                // - Changes to 'Active' state at 'activeHeight'.
                int startedHeight = network.Consensus.MinerConfirmationWindow - 1;
                int lockedInHeight = startedHeight + network.Consensus.MinerConfirmationWindow;
                int activeHeight = lockedInHeight + network.Consensus.MinerConfirmationWindow;

                // Generate enough blocks to cover all state changes.
                TestHelper.MineBlocks(stratisNode, activeHeight + 1);

                // Check that coldstaking states got updated as expected.
                ThresholdConditionCache cache = (stratisNode.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine).NodeDeployments.BIP9;
                Assert.Equal(ThresholdState.Defined, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(startedHeight - 1), StratisBIP9Deployments.ColdStaking));
                Assert.Equal(ThresholdState.Started, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(startedHeight), StratisBIP9Deployments.ColdStaking));
                Assert.Equal(ThresholdState.LockedIn, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(lockedInHeight), StratisBIP9Deployments.ColdStaking));
                Assert.Equal(ThresholdState.Active, cache.GetState(stratisNode.FullNode.ChainIndexer.GetHeader(activeHeight), StratisBIP9Deployments.ColdStaking));

                // Verify that the block created before activation does not have the 'CheckColdStakeVerify' flag set.
                var rulesEngine = stratisNode.FullNode.NodeService<IConsensusRuleEngine>();
                ChainedHeader prevHeader = stratisNode.FullNode.ChainIndexer.GetHeader(activeHeight - 1);
                DeploymentFlags flags1 = (rulesEngine as ConsensusRuleEngine).NodeDeployments.GetFlags(prevHeader);
                Assert.Equal(0, (int)(flags1.ScriptFlags & ScriptVerify.CheckColdStakeVerify));

                // Verify that the block created after activation has the 'CheckColdStakeVerify' flag set.
                DeploymentFlags flags2 = (rulesEngine as ConsensusRuleEngine).NodeDeployments.GetFlags(stratisNode.FullNode.ChainIndexer.Tip);
                Assert.NotEqual(0, (int)(flags2.ScriptFlags & ScriptVerify.CheckColdStakeVerify));
            }
        }
    }
}
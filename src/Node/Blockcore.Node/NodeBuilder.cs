﻿using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.BlockStore;
using Blockcore.Features.ColdStaking;
using Blockcore.Features.Consensus;
using Blockcore.Features.Diagnostic;
using Blockcore.Features.Dns;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.Miner;
using Blockcore.Features.NodeHost;
using Blockcore.Features.Notifications;
using Blockcore.Features.RPC;
using Blockcore.Features.Wallet;
using Blockcore.Networks.X1.Components;
using Blockcore.Persistence;

namespace Blockcore.Node
{
    public static class NodeBuilder
    {
        public static PersistenceProviderManager persistenceProviderManager;

        public static IFullNodeBuilder Create(string chain, NodeSettings settings)
        {
            chain = chain.ToUpperInvariant();

            IFullNodeBuilder nodeBuilder = CreateBaseBuilder(chain, settings);

            switch (chain)
            {
                case "BTC":
                case "XRC":
                    nodeBuilder.UsePowConsensus().AddMining().UseWallet();
                    break;
                case "X1":
                    nodeBuilder.UseX1Consensus().UseColdStakingWallet();
                    break;
                case "AMS":
                case "X42":
                case "BCP":
                case "CITY":
                case "STRAT":
                case "STRAX":
                case "RUTA":
                case "EXOS":
                case "XDS":
                case "XLR":
                case "IMPLX":
                case "MOL":
                case "HOME":
                case "SERF":
                case "CYBITS":
                case "SBC":
                case "RSC":
                    nodeBuilder.UsePosConsensus().AddPowPosMining().UseColdStakingWallet();
                    break;
            }

            return nodeBuilder;
        }

        private static IFullNodeBuilder CreateBaseBuilder(string chain, NodeSettings settings)
        {
            IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
            .UseNodeSettings(settings)
            .UseBlockStore()
            .UseMempool()
            .UseBlockNotification()
            .UseTransactionNotification()
            .UseNodeHost()
            .AddRPC()
            .UseDiagnosticFeature();

            UseDnsFullNode(nodeBuilder, settings);

            return nodeBuilder;
        }
        static void UseDnsFullNode(IFullNodeBuilder nodeBuilder, NodeSettings nodeSettings)
        {
            if (nodeSettings.ConfigReader.GetOrDefault("dnsfullnode", false, nodeSettings.Logger))
            {
                var dnsSettings = new DnsSettings(nodeSettings);

                if (string.IsNullOrWhiteSpace(dnsSettings.DnsHostName) || string.IsNullOrWhiteSpace(dnsSettings.DnsNameServer) || string.IsNullOrWhiteSpace(dnsSettings.DnsMailBox))
                    throw new ConfigurationException("When running as a DNS Seed service, the -dnshostname, -dnsnameserver and -dnsmailbox arguments must be specified on the command line.");

                nodeBuilder.UseDns();
            }
        }
    }
}

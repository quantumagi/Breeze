﻿using System;
using System.Linq;
using System.Threading;
using Breeze.Api;
using Breeze.TumbleBit;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Breeze.Wallet;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Miner;
using Stratis.Bitcoin.Notifications;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.WatchOnlyWallet;

namespace Breeze.Daemon
{
    public class Program
    {
        private const string DefaultBitcoinUri = "http://localhost:5000";
        private const string DefaultStratisUri = "http://localhost:5105";

        public static void Main(string[] args)
        {
            IFullNodeBuilder fullNodeBuilder = null;

            // configure logging
            Logs.Configure(Logs.GetLoggerFactory(args));

            // get the api uri 
            var apiUri = args.SingleOrDefault(arg => arg.StartsWith("apiuri"));
            if (!string.IsNullOrEmpty(apiUri))
            {
                apiUri = apiUri.Replace("apiuri=", string.Empty);
            }

            if (args.Contains("stratis"))
            {
                if (NodeSettings.PrintHelp(args, Network.StratisMain))
                    return;

                var network = args.Contains("-testnet") ? InitStratisTest() : Network.StratisMain;
                var nodeSettings = NodeSettings.FromArguments(args, "stratis", network, ProtocolVersion.ALT_PROTOCOL_VERSION);                
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultStratisUri : apiUri);

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseApi();
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseStratisConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .UseApi();
                }
            }
            else
            {
                NodeSettings nodeSettings = NodeSettings.FromArguments(args);
                nodeSettings.ApiUri = new Uri(string.IsNullOrEmpty(apiUri) ? DefaultBitcoinUri : apiUri);

                if (args.Contains("light"))
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseLightWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification()
                        .UseApi();
                }
                else
                {
                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .UseApi();
                }
            }

            // add the tumbler's settings
            var tumblerAddress = args.SingleOrDefault(arg => arg.StartsWith("-tumbler-uri="));
            if (!string.IsNullOrEmpty(tumblerAddress))
            {
                tumblerAddress = tumblerAddress.Replace("-tumbler-uri=", string.Empty);
                fullNodeBuilder.UseTumbleBit(new Uri(tumblerAddress));
                fullNodeBuilder.UseWatchOnlyWallet();
            }

            var node = fullNodeBuilder.Build();

            //start Full Node - this will also start the API
            node.Run();
        }

        private static Network InitStratisTest()
        {
            Block.BlockSignature = true;
            Transaction.TimeStamp = true;

            var consensus = Network.StratisMain.Consensus.Clone();
            consensus.PowLimit = new Target(uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000"));

            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var pchMessageStart = new byte[4];
            pchMessageStart[0] = 0x71;
            pchMessageStart[1] = 0x31;
            pchMessageStart[2] = 0x21;
            pchMessageStart[3] = 0x11;
            var magic = BitConverter.ToUInt32(pchMessageStart, 0); //0x5223570; 

            var genesis = Network.StratisMain.GetGenesis().Clone();
            genesis.Header.Time = 1493909211;
            genesis.Header.Nonce = 2433759;
            genesis.Header.Bits = consensus.PowLimit;
            consensus.HashGenesisBlock = genesis.GetHash();

            Guard.Assert(consensus.HashGenesisBlock == uint256.Parse("0x00000e246d7b73b88c9ab55f2e5e94d9e22d471def3df5ea448f5576b1d156b9"));

            var builder = new NetworkBuilder()
                .SetName("StratisTest")
                .SetConsensus(consensus)
                .SetMagic(magic)
                .SetGenesis(genesis)
                .SetPort(26178)
                .SetRPCPort(26174)
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[] { (65) })
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[] { (196) })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[] { (65 + 128) })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_NO_EC, new byte[] { 0x01, 0x42 })
                .SetBase58Bytes(Base58Type.ENCRYPTED_SECRET_KEY_EC, new byte[] { 0x01, 0x43 })
                .SetBase58Bytes(Base58Type.EXT_PUBLIC_KEY, new byte[] { (0x04), (0x88), (0xB2), (0x1E) })
                .SetBase58Bytes(Base58Type.EXT_SECRET_KEY, new byte[] { (0x04), (0x88), (0xAD), (0xE4) })
                .AddDNSSeeds(new[]
                {
                    new DNSSeedData("stratisplatform.com", "testnode1.stratisplatform.com"),
                });

            return builder.BuildAndRegister();
        }

    }
}

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blockcore.Networks;
using Blockcore.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class uint256_tests
    {
        private readonly Network networkMain;

        public uint256_tests()
        {
            this.networkMain = KnownNetworks.Main;
        }

        [Fact]
        public void uintTests()
        {
            var v = new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            var v2 = new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            var vless = new uint256("00000000fffffffffffffffffffffffffffffffffffffffffffffffffffffffe");
            var vplus = new uint256("00000001ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

            Assert.Equal("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff", v.ToString());
            Assert.Equal(new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"), v);
            Assert.Equal(new uint256("0x00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"), v);
            Assert.Equal(uint256.Parse("0x00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"), v);
            Assert.True(v < vplus);
            Assert.True(v > vless);
            uint256 unused;
            Assert.True(uint256.TryParse("0x00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.True(uint256.TryParse("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.True(uint256.TryParse("00000000ffffFFfFffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.False(uint256.TryParse("00000000gfffffffffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.False(uint256.TryParse("100000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.False(uint256.TryParse("1100000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff", out unused));
            Assert.Throws<FormatException>(() => uint256.Parse("1100000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            Assert.Throws<FormatException>(() => uint256.Parse("100000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            uint256.Parse("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            Assert.Throws<FormatException>(() => uint256.Parse("000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            Assert.True(v >= v2);
            Assert.True(v <= v2);
            Assert.False(v < v2);
            Assert.False(v > v2);

            Assert.True(v.ToBytes()[0] == 0xFF);
            Assert.True(v.ToBytes(false)[0] == 0x00);

            AssertEquals(v, new uint256(v.ToBytes()));
            AssertEquals(v, new uint256(v.ToBytes(false), false));

            Assert.Equal(0xFF, v.GetByte(0));
            Assert.Equal(0x00, v.GetByte(31));
            Assert.Equal(0x39, new uint256("39000001ffffffffffffffffffffffffffffffffffffffffffffffffffffffff").GetByte(31));
            Assert.Throws<ArgumentOutOfRangeException>(() => v.GetByte(32));
        }

        [Fact]
        public void CanSortuin256()
        {
            SortedDictionary<uint256, uint256> values = new SortedDictionary<uint256, uint256>();
            values.Add(uint256.Zero, uint256.Zero);
            values.Add(uint256.One, uint256.One);
            Assert.Equal(uint256.Zero, values.First().Key);
            Assert.Equal(uint256.One, values.Skip(1).First().Key);
            Assert.Equal(-1, ((IComparable<uint256>)uint256.Zero).CompareTo(uint256.One));
            Assert.Equal(1, ((IComparable<uint256>)uint256.One).CompareTo(uint256.Zero));
            Assert.Equal(1, ((IComparable)uint256.One).CompareTo(null));
            Assert.Equal(1, ((IComparable)uint256.Zero).CompareTo(null));

            Assert.True(null < uint256.Zero);
            Assert.True(uint256.Zero > null);
            Assert.True(null >= (null as uint256));
            Assert.True(null == (null as uint256));

            SortedDictionary<uint160, uint160> values2 = new SortedDictionary<uint160, uint160>();
            values2.Add(uint160.Zero, uint160.Zero);
            values2.Add(uint160.One, uint160.One);
            Assert.Equal(uint160.Zero, values2.First().Key);
            Assert.Equal(uint160.One, values2.Skip(1).First().Key);

            Assert.Equal(-1, ((IComparable<uint160>)uint160.Zero).CompareTo(uint160.One));
            Assert.Equal(1, ((IComparable<uint160>)uint160.One).CompareTo(uint160.Zero));
            Assert.Equal(1, ((IComparable)uint160.One).CompareTo(null));
            Assert.Equal(1, ((IComparable)uint160.Zero).CompareTo(null));

            Assert.True(null < uint160.Zero);
            Assert.True(uint160.Zero > null);
            Assert.True(null >= (null as uint160));
            Assert.True(null == (null as uint160));
        }

        [Fact]
        public void spanUintSerializationTests()
        {
            var v = new uint256(RandomUtils.GetBytes(32));
            Assert.Equal(v, new uint256(v.ToBytes()));
            AssertEx.CollectionEquals(v.ToBytes(), v.AsBitcoinSerializable().ToBytes());
            uint256.MutableUint256 mutable = new uint256.MutableUint256();
            mutable.ReadWrite(v.ToBytes());
            Assert.Equal(v, mutable.Value);
        }

        [Fact]
        public void uitnSerializationTests()
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true, this.networkMain.Consensus.ConsensusFactory);

            var v = new uint256("00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            var vless = new uint256("00000000fffffffffffffffffffffffffffffffffffffffffffffffffffffffe");
            var vplus = new uint256("00000001ffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

            stream.ReadWrite(ref v);
            Assert.NotNull(v);

            ms.Position = 0;
            stream = new BitcoinStream(ms, false, this.networkMain.Consensus.ConsensusFactory);

            uint256 v2 = uint256.Zero;
            stream.ReadWrite(ref v2);
            Assert.Equal(v, v2);

            v2 = null;
            ms.Position = 0;
            stream.ReadWrite(ref v2);
            Assert.Equal(v, v2);

            var vs = new List<uint256>()
            {
                v,vless,vplus
            };

            ms = new MemoryStream();
            stream = new BitcoinStream(ms, true, this.networkMain.Consensus.ConsensusFactory);
            stream.ReadWrite(ref vs);
            Assert.True(vs.Count == 3);

            ms.Position = 0;
            stream = new BitcoinStream(ms, false, this.networkMain.Consensus.ConsensusFactory);
            var vs2 = new List<uint256>();
            stream.ReadWrite(ref vs2);
            Assert.True(vs2.SequenceEqual(vs));

            ms.Position = 0;
            vs2 = null;
            stream.ReadWrite(ref vs2);
            Assert.True(vs2.SequenceEqual(vs));
        }

        private void AssertEquals(uint256 a, uint256 b)
        {
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a == b);
        }
    }
}
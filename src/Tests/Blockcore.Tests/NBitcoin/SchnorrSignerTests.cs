using System.Linq;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
#if HAS_SPAN
using NBitcoin.Secp256k1;
#endif
using Xunit;

namespace NBitcoin.Tests
{
    public class SchnorrSignerTests
    {
        [Fact]
        public void SingningTest()
        {
            var vectors = new string[][]{
                new []{"Test vector 1",
                    "0000000000000000000000000000000000000000000000000000000000000001",
                    "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798",
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "787A848E71043D280C50470E8E1532B2DD5D20EE912A45DBDD2BD1DFBF187EF67031A98831859DC34DFFEEDDA86831842CCD0079E1F92AF177F7F22CC1DCED05"},
                new []{"Test vector 2",
                    "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF",
                    "02DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659",
                    "243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89",
                    "2A298DACAE57395A15D0795DDBFD1DCB564DA82B0F269BC70A74F8220429BA1D1E51A22CCEC35599B8F266912281F8365FFC2D035A230434A1A64DC59F7013FD"},
                new []{"Test vector 3",
                    "C90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B14E5C7",
                    "03FAC2114C2FBB091527EB7C64ECB11F8021CB45E8E7809D3C0938E4B8C0E5F84B",
                    "5E2D58D8B3BCDF1ABADEC7829054F90DDA9805AAB56C77333024B9D0A508B75C",
                    "00DA9B08172A9B6F0466A2DEFD817F2D7AB437E0D253CB5395A963866B3574BE00880371D01766935B92D2AB4CD5C8A2A5837EC57FED7660773A05F0DE142380"}
            };

            foreach (var vector in vectors)
            {
                var privatekey = new Key(Encoders.Hex.DecodeData(vector[1]));
                var publicKey = new PubKey(Encoders.Hex.DecodeData(vector[2]));
                var message = Parseuint256(vector[3]);
                var expectedSignature = SchnorrSignature.Parse(vector[4]);

                var signature = privatekey.SignSchnorr(message);
                Assert.Equal(expectedSignature.ToBytes(), signature.ToBytes());

                Assert.True(publicKey.Verify(message, expectedSignature));
                Assert.True(privatekey.PubKey.Verify(message, expectedSignature));
            }
        }

        [Fact]
        public void ShouldPassVerifycation()
        {
            var publicKey = new PubKey(Encoders.Hex.DecodeData("03DEFDEA4CDB677750A420FEE807EACF21EB9898AE79B9768766E4FAA04A2D4A34"));
            var message = Parseuint256("4DF3C3F68FCC83B27E9D42C90431A72499F17875C81A599B566C9889B9696703");
            var signature = SchnorrSignature.Parse("00000000000000000000003B78CE563F89A0ED9414F5AA28AD0D96D6795F9C6302A8DC32E64E86A333F20EF56EAC9BA30B7246D6D25E22ADB8C6BE1AEB08D49D");
            Assert.True(publicKey.Verify(message, signature));
        }

        private uint256 Parseuint256(string hex)
        {
            var message = uint256.Parse(hex);
            return new uint256(message.ToBytes(false));
        }

        [Fact]
        public void ShouldFailVerifycation()
        {
            var vectors = new string[][]{
                new []{"Test vector 5",
                    "02DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659",
                    "243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89",
                    "2A298DACAE57395A15D0795DDBFD1DCB564DA82B0F269BC70A74F8220429BA1DFA16AEE06609280A19B67A24E1977E4697712B5FD2943914ECD5F730901B4AB7",
                    "incorrect R residuosity"},
                new []{"Test vector 6",
                    "03FAC2114C2FBB091527EB7C64ECB11F8021CB45E8E7809D3C0938E4B8C0E5F84B",
                    "5E2D58D8B3BCDF1ABADEC7829054F90DDA9805AAB56C77333024B9D0A508B75C",
                    "00DA9B08172A9B6F0466A2DEFD817F2D7AB437E0D253CB5395A963866B3574BED092F9D860F1776A1F7412AD8A1EB50DACCC222BC8C0E26B2056DF2F273EFDEC",
                    "negated message hash"},
                new []{"Test vector 7",
                    "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798",
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "787A848E71043D280C50470E8E1532B2DD5D20EE912A45DBDD2BD1DFBF187EF68FCE5677CE7A623CB20011225797CE7A8DE1DC6CCD4F754A47DA6C600E59543C",
                    "negated s value"},
                new []{"Test vector 8",
                    "03DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659",
                    "243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89",
                    "2A298DACAE57395A15D0795DDBFD1DCB564DA82B0F269BC70A74F8220429BA1D1E51A22CCEC35599B8F266912281F8365FFC2D035A230434A1A64DC59F7013FD",
                    "negated public key"}
            };

            foreach (var vector in vectors)
            {
                var publicKey = new PubKey(Encoders.Hex.DecodeData(vector[1]));
                var message = uint256.Parse(vector[2]);
                var expectedSignature = SchnorrSignature.Parse(vector[3]);
                var reason = vector[4];

                Assert.False(publicKey.Verify(message, expectedSignature), reason);
            }
        }

        [Fact]
        public void ShouldPassBatchVerifycation()
        {
            var vectors = new string[][]{
                new []{
                    "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798",
                    "0000000000000000000000000000000000000000000000000000000000000000",
                    "787A848E71043D280C50470E8E1532B2DD5D20EE912A45DBDD2BD1DFBF187EF67031A98831859DC34DFFEEDDA86831842CCD0079E1F92AF177F7F22CC1DCED05"},
                new []{
                    "02DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659",
                    "243F6A8885A308D313198A2E03707344A4093822299F31D0082EFA98EC4E6C89",
                    "2A298DACAE57395A15D0795DDBFD1DCB564DA82B0F269BC70A74F8220429BA1D1E51A22CCEC35599B8F266912281F8365FFC2D035A230434A1A64DC59F7013FD"},
                new []{
                    "03FAC2114C2FBB091527EB7C64ECB11F8021CB45E8E7809D3C0938E4B8C0E5F84B",
                    "5E2D58D8B3BCDF1ABADEC7829054F90DDA9805AAB56C77333024B9D0A508B75C",
                    "00DA9B08172A9B6F0466A2DEFD817F2D7AB437E0D253CB5395A963866B3574BE00880371D01766935B92D2AB4CD5C8A2A5837EC57FED7660773A05F0DE142380"}
            };

            var messages = vectors.Select(v => Parseuint256(v[1])).ToArray();
            var pubkeys = vectors.Select(v => new PubKey(Encoders.Hex.DecodeData(v[0]))).ToArray();
            var signatures = vectors.Select(v => SchnorrSignature.Parse(v[2])).ToArray();

            var randoms = Enumerable.Range(0, 2).Select(x => BigInteger.Arbitrary(256)).ToArray();
            var ok = SchnorrSigner.BatchVerify(messages, pubkeys, signatures, randoms);
            Assert.True(ok);
        }
    }
}

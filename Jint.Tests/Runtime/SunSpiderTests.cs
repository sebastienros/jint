using System;
using System.Net;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.Runtime
{
    public class SunSpiderTests
    {
        private Engine RunTest(string source)
        {
            var engine = new Engine()
                .WithMember("log", new Action<object>(Console.WriteLine))
                .WithMember("assert", new Action<bool>(Assert.True))
            ;

            engine.Execute(source);

            return engine;
        }

        [Theory]
        [InlineData("3d-cube", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/3d-cube.js")]
        public void ThreeDCube(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("3d-morph", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/3d-morph.js")]
        public void ThreeDMorph(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("3d-raytrace", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/3d-raytrace.js")]
        public void ThreeDRaytrace(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("access-binary-trees", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/access-binary-trees.js")]
        public void AccessBinaryTrees(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("access-fannkuch", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/access-fannkuch.js")]
        public void AccessFannkych(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("access-nbody", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/access-nbody.js")]
        public void AccessNBody(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("access-nsieve", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/access-nsieve.js")]
        public void AccessNSieve(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [InlineData("bitops-3bit-bits-in-byte", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/bitops-3bit-bits-in-byte.js")]
        public void Bitops3BitBitsInByte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("bitops-bits-in-byte", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/bitops-bits-in-byte.js")]
        public void BitopsBitsInByte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("bitops-bitwise-and", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/bitops-bitwise-and.js")]
        public void BitopsBitwiseAnd(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("bitops-nsieve-bits", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/bitops-nsieve-bits.js")]
        public void BitopsNSieveBits(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("controlflow-recursive", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/controlflow-recursive.js")]
        public void ControlFlowRecursive(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("crypto-aes", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/crypto-aes.js")]
        public void CryptoAES(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("crypto-md5", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/crypto-md5.js")]
        public void CryptoMD5(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [InlineData("crypto-sha1", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/crypto-sha1.js")]
        public void CryptoSha1(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("date-format-tofte", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/date-format-tofte.js")]
        public void DateFormatTofte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("date-format-xparb", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/date-format-xparb.js")]
        public void DateFormatXParb(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [InlineData("math-cordic", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/math-cordic.js")]
        public void MathCordic(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("math-partial-sums", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/math-partial-sums.js")]
        public void MathPartialSums(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("math-spectral-norm", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/math-spectral-norm.js")]
        public void MathSpectralNorm(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("regexp-dna", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/regexp-dna.js")]
        public void RegexpDna(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("string-base64", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/string-base64.js")]
        public void StringBase64(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [InlineData("string-fasta", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/string-fasta.js")]
        public void StringFasta(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("string-tagcloud", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/string-tagcloud.js")]
        public void StringTagCloud(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("string-unpack-code", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/string-unpack-code.js")]
        public void StringUnpackCode(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }

        [Theory]
        [InlineData("string-validate-input", "https://raw.github.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.1/string-validate-input.js")]
        public void StringValidateInput(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            Assert.DoesNotThrow(() => RunTest(content));
        }
    }
}

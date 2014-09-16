using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Jint.Runtime;
using Xunit;
using Xunit.Extensions;

namespace Jint.Tests.CommonScripts
{
    public class SunSpiderTests
    {
        private Engine RunTest(string source)
        {
            var engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
            ;

            try
            {
                engine.Execute(source);
            }
            catch (JavaScriptException je)
            {
                throw new Exception(je.ToString());
            }

            return engine;
        }

        [Theory]
        [InlineData("3d-cube", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/3d-cube.js")]
        public void ThreeDCube(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("3d-morph", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/3d-morph.js")]
        public void ThreeDMorph(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("3d-raytrace", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/3d-raytrace.js")]
        public void ThreeDRaytrace(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("access-binary-trees", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/access-binary-trees.js")]
        public void AccessBinaryTrees(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("access-fannkuch", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/access-fannkuch.js")]
        public void AccessFannkych(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("access-nbody", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/access-nbody.js")]
        public void AccessNBody(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("access-nsieve", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/access-nsieve.js")]
        public void AccessNSieve(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [InlineData("bitops-3bit-bits-in-byte", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/bitops-3bit-bits-in-byte.js")]
        public void Bitops3BitBitsInByte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("bitops-bits-in-byte", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/bitops-bits-in-byte.js")]
        public void BitopsBitsInByte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("bitops-bitwise-and", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/bitops-bitwise-and.js")]
        public void BitopsBitwiseAnd(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("bitops-nsieve-bits", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/bitops-nsieve-bits.js")]
        public void BitopsNSieveBits(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("controlflow-recursive", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/controlflow-recursive.js")]
        public void ControlFlowRecursive(string name, string url)
        {
            var t = new Thread(() =>
            {
                var content = new WebClient().DownloadString(url);
                RunTest(content);    
            }, 1000000000);
            
            t.Start();
            t.Join();
        }

        [Theory]
        [InlineData("crypto-aes", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/crypto-aes.js")]
        public void CryptoAES(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("crypto-md5", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/crypto-md5.js")]
        public void CryptoMD5(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [InlineData("crypto-sha1", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/crypto-sha1.js")]
        public void CryptoSha1(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("date-format-tofte", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/date-format-tofte.js")]
        public void DateFormatTofte(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("date-format-xparb", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/date-format-xparb.js")]
        public void DateFormatXParb(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [InlineData("math-cordic", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/math-cordic.js")]
        public void MathCordic(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("math-partial-sums", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/math-partial-sums.js")]
        public void MathPartialSums(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("math-spectral-norm", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/math-spectral-norm.js")]
        public void MathSpectralNorm(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("regexp-dna", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/regexp-dna.js")]
        public void RegexpDna(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("string-base64", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/string-base64.js")]
        public void StringBase64(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [InlineData("string-fasta", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/string-fasta.js")]
        public void StringFasta(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("string-tagcloud", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/string-tagcloud.js")]
        public void StringTagCloud(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("string-unpack-code", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/string-unpack-code.js")]
        public void StringUnpackCode(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }

        [Theory]
        [InlineData("string-validate-input", "https://raw.githubusercontent.com/WebKit/webkit/master/PerformanceTests/SunSpider/tests/sunspider-1.0.2/string-validate-input.js")]
        public void StringValidateInput(string name, string url)
        {
            var content = new WebClient().DownloadString(url);
            RunTest(content);
        }
    }
}

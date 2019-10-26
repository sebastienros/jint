using System;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Xunit;

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

        [Theory(DisplayName = "Sunspider")]
        [InlineData("3d-cube", "3d-cube.js")]
        [InlineData("3d-morph", "3d-morph.js")]
        [InlineData("3d-raytrace", "3d-raytrace.js")]
        [InlineData("access-binary-trees", "access-binary-trees.js")]
        [InlineData("access-fannkuch", "access-fannkuch.js")]
        [InlineData("access-nbody", "access-nbody.js")]
        [InlineData("access-nsieve", "access-nsieve.js")]
        [InlineData("bitops-3bit-bits-in-byte", "bitops-3bit-bits-in-byte.js")]
        [InlineData("bitops-bits-in-byte", "bitops-bits-in-byte.js")]
        [InlineData("bitops-bitwise-and", "bitops-bitwise-and.js")]
        [InlineData("bitops-nsieve-bits", "bitops-nsieve-bits.js")]
        [InlineData("controlflow-recursive", "controlflow-recursive.js")]
        [InlineData("crypto-aes", "crypto-aes.js")]
        [InlineData("crypto-md5", "crypto-md5.js")]
        [InlineData("crypto-sha1", "crypto-sha1.js")]
        [InlineData("date-format-tofte", "date-format-tofte.js")]
        [InlineData("date-format-xparb", "date-format-xparb.js")]
        [InlineData("math-cordic", "math-cordic.js")]
        [InlineData("math-partial-sums", "math-partial-sums.js")]
        [InlineData("math-spectral-norm", "math-spectral-norm.js")]
        [InlineData("regexp-dna", "regexp-dna.js")]
        [InlineData("string-base64", "string-base64.js")]
        [InlineData("string-fasta", "string-fasta.js")]
        [InlineData("string-tagcloud", "string-tagcloud.js")]
        [InlineData("string-unpack-code", "string-unpack-code.js")]
        [InlineData("string-validate-input", "string-validate-input.js")]
        public void RunScript(string name, string url)
        {
            var content = GetEmbeddedFile(url);
            RunTest(content);
        }

        private string GetEmbeddedFile(string filename)
        {
            const string prefix = "Jint.Tests.CommonScripts.Scripts.";

            var assembly = typeof(SunSpiderTests).GetTypeInfo().Assembly;
            var scriptPath = prefix + filename;

            using (var stream = assembly.GetManifestResourceStream(scriptPath))
            {
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadToEnd();
                }
            }
        }

    }
}

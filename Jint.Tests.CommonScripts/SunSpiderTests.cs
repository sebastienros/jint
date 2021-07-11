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
                .SetValue("assert", new Action<bool, string>(Assert.True))
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
        [InlineData("3d-cube.js")]
        [InlineData("3d-morph.js")]
        [InlineData("3d-raytrace.js")]
        [InlineData("access-binary-trees.js")]
        [InlineData("access-fannkuch.js")]
        [InlineData("access-nbody.js")]
        [InlineData("access-nsieve.js")]
        [InlineData("bitops-3bit-bits-in-byte.js")]
        [InlineData("bitops-bits-in-byte.js")]
        [InlineData("bitops-bitwise-and.js")]
        [InlineData("bitops-nsieve-bits.js")]
        [InlineData("controlflow-recursive.js")]
        [InlineData("crypto-aes.js")]
        [InlineData("crypto-md5.js")]
        [InlineData("crypto-sha1.js")]
        [InlineData("date-format-tofte.js")]
        [InlineData("date-format-xparb.js")]
        [InlineData("math-cordic.js")]
        [InlineData("math-partial-sums.js")]
        [InlineData("math-spectral-norm.js")]
        [InlineData("regexp-dna.js")]
        [InlineData("string-base64.js")]
        [InlineData("string-fasta.js")]
        [InlineData("string-tagcloud.js")]
        [InlineData("string-unpack-code.js")]
        [InlineData("string-validate-input.js")]
        [InlineData("babel-standalone.js")]
        public void RunScript(string url)
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

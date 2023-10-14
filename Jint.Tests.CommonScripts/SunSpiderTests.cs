using System.Reflection;
using Jint.Runtime;

namespace Jint.Tests.CommonScripts;

[Parallelizable(ParallelScope.All)]
public class SunSpiderTests
{
    private static void RunTest(string source)
    {
        var engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool, string>((condition, message) => Assert.That(condition, message)));

        try
        {
            engine.Execute(source);
        }
        catch (JavaScriptException je)
        {
            throw new Exception(je.ToString());
        }
    }

    [Test]
    [TestCase("3d-cube.js")]
    [TestCase("3d-morph.js")]
    [TestCase("3d-raytrace.js")]
    [TestCase("access-binary-trees.js")]
    [TestCase("access-fannkuch.js")]
    [TestCase("access-nbody.js")]
    [TestCase("access-nsieve.js")]
    [TestCase("bitops-3bit-bits-in-byte.js")]
    [TestCase("bitops-bits-in-byte.js")]
    [TestCase("bitops-bitwise-and.js")]
    [TestCase("bitops-nsieve-bits.js")]
#if !DEBUG // should only be ran in release mode when inlining happens
    [TestCase("controlflow-recursive.js")]
#endif
    [TestCase("crypto-aes.js")]
    [TestCase("crypto-md5.js")]
    [TestCase("crypto-sha1.js")]
    [TestCase("date-format-tofte.js")]
    [TestCase("date-format-xparb.js")]
    [TestCase("math-cordic.js")]
    [TestCase("math-partial-sums.js")]
    [TestCase("math-spectral-norm.js")]
    [TestCase("regexp-dna.js")]
    [TestCase("string-base64.js")]
    [TestCase("string-fasta.js")]
    [TestCase("string-tagcloud.js")]
    [TestCase("string-unpack-code.js")]
    [TestCase("string-validate-input.js")]
    [TestCase("babel-standalone.js")]
    public void Sunspider(string url)
    {
        var content = GetEmbeddedFile(url);
        RunTest(content);
    }

    internal static string GetEmbeddedFile(string filename)
    {
        const string Prefix = "Jint.Tests.CommonScripts.Scripts.";

        var assembly = typeof(SunSpiderTests).GetTypeInfo().Assembly;
        var scriptPath = Prefix + filename;

        using var stream = assembly.GetManifestResourceStream(scriptPath);
        using var sr = new StreamReader(stream);
        return sr.ReadToEnd();
    }
}

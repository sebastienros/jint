using System.Reflection;
using Jint.Runtime;

namespace Jint.Tests.CommonScripts;

[Parallelizable(ParallelScope.All)]
public class SunSpiderTests
{
    /// <summary>
    /// What a single regular-expression match in these scripts is given.
    /// </summary>
    /// <remarks>
    /// Nothing in this suite asserts anything about <c>Options.Constraints.RegexTimeout</c>; every test here
    /// asserts that a real-world script produces the right answer. The engine's ten-second default is
    /// therefore a wedge ceiling, and it was one sized for a machine running a single script: this fixture is
    /// <c>[Parallelizable(ParallelScope.All)]</c>, so twenty-eight CPU-bound workloads share whatever cores
    /// the runner has, and a Windows leg has been observed taking 7 m 19 s for the twenty-eight against ~20 s
    /// unloaded — with <c>RegexMatchTimeoutException</c> as the only symptom (#3358). A minute cannot be
    /// reached by a starved matcher on a pattern these scripts contain, only by a genuinely catastrophic one,
    /// and a catastrophic one reported after a minute is still reported. It stays finite deliberately:
    /// <c>Regex.InfiniteMatchTimeout</c> would turn that failure into a hung run.
    /// </remarks>
    private static readonly TimeSpan RegexWedgeCeiling = TimeSpan.FromMinutes(1);

    private static void RunTest(string source)
    {
        var engine = new Engine(options => options.Constraints.RegexTimeout = RegexWedgeCeiling)
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool, string>(static (condition, message) => condition.Should().BeTrue(message)));

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

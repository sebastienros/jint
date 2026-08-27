namespace Jint.Tests.CommonScripts;

[Parallelizable(ParallelScope.Fixtures)]
public class ConcurrencyTest
{
    [Test]
    public void ConcurrentEnginesCanUseSameAst()
    {
        var scriptContents = SunSpiderTests.GetEmbeddedFile("babel-standalone.js");
        var script = Engine.PrepareScript(scriptContents);

        Parallel.ForEach(Enumerable.Range(0, 3), x =>
        {
            // Same wedge ceiling as SunSpiderTests, and this fixture needs it more: it runs three engines
            // over babel-standalone.js at once, and ParallelScope.Fixtures puts them alongside that
            // fixture's twenty-eight. The engine's ten-second default was sized for one script on one
            // machine, and a starved matcher reaches it long before a catastrophic pattern would.
            new Engine(options => options.Constraints.RegexTimeout = SunSpiderTests.RegexWedgeCeiling)
                .SetValue("assert", new Action<bool, string>((condition, message)=> { }))
                .Evaluate(script);
        });
    }
}

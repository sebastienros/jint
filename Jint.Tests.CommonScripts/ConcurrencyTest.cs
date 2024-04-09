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
            new Engine()
                .SetValue("assert", new Action<bool, string>((condition, message)=> { }))
                .Evaluate(script);
        });
    }
}

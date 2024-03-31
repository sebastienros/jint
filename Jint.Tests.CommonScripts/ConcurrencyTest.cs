namespace Jint.Tests.CommonScripts;

[Parallelizable(ParallelScope.Fixtures)]
public class ConcurrencyTest
{
    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public void ConcurrentEnginesCanUseSameAst(bool prepared)
    {
        var scriptContents = SunSpiderTests.GetEmbeddedFile("babel-standalone.js");
        var script = prepared
            ? Engine.PrepareScript(scriptContents)
            : new JavaScriptParser().ParseScript(scriptContents);

        Parallel.ForEach(Enumerable.Range(0, 3), x =>
        {
            new Engine()
                .SetValue("assert", new Action<bool, string>((condition, message)=> { }))
                .Evaluate(script);
        });
    }
}

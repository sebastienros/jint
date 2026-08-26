using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>JSON.parse</c> rents its reviver's argument array from the engine's pool for the whole recursive
/// walk, and that walk exits by exception whenever the reviver throws — which is ordinary JavaScript.
/// Nothing a script can observe depends on the array coming back, so the bracket is pinned here on the
/// pool itself rather than in the public-interface suite.
/// </summary>
public class JsonPoolingInternalsTests
{
    [Test]
    public void AThrowingReviverGivesTheRentedArgumentArrayBack()
    {
        var engine = new Engine();

        // A bound function is not a ScriptFunction, so the invoker cannot take its register lane and
        // really does rent the three-element array a script reviver would never need.
        engine.Execute("function boom() { throw new Error('boom'); } var reviver = boom.bind(null);");

        var pool = engine._jsValueArrayPool;

        // Drain the pool so the array the parse rents is one this test can name, and put exactly that
        // one back, i.e. leave the pool holding it and nothing else.
        for (var i = 0; i < 16; i++)
        {
            pool.RentArray(3);
        }

        var primed = new JsValue[3];
        pool.ReturnArray(primed);

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("JSON.parse('{\"a\":1}', reviver)"));

        pool.RentArray(3).Should().BeSameAs(primed);
    }
}

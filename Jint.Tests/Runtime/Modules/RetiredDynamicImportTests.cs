using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Tests.Runtime.Modules;

public class RetiredDynamicImportTests
{
    [Test]
    public void DynamicImportAfterRetirementReturnsARejectedPromise()
    {
        using var engine = new Engine();
        engine.SetValue("retire", new Action(() => engine.Advanced.Retire()));

        engine.Execute("retire(); globalThis.result = import('module');");

        var promise = (JsPromise) engine.GetValue("result");
        promise.State.Should().Be(PromiseState.Rejected);
        promise.Value.Get("message").AsString().Should().Contain("retired");
    }
}

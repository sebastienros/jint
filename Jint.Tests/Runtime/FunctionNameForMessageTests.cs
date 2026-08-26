using Jint.Native.Function;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="Function.GetOwnFunctionNameForMessage"/> exists to render a name inside an error message
/// without running script — so it must never throw itself. A script function's own <c>name</c> starts as
/// the shared pending-lazy sentinel, whose value accessors throw by design to make a leak loud; reading
/// the descriptor's <c>Value</c> before anything materialized the property is exactly such a leak.
/// </summary>
public class FunctionNameForMessageTests
{
    [Test]
    public void APendingLazyNameFallsBackToTheClrTypeNameInsteadOfThrowing()
    {
        using var engine = new Engine();

        // The function's own "name" property exists from birth, but its descriptor is the pending
        // sentinel until something reads the property — which nothing here does.
        var function = (Function) engine.Evaluate("(function foo() {})");

        var name = function.GetOwnFunctionNameForMessage();

        Assert.That(name, Is.EqualTo(function.GetType().Name));
    }

    [Test]
    public void AMaterializedNameIsQuoted()
    {
        using var engine = new Engine();

        var function = (Function) engine.Evaluate("const f = function foo() {}; void f.name; f");

        Assert.That(function.GetOwnFunctionNameForMessage(), Is.EqualTo("foo"));
    }
}

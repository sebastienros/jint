#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A Function-constructor instance's call environment is parked on its definition when the call returns,
/// so the next instance built from the same source rents it instead of allocating. Escape analysis lets a
/// closure the call handed out survive that park — <c>EnvironmentMayEscape</c> only asks whether a closure
/// reads one of the environment's own bindings — and such a closure still resolves everything else
/// <em>through</em> that record, so the park must leave its outer link attached.
/// </summary>
public class DynamicFunctionEnvironmentReuseTests
{
    private const string Make =
        "var make = new Function('a', 'var b = a; return function () { return answer; };');";

    [Test]
    public void AClosureReturnedByADynamicFunctionStillReachesTheGlobalScope()
    {
        var engine = new Engine();
        engine.Execute("var answer = 42;");
        engine.Execute(Make);

        // The closure reads no binding of the call it came from, which is what makes that call's
        // environment poolable; its scope chain runs through the parked record all the same.
        engine.Evaluate("make(1)()").Should().Be(42);
    }

    [Test]
    public void TheClosureKeepsAnsweringAfterTheParkedEnvironmentIsRented()
    {
        var engine = new Engine();
        engine.Execute("var answer = 42;");
        engine.Execute(Make);

        engine.Execute("var first = make(1);");

        // The second call rents what the first parked, and re-binds it to that call. The first
        // closure reads nothing from it, so both keep answering.
        engine.Execute("var second = make(2);");
        engine.Evaluate("first()").Should().Be(42);
        engine.Evaluate("second()").Should().Be(42);
    }

    [Test]
    public void AClosureThatDoesReadTheCallKeepsItsOwnValues()
    {
        var engine = new Engine();
        engine.Execute("var make = new Function('a', 'var b = a * 10; return function () { return b; };');");

        // Reading a slot is what the escape analysis looks for, so this call's environment is not
        // pooled at all — the other half of the same rule, and the reason the shape above is the
        // one that could break.
        engine.Execute("var first = make(1); var second = make(2);");
        engine.Evaluate("first()").Should().Be(10);
        engine.Evaluate("second()").Should().Be(20);
    }

    [Test]
    public void AnObjectTheDynamicCallReturnedResolvesGlobalsThroughIt()
    {
        var engine = new Engine();
        engine.Execute("var answer = 7;");
        engine.Execute("var make = new Function('a', 'var b = a; return { read: function () { return answer; } };');");

        engine.Evaluate("make(1).read()").Should().Be(7);
    }
}

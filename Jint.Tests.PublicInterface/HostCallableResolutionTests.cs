#nullable enable

using System.Reflection;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// How a host names the function it wants to call, and — since one of the two ways used to compile and run
/// its argument — what a <see cref="string"/> reaching one of these entry points can and cannot do.
///
/// <para>
/// Through 4.16, <c>Engine.Call(string)</c> and <c>Engine.Construct(string)</c> passed their argument to
/// <see cref="Engine.Evaluate(string, string, ScriptParsingOptions)"/>, so <c>engine.Call(name)</c> executed
/// whatever JavaScript <c>name</c> happened to contain, while the identically documented
/// <see cref="Engine.Invoke(string, object[])"/> did a literal property lookup on the global object. A host
/// reading either XML doc — "the name of the function to call", "the name of the callable" — had no way to
/// tell them apart. Both string overloads are gone in v5; these tests are what stops one coming back.
/// See <see href="https://github.com/sebastienros/jint/issues/3289"/>.
/// </para>
/// </summary>
public class HostCallableResolutionTests
{
    /// <summary>
    /// The surviving <see cref="string"/> entry point resolves one property of the global object, by that
    /// name — the same name <see cref="Engine.SetValue(string, JsValue)"/> writes.
    /// </summary>
    [Test]
    public void InvokeResolvesOnePropertyOfTheGlobalObject()
    {
        var engine = new Engine();
        engine.Execute("function twice(x) { return x * 2; }");

        engine.Invoke("twice", 21).Should().Be(42);
    }

    /// <summary>
    /// A dot is part of the name, not a path separator, so a nested callable is unreachable by name — which
    /// is exactly the failure that used to push a host onto <c>Call(string)</c> and its parser.
    /// </summary>
    [Test]
    public void InvokeDoesNotWalkADottedPath()
    {
        var engine = new Engine();
        engine.Execute("var host = { greet: function () { return 'hi'; } };");

        Invoking(() => engine.Invoke("host.greet"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Can only invoke functions");
    }

    /// <summary>
    /// The documented way to reach one: read the value, then call it.
    /// </summary>
    [Test]
    public void ANestedCallableIsReadFirstAndThenInvoked()
    {
        var engine = new Engine();
        engine.Execute("var host = { greet: function (who) { return 'hi ' + who; } };");

        var greet = engine.GetValue(engine.GetValue("host"), "greet");

        engine.Invoke(greet, "world").Should().Be("hi world");
        engine.Call(greet, "world").Should().Be("hi world");
    }

    /// <summary>
    /// A host that passes caller-supplied text no longer has an execution sink: nothing parses it, so a
    /// function expression is just a name that resolves to nothing.
    /// </summary>
    [Test]
    public void AFunctionExpressionPassedByNameIsNeverExecuted()
    {
        var engine = new Engine();
        engine.SetValue("ran", false);

        Invoking(() => engine.Invoke("(function () { ran = true; return 1; })()"))
            .Should().Throw<JavaScriptException>();

        engine.Evaluate("ran").Should().Be(false);
    }

    /// <summary>
    /// The same string reaching <see cref="Engine.Call(JsValue, JsValue[])"/> — which it does now that the
    /// <see cref="string"/> overload is gone, because <see cref="JsValue"/> has an implicit conversion from
    /// <see cref="string"/> — is a <c>String</c> value that is not callable, so it fails loudly instead.
    /// </summary>
    [Test]
    public void AStringReachingCallIsAValueThatIsNotCallable()
    {
        var engine = new Engine();
        engine.SetValue("ran", false);

        Invoking(() => engine.Call("(function () { ran = true; return 1; })()"))
            .Should().Throw<ArgumentException>();

        engine.Evaluate("ran").Should().Be(false);
    }

    /// <summary>
    /// Same for the constructor entry, which had the identical defect.
    /// </summary>
    [Test]
    public void AStringReachingConstructIsAValueThatIsNotAConstructor()
    {
        var engine = new Engine();
        engine.SetValue("ran", false);

        Invoking(() => engine.Construct("(function () { ran = true; })"))
            .Should().Throw<ArgumentException>();

        engine.Evaluate("ran").Should().Be(false);
    }

    /// <summary>
    /// The API baselines pin the removal for a compiler, but only for the target frameworks a baseline
    /// covers; this states it as a fact about the shipped assembly, from outside it.
    /// </summary>
    [Test]
    public void NoInvocationEntryPointTakesAStringNameOtherThanInvoke()
    {
        var stringTaking = typeof(Engine)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name is "Call" or "Construct")
            .Where(method => method.GetParameters() is [{ ParameterType.FullName: "System.String" }, ..])
            .Select(method => method.Name)
            .ToArray();

        stringTaking.Should().BeEmpty();
    }

    /// <summary>
    /// "A property of the global object" is narrower than "a top-level declaration", and this is the edge
    /// that catches a host migrating off <c>Construct(string)</c>: <c>var</c> and <c>function</c> create a
    /// property, <c>class</c>, <c>let</c> and <c>const</c> create a lexical binding of the global
    /// environment, which is not one. Evaluating the identifier is what reads the second kind.
    /// </summary>
    [Test]
    public void ALexicalDeclarationIsNotAPropertyOfTheGlobalObject()
    {
        var engine = new Engine();
        engine.Execute("function Made(a) { this.a = a; } class Built { constructor(a) { this.a = a; } }");

        engine.GetValue("Made").IsCallable().Should().BeTrue();
        engine.GetValue("Built").Should().Be(JsValue.Undefined);

        engine.Construct(engine.GetValue("Made"), 1).Get("a").Should().Be(1);
        engine.Construct(engine.Evaluate("Built"), 1).Get("a").Should().Be(1);
    }

    /// <summary>
    /// Reading a value by name is a property read and stays one: it never reaches the parser either.
    /// </summary>
    [Test]
    public void GetValueByNameIsAPropertyReadAndNotAnEvaluation()
    {
        var engine = new Engine();
        engine.SetValue("ran", false);

        engine.GetValue("(ran = true)").Should().Be(JsValue.Undefined);
        engine.Evaluate("ran").Should().Be(false);
    }
}

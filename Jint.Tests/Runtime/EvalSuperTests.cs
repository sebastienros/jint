using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see href="https://tc39.es/ecma262/#sec-performeval">PerformEval</see> admits a direct
/// <c>super()</c> in eval code only when its <i>inDerivedConstructor</i> parameter is true. Jint hands
/// that computed flag to the parser as <c>ParserOptions.AllowDirectSuperOutsideMethod</c> rather than
/// switching the option on for every eval, so the parse itself refuses what the specification refuses.
/// The option follows the eval'd unit's this binding, which is why an arrow function declared in the
/// eval may call <c>super()</c> while an ordinary function declared there may not - the latter would
/// have no home object to find one through.
/// </summary>
public class EvalSuperTests
{
    private static string NameOfThrownError(string source)
    {
        var ex = Invoking(() => new Engine().Execute(source)).Should().ThrowExactly<JavaScriptException>().Which;
        return ex.Error.Get("name").AsString();
    }

    [Test]
    public void ADirectEvalInADerivedConstructorMayCallSuper()
    {
        var engine = new Engine();
        engine.Execute("""
            var ran = false;
            class B { constructor() { ran = true; } }
            class D extends B { constructor() { eval("super()"); } }
            new D();
            """);

        engine.Evaluate("ran").AsBoolean().Should().BeTrue();
    }

    // The two shapes staging/sm/class/derivedConstructorArrowEval*SuperCall.js pin: an arrow declared
    // in the eval shares the derived constructor's this binding, however deeply it is nested.
    [TestCase("""class D extends B { constructor() { eval("(() => super())()"); } }""")]
    [TestCase("""class D extends B { constructor() { eval("(() => (() => super())())()"); } }""")]
    [TestCase("""class D extends B { constructor() { eval("(() => { let f = () => super(); f(); })()"); } }""")]
    public void AnArrowDeclaredInTheEvalMayCallSuper(string derivedClass)
    {
        var engine = new Engine();
        engine.Execute("var ran = false; class B { constructor() { ran = true; } }");
        engine.Execute(derivedClass);
        engine.Execute("new D();");

        engine.Evaluate("ran").AsBoolean().Should().BeTrue();
    }

    // Nothing but a direct eval inside a derived constructor may carry one.
    [TestCase("""eval("super()");""")]
    [TestCase("""(0, eval)("super()");""")]
    [TestCase("""function f() { eval("super()"); } f();""")]
    [TestCase("""class B { constructor() { eval("super()"); } } new B();""")]
    [TestCase("""class B { m() { eval("super()"); } } new B().m();""")]
    [TestCase("""class B { static m() { eval("super()"); } } B.m();""")]
    public void EverywhereElseASuperCallInEvalIsASyntaxError(string source)
    {
        NameOfThrownError(source).Should().Be("SyntaxError");
    }

    [Test]
    public void AnArrowInTheDerivedConstructorCarriesTheSameEntitlementIntoItsOwnEval()
    {
        // The arrow shares the constructor's this binding, so a direct eval it makes is still one made
        // in a derived constructor. Calling super() twice is then the runtime error it always is - which
        // is the point: the parse admitted it, the runtime judged it.
        var ex = Invoking(() => new Engine().Execute("""
            class B { constructor() {} }
            class D extends B { constructor() { super(); (() => eval("super()"))(); } }
            new D();
            """)).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Error.Get("name").AsString().Should().Be("ReferenceError");
    }

    [Test]
    public void AnOrdinaryFunctionDeclaredInTheEvalMayNotCallSuper()
    {
        // The option follows the this binding, and an ordinary function introduces one of its own,
        // so this stays a Syntax Error even in the one context where the eval itself may carry a super call.
        NameOfThrownError("""
            class B { constructor() {} }
            class D extends B { constructor() { super(); eval("(function () { super() })"); } }
            new D();
            """).Should().Be("SyntaxError");
    }

    [Test]
    public void AComputedClassElementNameIsReachedByContainsSuperCall()
    {
        // A computed class element name is evaluated in the enclosing scope, so it is a super call of
        // the eval'd unit itself - allowed in a derived constructor...
        var engine = new Engine();
        engine.Execute("""
            var ran = false;
            class B { constructor() { ran = true; } }
            class D extends B { constructor() { eval("class X { [super()]() {} }"); } }
            new D();
            """);
        engine.Evaluate("ran").AsBoolean().Should().BeTrue();

        // ...and a Syntax Error anywhere else.
        NameOfThrownError("""class B { m() { eval("class X { [super()]() {} }"); } } new B().m();""").Should().Be("SyntaxError");
    }

    [Test]
    public void EvalOfAWholeDerivedClassIsUnaffected()
    {
        // The super call belongs to the class's own constructor, not to the eval'd unit, so no context
        // gating applies to it.
        var engine = new Engine();
        engine.Execute("""var C = eval("(class extends Object { constructor() { super(); this.ok = 1; } })"); var instance = new C();""");
        engine.Evaluate("instance.ok").AsNumber().Should().Be(1);
    }

    [Test]
    public void TheEvalCacheKeepsThePermittedAndForbiddenParsesApart()
    {
        // One source, two contexts: the cached parse of the permitted one must not make the forbidden
        // one run, and the forbidden one must not poison the permitted one afterwards.
        var engine = new Engine();
        engine.Execute("""
            var log = [];
            class B { constructor() { log.push("B"); } }
            class D extends B { constructor() { eval("super()"); } }
            class E { constructor() { try { eval("super()"); log.push("no error"); } catch (e) { log.push(e.name); } } }
            new D(); new D(); new E(); new D();
            """);

        engine.Evaluate("log.join(',')").AsString().Should().Be("B,B,SyntaxError,B");
    }

    [Test]
    public void ASuperPropertyInTheEvalStillFollowsTheThisBinding()
    {
        // The sibling option, AllowSuperOutsideMethod, is scoped the same way: a method's eval may read
        // a super property at its top level or from an arrow, but not from an ordinary function.
        var engine = new Engine();
        engine.Execute("""
            class B { m() { return [eval("super.constructor.name"), eval("(() => super.constructor.name)()")].join(","); } }
            var result = new B().m();
            """);
        engine.Evaluate("result").AsString().Should().Be("Object,Object");

        NameOfThrownError("""class B { m() { return eval("(function () { return super.x })"); } } new B().m();""").Should().Be("SyntaxError");
        NameOfThrownError("""eval("super.x");""").Should().Be("SyntaxError");
    }
}

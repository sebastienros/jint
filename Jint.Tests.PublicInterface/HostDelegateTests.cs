using System.Reflection;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Behaviour contract for host delegates registered through <see cref="Engine.SetValue(string, Delegate)"/>.
/// The wrapper caches signature metadata per target method and offers an arity-specialized call lane, so
/// these cover the arities, the params/optional shapes, return-type handling, exception propagation and
/// cross-engine reuse that those two mechanisms must leave untouched.
/// </summary>
public class HostDelegateTests
{
    private static int Sum(params int[] values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total += value;
        }
        return total;
    }

    private static string Join(string separator, params object[] parts) => string.Join(separator, parts);

    [Test]
    public void ZeroArgumentDelegate()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<string>(() => "zero"));

        engine.Evaluate("f()").AsString().Should().Be("zero");
        // extra JavaScript arguments are ignored
        engine.Evaluate("f(1, 2)").AsString().Should().Be("zero");
    }

    [Test]
    public void SingleArgumentDelegate()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<int, int>(x => x * 2));

        engine.Evaluate("f(21)").AsNumber().Should().Be(42);
        // repeated evaluation of one call site so the call-site fast lane is engaged
        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += f(i); } total")
            .AsNumber().Should().Be(90);
    }

    [Test]
    public void TwoAndThreeArgumentDelegates()
    {
        var engine = new Engine();
        engine.SetValue("two", new Func<int, int, int>((a, b) => a - b));
        engine.SetValue("three", new Func<int, int, int, string>((a, b, c) => $"{a}/{b}/{c}"));

        engine.Evaluate("two(10, 4)").AsNumber().Should().Be(6);
        engine.Evaluate("three(1, 2, 3)").AsString().Should().Be("1/2/3");
    }

    [Test]
    public void OverSuppliedArgumentsAreIgnored()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<int, int>(x => x + 1));

        engine.Evaluate("f(1, 99, 100)").AsNumber().Should().Be(2);
    }

    [Test]
    public void UnderSuppliedArgumentsGetClrDefaults()
    {
        var engine = new Engine();
        engine.SetValue("num", new Func<int, int>(x => x + 1));
        engine.SetValue("str", new Func<string, bool>(s => s is null));
        engine.SetValue("two", new Func<int, int, string>((a, b) => $"{a}/{b}"));

        // a missing value-type argument becomes default(T), a missing reference-type argument null
        engine.Evaluate("num()").AsNumber().Should().Be(1);
        engine.Evaluate("str()").AsBoolean().Should().BeTrue();
        engine.Evaluate("two(7)").AsString().Should().Be("7/0");
    }

    [Test]
    public void OptionalClrParametersAreNotSubstituted()
    {
        // A delegate over a method with an optional parameter is still bound positionally: the elided
        // argument gets default(T), not the declared default. This also pins the arity guard on the
        // fast-call lane, which must decline when there are fewer arguments than parameters.
        var engine = new Engine();
        engine.SetValue("f", new Func<int, int, string>(WithOptional));

        engine.Evaluate("f(1, 2)").AsString().Should().Be("1/2");
        engine.Evaluate("f(1)").AsString().Should().Be("1/0");
        engine.Evaluate("var r = ''; for (var i = 0; i < 5; i++) { r = f(i); } r")
            .AsString().Should().Be("4/0");

        static string WithOptional(int a, int b = 5) => $"{a}/{b}";
    }

    [Test]
    public void ParamsArrayDelegate()
    {
        var engine = new Engine();
        engine.SetValue("sum", new Func<int[], int>(Sum));

        engine.Evaluate("sum()").AsNumber().Should().Be(0);
        engine.Evaluate("sum(1)").AsNumber().Should().Be(1);
        engine.Evaluate("sum(1, 2, 3, 4)").AsNumber().Should().Be(10);
    }

    [Test]
    public void ParamsArrayDelegateWithLeadingParameter()
    {
        var engine = new Engine();
        engine.SetValue("join", new Func<string, object[], string>(Join));

        engine.Evaluate("join('-')").AsString().Should().Be("");
        engine.Evaluate("join('-', 'a', 'b', 'c')").AsString().Should().Be("a-b-c");
    }

    private static string Describe(params JsValue[] values)
    {
        var parts = new List<string>();
        foreach (var value in values)
        {
            parts.Add(value.Type.ToString());
        }
        return string.Join(",", parts);
    }

    [Test]
    public void ParamsJsValueArrayDelegatePassesEveryArgumentThrough()
    {
        // A `params JsValue[]` tail is the one params shape whose elements need no conversion at all, so
        // it is built as the typed array it already is. Every value kind has to survive that unchanged.
        var engine = new Engine();
        engine.SetValue("describe", new Func<JsValue[], string>(Describe));

        engine.Evaluate("describe()").AsString().Should().Be("");
        engine.Evaluate("describe(1)").AsString().Should().Be("Number");
        engine.Evaluate("describe('a', 1, true, null, undefined, {}, [])").AsString()
            .Should().Be("String,Number,Boolean,Null,Undefined,Object,Object");
        // repeated evaluation of one call site, so a warmed site is covered too
        engine.Evaluate("function f() { return describe('a', 1); } f(); f(); f()").AsString()
            .Should().Be("String,Number");
    }

    [Test]
    public void ValueReferenceAndVoidReturnTypes()
    {
        var engine = new Engine();
        var sideEffects = new List<int>();

        engine.SetValue("valueReturn", new Func<double>(() => 1.5));
        engine.SetValue("referenceReturn", new Func<string>(() => "text"));
        engine.SetValue("objectReturn", new Func<object>(() => new Dictionary<string, object> { ["k"] = 1 }));
        engine.SetValue("voidReturn", new Action<int>(sideEffects.Add));
        engine.SetValue("voidNoArgs", new Action(() => sideEffects.Add(-1)));

        engine.Evaluate("valueReturn()").AsNumber().Should().Be(1.5);
        engine.Evaluate("referenceReturn()").AsString().Should().Be("text");
        engine.Evaluate("objectReturn().k").AsNumber().Should().Be(1);

        engine.Evaluate("voidReturn(3)").Should().Be(JsValue.Null);
        engine.Evaluate("voidNoArgs()").Should().Be(JsValue.Null);
        sideEffects.Should().Equal(3, -1);
    }

    [Test]
    public void NumericParametersOfEveryWidthBind()
    {
        // The numeric parameter shapes whose boxed representation does not simply follow from the
        // JavaScript value: a wider integral type, a floating-point one, and a nullable both with and
        // without a value.
        var engine = new Engine();
        engine.SetValue("asLong", new Func<long, string>(v => $"{v}"));
        engine.SetValue("asDouble", new Func<double, double>(v => v / 2));
        engine.SetValue("asNullable", new Func<int?, string>(v => v.HasValue ? $"{v.Value}" : "none"));

        engine.Evaluate("asLong(3)").AsString().Should().Be("3");
        engine.Evaluate("var r; for (var i = 0; i < 5; i++) { r = asLong(i); } r").AsString().Should().Be("4");
        engine.Evaluate("asDouble(9)").AsNumber().Should().Be(4.5);
        engine.Evaluate("asNullable(5)").AsString().Should().Be("5");
        engine.Evaluate("asNullable(null)").AsString().Should().Be("none");
    }

    [Test]
    public void JsValueParametersArePassedThroughUnconverted()
    {
        var engine = new Engine();
        engine.SetValue("describe", new Func<JsValue, JsValue, string>((a, b) => $"{a.Type}/{b.Type}"));

        engine.Evaluate("describe('x', 1)").AsString().Should().Be("String/Number");
        engine.Evaluate("describe(undefined, null)").AsString().Should().Be("Undefined/Null");
    }

    [Test]
    public void AJsValueParameterNarrowerThanItsArgumentKeepsTheBinderError()
    {
        // A JsValue-typed parameter takes its argument straight through unconverted, so nothing before
        // the invocation checks that the value fits the declared parameter. The reported failure has to
        // stay the reflection binder's, rather than becoming a cast failure surfaced as a host error.
        var engine = new Engine();
        engine.SetValue("f", new Func<JsString, string>(v => v.ToString()));

        engine.Evaluate("f('ok')").AsString().Should().Be("ok");

        Invoking(() => engine.Execute("f(1);")).Should().Throw<ArgumentException>();
    }

    private sealed class Recorder
    {
        public List<object> Values { get; } = new();

        public void Record(object value) => Values.Add(value);
    }

    [Test]
    public void AVarianceRelaxedDelegateKeepsTheBinderError()
    {
        // Relaxed delegate binding lets the target's parameter be a base type of the delegate's own, so an
        // Action<string> can stand for a void M(object). Arguments are converted to the target's signature
        // and the delegate's is what actually gets invoked, so a value the target would accept can still be
        // rejected - as the reflection binder's ArgumentException against the delegate signature, never as a
        // cast failure surfaced as a host error by a strongly-typed invocation lane.
        var recorder = new Recorder();
        Action<string> narrowed = recorder.Record;

        var engine = new Engine();
        engine.SetValue("f", narrowed);

        engine.Execute("f('ok');");
        recorder.Values.Should().ContainSingle().Which.Should().Be("ok");

        Invoking(() => engine.Execute("f(1);")).Should().Throw<ArgumentException>();
    }

    [Test]
    public void AnOpenInstanceDelegateIsNotMisbound()
    {
        // The delegate type's Invoke declares the receiver as its first parameter while the target
        // MethodInfo does not, so the wrapper's positional binding produces an argument list one short.
        // That has never worked; what matters is that it keeps failing as a binding error instead of
        // being read past the end by a strongly-typed invocation lane.
        var engine = new Engine();
        var length = (Func<string, int>) Delegate.CreateDelegate(
            typeof(Func<string, int>),
            typeof(string).GetProperty(nameof(string.Length))!.GetGetMethod()!);

        engine.SetValue("len", length);

        Invoking(() => engine.Execute("len('abcd');")).Should().Throw<TargetParameterCountException>();
    }

    [Test]
    public void ThrowingDelegateBubblesTheClrException()
    {
        var engine = new Engine();
        engine.SetValue("boom", new Func<int, int>(_ => throw new InvalidOperationException("host failure")));

        var exception = Invoking(() => engine.Execute("boom(1);"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("host failure")
            .Which;

        // the original throw site is preserved rather than replaced by a reflection frame
        exception.StackTrace.Should().Contain(nameof(ThrowingDelegateBubblesTheClrException));
    }

    [Test]
    public void ThrowingDelegateIsCatchableWhenClrExceptionsAreCaught()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("boom", new Func<int>(() => throw new InvalidOperationException("host failure")));

        engine.Evaluate("var m; try { boom(); } catch (e) { m = e.message; } m")
            .AsString().Should().Be("A host operation failed.");
    }

    [Test]
    public void ThrowingDelegateKeepsTheJavaScriptStackTrace()
    {
        var engine = new Engine();
        engine.SetValue("boom", new Action(() => throw new InvalidOperationException("host failure")));

        // a JavaScript error raised from inside the host call keeps the interpreted frames around it
        engine.SetValue("raise", new Action<Engine>(e => e.Execute("throw new Error('inner');")));
        engine.SetValue("self", engine);

        Invoking(() => engine.Execute("function outer() { boom(); } outer();"))
            .Should().Throw<InvalidOperationException>();

        var jsException = Invoking(() => engine.Execute("function outer() { raise(self); } outer();"))
            .Should().Throw<JavaScriptException>().Which;

        jsException.Message.Should().Be("inner");
    }

    [Test]
    public void AwaitableResultsBecomePromises()
    {
        var engine = new Engine();
        engine.SetValue("later", new Func<int, Task<int>>(x => Task.FromResult(x + 1)));

        var promise = engine.Evaluate("later(41)");
        promise.UnwrapIfPromise().AsNumber().Should().Be(42);
    }

    [Test]
    public void TheSameDelegateInstanceWorksOnTwoEngines()
    {
        var shared = new Func<int, int>(x => x * 3);

        var first = new Engine();
        var second = new Engine();
        first.SetValue("f", shared);
        second.SetValue("f", shared);

        first.Evaluate("f(2)").AsNumber().Should().Be(6);
        second.Evaluate("f(3)").AsNumber().Should().Be(9);
        first.Evaluate("f(4)").AsNumber().Should().Be(12);
    }

    [Test]
    public void CapturedStateIsPerDelegateNotPerTargetMethod()
    {
        // Both lambdas compile to the same MethodInfo, which is the metadata cache key - registering
        // them on different engines must not let one wrapper observe the other's captured state.
        static Func<int, int> Adder(int offset) => x => x + offset;

        var first = new Engine();
        var second = new Engine();
        first.SetValue("f", Adder(1));
        second.SetValue("f", Adder(100));

        first.Evaluate("f(10)").AsNumber().Should().Be(11);
        second.Evaluate("f(10)").AsNumber().Should().Be(110);
    }

    [Test]
    public void DirectInvokeAgreesWithTheInterpretedCallSite()
    {
        // Engine.Invoke always takes the array-based entry point while a repeated interpreted call site
        // can take the arity-specialized one; the two must produce the same value for every shape.
        var engine = new Engine();
        engine.SetValue("zero", new Func<string>(() => "z"));
        engine.SetValue("one", new Func<int, string>(a => $"{a}"));
        engine.SetValue("two", new Func<int, string, string>((a, b) => $"{a}{b}"));

        engine.Execute("""
            function callZero() { return zero(); }
            function callOne() { return one(5); }
            function callTwo() { return two(5, 'x'); }
            for (var i = 0; i < 5; i++) { callZero(); callOne(); callTwo(); }
            """);

        engine.Evaluate("callZero()").Should().Be(engine.Invoke("zero"));
        engine.Evaluate("callOne()").Should().Be(engine.Invoke("one", 5));
        engine.Evaluate("callTwo()").Should().Be(engine.Invoke("two", 5, "x"));
    }

    // The tests below warm one call site before asserting, so the arity-specialized lane — only selected
    // once a site has dispatched at least once — is the one under test. Each has an unwarmed twin above;
    // together they pin that the specialized lane answers identically.

    [Test]
    public void AWarmedSiteConvertsAndReturnsExactlyAsAColdOne()
    {
        var engine = new Engine();
        engine.SetValue("one", new Func<int, string>(a => $"{a}"));
        engine.SetValue("two", new Func<int, string, string>((a, b) => $"{a}|{b}"));
        engine.SetValue("nullable", new Func<int?, string>(a => a?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"));

        engine.Execute("""
            function callOne(v) { return one(v); }
            function callTwo(a, b) { return two(a, b); }
            function callNullable(v) { return nullable(v); }
            for (var i = 0; i < 5; i++) { callOne(1); callTwo(1, 'a'); callNullable(1); }
            """);

        engine.Evaluate("callOne(7)").AsString().Should().Be("7");
        engine.Evaluate("callTwo(7, 'z')").AsString().Should().Be("7|z");
        engine.Evaluate("callNullable(7)").AsString().Should().Be("7");
        engine.Evaluate("callNullable(null)").AsString().Should().Be("none");
    }

    [Test]
    public void AWarmedVarianceRelaxedSiteKeepsTheBinderError()
    {
        // The unwarmed twin is AVarianceRelaxedDelegateKeepsTheBinderError. The specialized lane declines
        // on exactly the same exact-type check, so a wrong-typed argument must still reach the reflection
        // binder rather than a cast failure surfaced as a host error.
        var recorder = new Recorder();
        Action<string> narrowed = recorder.Record;

        var engine = new Engine();
        engine.SetValue("f", narrowed);
        engine.Execute("function call(v) { f(v); } for (var i = 0; i < 5; i++) { call('warm'); }");

        recorder.Values.Should().HaveCount(5);
        Invoking(() => engine.Execute("call(1);")).Should().Throw<ArgumentException>();
    }

    [Test]
    public void AWarmedJsValueParameterNarrowerThanItsArgumentKeepsTheBinderError()
    {
        var engine = new Engine();
        engine.SetValue("f", new Func<JsString, string>(v => v.ToString()));
        engine.Execute("function call(v) { return f(v); } for (var i = 0; i < 5; i++) { call('warm'); }");

        engine.Evaluate("call('ok')").AsString().Should().Be("ok");
        Invoking(() => engine.Execute("call(1);")).Should().Throw<ArgumentException>();
    }

    [Test]
    public void AWarmedSiteKeepsTheHostExceptionAndItsThrowSite()
    {
        var engine = new Engine();
        engine.SetValue("boom", new Func<int, int>(x => x < 0 ? throw new InvalidOperationException("host failure") : x));
        engine.Execute("function call(v) { return boom(v); } for (var i = 0; i < 5; i++) { call(1); }");

        var exception = Invoking(() => engine.Execute("call(-1);"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("host failure")
            .Which;

        exception.StackTrace.Should().Contain(nameof(AWarmedSiteKeepsTheHostExceptionAndItsThrowSite));
    }

    [Test]
    public void AWarmedSiteStillConvertsAnAwaitableResult()
    {
        var engine = new Engine();
        engine.SetValue("later", new Func<int, Task<int>>(x => Task.FromResult(x + 1)));
        engine.Execute("function call(v) { return later(v); } for (var i = 0; i < 5; i++) { call(1); }");

        engine.Evaluate("call(41)").UnwrapIfPromise().AsNumber().Should().Be(42);
    }
}

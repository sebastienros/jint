using System.Reflection;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class CallableFlagTests
{
    /// <summary>
    /// Call sites decide callability from <c>InternalTypes.Callable</c> rather than an
    /// <c>is ICallable</c> interface check, so every <see cref="JsValue"/> that implements
    /// <c>ICallable</c> must set the flag in its constructor chain. Rather than enumerate every
    /// concrete function class (there are dozens, and the list would churn), this pins the actual
    /// invariant: each one derives from a root that sets the flag. A new <c>ICallable</c> value type
    /// that skips the flag fails here instead of silently throwing "x is not a function" at runtime.
    /// </summary>
    [Fact]
    public void EveryCallableJsValueDerivesFromAFlagSettingRoot()
    {
        var flagSettingRoots = new[]
        {
            typeof(Function),
            typeof(BindFunction),
            typeof(NamespaceReference),
            // internal: JsProxy and IsHTMLDDA are resolved by name below
        };

        var assembly = typeof(Engine).Assembly;
        var callableInterface = assembly.GetType("Jint.Native.ICallable", throwOnError: true)!;
        var roots = flagSettingRoots
            .Concat(new[] { assembly.GetType("Jint.Native.JsProxy", throwOnError: true)!, assembly.GetType("Jint.Native.IsHTMLDDA", throwOnError: true)! })
            .ToArray();

        var unflagged = assembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && !t.IsInterface
                        && typeof(JsValue).IsAssignableFrom(t)
                        && callableInterface.IsAssignableFrom(t)
                        && !Array.Exists(roots, r => r.IsAssignableFrom(t)))
            .Select(t => t.FullName)
            .ToArray();

        unflagged.Should().BeEmpty(
            "every ICallable JsValue must set InternalTypes.Callable in its constructor (add it, then add the type here as a root if it is a new root)");
    }

    /// <summary>
    /// The call path also decides whether a callee gets a call-stack frame from
    /// <c>InternalTypes.Function</c> rather than <c>is Function</c>, so the flag must be set by
    /// <see cref="Function"/> and by nothing else. The other <c>ICallable</c> roots must NOT carry it —
    /// if one did, it would be reinterpreted as a <see cref="Function"/> and pushed onto the call stack.
    /// </summary>
    [Fact]
    public void OnlyFunctionCarriesTheFunctionFlag()
    {
        var engine = new Engine(options => options.AllowClr(typeof(System.Collections.Generic.List<>).Assembly));

        // a Function: ordinary script function, and a built-in
        AssertFunctionFlag(engine.Evaluate("(function () {})"), expected: true);
        AssertFunctionFlag(engine.Evaluate("String.prototype.charAt"), expected: true);

        // ICallable but NOT Function — these must not be reinterpreted as functions
        AssertFunctionFlag(engine.Evaluate("(function () {}).bind(null)"), expected: false);   // BindFunction
        AssertFunctionFlag(engine.Evaluate("new Proxy(function () {}, {})"), expected: false); // JsProxy
        AssertFunctionFlag(engine.Evaluate("importNamespace('System.Collections.Generic')"), expected: false); // NamespaceReference

        // and calling each of them still works, i.e. the non-Function branch is exercised
        Assert.Equal(3d, engine.Evaluate("(function (a, b) { return a + b; }).bind(null, 1)(2)").AsNumber());
        Assert.Equal(3d, engine.Evaluate("new Proxy(function (a, b) { return a + b; }, {})(1, 2)").AsNumber());
    }

    private static void AssertFunctionFlag(Jint.Native.JsValue value, bool expected)
    {
        var isFunction = value is Function;
        isFunction.Should().Be(expected, "`is Function` for {0}", value.GetType());

        // read the internal flag through the same enum the call path uses
        var type = (global::Jint.Runtime.InternalTypes) typeof(Jint.Native.JsValue)
            .GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(value)!;
        var flagged = (type & global::Jint.Runtime.InternalTypes.Function) != global::Jint.Runtime.InternalTypes.Empty;

        flagged.Should().Be(expected, "InternalTypes.Function for {0} must agree with `is Function`", value.GetType());
    }

    /// <summary>
    /// The flag is only meaningful if it agrees with the interface check for values that actually
    /// reach a call site, so exercise the four shapes that are easy to get wrong: an ordinary
    /// function, a bound function, a proxy over a function, and the interop namespace reference
    /// (which is callable purely to bind generic types).
    /// </summary>
    [Fact]
    public void FlaggedCallablesAreCallableFromScript()
    {
        var engine = new Engine(options => options.AllowClr(typeof(System.Collections.Generic.List<>).Assembly));

        Assert.Equal(3d, engine.Evaluate("(function (a, b) { return a + b; })(1, 2)").AsNumber());
        Assert.Equal(3d, engine.Evaluate("(function (a, b) { return a + b; }).bind(null, 1)(2)").AsNumber());
        Assert.Equal(3d, engine.Evaluate("new Proxy(function (a, b) { return a + b; }, {})(1, 2)").AsNumber());

        // NamespaceReference.Call: binds the generic type argument, so the result is constructible.
        Assert.Equal(1d, engine.Evaluate("""
            var listType = importNamespace('System.Collections.Generic').List(System.String);
            var list = new listType();
            list.Add('x');
            list.Count;
            """).AsNumber());
    }
}

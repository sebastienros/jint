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

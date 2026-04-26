using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class ReflectTests
{
    // Regression tests for the spec's "argument is not present" vs "argument is undefined"
    // distinction in Reflect.construct / get / set. Per ECMA-262, "not present" defaults to
    // target / undefined-target paths, but explicit `undefined` is preserved and reaches the
    // downstream operation (IsConstructor check, [[Set]] receiver-not-Object branch, accessor
    // `this`).

    [Fact]
    public void ReflectConstructExplicitUndefinedNewTargetThrows()
    {
        var engine = new Engine();
        engine.Execute("class C { constructor() {} }");

        // newTarget not present — defaults to target, succeeds
        engine.Evaluate("Reflect.construct(C, [])");

        // newTarget explicitly undefined — IsConstructor(undefined) is false, throws TypeError
        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("Reflect.construct(C, [], undefined)"));
        Assert.Equal("TypeError", ex.Error.Get("name").AsString());
    }

    [Fact]
    public void ReflectSetExplicitUndefinedReceiverReturnsFalse()
    {
        var engine = new Engine();

        // receiver not present — defaults to target, set succeeds
        Assert.True(engine.Evaluate("Reflect.set({}, 'p', 1)").AsBoolean());

        // receiver explicitly undefined — non-Object receiver, [[Set]] returns false
        Assert.False(engine.Evaluate("Reflect.set({}, 'p', 1, undefined)").AsBoolean());
    }

    [Fact]
    public void ReflectGetExplicitUndefinedReceiverPropagatesToAccessor()
    {
        var engine = new Engine();
        engine.Execute("""
            "use strict";
            var seen;
            var obj = {};
            Object.defineProperty(obj, 'p', { get: function() { seen = this; return 1; } });
        """);

        // receiver not present — defaults to target, getter sees `this === obj`
        engine.Evaluate("Reflect.get(obj, 'p')");
        Assert.True(engine.Evaluate("seen === obj").AsBoolean());

        // receiver explicitly undefined — getter sees `this === undefined`
        engine.Evaluate("Reflect.get(obj, 'p', undefined)");
        Assert.True(engine.Evaluate("seen === undefined").AsBoolean());
    }
}

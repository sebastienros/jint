using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Compound assignment to slot-stored bindings (the discard-mode fast paths) must stay
/// observably identical to the materialized path, including the in-place rope-append skip.
/// </summary>
public class CompoundAssignmentTests
{
    [Fact]
    public void StringAppendLoopInFunction()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("function f() { var s = ''; for (var i = 0; i < 100; i++) { s += 'ab'; } return s; } f();").AsString();

        Assert.Equal(string.Concat(Enumerable.Repeat("ab", 100)), result);
    }

    [Fact]
    public void RightSideReassignmentPinsCurrentInPlaceAppendBehavior()
    {
        // The materialized lane skips the write-back when the rope was mutated in place,
        // so a right-hand side that reassigns the binding wins. This pins that behavior
        // so the slot lane cannot silently diverge from the slow path.
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("function f() { var s = ''; s += 'x'; s += (s = 'z', 'a'); return s; } f();").AsString();

        Assert.Equal("z", result);
    }

    [Fact]
    public void RightSideReassignmentOnNumberSlot()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("function f() { var n = 5; n += (n = 1, 2); return n; } f();").AsNumber();

        Assert.Equal(7, result);
    }

    [Fact]
    public void CompoundAssignToConstThrowsTypeError()
    {
        var engine = new Engine(static options => options.Strict());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("function f() { const c = 'a'; c += 'b'; } f();"));
        Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.TypeError));
    }

    [Fact]
    public void CompoundAssignInTemporalDeadZoneThrowsReferenceError()
    {
        var engine = new Engine(static options => options.Strict());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("function f() { { t += 'x'; let t; } } f();"));
        Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.ReferenceError));
    }

    [Fact]
    public void NumberSlotWithStringRightHandSide()
    {
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("function f() { var n = 2; n += 'x'; return n; } f();").AsString();

        Assert.Equal("2x", result);
    }

    [Fact]
    public void RightSideReassignmentPinnedInEvalBody()
    {
        // eval bodies have completion-value semantics, so their expression statements take the
        // value-producing lane; the in-place-append skip must behave identically there.
        var engine = new Engine(static options => options.Strict());
        var result = engine.Evaluate("eval(\"var s = ''; s += 'x'; s += (s = 'z', 'a'); s;\")").AsString();

        Assert.Equal("z", result);
    }

    [Fact]
    public void CompoundAssignmentIsTheEvalCompletionValue()
    {
        var engine = new Engine(static options => options.Strict());

        Assert.Equal("ab", engine.Evaluate("eval(\"var s = 'a'; s += 'b';\")").AsString());
        Assert.Equal("ab|ab", engine.Evaluate("eval(\"var q = 'a'; var t = (q += 'b'); t + '|' + q;\")").AsString());
    }

    [Fact]
    public void ConstCompoundAssignInEvalThrowsTypeError()
    {
        var engine = new Engine(static options => options.Strict());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Execute("eval(\"const c = 'a'; c += 'b';\");"));
        Assert.True(ex.Error.InstanceofOperator(engine.Intrinsics.TypeError));
    }

    [Fact]
    public void StringAppendLoopInsideEval()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute("var out; var src = \"var s = ''; for (var i = 0; i < 50; i++) { s += 'q'; } out = s;\";");

        for (var i = 0; i < 3; i++)
        {
            engine.Execute("eval(src);");
            Assert.Equal(new string('q', 50), engine.Evaluate("out").AsString());
        }
    }
}

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the unboxed number binding fast paths: numeric read-modify-write statements on
/// function-local slot bindings store raw doubles; materializing reads must observe exact
/// JS number semantics (including -0, NaN, infinities and int32 boundaries) and all
/// non-qualifying shapes must keep their full error/coercion behavior.
/// </summary>
public class UnboxedBindingTests
{
    private readonly Engine _engine = new();

    [Fact]
    public void DoubleAccumulatorProducesExactResult()
    {
        Assert.Equal(1.0, _engine.Evaluate("function f() { var s = 0.5; s += 0.25; s += 0.25; return s; } f()").AsNumber());
        Assert.Equal(2500d, _engine.Evaluate("function f() { var s = 0; for (var i = 0; i < 10000; i++) { s += 0.25; } return s; } f()").AsNumber());
    }

    [Fact]
    public void AllCompoundOperatorsWork()
    {
        Assert.Equal(7d, _engine.Evaluate("function f() { var x = 10; x -= 3; return x; } f()").AsNumber());
        Assert.Equal(2.5, _engine.Evaluate("function f() { var x = 5; x *= 0.5; return x; } f()").AsNumber());
        Assert.Equal(2.5, _engine.Evaluate("function f() { var x = 5; x /= 2; return x; } f()").AsNumber());
        Assert.Equal(1d, _engine.Evaluate("function f() { var x = 7; x %= 3; return x; } f()").AsNumber());
        Assert.Equal(4d, _engine.Evaluate("function f() { var x = 6; x &= 12; return x; } f()").AsNumber());
        Assert.Equal(14d, _engine.Evaluate("function f() { var x = 6; x |= 12; return x; } f()").AsNumber());
        Assert.Equal(10d, _engine.Evaluate("function f() { var x = 6; x ^= 12; return x; } f()").AsNumber());
        Assert.Equal(24d, _engine.Evaluate("function f() { var x = 6; x <<= 2; return x; } f()").AsNumber());
        Assert.Equal(1d, _engine.Evaluate("function f() { var x = 6; x >>= 2; return x; } f()").AsNumber());
        Assert.Equal(1073741822d, _engine.Evaluate("function f() { var x = -6; x >>>= 2; return x; } f()").AsNumber());
        Assert.Equal(8d, _engine.Evaluate("function f() { var x = 2; x **= 3; return x; } f()").AsNumber());
    }

    [Fact]
    public void UpdateExpressionsWork()
    {
        Assert.Equal(3d, _engine.Evaluate("function f() { var i = 0; i++; i++; ++i; return i; } f()").AsNumber());
        Assert.Equal(-3d, _engine.Evaluate("function f() { var i = 0; i--; i--; --i; return i; } f()").AsNumber());
        Assert.Equal(0.5, _engine.Evaluate("function f() { var i = -0.5; i++; return i; } f()").AsNumber());
    }

    [Fact]
    public void Int32BoundaryWidensCorrectly()
    {
        Assert.Equal(2147483648d, _engine.Evaluate("function f() { var i = 2147483647; i++; return i; } f()").AsNumber());
        Assert.Equal(-2147483649d, _engine.Evaluate("function f() { var i = (1<<31)|0; i--; return i; } f()").AsNumber());
        Assert.Equal(2147483648d, _engine.Evaluate("function f() { var i = (1<<31)|0; i /= -1; return i; } f()").AsNumber());
    }

    [Fact]
    public void SpecialValuesSurviveMaterialization()
    {
        Assert.True(_engine.Evaluate("function f() { var z = 0; z *= -1; return Object.is(z, -0); } f()").AsBoolean());
        Assert.True(_engine.Evaluate("function f() { var n = 0; n /= 0; return Number.isNaN(n); } f()").AsBoolean());
        Assert.Equal(double.PositiveInfinity, _engine.Evaluate("function f() { var x = 1; x /= 0; return x; } f()").AsNumber());
        Assert.Equal(double.NegativeInfinity, _engine.Evaluate("function f() { var x = -1; x /= 0; return x; } f()").AsNumber());
        Assert.True(_engine.Evaluate("function f() { var x = -6; x %= 3; return Object.is(x, -0); } f()").AsBoolean());
    }

    [Fact]
    public void ReadAfterUnboxedWriteReturnsCorrectValue()
    {
        // materializing consumers (call arguments, comparisons, returns) after unboxed writes
        Assert.Equal(0.75, _engine.Evaluate("function g(v) { return v; } function f() { var s = 0.5; s += 0.25; return g(s); } f()").AsNumber());
        Assert.True(_engine.Evaluate("function f() { var s = 0.5; s += 0.25; return s === 0.75; } f()").AsBoolean());
        Assert.Equal("0.75", _engine.Evaluate("function f() { var s = 0.5; s += 0.25; return '' + s; } f()").AsString());
    }

    [Fact]
    public void RepeatedReadsReturnSameInstance()
    {
        // write-back memoization: two reads of the same unboxed binding observe one value
        Assert.True(_engine.Evaluate("function f() { var s = 0.5; s += 0.25; var a = s; var b = s; return a === b; } f()").AsBoolean());
    }

    [Fact]
    public void NonNumberRightHandSideCompletesCorrectly()
    {
        Assert.Equal("0.5x", _engine.Evaluate("function f() { var s = 0.5; s += 'x'; return s; } f()").AsString());
        Assert.Equal(0.5, _engine.Evaluate("function f() { var s = 1; s *= { valueOf: function() { return 0.5; } }; return s; } f()").AsNumber());
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { var s = 1; s += 1n; return s; } f()"));
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { var s = 1; s &= 1n; return s; } f()"));
    }

    [Fact]
    public void ExponentiationKeepsSpecSemantics()
    {
        Assert.True(_engine.Evaluate("function f() { var x = 1; x **= Infinity; return Number.isNaN(x); } f()").AsBoolean());
    }

    [Fact]
    public void RightHandSideRunsExactlyOnce()
    {
        Assert.Equal(1d, _engine.Evaluate("function f() { var calls = 0; function g() { calls++; return 2; } var s = 1; s += g(); return calls; } f()").AsNumber());
        Assert.Equal("1x|1", _engine.Evaluate("function f() { var calls = 0; function g() { calls++; return 'x'; } var s = 1; s += g(); return s + '|' + calls; } f()").AsString());
    }

    [Fact]
    public void ConstAndTdzKeepProperErrors()
    {
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { 'use strict'; const c = 1.5; c += 1; } f()"));
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { { x += 1.5; let x; } } f()"));
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { { y++; let y; } } f()"));

        // const bindings reject compound assignment in sloppy mode too (binding-level strictness)
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => _engine.Evaluate("function f() { const c = 2.5; c += 1; return c; } f()"));
    }

    [Fact]
    public void ObservableContextsKeepMaterializedResults()
    {
        // value-producing positions never take the discard path
        Assert.Equal(1.5, _engine.Evaluate("function f() { var s = 1; var y = (s += 0.5); return y; } f()").AsNumber());
        Assert.Equal(2d, _engine.Evaluate("function f() { var i = 1; var y = i++; return i; } f()").AsNumber());
        Assert.Equal(1d, _engine.Evaluate("function f() { var i = 1; var y = i++; return y; } f()").AsNumber());
    }

    [Fact]
    public void GeneratorsAndAsyncKeepFullSemantics()
    {
        Assert.Equal(1.5, _engine.Evaluate("function* g() { var s = 1; s += 0.5; yield s; } g().next().value").AsNumber());
        Assert.Equal(2.5, _engine.Evaluate("async function a() { var s = 2; s += 0.5; return s; } a()").UnwrapIfPromise().AsNumber());
    }

    [Fact]
    public void ClosureObservesUnboxedWrites()
    {
        Assert.Equal(0.75, _engine.Evaluate("function f() { var s = 0.5; var get = function() { return s; }; s += 0.25; return get(); } f()").AsNumber());
    }

    [Fact]
    public void ForLoopCountersStayCorrect()
    {
        Assert.Equal(100000d, _engine.Evaluate("function f() { var i; for (i = 0; i < 100000; i++) { } return i; } f()").AsNumber());
        Assert.Equal(12502500d, _engine.Evaluate("function f() { var s = 0; for (var i = 0; i <= 5000; i++) { s += i * 0.5; } return s * 2; } f()").AsNumber());
    }
}

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
        _engine.Evaluate("function f() { var s = 0.5; s += 0.25; s += 0.25; return s; } f()").AsNumber().Should().Be(1.0);
        _engine.Evaluate("function f() { var s = 0; for (var i = 0; i < 10000; i++) { s += 0.25; } return s; } f()").AsNumber().Should().Be(2500d);
    }

    [Fact]
    public void AllCompoundOperatorsWork()
    {
        _engine.Evaluate("function f() { var x = 10; x -= 3; return x; } f()").AsNumber().Should().Be(7d);
        _engine.Evaluate("function f() { var x = 5; x *= 0.5; return x; } f()").AsNumber().Should().Be(2.5);
        _engine.Evaluate("function f() { var x = 5; x /= 2; return x; } f()").AsNumber().Should().Be(2.5);
        _engine.Evaluate("function f() { var x = 7; x %= 3; return x; } f()").AsNumber().Should().Be(1d);
        _engine.Evaluate("function f() { var x = 6; x &= 12; return x; } f()").AsNumber().Should().Be(4d);
        _engine.Evaluate("function f() { var x = 6; x |= 12; return x; } f()").AsNumber().Should().Be(14d);
        _engine.Evaluate("function f() { var x = 6; x ^= 12; return x; } f()").AsNumber().Should().Be(10d);
        _engine.Evaluate("function f() { var x = 6; x <<= 2; return x; } f()").AsNumber().Should().Be(24d);
        _engine.Evaluate("function f() { var x = 6; x >>= 2; return x; } f()").AsNumber().Should().Be(1d);
        _engine.Evaluate("function f() { var x = -6; x >>>= 2; return x; } f()").AsNumber().Should().Be(1073741822d);
        _engine.Evaluate("function f() { var x = 2; x **= 3; return x; } f()").AsNumber().Should().Be(8d);
    }

    [Fact]
    public void UpdateExpressionsWork()
    {
        _engine.Evaluate("function f() { var i = 0; i++; i++; ++i; return i; } f()").AsNumber().Should().Be(3d);
        _engine.Evaluate("function f() { var i = 0; i--; i--; --i; return i; } f()").AsNumber().Should().Be(-3d);
        _engine.Evaluate("function f() { var i = -0.5; i++; return i; } f()").AsNumber().Should().Be(0.5);
    }

    [Fact]
    public void Int32BoundaryWidensCorrectly()
    {
        _engine.Evaluate("function f() { var i = 2147483647; i++; return i; } f()").AsNumber().Should().Be(2147483648d);
        _engine.Evaluate("function f() { var i = (1<<31)|0; i--; return i; } f()").AsNumber().Should().Be(-2147483649d);
        _engine.Evaluate("function f() { var i = (1<<31)|0; i /= -1; return i; } f()").AsNumber().Should().Be(2147483648d);
    }

    [Fact]
    public void SpecialValuesSurviveMaterialization()
    {
        _engine.Evaluate("function f() { var z = 0; z *= -1; return Object.is(z, -0); } f()").AsBoolean().Should().BeTrue();
        _engine.Evaluate("function f() { var n = 0; n /= 0; return Number.isNaN(n); } f()").AsBoolean().Should().BeTrue();
        _engine.Evaluate("function f() { var x = 1; x /= 0; return x; } f()").AsNumber().Should().Be(double.PositiveInfinity);
        _engine.Evaluate("function f() { var x = -1; x /= 0; return x; } f()").AsNumber().Should().Be(double.NegativeInfinity);
        _engine.Evaluate("function f() { var x = -6; x %= 3; return Object.is(x, -0); } f()").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReadAfterUnboxedWriteReturnsCorrectValue()
    {
        // materializing consumers (call arguments, comparisons, returns) after unboxed writes
        _engine.Evaluate("function g(v) { return v; } function f() { var s = 0.5; s += 0.25; return g(s); } f()").AsNumber().Should().Be(0.75);
        _engine.Evaluate("function f() { var s = 0.5; s += 0.25; return s === 0.75; } f()").AsBoolean().Should().BeTrue();
        _engine.Evaluate("function f() { var s = 0.5; s += 0.25; return '' + s; } f()").AsString().Should().Be("0.75");
    }

    [Fact]
    public void RepeatedReadsReturnSameInstance()
    {
        // write-back memoization: two reads of the same unboxed binding observe one value
        _engine.Evaluate("function f() { var s = 0.5; s += 0.25; var a = s; var b = s; return a === b; } f()").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void NonNumberRightHandSideCompletesCorrectly()
    {
        _engine.Evaluate("function f() { var s = 0.5; s += 'x'; return s; } f()").AsString().Should().Be("0.5x");
        _engine.Evaluate("function f() { var s = 1; s *= { valueOf: function() { return 0.5; } }; return s; } f()").AsNumber().Should().Be(0.5);
        Invoking(() => _engine.Evaluate("function f() { var s = 1; s += 1n; return s; } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
        Invoking(() => _engine.Evaluate("function f() { var s = 1; s &= 1n; return s; } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void ExponentiationKeepsSpecSemantics()
    {
        _engine.Evaluate("function f() { var x = 1; x **= Infinity; return Number.isNaN(x); } f()").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void RightHandSideRunsExactlyOnce()
    {
        _engine.Evaluate("function f() { var calls = 0; function g() { calls++; return 2; } var s = 1; s += g(); return calls; } f()").AsNumber().Should().Be(1d);
        _engine.Evaluate("function f() { var calls = 0; function g() { calls++; return 'x'; } var s = 1; s += g(); return s + '|' + calls; } f()").AsString().Should().Be("1x|1");
    }

    [Fact]
    public void ConstAndTdzKeepProperErrors()
    {
        Invoking(() => _engine.Evaluate("function f() { 'use strict'; const c = 1.5; c += 1; } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
        Invoking(() => _engine.Evaluate("function f() { { x += 1.5; let x; } } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
        Invoking(() => _engine.Evaluate("function f() { { y++; let y; } } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();

        // const bindings reject compound assignment in sloppy mode too (binding-level strictness)
        Invoking(() => _engine.Evaluate("function f() { const c = 2.5; c += 1; return c; } f()")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>();
    }

    [Fact]
    public void ObservableContextsKeepMaterializedResults()
    {
        // value-producing positions never take the discard path
        _engine.Evaluate("function f() { var s = 1; var y = (s += 0.5); return y; } f()").AsNumber().Should().Be(1.5);
        _engine.Evaluate("function f() { var i = 1; var y = i++; return i; } f()").AsNumber().Should().Be(2d);
        _engine.Evaluate("function f() { var i = 1; var y = i++; return y; } f()").AsNumber().Should().Be(1d);
    }

    [Fact]
    public void GeneratorsAndAsyncKeepFullSemantics()
    {
        _engine.Evaluate("function* g() { var s = 1; s += 0.5; yield s; } g().next().value").AsNumber().Should().Be(1.5);
        _engine.Evaluate("async function a() { var s = 2; s += 0.5; return s; } a()").UnwrapIfPromise().AsNumber().Should().Be(2.5);
    }

    [Fact]
    public void ClosureObservesUnboxedWrites()
    {
        _engine.Evaluate("function f() { var s = 0.5; var get = function() { return s; }; s += 0.25; return get(); } f()").AsNumber().Should().Be(0.75);
    }

    [Fact]
    public void WithStatementDynamicShadowingResolvesPerExecution()
    {
        // write paths: the slot-location cache must not skip a with-object that gains
        // the property after the cache was populated
        _engine.Evaluate(
            "function f() { var i = 0; var r = ''; for (var k = 0; k < 3; k++) { var obj = (k === 1) ? { i: 100 } : {}; with (obj) { i++; } if (k === 1) { r = '' + obj.i; } } return i + '|' + r; } f();").AsString().Should().Be("2|101");

        _engine.Evaluate(
            "function g() { var i = 0; var r = ''; for (var k = 0; k < 3; k++) { var obj = (k === 1) ? { i: 100 } : {}; with (obj) { i += 1; } if (k === 1) { r = '' + obj.i; } } return i + '|' + r; } g();").AsString().Should().Be("2|101");

        // read path: the identifier slot cache has the same obligation
        _engine.Evaluate(
            "function h() { var i = 42; var out = ''; for (var k = 0; k < 2; k++) { var obj = (k === 1) ? { i: 999 } : {}; with (obj) { out += i + ';'; } } return out; } h();").AsString().Should().Be("42;999;");

        // a with-object that owns the property from the start
        _engine.Evaluate(
            "function w() { var s = 1; var o = { s: 100 }; with (o) { s += 5; } return s + '|' + o.s; } w();").AsString().Should().Be("1|105");
    }

    [Fact]
    public void SequenceExpressionUpdatesInForLoop()
    {
        _engine.Evaluate(
            "function f() { var i = 0, j = 10; for (; i < j; i++, j--) { } return i + '|' + j; } f();").AsString().Should().Be("5|5");
        _engine.Evaluate(
            "function f() { var a = 0, b = 0; for (var k = 0; k < 5; k++, a += 0.25, b += 0.25) { } return a + b; } f();").AsNumber().Should().Be(2.5);
    }

    [Fact]
    public void SwitchBodiesInsideFunctionsStayCorrect()
    {
        _engine.Evaluate(
            "function f() { var s = 0; switch (1) { case 1: s += 0.5; s += 0.25; break; } return s; } f();").AsNumber().Should().Be(0.75);
        _engine.Evaluate(
            "function f() { var acc = 0; for (var i = 0; i < 100; i++) { switch (i % 2) { case 0: acc += 1; break; default: acc -= 0; break; } } return acc; } f();").AsNumber().Should().Be(50d);
    }

    [Fact]
    public void ForLoopCountersStayCorrect()
    {
        _engine.Evaluate("function f() { var i; for (i = 0; i < 100000; i++) { } return i; } f()").AsNumber().Should().Be(100000d);
        _engine.Evaluate("function f() { var s = 0; for (var i = 0; i <= 5000; i++) { s += i * 0.5; } return s * 2; } f()").AsNumber().Should().Be(12502500d);
    }
}

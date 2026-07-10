namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of State.CanSkipThisBinding: functions that provably never resolve
/// this/super/new.target skip OrdinaryCallBindThis, and every shape that CAN observe the
/// binding must keep today's exact behavior.
/// </summary>
public class ThisBindingElisionTests
{
    [Fact]
    public void LeafClosuresBehaveIdenticallyUnderAnyReceiver()
    {
        var engine = new Engine();
        // sloppy mode: these closures never read this, so eliding the ToObject(receiver) is
        // unobservable regardless of what the receiver is
        var result = engine.Evaluate("""
            (function () {
                var count = 0;
                var f = function () { count++; };
                f();
                f.call(null);
                f.call(5);
                f.call('str');
                var o = { m: f };
                o.m();
                return count;
            })()
            """).AsNumber();

        Assert.Equal(5, result);
    }

    [Fact]
    public void ThisUsingFunctionsKeepSloppyToObjectSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function typeOfThis() { return typeof this; }
                function valueOfThis() { return this.valueOf(); }
                return typeOfThis.call(5) + ':' + valueOfThis.call(5)
                    + ':' + typeOfThis.call(null);
            })()
            """).AsString();

        // sloppy: primitive receiver boxed to object; null falls back to globalThis
        Assert.Equal("object:5:object", result);
    }

    [Fact]
    public void ThisUsingFunctionsKeepStrictPassThroughSemantics()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                'use strict';
                function typeOfThis() { return typeof this; }
                return typeOfThis.call(5) + ':' + typeOfThis.call(undefined);
            })()
            """).AsString();

        Assert.Equal("number:undefined", result);
    }

    [Fact]
    public void NestedArrowResolvingThisForcesBinding()
    {
        var engine = new Engine();
        // the arrow resolves `this` through the enclosing function's frame — the escape
        // analysis keeps the flag off, so the binding must exist
        var result = engine.Evaluate("""
            (function () {
                function viaArrow() { return (() => typeof this)(); }
                return viaArrow.call(42);
            })()
            """).AsString();

        Assert.Equal("object", result);
    }

    [Fact]
    public void NewTargetUsingFunctionKeepsBinding()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function probe() { return new.target === undefined; }
                var viaCall = probe();
                var viaNew = new probe();
                return viaCall + ':' + (typeof viaNew);
            })()
            """).AsString();

        Assert.Equal("true:object", result);
    }

    [Fact]
    public void LeafGettersAndMethodShorthandWork()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var hits = 0;
                var o = {
                    get probe() { hits++; return 7; },
                    bump() { hits++; }
                };
                var v = o.probe;
                o.bump();
                return v + ':' + hits;
            })()
            """).AsString();

        Assert.Equal("7:2", result);
    }

    [Fact]
    public void CapturedStateClosuresKeepDistinctInstances()
    {
        var engine = new Engine();
        // two instances interleaved: elided frames must not leak captured state across instances
        var result = engine.Evaluate("""
            (function () {
                function Counter() {
                    var n = 0;
                    this.inc = function () { n = n + 1; };
                    this.get = function () { return n; };
                }
                var a = new Counter();
                var b = new Counter();
                for (var i = 0; i < 100; i++) {
                    a.inc();
                    if (i % 2 === 0) b.inc();
                }
                return a.get() + ':' + b.get();
            })()
            """).AsString();

        Assert.Equal("100:50", result);
    }
}

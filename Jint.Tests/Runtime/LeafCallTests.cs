namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of env-less leaf calls (State.SupportsLeafCall): frames that run against
/// the captured environment directly must be indistinguishable from ordinary calls for every
/// observable behavior — captured state, error stacks, dispose timing, strictness, construction.
/// </summary>
public class LeafCallTests
{
    [Fact]
    public void CapturedStateStaysPerInstanceAcrossInterleavedLeafCalls()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function Stopwatch() {
                    var sw = this;
                    var running = false;
                    var count = 0;
                    sw.start = function () { if (running) return; running = true; count++; };
                    sw.stop = function () { if (!running) return; running = false; };
                    sw.count = function () { return count; };
                }
                var a = new Stopwatch();
                var b = new Stopwatch();
                for (var i = 0; i < 100; i++) {
                    a.start(); a.stop();
                    if (i % 2 === 0) { b.start(); b.stop(); }
                }
                return a.count() + ':' + b.count();
            })()
            """).AsString();

        Assert.Equal("100:50", result);
    }

    [Fact]
    public void ErrorStackIncludesLeafFrame()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function Box() {
                    this.boom = function leafBoom() { mustNotExist(); };
                }
                var b = new Box();
                // warm the call site so the leaf arm is active
                try { b.boom(); } catch (e) { }
                try { b.boom(); } catch (e) { return e.stack; }
                return 'no-throw';
            })()
            """).AsString();

        Assert.Contains("leafBoom", result);
    }

    [Fact]
    public void NewErrorInsideLeafBodyCapturesLeafFrame()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var captured = null;
                function Box() {
                    this.snap = function leafSnap() { captured = new Error('here').stack; };
                }
                var b = new Box();
                b.snap();
                b.snap();
                return captured;
            })()
            """).AsString();

        Assert.Contains("leafSnap", result);
    }

    [Fact]
    public void ConstructingLeafEligibleFunctionTakesOrdinaryPath()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function Leaf() { }
                var byCall = Leaf();
                var byNew = new Leaf();
                return (byCall === undefined) + ':' + (typeof byNew) + ':' + (Object.getPrototypeOf(byNew) === Leaf.prototype);
            })()
            """).AsString();

        Assert.Equal("true:object:true", result);
    }

    [Fact]
    public void LeafCallsWorkThroughEveryInvocationRoute()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var n = 0;
                function bump() { n++; }
                bump();
                bump.call(null);
                bump.apply(undefined, []);
                bump.bind(123)();
                [1, 2].forEach(bump);
                Reflect.apply(bump, null, []);
                return n;
            })()
            """).AsNumber();

        Assert.Equal(7, result);
    }

    [Fact]
    public void ConstructorUsingResourcesSurviveLeafCallsInside()
    {
        var engine = new Engine();
        // the leaf frame's environments point at the constructor's env; function-level dispose
        // must NOT run against it (that would drain the constructor's pending `using` early)
        var result = engine.Evaluate("""
            (function () {
                var log = [];
                function Host() {
                    using r = { [Symbol.dispose]() { log.push('dispose'); } };
                    this.leaf = function () { log.push('leaf'); };
                    this.leaf();
                    this.leaf();
                    log.push('ctor-end');
                }
                new Host();
                return log.join(',');
            })()
            """).AsString();

        Assert.Equal("leaf,leaf,ctor-end,dispose", result);
    }

    [Fact]
    public void NestedBlockUsingInsideLeafBodyDisposesAtBlockExit()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var log = [];
                function Box() {
                    this.work = function () {
                        {
                            using r = { [Symbol.dispose]() { log.push('d'); } };
                            log.push('u');
                        }
                        log.push('after');
                    };
                }
                var b = new Box();
                b.work();
                b.work();
                return log.join(',');
            })()
            """).AsString();

        Assert.Equal("u,d,after,u,d,after", result);
    }

    [Fact]
    public void StrictLeafKeepsStrictAssignmentSemantics()
    {
        var engine = new Engine();
        // sloppy caller invoking a strict leaf: the ambient strictness differs, so the leaf arm
        // must still establish strict mode for the body (assignment to undeclared throws)
        var result = engine.Evaluate("""
            (function () {
                function makeStrict() {
                    'use strict';
                    return function () { strictLeafUndeclared = 1; };
                }
                var f = makeStrict();
                try { f(); } catch (e) { }
                try { f(); return 'no-throw'; } catch (e) { return e.constructor.name; }
            })()
            """).AsString();

        Assert.Equal("ReferenceError", result);
    }

    [Fact]
    public void SloppyLeafCreatesGlobalOnUndeclaredAssignment()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function Box() {
                    this.set = function () { sloppyLeafGlobal = 42; };
                }
                var b = new Box();
                b.set();
                b.set();
                return globalThis.sloppyLeafGlobal;
            })()
            """).AsNumber();

        Assert.Equal(42, result);
    }

    [Fact]
    public void DirectlyRecursiveLeafFunctionTerminates()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var depth = 0;
                function drill() {
                    depth++;
                    if (depth < 50) drill();
                }
                drill();
                var first = depth;
                depth = 0;
                drill();
                return first + ':' + depth;
            })()
            """).AsString();

        Assert.Equal("50:50", result);
    }

    [Fact]
    public void LeafFlagFiresOnStopwatchClosureShapesOnly()
    {
        var engine = new Engine();
        engine.Execute("""
            function Host() {
                var state = 0;
                this.leaf = function () { state = 1; };
                this.usesThis = function () { return this; };
                this.usesArguments = function () { return arguments.length; };
                this.makesClosure = function () { return function () { return state; }; };
                this.hasParam = function (p) { state = p; };
            }
            var h = new Host();
            """);

        static bool SupportsLeafCall(Engine engine, string expression)
        {
            var fn = (Jint.Native.Function.ScriptFunction) engine.Evaluate(expression);
            return fn._functionDefinition!.Initialize().SupportsLeafCall;
        }

        Assert.True(SupportsLeafCall(engine, "h.leaf"));
        Assert.False(SupportsLeafCall(engine, "h.usesThis"));
        Assert.False(SupportsLeafCall(engine, "h.usesArguments"));
        Assert.False(SupportsLeafCall(engine, "h.makesClosure"));
        Assert.False(SupportsLeafCall(engine, "h.hasParam"));
    }

    [Fact]
    public void TypeofUnresolvableInsideLeafBodyStaysUndefined()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                function Box() {
                    this.probe = function () { return typeof neverDeclaredAnywhere; };
                }
                var b = new Box();
                b.probe();
                return b.probe();
            })()
            """).AsString();

        Assert.Equal("undefined", result);
    }
}

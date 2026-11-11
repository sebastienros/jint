#nullable enable

using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime;

/// <summary>
/// An arrow function has no <c>arguments</c> binding of its own: it is created with the lexical this
/// mode (https://tc39.es/ecma262/#sec-ordinaryfunctioncreate), and for that mode
/// https://tc39.es/ecma262/#sec-functiondeclarationinstantiation notes that "arrow functions never have
/// an arguments object". So <c>arguments</c> in one resolves to the enclosing function's binding, and
/// the arrow can run long after that call has returned. A direct eval resolves the name the same way
/// and can leave such an arrow behind (https://tc39.es/ecma262/#sec-performeval).
/// </summary>
/// <remarks>
/// Jint pools the arguments object and reads its values straight out of the caller's argument
/// array, releasing both when the call returns unless the object was handed on during the call.
/// A closure reads the binding after the call, which is too late to notice, so the next call
/// reused the object, or the cleared array came back as a host <see cref="NullReferenceException"/>
/// (sebastienros/jint#4545). Each source below creates a closure, calls the function again with other
/// arguments to recycle what the first call released, and only then asks the closure. The expected
/// values are node's.
/// </remarks>
public class CapturedArgumentsTests
{
    // 4.x's ObjectPool has no PooledCount (main added it for GetMemoryReport), so read the same two
    // storage fields it is derived from.
    private static int PooledCount(object argumentsPool)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var pool = argumentsPool.GetType().GetField("_pool", flags)!.GetValue(argumentsPool)!;
        var first = pool.GetType().GetField("_firstItem", flags)!.GetValue(pool);
        var slow = (int) pool.GetType().GetField("_currentSlowPooledItems", flags)!.GetValue(pool)!;
        return (first is null ? 0 : 1) + slow;
    }

    private static JintFunctionDefinition.State StateOf(Engine engine, string name)
    {
        var function = (ScriptFunction) engine.GetValue(name);
        return function._functionDefinition!.Initialize();
    }

    // The issue, with literal arguments (a cached array) and with computed ones (a pooled array).
    [InlineData("function f() { return () => arguments[0]; } var h = f(\"first\"); f(\"second\"); h()", "first")]
    [InlineData("function f() { return () => arguments[0]; } var k = f({ v: 1 }); f({ v: 2 }); typeof k()", "object")]
    [InlineData("function f() { return () => arguments[0]; } var k = f({ v: 1 }); f({ v: 2 }); k().v", "1")]
    // Every way of reading it: a bare name read after the call is just as late as a member read.
    [InlineData("function f() { return () => arguments.length; } var h = f(1, 2, 3); f(4); h()", "3")]
    [InlineData("function f() { return () => arguments; } var h = f(\"first\", { v: 1 }); f(\"second\", { v: 2 }); h()[0] + h()[1].v", "first1")]
    [InlineData("function f() { return () => [...arguments].join(); } var h = f(1, { v: 2 }.v, 3); f(4, { v: 5 }.v); h()", "1,2,3")]
    [InlineData("function f() { return () => Array.from(arguments).join(); } var h = f(1, { v: 2 }.v); f(3, { v: 4 }.v, 5); h()", "1,2")]
    [InlineData("function f() { return () => { const [x, y] = arguments; return x + y; }; } var h = f(\"a\", { v: \"b\" }.v); f(\"c\", { v: \"d\" }.v); h()", "ab")]
    [InlineData("function f() { return () => { var x, y; ({ 0: x, 1: y } = arguments); return x + y; }; } var h = f(\"a\", { v: \"b\" }.v); f(\"c\", { v: \"d\" }.v); h()", "ab")]
    [InlineData("function f() { return () => arguments[arguments.length - 1]; } var h = f(1, { v: 2 }.v); f(3, { v: 4 }.v, 5); h()", "2")]
    [InlineData("function f() { return () => Object.keys(arguments).join(); } var h = f(1, { v: 2 }.v); f(3, 4, { v: 5 }.v); h()", "0,1")]
    // Two levels of arrows, and an arrow in a parameter default.
    [InlineData("function f() { return () => () => arguments[0]; } var h = f(\"first\"); f(\"second\"); h()()", "first")]
    [InlineData("function f() { return () => () => arguments[0]; } var h = f({ v: 1 }); f({ v: 2 }); h()().v", "1")]
    [InlineData("function f(a, g = () => arguments[2]) { return g; } var h = f(0, undefined, { v: \"first\" }); f(0, undefined, { v: \"second\" }); h().v", "first")]
    // Strict mode and a non-simple parameter list, which both make the unmapped object.
    [InlineData("function f() { \"use strict\"; return () => arguments[0]; } var h = f(\"first\"); f(\"second\"); h()", "first")]
    [InlineData("function f() { \"use strict\"; return () => arguments[0]; } var k = f({ v: 1 }); f({ v: 2 }); k().v", "1")]
    [InlineData("function f(...rest) { return () => arguments[0]; } var k = f({ v: 1 }); f({ v: 2 }); k().v", "1")]
    // A method, a class constructor and a plain constructor ([[Construct]] runs the same body).
    [InlineData("var o = { m() { return () => arguments[0]; } }; var h = o.m({ v: \"first\" }); o.m({ v: \"second\" }); h().v", "first")]
    [InlineData("class C { constructor() { this.h = () => arguments[0]; } } var c = new C({ v: \"first\" }); new C({ v: \"second\" }); c.h().v", "first")]
    [InlineData("function F() { this.h = () => arguments[0]; } var c = new F({ v: \"first\" }); new F({ v: \"second\" }); c.h().v", "first")]
    // The argument array arriving through a bound function, a spread and apply.
    [InlineData("function f() { return () => arguments[0]; } var b = f.bind(null, { v: \"first\" }); var h = b(); b(); f({ v: \"second\" }); h().v", "first")]
    [InlineData("function f() { return () => arguments[1]; } var xs = [{ v: \"a\" }, { v: \"first\" }]; var h = f(...xs); f(...[{ v: \"b\" }, { v: \"second\" }]); h().v", "first")]
    [InlineData("function f() { return () => arguments[0]; } var h = f.apply(null, [{ v: \"first\" }]); f.apply(null, [{ v: \"second\" }]); h().v", "first")]
    // 200 calls through one warm site, each closure keeping its own arguments.
    [InlineData("function f(a) { return () => arguments[0] + arguments.length; } var hs = []; for (var i = 0; i < 200; i++) { hs.push(f(i, { v: i })); } var t = 0; for (var j = 0; j < 200; j++) { t += hs[j](); } t", "20300")]
    [Theory]
    public void AnArrowKeepsTheArgumentsOfTheCallThatCreatedIt(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    // An eval inside an arrow, and an arrow an eval creates in the function itself, strict or not.
    [InlineData("function f() { return () => eval(\"arguments\"); } var h = f(\"first\", { v: 1 }); f(\"second\", { v: 2 }); h()[0] + h()[1].v", "first1")]
    [InlineData("function f() { return () => eval(\"arguments[0]\"); } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().v", "first")]
    [InlineData("function f() { return eval(\"() => arguments[0]\"); } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().v", "first")]
    [InlineData("function f() { eval(\"var g = () => arguments[0]\"); return g; } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().v", "first")]
    [InlineData("function f() { \"use strict\"; return eval(\"() => arguments[0]\"); } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().v", "first")]
    [Theory]
    public void ADirectEvalKeepsTheArgumentsOfTheCallThatCreatedIt(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createmappedargumentsobject: a sloppy function with a simple
    /// parameter list maps each index to its parameter's binding, and nothing ends that mapping when the
    /// call returns. A closure over the environment can still write either side afterwards; the unmapped
    /// object of a strict function or a non-simple parameter list stays a snapshot.
    /// </summary>
    [InlineData("function f(a) { return [() => arguments[0], v => { a = v; }, v => { arguments[0] = v; }, () => a]; } var r = f(1); r[1](2); var x = r[0](); r[2](3); x + \",\" + r[3]()", "2,3")]
    [InlineData("function f(a) { return [() => arguments[0], v => { a = v; }, v => { arguments[0] = v; }, () => a]; } var r = f({ v: 1 }); r[1](2); var x = r[0](); r[2](3); x + \",\" + r[3]()", "2,3")]
    [InlineData("function f(a) { return [arguments, () => a, v => { a = v; }]; } var r = f(1); r[0][0] = 5; var x = r[1](); r[2](7); x + \",\" + r[0][0]", "5,7")]
    [InlineData("function f(a, b) { return [() => arguments, v => { b = v; }]; } var r = f(1, 2, 3); r[1](9); var args = r[0](); [args[0], args[1], args[2], args.length].join()", "1,9,3,3")]
    [InlineData("function f(a) { \"use strict\"; return [() => arguments[0], v => { a = v; }, v => { arguments[0] = v; }, () => a]; } var r = f(1); r[1](2); var x = r[0](); r[2](3); x + \",\" + r[3]() + \",\" + r[0]()", "1,2,3")]
    [InlineData("function f(a = 0) { return [() => arguments[0], v => { a = v; }, v => { arguments[0] = v; }, () => a]; } var r = f(1); r[1](2); var x = r[0](); r[2](3); x + \",\" + r[3]() + \",\" + r[0]()", "1,2,3")]
    [Theory]
    public void AMappedArgumentsObjectStaysMappedAfterTheCallReturns(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    /// <summary>
    /// A mapped index reads its parameter through the map whatever the parameter holds, undefined included:
    /// https://tc39.es/ecma262/#sec-arguments-exotic-objects-getownproperty-p always takes the value from
    /// the map, so the value the property was created with never shows through. Once the mapping outlived the
    /// call, nothing froze the binding into that stale value any more, and an <c>undefined</c> parameter read
    /// back as its original argument.
    /// </summary>
    [InlineData("function f(a) { a = undefined; var g = function () {}; return arguments; } var o = f(1); Object.getOwnPropertyDescriptor(o, '0').value")]
    [InlineData("function f(a) { a = undefined; var g = function () {}; Object.keys(arguments); return arguments; } f(1)[0]")]
    [InlineData("function f(a) { a = undefined; var g = function () {}; return arguments; } var o = f(1); Object.keys(o); o[0]")]
    [InlineData("function f(a) { return [() => arguments, v => { a = v; }]; } var r = f(1); var o = r[0](); Object.keys(o); r[1](undefined); o[0]")]
    // The same rule during the call, where the map is live on every branch.
    [InlineData("function f(a) { Object.keys(arguments); a = undefined; return arguments[0]; } f(1)")]
    [InlineData("function f(a) { a = undefined; return Object.getOwnPropertyDescriptor(arguments, '0').value; } f(1)")]
    // A non-writable redefinition with no value takes the mapped value (sec-arguments-exotic-objects-defineownproperty-p-desc).
    [InlineData("function f(a) { a = undefined; Object.defineProperty(arguments, '0', { writable: false }); a = 7; return arguments[0]; } f(1)")]
    [Theory]
    public void AMappedIndexHoldingUndefinedReadsUndefined(string source)
    {
        var engine = new Engine();

        engine.Evaluate(source).Should().BeUndefined();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arguments-exotic-objects-defineownproperty-p-desc: a data descriptor that
    /// has a value sets it through the map, and <c>undefined</c> is a value like any other.
    /// </summary>
    [InlineData("function f(a) { Object.defineProperty(arguments, '0', { value: undefined }); return a; } f(1)")]
    [InlineData("function f(a) { Object.defineProperty(arguments, '0', { value: undefined }); return arguments[0]; } f(1)")]
    [InlineData("function f(a) { return [() => arguments, () => a]; } var r = f(1); Object.defineProperty(r[0](), '0', { value: undefined }); r[1]()")]
    [Theory]
    public void DefiningAMappedIndexAsUndefinedWritesTheParameter(string source)
    {
        var engine = new Engine();

        engine.Evaluate(source).Should().BeUndefined();
    }

    // 4.x only: GetOwnProperties is still a virtual that JsArguments overrides from the raw bag, so a host enumerating
    // a mapped arguments object that outlived its call has to see the parameter's current value, as Get does.
    [Fact]
    public void AHostEnumeratingAMappedArgumentsObjectAfterItsCallSeesTheParametersValue()
    {
        var engine = new Engine();

        var arguments = engine.Evaluate("function f(a) { var g = function () { return a; }; a = 5; return arguments; } f(1)").AsObject();

        arguments.GetOwnProperties().First(p => p.Key == "0").Value.Value.Should().Be(5);
        arguments.Get("0").Should().Be(5);
    }

    [Fact]
    public void DefiningAMappedIndexKeepsTheRestOfItsRules()
    {
        var engine = new Engine();

        // A value other than undefined, and a non-writable redefinition with no value of its own, which keeps
        // the current mapped value and then unmaps the index.
        engine.Evaluate("function f(a) { Object.defineProperty(arguments, '0', { value: 5 }); return a; } f(1)").Should().Be(5);
        engine.Evaluate("function f(a) { Object.defineProperty(arguments, '0', { writable: false }); a = 7; return arguments[0]; } f(1)").Should().Be(1);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createmappedargumentsobject makes the parameter map with
    /// OrdinaryObjectCreate(null), and [[GetOwnProperty]] reads it with Get. A map inheriting from
    /// %Object.prototype% answered for every own property it does not hold (an argument past the formals, an
    /// index it no longer maps, <c>length</c>, <c>callee</c>, a name the script defined), and a getter there ran
    /// with the map as its receiver.
    /// </summary>
    [InlineData("Object.prototype[1] = undefined; function f(a) { Object.keys(arguments); return arguments[1]; } String(f(1, 2))", "2")]
    [InlineData("Object.prototype[1] = 9; function f(a) { Object.keys(arguments); return arguments[1]; } f(1, 2)", 2)]
    [InlineData("function f(a) { arguments.toString = 5; return arguments.toString; } f(1)", 5)]
    [InlineData("Object.defineProperty(Object.prototype, 'x', { get() { return undefined; }, configurable: true }); function f(a) { Object.defineProperty(arguments, 'x', { value: 3, writable: true, enumerable: true, configurable: true }); return arguments.x; } f(1)", 3)]
    [InlineData("Object.prototype[0] = undefined; function f(a) { Object.defineProperty(arguments, '0', { writable: false }); return String(arguments[0]); } f(1)", "1")]
    [InlineData("Object.prototype[0] = undefined; function f(a) { delete arguments[0]; arguments[0] = 4; return String(arguments[0]); } f(1)", "4")]
    [InlineData("var seen = 'none'; Object.defineProperty(Object.prototype, 'y', { get() { seen = this === Object.prototype ? 'proto' : typeof this; return 4; }, configurable: true }); function f(a) { Object.defineProperty(arguments, 'y', { value: 1, writable: true, enumerable: true, configurable: true }); return arguments.y + ',' + seen; } f(1)", "1,none")]
    [InlineData("Object.prototype.length = undefined; function f(a) { Object.keys(arguments); return String(arguments.length); } f(1, 2)", "2")]
    [InlineData("Object.prototype.callee = undefined; function f(a) { Object.keys(arguments); return typeof arguments.callee; } f(1)", "function")]
    [Theory]
    public void TheParameterMapInheritsNothing(string source, object expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).Should().Be(JsValue.FromObject(engine, expected));
    }

    /// <summary>
    /// The other ways a script reaches the map's define, write and delete paths, each with node's answer: an
    /// explicit undefined through every define entry point, sealing and freezing, an accessor and a generic
    /// redefinition, and an array method writing through the object.
    /// </summary>
    [InlineData("function f(a) { Object.defineProperty(arguments, '0', { value: undefined, writable: false }); a = 3; return String(arguments[0]) + ',' + a; } f(1)", "undefined,3")]
    [InlineData("function f(a) { Object.defineProperties(arguments, { 0: { value: undefined } }); return String(a); } f(1)", "undefined")]
    [InlineData("function f(a) { Reflect.defineProperty(arguments, '0', { value: undefined }); return String(a); } f(1)", "undefined")]
    [InlineData("function f(a) { Reflect.defineProperty(new Proxy(arguments, {}), '0', { value: undefined }); return String(a); } f(1)", "undefined")]
    [InlineData("function f(a) { Object.seal(arguments); a = 5; return arguments[0]; } f(1)", 5)]
    [InlineData("function f(a) { a = undefined; Object.freeze(arguments); a = 5; return String(arguments[0]); } f(1)", "undefined")]
    [InlineData("function f(a) { Object.defineProperty(arguments, '0', { get() { return 'g'; }, configurable: true }); a = 5; return arguments[0] + ',' + a; } f(1)", "g,5")]
    [InlineData("function f(a) { Object.defineProperty(arguments, '0', { enumerable: false }); a = 5; return arguments[0]; } f(1)", 5)]
    [InlineData("function f(a, b) { Array.prototype.splice.call(arguments, 0, 1, undefined); return String(a) + ',' + b; } f(1, 2)", "undefined,2")]
    [Theory]
    public void EveryRouteIntoTheMapKeepsTheSpecsAnswer(string source, object expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).Should().Be(JsValue.FromObject(engine, expected));
    }

    /// <summary>
    /// A call through a property of the arguments object passes the object itself as the callee's
    /// <c>this</c> (https://tc39.es/ecma262/#sec-evaluatecall), and the callee can keep it.
    /// </summary>
    [InlineData("function f() { return arguments[0](); } var a = f(function () { return this; }, \"first\"); f(function () { return 1; }, \"second\"); a[1]", "first")]
    [InlineData("function f() { return arguments[0](); } var a = f(function () { return this; }, { v: \"first\" }); f(function () { return 1; }, { v: \"second\" }); a[1].v", "first")]
    [InlineData("function f() { return arguments[0]?.(); } var a = f(function () { return this; }, { v: \"first\" }); f(function () { return 1; }, { v: \"second\" }); a[1].v", "first")]
    [InlineData("function tag() { return this; } function f() { return arguments[0]`x`; } var a = f(tag, { v: \"first\" }); f(tag, { v: \"second\" }); a[1].v", "first")]
    [InlineData("function tag() { return this; } function f() { arguments.t = tag; return arguments.t`x`; } var a = f({ v: \"first\" }); f({ v: \"second\" }); a[0].v", "first")]
    [InlineData("function f() { return arguments.valueOf(); } var a = f({ v: \"first\" }); f({ v: \"second\" }); a[0].v", "first")]
    [Theory]
    public void ACallThroughTheArgumentsObjectKeepsTheReceiverItHandsOn(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    // A generator's frame suspends and later completes; the arrow it made outlives both.
    [InlineData("function* g() { const h = () => arguments[0]; yield h; } var it = g({ v: \"first\" }); var h = it.next().value; g({ v: \"second\" }).next(); h().v", "first")]
    [InlineData("function* g() { const h = () => arguments[0]; yield h; } var it = g({ v: \"first\" }); var h = it.next().value; it.next(); var it2 = g({ v: \"second\" }); it2.next(); it2.next(); h().v", "first")]
    [InlineData("function* g(a) { yield () => arguments[0]; yield v => { a = v; }; } var it = g(1); var get = it.next().value; var set = it.next().value; it.next(); set(5); get()", "5")]
    [Theory]
    public void AGeneratorsArrowKeepsItsArguments(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).ToString().Should().Be(expected);
    }

    // The async side of the generator rows above: an async arrow that resumes after its creator
    // returned, and an arrow an async function or async generator made before or after suspending.
    [InlineData("function f() { return async () => { await null; return arguments[0]; }; } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().then(x => x.v)", "first")]
    [InlineData("function f() { return async () => arguments[0]; } var h = f({ v: \"first\" }); f({ v: \"second\" }); h().then(x => x.v)", "first")]
    [InlineData("async function a() { await null; return () => arguments[0]; } (async () => { const h = await a({ v: \"first\" }); await a({ v: \"second\" }); return h().v; })()", "first")]
    [InlineData("async function a() { const h = () => arguments[0]; await null; return h; } (async () => { const p = a({ v: \"first\" }); await a({ v: \"second\" }); return (await p)().v; })()", "first")]
    [InlineData("async function* ag() { yield () => arguments[0]; } (async () => { const it = ag({ v: \"first\" }); const h = (await it.next()).value; await ag({ v: \"second\" }).next(); await it.next(); return h().v; })()", "first")]
    [Theory]
    public void AnAsyncArrowKeepsItsArguments(string source, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(source).UnwrapIfPromise().ToString().Should().Be(expected);
    }

    [Fact]
    public void TheHostShapedReproductionFromTheIssue()
    {
        var engine = new Engine();
        engine.Execute("""
            function f() { return () => arguments[0]; }
            const h = f("first");
            f("second");
            """);

        engine.Evaluate("h()").Should().Be("first");

        engine.Execute("const k = f({ v: 1 });");
        engine.Evaluate("typeof k()").Should().Be("object");
        engine.Evaluate("k().v").Should().Be(1);
    }

    [Fact]
    public void OnlyAClosureOrADirectEvalOverArgumentsMakesItOutliveTheCall()
    {
        var engine = new Engine();
        engine.Execute("""
            function byArrow() { return () => arguments[0]; }
            function byNestedArrow() { return () => () => arguments.length; }
            function byParameterDefault(a, g = () => arguments) { return g; }
            function byEvalInArrow() { return () => eval('1'); }
            function bySloppyEval() { return eval('1'); }
            function byStrictEval() { 'use strict'; return eval('arguments[0]'); }
            function byArrowInClassKey() { class K { [(() => arguments[0])()]() { } } return K; }

            function readOnly() { return arguments[0]; }
            function arrowWithoutArguments() { var g = () => 1; return arguments[0] + g(); }
            function nestedFunctionsOwn() { return function () { return arguments[0]; }; }
            function arrowAroundNestedFunctionsOwn() { return () => function () { return arguments; }; }
            function methodsOwn() { return { m() { return arguments; } }; }
            function* generatorArrow() { yield () => arguments[0]; }
            async function asyncArrow() { return () => arguments[0]; }
            """);

        StateOf(engine, "byArrow").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "byNestedArrow").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "byParameterDefault").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "byEvalInArrow").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "bySloppyEval").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "byStrictEval").ArgumentsMayOutliveCall.Should().BeTrue();
        StateOf(engine, "byArrowInClassKey").ArgumentsMayOutliveCall.Should().BeTrue();

        // Read only during the call: the object may go back to the pool, which is the point of it.
        StateOf(engine, "readOnly").ArgumentsMayOutliveCall.Should().BeFalse();
        StateOf(engine, "arrowWithoutArguments").ArgumentsMayOutliveCall.Should().BeFalse();

        // A nested non-arrow function has an arguments object of its own, which is the one it names.
        StateOf(engine, "nestedFunctionsOwn").ArgumentsMayOutliveCall.Should().BeFalse();
        StateOf(engine, "arrowAroundNestedFunctionsOwn").ArgumentsMayOutliveCall.Should().BeFalse();
        StateOf(engine, "methodsOwn").ArgumentsMayOutliveCall.Should().BeFalse();

        // A generator or async function never releases its arguments object, and already owns the
        // array behind it (RequiresInputArgumentsOwnership), so there is nothing to snapshot.
        StateOf(engine, "generatorArrow").ArgumentsObjectNeeded.Should().BeTrue();
        StateOf(engine, "generatorArrow").ArgumentsMayOutliveCall.Should().BeFalse();
        StateOf(engine, "asyncArrow").ArgumentsMayOutliveCall.Should().BeFalse();
    }

    [Fact]
    public void AnArgumentsObjectNothingKeepsStillGoesBackToThePool()
    {
        var engine = new Engine();
        engine.Execute("""
            function untouched(a) { return a > 0 ? a : arguments.length; }
            function writesThroughTheMap(a) { arguments[0] = a + 1; return a; }
            function arrowWithoutArguments(a) { var g = () => 1; return a > 0 ? a + g() : arguments.length; }
            function captured() { return () => arguments[0]; }
            """);

        var pool = engine._argumentsInstancePool;
        PooledCount(pool).Should().Be(0);

        // Nothing these calls do hands the object on, so each returns what it rented and sequential calls
        // keep exactly one instance in the pool.
        engine.Evaluate("untouched(1); untouched(2); writesThroughTheMap(1); arrowWithoutArguments(1)");
        PooledCount(pool).Should().Be(1);

        // A captured one takes that instance and never gives it back.
        engine.Evaluate("captured({ v: 1 })");
        PooledCount(pool).Should().Be(0);

        engine.Evaluate("untouched(3)");
        PooledCount(pool).Should().Be(1);
    }
}

#nullable enable

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// A read of an index an array does not hold — a hole, or an index past the dense storage — continues on the prototype,
/// and OrdinaryGet continues there with <c>parent.[[Get]](P, Receiver)</c>
/// (https://tc39.es/ecma262/#sec-ordinary-object-internal-methods-and-internal-slots-get-p-receiver, OrdinaryGet step
/// 2.c). Jint walked the chain itself, asking each link for an own property, which is the same thing only while every
/// link's <c>[[Get]]</c> is the ordinary one. A Proxy on the chain then answered from its <c>getOwnPropertyDescriptor</c>
/// and <c>getPrototypeOf</c> traps instead of its <c>get</c> trap, and a typed array, whose <c>[[Get]]</c> answers a
/// canonical numeric key itself and never consults its own prototype
/// (https://tc39.es/ecma262/#sec-typedarray-get), was walked past, so an index on <c>Object.prototype</c> leaked
/// through it.
/// <para>
/// The algorithms that ask <c>HasProperty</c> before <c>Get</c> (<c>forEach</c>, <c>map</c> and the other
/// <c>Array.prototype</c> methods) ask both questions of an exotic link, and the ones that only read (an indexed read,
/// <c>join</c>, <c>find</c>, the array iterator and the array patterns that stand in for it) ask only
/// <c>[[Get]]</c>. Every expected string is Node 26's answer to the same script, run in a fresh realm per row.
/// </para>
/// </summary>
/// <remarks>
/// Every case builds its own <see cref="Engine"/>, since most rows write an index to an intrinsic prototype, and the
/// fixture holds no mutable state, so its cases run in parallel.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class ArrayHoleExoticPrototypeTests
{
    /// <summary>
    /// The log records every trap the proxy runs, and whether a <c>get</c> came with the array as its receiver. Its
    /// target is <c>Array.prototype</c> so that iteration still finds the array's own iterator; its <c>get</c> answers
    /// <c>'P'</c> for index 1 and its <c>has</c> reports index 1 present, so a read that reaches the trap is visible in
    /// the value as well as in the log.
    /// </summary>
    private const string ProxyPrelude = """
        var result;
        var log = [];
        function key(k) { return typeof k === 'symbol' ? k.toString() : String(k); }
        function report(value) { return log.join(' ') + ' => ' + value; }
        var handler = {
            get: function (t, k, r) { log.push('get ' + key(k) + (r === arr ? ' (arr)' : ' (other)')); return k === '1' ? 'P' : Reflect.get(t, k, r); },
            has: function (t, k) { log.push('has ' + key(k)); return k === '1' || Reflect.has(t, k); },
            getOwnPropertyDescriptor: function (t, k) { log.push('getOwnPropertyDescriptor ' + key(k)); return Reflect.getOwnPropertyDescriptor(t, k); },
            getPrototypeOf: function (t) { log.push('getPrototypeOf'); return Reflect.getPrototypeOf(t); },
            ownKeys: function (t) { log.push('ownKeys'); return Reflect.ownKeys(t); },
            set: function (t, k, v, r) { log.push('set ' + key(k)); return Reflect.set(t, k, v, r); },
            defineProperty: function (t, k, d) { log.push('defineProperty ' + key(k)); return Reflect.defineProperty(t, k, d); },
            deleteProperty: function (t, k) { log.push('deleteProperty ' + key(k)); return Reflect.deleteProperty(t, k); }
        };
        var proxy = new Proxy(Array.prototype, handler);
        var getOnly = new Proxy(Array.prototype, {
            get: function (t, k, r) { log.push('get ' + key(k) + (r === arr ? ' (arr)' : ' (other)')); return k === '1' ? 'G' : Reflect.get(t, k, r); }
        });
        var arr = [1, , 3];
        """;

    private static readonly (string Name, string Script, string Expected)[] ProxyRows =
    [
        // [[Get]] alone: the proxy's get trap, with the array as the receiver, and nothing else.
        ("Index",
            "Object.setPrototypeOf(arr, proxy); result = report(arr[1]);",
            "get 1 (arr) => P"),
        ("IndexPastTheEnd",
            "Object.setPrototypeOf(arr, proxy); result = report(arr[5]);",
            "get 5 (arr) => undefined"),
        ("StringKey",
            "Object.setPrototypeOf(arr, proxy); var k = '1'; result = report(arr[k]);",
            "get 1 (arr) => P"),
        ("ReflectGet",
            "Object.setPrototypeOf(arr, proxy); result = report(Reflect.get(arr, 1));",
            "get 1 (arr) => P"),
        ("At",
            "Object.setPrototypeOf(arr, proxy); result = report(Array.prototype.at.call(arr, 1));",
            "get 1 (arr) => P"),
        ("Join",
            "Object.setPrototypeOf(arr, proxy); result = report(Array.prototype.join.call(arr, '-'));",
            "get 1 (arr) => 1-P-3"),

        // The array iterator's step is Get(array, index) (https://tc39.es/ecma262/#sec-createarrayiterator).
        ("ForOf",
            "Object.setPrototypeOf(arr, proxy); var out = []; for (var v of arr) out.push(v); result = report(out.join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("Spread",
            "Object.setPrototypeOf(arr, proxy); result = report([...arr].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ArrayFrom",
            "Object.setPrototypeOf(arr, proxy); result = report(Array.from(arr).join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterFunction",
            "Object.setPrototypeOf(arr, proxy); (function ([a, b, c]) { result = report([a, b, c].join()); })(arr);",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterArrow",
            "Object.setPrototypeOf(arr, proxy); (([a, b, c]) => { result = report([a, b, c].join()); })(arr);",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterGenerator",
            "Object.setPrototypeOf(arr, proxy); (function* ([a, b, c]) { result = report([a, b, c].join()); })(arr).next();",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterAsyncFunction",
            "Object.setPrototypeOf(arr, proxy); (async function ([a, b, c]) { result = report([a, b, c].join()); })(arr);",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterAsyncGenerator",
            "Object.setPrototypeOf(arr, proxy); (async function* ([a, b, c]) { result = report([a, b, c].join()); })(arr).next();",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ParameterRest",
            "Object.setPrototypeOf(arr, proxy); (function ([a, ...r]) { result = report(r.join()); })(arr);",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => P,3"),
        ("ParameterRestGenerator",
            "Object.setPrototypeOf(arr, proxy); (function* ([a, ...r]) { result = report(r.join()); })(arr).next();",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => P,3"),

        // A declaration's or an assignment's array pattern reads the array as its iterator would, with Get alone.
        ("VarPattern",
            "Object.setPrototypeOf(arr, proxy); var [a, b, c] = arr; result = report([a, b, c].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("AssignmentPattern",
            "Object.setPrototypeOf(arr, proxy); var a, b, c; [a, b, c] = arr; result = report([a, b, c].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("VarPatternElision",
            "Object.setPrototypeOf(arr, proxy); var [a, , c] = arr; result = report([a, c].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,3"),
        ("VarPatternRest",
            "Object.setPrototypeOf(arr, proxy); var [a, ...r] = arr; result = report(r.join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => P,3"),
        ("AssignmentPatternRest",
            "Object.setPrototypeOf(arr, proxy); var a, r; [a, ...r] = arr; result = report(r.join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => P,3"),
        ("LetPattern",
            "Object.setPrototypeOf(arr, proxy); let [a, b, c] = arr; result = report([a, b, c].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("ForOfHeadPattern",
            "Object.setPrototypeOf(arr, proxy); var outer = [arr]; log.length = 0; for (var [a, b, c] of outer) result = report([a, b, c].join());",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("VarPatternInAGenerator",
            "Object.setPrototypeOf(arr, proxy); (function* () { var [a, b, c] = arr; result = report([a, b, c].join()); })().next();",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),
        ("VarPatternInAnAsyncFunction",
            "Object.setPrototypeOf(arr, proxy); (async function () { var [a, b, c] = arr; result = report([a, b, c].join()); })();",
            "get Symbol(Symbol.iterator) (arr) get 1 (arr) => 1,P,3"),

        // FindViaPredicate reads every index with Get alone (https://tc39.es/ecma262/#sec-findviapredicate), so a
        // proxy that answers get but not has still hands the callback its value.
        ("Find",
            "Object.setPrototypeOf(arr, proxy); var seen = []; arr.find(function (v) { seen.push(v); }); result = report(seen.join());",
            "get find (arr) get 1 (arr) => 1,P,3"),
        ("FindIndex",
            "Object.setPrototypeOf(arr, proxy); var seen = []; var found = arr.findIndex(function (v) { seen.push(v); return v === 'P'; }); result = report(seen.join() + ' ' + found);",
            "get findIndex (arr) get 1 (arr) => 1,P 1"),
        ("FindLast",
            "Object.setPrototypeOf(arr, proxy); var seen = []; arr.findLast(function (v) { seen.push(v); }); result = report(seen.join());",
            "get findLast (arr) get 1 (arr) => 3,P,1"),
        ("FindLastIndex",
            "Object.setPrototypeOf(arr, proxy); var seen = []; var found = arr.findLastIndex(function (v) { seen.push(v); return v === 'P'; }); result = report(seen.join() + ' ' + found);",
            "get findLastIndex (arr) get 1 (arr) => 3,P 1"),
        ("FindThroughAGetOnlyProxy",
            "Object.setPrototypeOf(arr, getOnly); var seen = []; arr.find(function (v) { seen.push(v); }); result = report(seen.join());",
            "get find (arr) get 1 (arr) => 1,G,3"),
        ("FindLastThroughAGetOnlyProxy",
            "Object.setPrototypeOf(arr, getOnly); var seen = []; arr.findLast(function (v) { seen.push(v); }); result = report(seen.join());",
            "get findLast (arr) get 1 (arr) => 3,G,1"),

        // A keys step reads no element; a values or entries step reads it with Get.
        ("Keys",
            "Object.setPrototypeOf(arr, proxy); result = report([...arr.keys()].join());",
            "get keys (arr) => 0,1,2"),
        ("KeysForOf",
            "Object.setPrototypeOf(arr, proxy); var out = []; for (var k of arr.keys()) out.push(k); result = report(out.join());",
            "get keys (arr) => 0,1,2"),
        ("Entries",
            "Object.setPrototypeOf(arr, proxy); result = report([...arr.entries()].join(';'));",
            "get entries (arr) get 1 (arr) => 0,1;1,P;2,3"),
        ("Values",
            "Object.setPrototypeOf(arr, proxy); result = report([...arr.values()].join());",
            "get values (arr) get 1 (arr) => 1,P,3"),

        // HasProperty, then Get when it answers true.
        ("ForEach",
            "Object.setPrototypeOf(arr, proxy); var out = []; Array.prototype.forEach.call(arr, function (v) { out.push(v); }); result = report(out.join());",
            "has 1 get 1 (arr) => 1,P,3"),
        ("In",
            "Object.setPrototypeOf(arr, proxy); result = report(1 in arr);",
            "has 1 => true"),

        // The prototype changes after the algorithm chose the array's own lane.
        ("SwappedDuringJoin",
            "arr[0] = { toString: function () { Object.setPrototypeOf(arr, proxy); return 'a'; } }; result = report(arr.join('-'));",
            "get 1 (arr) => a-P-3"),
        ("SwappedDuringForOf",
            "var out = []; for (var v of arr) { if (v === 1) Object.setPrototypeOf(arr, proxy); out.push(v); } result = report(out.join());",
            "get 1 (arr) => 1,P,3"),
        ("SwappedDuringParameterRest",
            "arr = [1, 2, , 4]; Object.defineProperty(arr, 1, { get: function () { Object.setPrototypeOf(arr, proxy); return 2; } }); (function ([a, ...r]) { result = report(r.join()); })(arr);",
            "get 2 (arr) => 2,,4"),
    ];

    private static readonly (string Name, string Script, string Expected)[] ChainRows =
    [
        // A typed array's [[Get]] answers a canonical numeric key itself: an invalid index is undefined, and nothing
        // above the typed array is asked.
        ("TypedArrayOnTheChain.Index",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; Object.prototype[7] = 'o'; result = String(arr[1]) + ' ' + String(arr[7]);",
            "undefined undefined"),
        ("TypedArrayOnTheChain.StringKey",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; var k = '1'; result = String(arr[k]);",
            "undefined"),
        ("TypedArrayOnTheChain.ReflectGet",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; result = String(Reflect.get(arr, 1));",
            "undefined"),
        ("TypedArrayOnTheChain.Join",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; result = Array.prototype.join.call(arr, '-');",
            "1--3"),
        ("TypedArrayOnTheChain.ForOf",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; var out = []; for (var v of arr) out.push(String(v)); result = out.join();",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.ParameterFunction",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; (function ([a, b, c]) { result = [a, b, c].map(String).join(); })(arr);",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.ParameterGenerator",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; (function* ([a, b, c]) { result = [a, b, c].map(String).join(); })(arr).next();",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.ParameterAsyncFunction",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; (async function ([a, b, c]) { result = [a, b, c].map(String).join(); })(arr);",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.ParameterRest",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; (function ([a, ...r]) { result = r.map(String).join(); })(arr);",
            "undefined,3"),
        ("TypedArrayOnTheChain.VarPattern",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; var [a, b, c] = arr; result = [a, b, c].map(String).join();",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.AssignmentPattern",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; var a, b, c; [a, b, c] = arr; result = [a, b, c].map(String).join();",
            "1,undefined,3"),
        ("TypedArrayOnTheChain.ForEach",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; var out = []; Array.prototype.forEach.call(arr, function (v, i) { out.push(i + '=' + v); }); result = out.join();",
            "0=1,2=3"),
        ("TypedArrayOnTheChain.In",
            "var x = Object.create(new Int8Array(0)); var arr = [1, , 3]; Object.setPrototypeOf(arr, x); Object.prototype[1] = 'o'; result = String(1 in arr);",
            "false"),
        ("TypedArrayAsThePrototype.Index",
            "var arr = [1, , 3]; Object.setPrototypeOf(arr, new Int8Array(4)); Object.prototype[5] = 'o'; result = String(arr[1]) + ' ' + String(arr[5]) + ' ' + String(5 in arr);",
            "0 undefined false"),

        // A getter on an ordinary prototype still runs with the array as its receiver.
        ("GetterOnAnOrdinaryPrototype.Index",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); result = String(arr[1]);",
            "true"),
        ("GetterOnAnOrdinaryPrototype.ForOf",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); var out = []; for (var v of arr) out.push(v); result = out.join();",
            "1,true,3"),
        ("GetterOnAnOrdinaryPrototype.ParameterFunction",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); (function ([a, b, c]) { result = [a, b, c].join(); })(arr);",
            "1,true,3"),
        ("GetterOnAnOrdinaryPrototype.ParameterGenerator",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); (function* ([a, b, c]) { result = [a, b, c].join(); })(arr).next();",
            "1,true,3"),
        ("GetterOnAnOrdinaryPrototype.Join",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); result = arr.join();",
            "1,true,3"),
        ("GetterOnAnOrdinaryPrototype.SwappedDuringJoin",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); arr[0] = { toString: function () { Object.setPrototypeOf(arr, proto); return 'a'; } }; result = arr.join();",
            "a,true,3"),
        ("GetterOnAnOrdinaryPrototype.ForEach",
            "var arr = [1, , 3]; var proto = Object.create(Array.prototype); Object.defineProperty(proto, 1, { get: function () { return this === arr; } }); Object.setPrototypeOf(arr, proto); var out = []; arr.forEach(function (v) { out.push(v); }); result = out.join();",
            "1,true,3"),
        ("GetterOnArrayPrototype.Index",
            "var arr = [1, , 3]; Object.defineProperty(Array.prototype, 1, { get: function () { return this === arr; }, configurable: true }); result = String(arr[1]);",
            "true"),
        ("GetterOnArrayPrototype.ForOf",
            "var arr = [1, , 3]; Object.defineProperty(Array.prototype, 1, { get: function () { return this === arr; }, configurable: true }); var out = ''; for (var v of arr) out += v + ';'; result = out;",
            "1;true;3;"),

        // A String object is exotic in [[GetOwnProperty]] only, and an arguments object answers its mapped indices
        // from its own [[Get]]: both resolve index 1 themselves and pass anything else on.
        ("StringObjectOnTheChain.Index",
            "var arr = [1, , 3]; Object.setPrototypeOf(arr, new String('xyz')); Object.prototype[5] = 'o'; result = arr[1] + ' ' + arr[5] + ' ' + String(arr[6]);",
            "y o undefined"),
        ("StringObjectOnTheChain.ForOf",
            "var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, new String('xyz')); var out = []; for (var v of arr) out.push(v); result = out.join();",
            "1,y,3"),
        ("StringObjectOnTheChain.ParameterFunction",
            "var arr = [1, , 3]; arr[Symbol.iterator] = Array.prototype.values; Object.setPrototypeOf(arr, new String('xyz')); (function ([a, b, c]) { result = [a, b, c].join(); })(arr);",
            "1,y,3"),
        ("MappedArgumentsOnTheChain.Index",
            "function g(a, b, c) { return arguments; } var arr = [1, , 3]; Object.setPrototypeOf(arr, g('a', 'b', 'c')); Object.prototype[1] = 'o'; Object.prototype[4] = 'o4'; result = arr[1] + ' ' + arr[4];",
            "b o4"),
        ("MappedArgumentsOnTheChain.ForOf",
            "function g(a, b, c) { return arguments; } var arr = [1, , 3]; Object.setPrototypeOf(arr, g('a', 'b', 'c')); Object.prototype[1] = 'o'; var out = []; for (var v of arr) out.push(v); result = out.join();",
            "1,b,3"),
        ("MappedArgumentsOnTheChain.ParameterFunction",
            "function g(a, b, c) { return arguments; } var arr = [1, , 3]; Object.setPrototypeOf(arr, g('a', 'b', 'c')); Object.prototype[1] = 'o'; (function ([a, b, c]) { result = [a, b, c].join(); })(arr);",
            "1,b,3"),
        ("UnmappedArgumentsOnTheChain.Index",
            "function g(a, b, c) { 'use strict'; return arguments; } var arr = [1, , 3]; Object.setPrototypeOf(arr, g('a', 'b', 'c')); Object.prototype[1] = 'o'; result = arr[1];",
            "b"),

        // A keys step reads no element at all, so even an own getter does not run.
        ("OwnGetter.KeysReadNoElement",
            "var log = []; var arr = [1, 2]; Object.defineProperty(arr, 0, { get: function () { log.push('get 0'); return 1; } }); var ks = [...arr.keys()]; result = log.join() + ' => ' + ks.join();",
            " => 0,1"),
        ("OwnGetter.EntriesReadEachElementOnce",
            "var log = []; var arr = [1, 2]; Object.defineProperty(arr, 0, { get: function () { log.push('get 0'); return 1; } }); var es = [...arr.entries()]; result = log.join() + ' => ' + es.join(';');",
            "get 0 => 0,1;1,2"),

        // The intrinsic chain.
        ("PlainArray.Hole",
            "var arr = [1, , 3]; result = String(arr[1]) + ' ' + String(arr[5]);",
            "undefined undefined"),
        ("PlainArray.HoleForOf",
            "var arr = [1, , 3]; var out = []; for (var v of arr) out.push(String(v)); result = out.join();",
            "1,undefined,3"),
        ("PlainArray.InheritedIndex",
            "Array.prototype[1] = 'p'; var arr = [1, , 3]; var out = []; for (var v of arr) out.push(v); result = arr[1] + ' ' + out.join() + ' ' + String(arr[5]);",
            "p 1,p,3 undefined"),
        ("PlainArray.InheritedIndexOnObjectPrototype",
            "Object.prototype[1] = 'o'; var arr = [1, , 3]; (function ([a, b, c]) { result = arr[1] + ' ' + [a, b, c].join(); })(arr);",
            "o 1,o,3"),
        ("PlainArray.ForEachSkipsHoles",
            "var arr = [1, , 3]; var out = []; arr.forEach(function (v, i) { out.push(i); }); result = out.join();",
            "0,2"),
    ];

    public static TestCases<string, string, string> ProxyCases() => Cases(ProxyRows);

    public static TestCases<string, string, string> ChainCases() => Cases(ChainRows);

    private static TestCases<string, string, string> Cases((string Name, string Script, string Expected)[] rows)
    {
        var cases = new TestCases<string, string, string>();
        foreach (var row in rows)
        {
            cases.Add(row.Name, row.Script, row.Expected);
        }

        return cases;
    }

    [TestCaseSource(nameof(ProxyCases))]
    public void AHoleReadThroughAProxyPrototypeRunsTheTrapsNodeRuns(string name, string script, string expected)
    {
        var engine = new Engine();
        engine.Execute(ProxyPrelude);
        engine.Execute(script);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("result").Should().Be(expected, name);
    }

    [TestCaseSource(nameof(ChainCases))]
    public void AHoleReadResolvesOnTheChainAsGetWould(string name, string script, string expected)
    {
        var engine = new Engine();
        engine.Execute("var result;");
        engine.Execute(script);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("result").Should().Be(expected, name);
    }

    /// <summary>
    /// The public read is hole-aware: <c>false</c> means the index resolves to nothing, so with a Proxy on the chain it
    /// asks <c>has</c> before <c>get</c>, as <c>HasProperty</c> then <c>Get</c> would. The indexer is a plain
    /// <c>[[Get]]</c> and asks <c>get</c> alone.
    /// </summary>
    [Test]
    public void TryGetValueAsksAProxyPrototypeHasThenGetAndTheIndexerOnlyGet()
    {
        var engine = new Engine();
        engine.Execute(ProxyPrelude);
        engine.Execute("Object.setPrototypeOf(arr, proxy);");
        var array = engine.GetValue("arr").AsArray();

        array.TryGetValue(1, out var inherited).Should().BeTrue();
        inherited.Should().Be("P");
        engine.Evaluate("report('')").Should().Be("has 1 get 1 (arr) => ");

        engine.Execute("log.length = 0;");
        array.TryGetValue(5, out var absent).Should().BeFalse();
        absent.Should().BeUndefined();
        engine.Evaluate("report('')").Should().Be("has 5 => ");

        engine.Execute("log.length = 0;");
        array[1].Should().Be("P");
        engine.Evaluate("report('')").Should().Be("get 1 (arr) => ");
    }

    /// <summary>
    /// <c>ToArray</c> is a sequence of plain reads, like the indexer and the enumerator, so a proxy that answers
    /// <c>get</c> for an index but not <c>has</c> is read the same way by all three, where the hole-aware
    /// <c>TryGetValue</c> reports the index absent.
    /// </summary>
    [Test]
    public void ToArrayReadsAsTheIndexerDoesThroughAGetOnlyProxy()
    {
        var engine = new Engine();
        engine.Execute(ProxyPrelude);
        engine.Execute("Object.setPrototypeOf(arr, getOnly);");
        var array = engine.GetValue("arr").AsArray();

        var copy = array.ToArray();
        copy.Should().HaveCount(3);
        copy[1].Should().Be("G");
        engine.Evaluate("report('')").Should().Be("get 1 (arr) => ");

        engine.Execute("log.length = 0;");
        array[1].Should().Be("G");
        array.Select(element => element.ToString()).Should().Equal("1", "G", "3");
        engine.Evaluate("report('')").Should().Be("get 1 (arr) get 1 (arr) => ");

        engine.Execute("log.length = 0;");
        array.TryGetValue(1, out var probed).Should().BeFalse();
        probed.Should().BeUndefined();
        engine.Evaluate("report('')").Should().Be(" => ");
    }

    /// <summary>
    /// A host prototype that overrides <c>Get</c> is asked for the index, with the array as receiver, rather than being
    /// probed for an own property and walked past to whatever its own prototype holds.
    /// </summary>
    [Test]
    public void AHostPrototypeOverridingGetAnswersAHoleWithTheArrayAsReceiver()
    {
        var engine = new Engine();
        var host = new SynthesizingPrototype(engine);
        engine.SetValue("host", host);
        engine.Execute("""
            Object.setPrototypeOf(host, Array.prototype);
            Object.prototype[1] = 'o';
            var arr = [1, , 3];
            Object.setPrototypeOf(arr, host);
            """);
        var array = engine.GetValue("arr");

        engine.Evaluate("arr[1]").Should().Be("host 1");
        host.Receivers.Should().ContainSingle().Which.Should().BeSameAs(array);

        host.Receivers.Clear();
        engine.Evaluate("var out = []; for (var v of arr) out.push(v); out.join()").Should().Be("1,host 1,3");
        host.Receivers.Should().HaveCount(1).And.OnlyContain(r => ReferenceEquals(r, array));

        host.Receivers.Clear();
        engine.Evaluate("(function ([a, b, c]) { return [a, b, c].join(); })(arr)").Should().Be("1,host 1,3");
        host.Receivers.Should().HaveCount(1).And.OnlyContain(r => ReferenceEquals(r, array));
    }

    /// <summary>Answers every array index itself, and everything else as an ordinary object would.</summary>
    private sealed class SynthesizingPrototype(Engine engine) : ObjectInstance(engine)
    {
        public List<JsValue> Receivers { get; } = [];

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (property.IsString() && uint.TryParse(property.AsString(), out _))
            {
                Receivers.Add(receiver);
                return "host " + property.AsString();
            }

            return base.Get(property, receiver);
        }
    }
}

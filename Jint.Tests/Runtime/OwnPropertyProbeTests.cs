using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="ObjectInstance.ProbeOwnProperty"/> answers existence and enumerability without building a
/// <see cref="PropertyDescriptor"/>, and the engine trusts the answer without re-verifying it: a wrong
/// <see cref="OwnPropertyProbe.Missing"/> silently drops the key from <c>in</c>, <c>hasOwnProperty</c>,
/// <c>propertyIsEnumerable</c>, <c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>Object.assign</c>,
/// object spread and <c>JSON.stringify</c>.
/// <para>
/// These tests are the checker for the in-box exotics that override it. Each one probes a spread of keys
/// <em>first</em>, then reads the descriptor for the same keys, and requires the two to agree — which is
/// the whole contract. Probing before reading matters for the types whose <c>GetOwnProperty</c> mutates
/// state (an arguments object materializes; an array materializes a per-index descriptor), because the
/// claim under test is that the probe answered what <c>GetOwnProperty</c> <em>would have</em> answered.
/// </para>
/// </summary>
public class OwnPropertyProbeTests
{
    private static void AssertProbesAgreeWithDescriptors(ObjectInstance target, params JsValue[] keys)
    {
        var probes = new OwnPropertyProbe[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            probes[i] = target.ProbeOwnProperty(keys[i]);
        }

        for (var i = 0; i < keys.Length; i++)
        {
            var descriptor = target.GetOwnProperty(keys[i]);
            var expected = ReferenceEquals(descriptor, PropertyDescriptor.Undefined)
                ? OwnPropertyProbe.Missing
                : descriptor.Enumerable
                    ? OwnPropertyProbe.Enumerable
                    : OwnPropertyProbe.NonEnumerable;

            probes[i].Should().Be(expected, "the probe of '{0}' must agree with GetOwnProperty on {1}", keys[i], target.GetType().Name);
        }
    }

    /// <summary>
    /// Virtual read mode is private state with no public surface (every internal accessor for it
    /// materializes on the way out), so the one assertion that needs to see it reads the field.
    /// </summary>
    private static bool IsVirtual(JsArguments target) =>
        (bool) typeof(JsArguments)
            .GetField("_virtualMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(target)!;

    private static JsValue[] IndexLikeKeys() =>
    [
        JsString.Create("0"),
        JsString.Create("1"),
        JsString.Create("2"),
        JsString.Create("3"),
        JsString.Create("42"),
        JsString.Create("-1"),
        JsString.Create("-0"),
        JsString.Create("1.5"),
        JsString.Create("0.0"),
        JsString.Create("00"),
        JsString.Create("1e21"),
        JsString.Create("4294967295"),
        JsString.Create("NaN"),
        JsString.Create("Infinity"),
        JsString.Create("length"),
        JsString.Create("nope"),
        JsString.Create("toString"),
        JsNumber.Create(0),
        JsNumber.Create(2),
        JsNumber.Create(7),
        GlobalSymbolRegistry.Iterator,
        GlobalSymbolRegistry.ToStringTag,
    ];

    [Fact]
    public void StringInstanceProbeAgreesWithDescriptor()
    {
        var engine = new Engine();
        var target = (ObjectInstance) engine.Evaluate("new String('abc')");

        AssertProbesAgreeWithDescriptors(target, IndexLikeKeys());
    }

    [Fact]
    public void StringInstanceProbeAgreesForEmptyAndRedefinedShapes()
    {
        var engine = new Engine();

        AssertProbesAgreeWithDescriptors((ObjectInstance) engine.Evaluate("new String('')"), IndexLikeKeys());

        // an own property shadowing an index, and a non-enumerable own property, must both keep their
        // own flags rather than the index lane's Enumerable
        var shadowed = (ObjectInstance) engine.Evaluate(
            "var s = new String('abc'); Object.defineProperty(s, 'hidden', { value: 1, enumerable: false }); s");
        AssertProbesAgreeWithDescriptors(shadowed, [.. IndexLikeKeys(), JsString.Create("hidden")]);

        // String.prototype is itself a StringInstance, and a shaped built-in
        AssertProbesAgreeWithDescriptors(
            engine.Realm.Intrinsics.String.PrototypeObject,
            [.. IndexLikeKeys(), JsString.Create("charAt"), JsString.Create("constructor")]);
    }

    [Fact]
    public void StringInstanceEnumerationIsUnchanged()
    {
        var engine = new Engine();

        engine.Evaluate("JSON.stringify(Object.keys(new String('abc')))").AsString().Should().Be("[\"0\",\"1\",\"2\"]");
        engine.Evaluate("JSON.stringify(Object.assign({}, new String('ab')))").AsString().Should().Be("{\"0\":\"a\",\"1\":\"b\"}");
        engine.Evaluate("JSON.stringify({ ...new String('ab') })").AsString().Should().Be("{\"0\":\"a\",\"1\":\"b\"}");
        engine.Evaluate("new String('abc').hasOwnProperty('2')").AsBoolean().Should().BeTrue();
        engine.Evaluate("new String('abc').hasOwnProperty('3')").AsBoolean().Should().BeFalse();
        engine.Evaluate("new String('abc').hasOwnProperty('length')").AsBoolean().Should().BeTrue();
        engine.Evaluate("new String('abc').propertyIsEnumerable('length')").AsBoolean().Should().BeFalse();
        engine.Evaluate("new String('abc').propertyIsEnumerable('0')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'0' in new String('abc')").AsBoolean().Should().BeTrue();
        engine.Evaluate("'Infinity' in new String('abc')").AsBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("new Uint8Array([1,2,3])")]
    [InlineData("new Float64Array([1.5,2.5])")]
    [InlineData("new BigInt64Array(2)")]
    [InlineData("new Uint8Array(0)")]
    [InlineData("new Uint8Array(new ArrayBuffer(8, { maxByteLength: 16 }))")]
    public void TypedArrayProbeAgreesWithDescriptor(string source)
    {
        var engine = new Engine();
        var target = (ObjectInstance) engine.Evaluate(source);

        AssertProbesAgreeWithDescriptors(target, IndexLikeKeys());
    }

    [Fact]
    public void DetachedTypedArrayProbeReportsEveryIndexMissing()
    {
        var engine = new Engine();
        var target = (ObjectInstance) engine.Evaluate(
            "var b = new ArrayBuffer(8); var ta = new Uint8Array(b); ta.buffer.transfer(); ta");

        AssertProbesAgreeWithDescriptors(target, IndexLikeKeys());

        target.ProbeOwnProperty(JsString.Create("0")).Should().Be(OwnPropertyProbe.Missing);
        engine.Evaluate("0 in ta").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.keys(ta).length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void TypedArrayEnumerationIsUnchanged()
    {
        var engine = new Engine();

        engine.Evaluate("JSON.stringify(Object.keys(new Uint8Array([1,2,3])))").AsString().Should().Be("[\"0\",\"1\",\"2\"]");
        engine.Evaluate("JSON.stringify(Object.assign({}, new Uint8Array([1,2])))").AsString().Should().Be("{\"0\":1,\"1\":2}");
        engine.Evaluate("JSON.stringify(new Uint8Array([1,2]))").AsString().Should().Be("{\"0\":1,\"1\":2}");
        engine.Evaluate("new Uint8Array([1,2]).hasOwnProperty(1)").AsBoolean().Should().BeTrue();
        engine.Evaluate("new Uint8Array([1,2]).hasOwnProperty(2)").AsBoolean().Should().BeFalse();
        engine.Evaluate("new Uint8Array([1,2]).propertyIsEnumerable(0)").AsBoolean().Should().BeTrue();
        engine.Evaluate("'1.5' in new Uint8Array([1,2])").AsBoolean().Should().BeFalse();
        engine.Evaluate("'-0' in new Uint8Array([1,2])").AsBoolean().Should().BeFalse();
    }

    [Theory]
    // mapped (sloppy, simple parameter list), unmapped (strict), rest/pattern parameter lists,
    // and both the still-virtual and the already-materialized states
    [InlineData("(function (a, b) { return arguments; })(1, 2)")]
    [InlineData("(function (a, b) { 'use strict'; return arguments; })(1, 2)")]
    [InlineData("(function (...r) { return arguments; })(1, 2)")]
    [InlineData("(function ([a], b) { return arguments; })([1], 2)")]
    [InlineData("(function (a, a) { return arguments; })(1, 2)")]
    [InlineData("(function (a) { return arguments; })()")]
    [InlineData("(function (a, b) { var x = Object.keys(arguments); return arguments; })(1, 2)")]
    [InlineData("(function (a, b) { delete arguments[0]; return arguments; })(1, 2)")]
    [InlineData("(function (a, b) { Object.defineProperty(arguments, '0', { enumerable: false }); return arguments; })(1, 2)")]
    [InlineData("(function (a, b) { arguments[5] = 9; return arguments; })(1, 2)")]
    public void ArgumentsProbeAgreesWithDescriptor(string source)
    {
        var engine = new Engine();
        var target = (ObjectInstance) engine.Evaluate(source);

        AssertProbesAgreeWithDescriptors(target, [.. IndexLikeKeys(), JsString.Create("callee")]);
    }

    [Fact]
    public void ArgumentsProbeDoesNotMaterialize()
    {
        var engine = new Engine();
        var target = (JsArguments) engine.Evaluate("(function (a, b) { return arguments; })(1, 2)");

        // the object is still virtual here, so the probe must answer without running Initialize
        IsVirtual(target).Should().BeTrue("the returned arguments object has not been materialized yet");
        target.ProbeOwnProperty(JsString.Create("0")).Should().Be(OwnPropertyProbe.Enumerable);
        target.ProbeOwnProperty(JsString.Create("5")).Should().Be(OwnPropertyProbe.Missing);
        target.ProbeOwnProperty(JsString.Create("length")).Should().Be(OwnPropertyProbe.NonEnumerable);
        target.ProbeOwnProperty(JsString.Create("callee")).Should().Be(OwnPropertyProbe.NonEnumerable);
        target.ProbeOwnProperty(GlobalSymbolRegistry.Iterator).Should().Be(OwnPropertyProbe.NonEnumerable);
        target.ProbeOwnProperty(JsString.Create("nope")).Should().Be(OwnPropertyProbe.Missing);
        IsVirtual(target).Should().BeTrue("no probe may run Initialize");

        // ...and the materialized answer is the same one
        AssertProbesAgreeWithDescriptors(target, [.. IndexLikeKeys(), JsString.Create("callee")]);
    }

    [Fact]
    public void ArgumentsEnumerationIsUnchanged()
    {
        var engine = new Engine();

        engine.Evaluate("(function (a, b) { return JSON.stringify(Object.keys(arguments)); })(1, 2)").AsString().Should().Be("[\"0\",\"1\"]");
        engine.Evaluate("(function (a, b) { return 0 in arguments; })(1, 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function (a, b) { return 2 in arguments; })(1, 2)").AsBoolean().Should().BeFalse();
        engine.Evaluate("(function (a, b) { return arguments.hasOwnProperty('length'); })(1, 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function (a, b) { return arguments.hasOwnProperty('callee'); })(1, 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function (a, b) { 'use strict'; return arguments.hasOwnProperty('callee'); })(1, 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function (a, b) { return arguments.propertyIsEnumerable('length'); })(1, 2)").AsBoolean().Should().BeFalse();
        engine.Evaluate("(function (a, b) { return arguments.propertyIsEnumerable(0); })(1, 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("(function (a, b) { return JSON.stringify(Object.assign({}, arguments)); })(1, 2)").AsString().Should().Be("{\"0\":1,\"1\":2}");
        engine.Evaluate("(function (a, b) { var s = ''; for (var k in arguments) s += k; return s; })(1, 2)").AsString().Should().Be("01");

        // `n in arguments` must not disturb the mapped-binding overlay
        engine.Evaluate("(function (a) { var seen = 0 in arguments; a = 7; return arguments[0]; })(1)").AsNumber().Should().Be(7);
        engine.Evaluate("(function (a) { var seen = 0 in arguments; arguments[0] = 7; return a; })(1)").AsNumber().Should().Be(7);
    }

    /// <summary>
    /// The wrapped string-keyed dictionary answers existence from the target's <c>ContainsKey</c> instead
    /// of from the descriptor its read path would build. The spread of keys covers the four things that
    /// have to keep agreeing: a plain key, a key whose value is an object (the case whose descriptor drags
    /// a whole nested wrapper along), a key that shadows a CLR member, and names the dictionary does not
    /// carry — one that resolves as a CLR member anyway and one that resolves as nothing.
    /// </summary>
    [Fact]
    public void WrappedDictionaryProbeAgreesWithDescriptor()
    {
        var engine = new Engine();
        engine.SetValue("doc", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["plain"] = 1d,
            ["nested"] = new Dictionary<string, object>(StringComparer.Ordinal) { ["deep"] = 2d },
            ["Count"] = "shadow",
        });

        var target = (ObjectInstance) engine.GetValue("doc");

        AssertProbesAgreeWithDescriptors(
            target,
            JsString.Create("plain"),
            JsString.Create("nested"),
            JsString.Create("Count"),
            JsString.Create("Keys"),
            JsString.Create("absent"),
            JsString.Create("0"),
            GlobalSymbolRegistry.Iterator);
    }

    /// <summary>
    /// Anything stored on the wrapper itself outranks the dictionary, so the probe has to consult that
    /// store first — a defined non-enumerable descriptor must not be reported as the enumerable key the
    /// dictionary still carries underneath it.
    /// </summary>
    [Fact]
    public void WrappedDictionaryProbeAgreesWhenAScriptHasRedefinedAKey()
    {
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        engine.SetValue("doc", new Dictionary<string, object>(StringComparer.Ordinal) { ["a"] = 1d, ["b"] = 2d });
        engine.Execute("Object.defineProperty(doc, 'a', { value: 9, enumerable: false, configurable: true });");

        var target = (ObjectInstance) engine.GetValue("doc");

        AssertProbesAgreeWithDescriptors(target, JsString.Create("a"), JsString.Create("b"));
        target.ProbeOwnProperty(JsString.Create("a")).Should().Be(OwnPropertyProbe.NonEnumerable);
    }

    [Fact]
    public void ModuleNamespaceProbeAgreesWithDescriptor()
    {
        var engine = new Engine();
        engine.Modules.Add("lib", "export const a = 1; export function b() {} export default 3;");
        var target = (ObjectInstance) engine.Modules.Import("lib");

        AssertProbesAgreeWithDescriptors(
            target,
            [
                JsString.Create("a"),
                JsString.Create("b"),
                JsString.Create("default"),
                JsString.Create("missing"),
                JsString.Create("then"),
                JsString.Create("toString"),
                GlobalSymbolRegistry.ToStringTag,
                GlobalSymbolRegistry.Iterator,
            ]);
    }

    [Fact]
    public void ModuleNamespaceProbeStillThrowsForAnUninitializedBinding()
    {
        // https://tc39.es/ecma262/#sec-module-namespace-exotic-objects-getownproperty-p step 4 performs
        // [[Get]], which throws for a TDZ binding; every probe consumer has to propagate that, so the
        // probe cannot answer from the export-name set alone.
        var engine = new Engine();
        engine.Modules.Add("self", """
            import * as ns from 'self';
            export const observed = (function () {
                var results = [];
                results.push(typeof ns);
                try { Object.prototype.hasOwnProperty.call(ns, 'later'); results.push('no-throw'); }
                catch (e) { results.push(e.constructor.name); }
                try { Object.keys(ns); results.push('no-throw'); }
                catch (e) { results.push(e.constructor.name); }
                try { Object.prototype.propertyIsEnumerable.call(ns, 'later'); results.push('no-throw'); }
                catch (e) { results.push(e.constructor.name); }
                // [[HasProperty]] does not resolve the binding, so `in` must NOT throw
                results.push('later' in ns);
                results.push('absent' in ns);
                return results.join('|');
            })();
            export const later = 1;
            """);

        var ns = engine.Modules.Import("self");
        ns.Get("observed").AsString().Should().Be("object|ReferenceError|ReferenceError|ReferenceError|true|false");
    }
}

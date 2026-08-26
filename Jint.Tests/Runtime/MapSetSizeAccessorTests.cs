using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>size</c> is an accessor on <c>Map.prototype</c> / <c>Set.prototype</c> and lives nowhere else —
/// https://tc39.es/ecma262/#sec-get-map.prototype.size and
/// https://tc39.es/ecma262/#sec-get-set.prototype.size. Both halves of that used to be wrong at once, from one
/// cause: the prototype getter was a brand check with a hard-coded <c>0</c> for a body, and the real count was
/// served instead from an own data descriptor <c>JsMap</c> / <c>JsSet</c> synthesized inside
/// <c>GetOwnProperty</c>. So the getter answered 0 for a valid receiver, and every instance reported an own
/// non-configurable <c>size</c> that no key enumeration ever listed.
/// </summary>
public class MapSetSizeAccessorTests
{
    private const string Map = "new Map([[1, 'a'], [2, 'b'], [3, 'c']])";
    private const string Set = "new Set([1, 2, 3])";

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void TheExtractedPrototypeGetterReadsItsReceiver(string kind, string create)
    {
        new Engine().Evaluate($$"""
            var c = {{create}};
            var get = Object.getOwnPropertyDescriptor({{kind}}.prototype, 'size').get;
            get.call(c);
            """).AsNumber().Should().Be(3);
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void TheExtractedPrototypeGetterTracksLaterMutations(string kind, string create)
    {
        new Engine().Evaluate($$"""
            var c = {{create}};
            var get = Object.getOwnPropertyDescriptor({{kind}}.prototype, 'size').get;
            var before = get.call(c);
            c.clear();
            before + '/' + get.call(c);
            """).AsString().Should().Be("3/0");
    }

    [TestCase("Map")]
    [TestCase("Set")]
    public void TheExtractedPrototypeGetterKeepsItsBrandCheck(string kind)
    {
        // The other collection is the interesting negative: it has a `size` of its own, so only a real
        // [[MapData]] / [[SetData]] check can tell the two apart.
        var other = string.Equals(kind, "Map", StringComparison.Ordinal) ? "new Set([1])" : "new Map()";

        var engine = new Engine();
        engine.Execute($"var get = Object.getOwnPropertyDescriptor({kind}.prototype, 'size').get;");

        foreach (var receiver in new[] { "{}", "[]", "undefined", $"{kind}.prototype", other })
        {
            Invoking(() => engine.Evaluate($"get.call({receiver});"))
                .Should().ThrowExactly<JavaScriptException>(receiver)
                .WithMessage($"Method {kind}.prototype.get size called on incompatible receiver");
        }
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void AnInstanceHasNoOwnSize(string kind, string create)
    {
        _ = kind;
        var engine = new Engine();
        engine.Execute($"var c = {create};");

        engine.Evaluate("c.hasOwnProperty('size')").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.getOwnPropertyDescriptor(c, 'size')").IsUndefined().Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyNames(c).length").AsNumber().Should().Be(0);
        engine.Evaluate("Reflect.ownKeys(c).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.keys(c).length").AsNumber().Should().Be(0);
        engine.Evaluate("JSON.stringify(c)").AsString().Should().Be("{}");

        // ...but it is still reachable, and still reads, through the prototype.
        engine.Evaluate("'size' in c").AsBoolean().Should().BeTrue();
        engine.Evaluate("c.size").AsNumber().Should().Be(3);
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void AnOwnShadowingAccessorCanBeDefinedAndDeleted(string kind, string create)
    {
        _ = kind;

        // Defining `size` on an instance is legal — the phantom own descriptor used to be non-configurable
        // and turned this into a TypeError.
        new Engine().Evaluate($$"""
            var c = {{create}};
            Object.defineProperty(c, 'size', { get: function () { return 99; }, configurable: true });
            var shadowed = c.size + '/' + c.hasOwnProperty('size') + '/' + Object.keys(c).length;
            var removed = delete c.size;
            shadowed + '|' + removed + '|' + c.size + '/' + c.hasOwnProperty('size');
            """).AsString().Should().Be("99/true/0|true|3/false");
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void AnOwnShadowingDataPropertyCanBeDefinedAndDeleted(string kind, string create)
    {
        _ = kind;
        new Engine().Evaluate($$"""
            var c = {{create}};
            Object.defineProperty(c, 'size', { value: 7, writable: true, enumerable: true, configurable: true });
            var shadowed = c.size + '/' + Object.keys(c).join();
            delete c.size;
            shadowed + '|' + c.size;
            """).AsString().Should().Be("7/size|3");
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void DeletingSizeFromAnInstanceThatNeverDefinedItSucceeds(string kind, string create)
    {
        _ = kind;

        // No own property, so [[Delete]] is vacuously true — and strict mode agrees. The phantom descriptor
        // was non-configurable, so this used to answer false / throw.
        new Engine().Evaluate($$"""
            'use strict';
            var c = {{create}};
            (delete c.size) + '/' + c.size;
            """).AsString().Should().Be("true/3");
    }

    [TestCase("Map", "class C extends Map {}", "new C([[1, 'a'], [2, 'b'], [3, 'c']])")]
    [TestCase("Set", "class C extends Set {}", "new C([1, 2, 3])")]
    public void ASubclassInstanceReadsTheInheritedAccessor(string kind, string declare, string create)
    {
        _ = kind;
        new Engine().Evaluate($$"""
            {{declare}}
            var c = {{create}};
            c.size + '/' + c.hasOwnProperty('size') + '/' + Object.getOwnPropertyNames(c).length;
            """).AsString().Should().Be("3/false/0");
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void AProxyGetTrapForwardingTheTargetAsReceiverStillReadsSize(string kind, string create)
    {
        _ = kind;
        new Engine().Evaluate($$"""
            var c = {{create}};
            var p = new Proxy(c, { get: function (t, k) { return Reflect.get(t, k, t); } });
            p.size;
            """).AsNumber().Should().Be(3);
    }

    [TestCase("Map", Map)]
    [TestCase("Set", Set)]
    public void ABareProxyIsAnIncompatibleReceiverForSize(string kind, string create)
    {
        // A proxy without a `get` trap forwards to the target with the *proxy* as receiver, so the accessor
        // brand-checks the proxy and refuses it. Reading a `size` served from an own data property never
        // reached the getter and answered 3 instead.
        Invoking(() => new Engine().Evaluate($"new Proxy({create}, {{}}).size;"))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage($"Method {kind}.prototype.get size called on incompatible receiver");
    }

    [TestCase("Map")]
    [TestCase("Set")]
    public void ThePrototypeCarriesTheAccessorItself(string kind)
    {
        new Engine().Evaluate($$"""
            var d = Object.getOwnPropertyDescriptor({{kind}}.prototype, 'size');
            typeof d.get + '/' + (d.set === undefined) + '/' + d.enumerable + '/' + d.configurable
                + '/' + d.get.name + '/' + d.get.length;
            """).AsString().Should().Be("function/true/false/true/get size/0");
    }
}

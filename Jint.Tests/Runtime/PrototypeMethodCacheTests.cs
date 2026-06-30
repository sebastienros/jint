using System.Reflection;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Guards the prototype-method inline cache in <c>JintMemberExpression</c> (resolves <c>obj.method</c>
/// when <c>method</c> lives on the receiver's direct prototype). The cache bypasses the receiver's
/// <c>[[Get]]</c> on a hit, so it must stay consistent with ordinary lookup across the mutations that
/// invalidate it.
/// </summary>
public class PrototypeMethodCacheTests
{
    /// <summary>
    /// Tripwire: the cache short-circuits a receiver's <c>[[Get]]</c> on a hit, so it is only sound for
    /// objects whose <c>[[Get]]</c> is <em>ordinary for string-keyed lookups that miss the own property</em>
    /// — i.e. "no own property ⇒ walk the prototype". Every <see cref="ObjectInstance"/> subclass overriding
    /// <c>Get(JsValue, JsValue)</c> must therefore be one of:
    /// <list type="bullet">
    /// <item>non-ordinary for named keys — must set <c>InternalTypes.ExoticGet</c> in its constructor so the
    /// cache skips it as both receiver and prototype (Proxy traps, TypedArray canonical indices,
    /// IteratorResult inline value/done, interop member resolution, module exports); or</item>
    /// <item>non-ordinary only for keys it also reports from <c>GetOwnProperty</c> (array integer indices and
    /// <c>length</c>) — safe <em>without</em> the flag, because the populate path defers to the full
    /// <c>Get</c> whenever <c>GetOwnProperty</c> is non-undefined. <see cref="ArrayInstance"/> is the one
    /// such case, and must stay unflagged so <c>arr.push</c> / <c>arr.pop</c> stay cacheable.</item>
    /// </list>
    /// When a new override appears this test fails, forcing the author to classify it rather than silently
    /// regressing (or mis-caching) prototype-method reads.
    /// </summary>
    [Fact]
    public void EveryGetOverrideIsClassifiedForThePrototypeMethodCache()
    {
        var assembly = typeof(Engine).Assembly;
        var getParameters = new[] { typeof(JsValue), typeof(JsValue) };

        var overriders = assembly.GetTypes()
            .Where(t => typeof(ObjectInstance).IsAssignableFrom(t) && t != typeof(ObjectInstance))
            .Where(t => t.GetMethod(
                "Get",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null,
                getParameters,
                modifiers: null) is not null)
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // Non-ordinary for named keys — each sets InternalTypes.ExoticGet in its constructor
        // (ArrayLikeWrapper inherits it from the ObjectWrapper base constructor).
        var exoticGet = new[]
        {
            "Jint.Native.Iterator.IteratorResult",
            "Jint.Native.JsProxy",
            "Jint.Native.JsTypedArray",
            "Jint.Runtime.Interop.ArrayLikeWrapper",
            "Jint.Runtime.Interop.NamespaceReference",
            "Jint.Runtime.Interop.ObjectWrapper",
            "Jint.Runtime.Modules.ModuleNamespace",
        };

        // Index/length-only exotic — ordinary for named keys, safe (and intentionally) unflagged.
        var safeWithoutFlag = new[]
        {
            "Jint.Native.Array.ArrayInstance",
        };

        var expected = exoticGet.Concat(safeWithoutFlag).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, overriders);
    }

    [Fact]
    public void ResolvesPrototypeMethodRepeatedly()
    {
        const string script = """
            function C() { this.v = 0; }
            C.prototype.inc = function () { return ++this.v; };
            var c = new C(), last = 0;
            for (var i = 0; i < 50; i++) { last = c.inc(); }
            last;
            """;
        Assert.Equal(50, new Engine().Evaluate(script).AsNumber());
    }

    [Fact]
    public void OwnPropertyAddedAfterCachingShadowsPrototype()
    {
        // First reads hit the prototype method and populate the cache; assigning an own property of the
        // same name must shadow it on every subsequent read (the receiver version bump invalidates).
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'proto'; };
            var c = new C(), out = [];
            for (var i = 0; i < 5; i++) {
                if (i === 3) { c.tag = function () { return 'own'; }; }
                out.push(c.tag());
            }
            out.join(',');
            """;
        Assert.Equal("proto,proto,proto,own,own", new Engine().Evaluate(script).AsString());
    }

    [Fact]
    public void PrototypeMethodRedefinedAfterCachingIsReResolved()
    {
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'v1'; };
            var c = new C(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { C.prototype.tag = function () { return 'v2'; }; }
                out.push(c.tag());
            }
            out.join(',');
            """;
        Assert.Equal("v1,v1,v2,v2", new Engine().Evaluate(script).AsString());
    }

    [Fact]
    public void PrototypeMethodDeletedAfterCachingFallsBack()
    {
        const string script = """
            function C() {}
            C.prototype.tag = function () { return 'v1'; };
            var c = new C(), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { delete C.prototype.tag; }
                out.push(typeof c.tag === 'function' ? c.tag() : 'gone');
            }
            out.join(',');
            """;
        Assert.Equal("v1,v1,gone,gone", new Engine().Evaluate(script).AsString());
    }

    [Fact]
    public void PrototypeReassignedAfterCachingIsHonoured()
    {
        const string script = """
            var protoA = { tag: function () { return 'A'; } };
            var protoB = { tag: function () { return 'B'; } };
            var o = Object.create(protoA), out = [];
            for (var i = 0; i < 4; i++) {
                if (i === 2) { Object.setPrototypeOf(o, protoB); }
                out.push(o.tag());
            }
            out.join(',');
            """;
        Assert.Equal("A,A,B,B", new Engine().Evaluate(script).AsString());
    }

    [Fact]
    public void PrototypeGetterIsReInvokedEachRead()
    {
        // An accessor on the prototype must run its getter on every read, not be frozen as a value.
        const string script = """
            var n = 0;
            var proto = {};
            Object.defineProperty(proto, 'next', { get: function () { return ++n; } });
            var o = Object.create(proto), out = [];
            for (var i = 0; i < 4; i++) { out.push(o.next); }
            out.join(',');
            """;
        Assert.Equal("1,2,3,4", new Engine().Evaluate(script).AsString());
    }
}

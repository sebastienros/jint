#nullable enable

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The lane a collection reaches by declaring <c>PristineLengthGetter</c>: a <c>length</c> read answered from
/// the host's own count, with the prototype accessor never invoked. Everything asserted here is a
/// <em>semantic</em> claim — that declaring the hook changes no answer a script can observe — because the only
/// thing the lane may buy is speed. The three ways a page can make the accessor answer something else are the
/// three the web-platform-tests define (<c>NodeList-static-length-getter-tampered-{1,2,3}</c>), and each has a
/// test below; the fourth shape, a <c>Proxy</c> in front of the collection, is here because the lane is
/// keyed on the receiver's runtime type and a proxy is a different object.
/// </summary>
public class HostArrayLikeInheritedLengthTests
{
    /// <summary>
    /// The WebIDL collection shape: <c>length</c> is an accessor on the interface prototype, and the host
    /// declares that accessor as the one whose answer is <c>Length</c>. Restricted to the public
    /// surface like every host type in this project — this assembly has no <c>InternalsVisibleTo</c>, which is
    /// the whole reason a test here means anything.
    /// </summary>
    private sealed class CollectionList : ArrayLikeObject
    {
        private readonly List<string> _items;
        private readonly ObjectInstance? _lengthGetter;

        public CollectionList(Engine engine, ObjectInstance prototype, ObjectInstance? lengthGetter, params string[] items)
            : base(engine)
        {
            _items = new List<string>(items);
            _lengthGetter = lengthGetter;
            Prototype = prototype;
        }

        public override uint Length => (uint) _items.Count;

        protected override bool OwnsLength => false;

        protected override ObjectInstance? PristineLengthGetter => _lengthGetter;

        public void Add(string item) => _items.Add(item);

        public void RemoveLast() => _items.RemoveAt(_items.Count - 1);

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Count)
            {
                value = _items[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }

    /// <summary>
    /// Builds the WebIDL arrangement the way a real host does: the prototype declares the accessor first, and
    /// the collection is handed the getter <em>that prototype was created with</em>. Nothing else may be
    /// passed — the whole guard is that the accessor still resolving for <c>length</c> is that object.
    /// </summary>
    private static Engine CreateEngine(out CollectionList list, params string[] items)
    {
        var engine = new Engine();

        // The accessor's body has to be able to answer the real count for the tests that force it to run;
        // a real host's accessor reads its native state directly, and this stands in for that.
        CollectionList? collection = null;
        engine.SetValue("__count", new Func<double>(() => collection!.Length));

        var prototype = engine.Evaluate("""
            (function () {
                var proto = Object.create(Array.prototype);
                Object.defineProperty(proto, 'length', {
                    configurable: true,
                    enumerable: true,
                    get: function () { return __count(); }
                });
                return proto;
            })()
            """).AsObject();

        var lengthGetter = prototype.GetOwnProperty("length").Get!.AsObject();

        collection = new CollectionList(engine, prototype, lengthGetter, items);
        list = collection;

        engine.SetValue("list", list);
        engine.SetValue("proto", prototype);
        return engine;
    }

    [Test]
    public void TheDeclaredAccessorIsWhatEveryLengthConsumerAnswers()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list.hasOwnProperty('length')").Should().Be(false);
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(2);
        engine.Evaluate("[...list].join(',')").Should().Be("a,b,c");
        engine.Evaluate("JSON.stringify(list)").Should().Be("""["a","b","c"]""");

        var loop = "var n = 0; for (var i = 0; i < list.length; i++) { n += list[i].length; } n;";
        engine.Evaluate(loop).Should().Be(3);
    }

    [Test]
    public void TheCollectionIsStillReadLiveBetweenAndDuringReads()
    {
        var engine = CreateEngine(out var list, "a", "b");

        engine.Evaluate("list.length").Should().Be(2);
        list.Add("c");
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(2);

        list.RemoveLast();
        list.RemoveLast();
        engine.Evaluate("list.length").Should().Be(1);
        engine.Evaluate("[...list].join(',')").Should().Be("a");

        // The loop test is re-read every iteration, so a collection that grows underneath one is followed.
        engine.SetValue("grow", new Action(() => list.Add("x")));
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; if (n < 4) grow(); } n;")
            .Should().Be(4);
    }

    // -------------------------------------------------------------------------------------------------
    // The three tamper shapes, which are the three web-platform-tests documents
    // -------------------------------------------------------------------------------------------------

    [Test]
    public void AnOwnLengthOnTheInstanceWins()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Evaluate("list.length").Should().Be(3);
        engine.Execute("Object.defineProperty(list, 'length', { configurable: true, get: function () { return 1; } })");

        engine.Evaluate("list.length").Should().Be(1);
        engine.Evaluate("list.hasOwnProperty('length')").Should().Be(true);
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(-1);
        engine.Evaluate("[...list].join(',')").Should().Be("a");
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(1);

        // and deleting it hands the collection back to its prototype accessor
        engine.Evaluate("delete list.length").Should().Be(true);
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(3);
    }

    [Test]
    public void RePointingThePrototypeWins()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Evaluate("list.length").Should().Be(3);
        engine.Execute("Object.setPrototypeOf(list, { get length() { return 1; } })");

        engine.Evaluate("list.length").Should().Be(1);
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(-1);
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(1);

        engine.Execute("Object.setPrototypeOf(list, proto)");
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(3);
    }

    [Test]
    public void RedefiningTheAccessorOnThePrototypeWins()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Evaluate("list.length").Should().Be(3);
        engine.Execute("Object.defineProperty(proto, 'length', { configurable: true, get: function () { return 1; } })");

        engine.Evaluate("list.length").Should().Be(1);
        engine.Evaluate("Array.prototype.indexOf.call(list, 'c')").Should().Be(-1);
        engine.Evaluate("[...list].join(',')").Should().Be("a");
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(1);
    }

    [Test]
    public void ADifferentAccessorIsNeverMistakenForTheDeclaredOneEvenAfterItIsRestored()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Evaluate("list.length").Should().Be(3);

        // Replace the accessor with a function that answers the real count, then restore a *new* function
        // that also answers the real count. Neither is the declared object, so both must be invoked — which
        // is observable because each counts its own calls.
        engine.Execute("""
            var calls = 0;
            Object.defineProperty(proto, 'length', {
                configurable: true,
                get: function () { return calls++, __count(); }
            });
            """);

        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("calls").Should().Be(2);

        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(3);
        engine.Evaluate("calls > 2").Should().Be(true);
    }

    [Test]
    public void AProxyInFrontOfTheCollectionRunsItsTraps()
    {
        var engine = CreateEngine(out _, "a", "b", "c");

        engine.Execute("""
            var reads = [];
            var p = new Proxy(list, {
                get: function (target, key, receiver) {
                    reads.push(String(key));
                    return key === 'length' ? 2 : Reflect.get(target, key, receiver);
                }
            });
            """);

        engine.Evaluate("p.length").Should().Be(2);
        engine.Evaluate("var n = 0; for (var i = 0; i < p.length; i++) { n++; } n;").Should().Be(2);
        engine.Evaluate("Array.prototype.indexOf.call(p, 'c')").Should().Be(-1);
        engine.Evaluate("reads.indexOf('length') >= 0").Should().Be(true);
    }

    [Test]
    public void AHostThatDeclaresNoAccessorKeepsEveryReadOnTheOrdinaryPath()
    {
        // The default PristineLengthGetter is null, which is what every collection written before the hook
        // existed answers: the accessor runs on every read, so a page can count the calls.
        var engine = new Engine();
        var prototype = engine.Evaluate("""
            (function () {
                var proto = Object.create(Array.prototype);
                globalThis.calls = 0;
                Object.defineProperty(proto, 'length', {
                    configurable: true,
                    get: function () { return calls++, 3; }
                });
                return proto;
            })()
            """).AsObject();

        var list = new CollectionList(engine, prototype, lengthGetter: null, "a", "b", "c");
        engine.SetValue("list", list);

        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("calls").Should().Be(2);
    }

    [Test]
    public void AnOwnedLengthIsUnchangedByAnyOfThis()
    {
        // The default: `length` is an own, non-writable, non-configurable-to-redefine property answered from
        // Length. Neither assignment nor defineProperty can move it, and the prototype is never consulted.
        var engine = new Engine();
        var list = new OwnedLengthList(engine, "a", "b", "c");
        engine.SetValue("list", list);

        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("list.hasOwnProperty('length')").Should().Be(true);
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyDescriptor(list, 'length'))")
            .Should().Be("""{"value":3,"writable":false,"enumerable":false,"configurable":true}""");

        Caught.Exception(() => engine.Execute("'use strict'; list.length = 9;")).Should().BeOfType<JavaScriptException>();
        engine.Evaluate("list.length").Should().Be(3);

        engine.Evaluate("Reflect.defineProperty(list, 'length', { value: 9 })").Should().Be(false);
        engine.Evaluate("list.length").Should().Be(3);
        engine.Evaluate("var n = 0; for (var i = 0; i < list.length; i++) { n++; } n;").Should().Be(3);
    }

    private sealed class OwnedLengthList : ArrayLikeObject
    {
        private readonly string[] _items;

        public OwnedLengthList(Engine engine, params string[] items) : base(engine)
        {
            _items = items;
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }

        public override uint Length => (uint) _items.Length;

        public override bool TryGetIndex(uint index, out JsValue value)
        {
            if (index < (uint) _items.Length)
            {
                value = _items[index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }
    }
}

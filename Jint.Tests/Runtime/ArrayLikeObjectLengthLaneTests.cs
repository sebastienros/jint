#nullable enable

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// Witness for the inherited-<c>length</c> lane: <em>which</em> read answered. The observable behaviour is
/// pinned from the public surface in
/// <c>Jint.Tests.PublicInterface/HostArrayLikeInheritedLengthTests.cs</c> — and every one of those tests would
/// still pass if the lane never engaged at all, because a lane that is semantically invisible is invisible to
/// a test too. This project has <c>InternalsVisibleTo</c>, so it can ask the object directly.
/// </summary>
public class ArrayLikeObjectLengthLaneTests
{
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

    private static Engine Create(out CollectionList list, bool declareGetter = true)
    {
        var engine = new Engine();
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

        var getter = declareGetter ? prototype.GetOwnProperty("length").Get!.AsObject() : null;
        collection = new CollectionList(engine, prototype, getter, "a", "b", "c");
        list = collection;
        engine.SetValue("list", list);
        engine.SetValue("proto", prototype);
        return engine;
    }

    [Test]
    public void AnOwnedLengthNeedsNoGuardAtAll()
    {
        var engine = new Engine();
        var list = new OwnedLengthList(engine, "a", "b", "c");

        // OwnsLength is the default, so `length` is an own data property answered from Length before the
        // property bag or the prototype chain is consulted: fast and slow are the same read by construction.
        list.TryReadLength(out var length).Should().Be(true);
        length.Should().Be(3u);
    }

    [Test]
    public void TheLaneEngagesOnlyWhenTheHostDeclaresTheAccessor()
    {
        var undeclared = Create(out var withoutGetter, declareGetter: false);
        undeclared.Evaluate("list.length").Should().Be(3);
        withoutGetter.TryReadLength(out _).Should().Be(false);

        Create(out var withGetter);
        withGetter.TryReadLength(out var length).Should().Be(true);
        length.Should().Be(3u);
    }

    [Test]
    public void EachOfTheThreeTamperShapesDisarmsTheLaneAndRestoringItRearms()
    {
        // 1. an own `length` on the instance — NodeList-static-length-getter-tampered-1
        var engine = Create(out var list);
        list.TryReadLength(out _).Should().Be(true);
        engine.Execute("Object.defineProperty(list, 'length', { configurable: true, get: function () { return 1; } })");
        list.TryReadLength(out _).Should().Be(false);
        engine.Execute("delete list.length");
        list.TryReadLength(out _).Should().Be(true);

        // 2. the prototype re-pointed — NodeList-static-length-getter-tampered-2
        engine.Execute("Object.setPrototypeOf(list, { get length() { return 1; } })");
        list.TryReadLength(out _).Should().Be(false);
        engine.Execute("Object.setPrototypeOf(list, proto)");
        list.TryReadLength(out _).Should().Be(true);

        // 3. the accessor redefined on the prototype — NodeList-static-length-getter-tampered-3. This one
        // never re-arms: the declared getter is the object that prototype was created with, and a redefinition
        // replaces it for good, even by a function that computes the same answer.
        engine.Execute("Object.defineProperty(proto, 'length', { configurable: true, get: function () { return __count(); } })");
        list.TryReadLength(out _).Should().Be(false);
        engine.Evaluate("list.length").Should().Be(3);
        list.TryReadLength(out _).Should().Be(false);
    }

    [Test]
    public void TheVerdictSurvivesAsACacheInBothDirections()
    {
        var engine = Create(out var list);

        // Positive verdict, repeated: still the same answer, and the collection is still read live.
        list.TryReadLength(out var first).Should().Be(true);
        list.TryReadLength(out var second).Should().Be(true);
        first.Should().Be(3u);
        second.Should().Be(3u);

        // Negative verdict, repeated: a tampered collection must not re-walk its prototype chain per read,
        // and must keep declining until something the guard watches actually moves.
        engine.Execute("Object.defineProperty(proto, 'length', { configurable: true, get: function () { return 1; } })");
        list.TryReadLength(out _).Should().Be(false);
        list.TryReadLength(out _).Should().Be(false);
        engine.Evaluate("list.length").Should().Be(1);
    }

    [Test]
    public void AnExoticPrototypeIsRefused()
    {
        var engine = Create(out var list);
        list.TryReadLength(out _).Should().Be(true);

        // A Proxy resolves `length` through its own [[Get]] trap, which this lane must never bypass — even
        // when the trap forwards to the very accessor the host declared.
        engine.Execute("Object.setPrototypeOf(list, new Proxy(proto, {}))");
        list.TryReadLength(out _).Should().Be(false);
        engine.Evaluate("list.length").Should().Be(3);
    }
}

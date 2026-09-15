#nullable enable

using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>[[Get]]</c>, <c>[[Set]]</c> and <c>[[HasProperty]]</c> walk the prototype chain in a loop rather than
/// by recursing into each link, because a chain is built by script and its depth is an input
/// (sebastienros/jint#4076, and <c>ObjectInstance.GetFromPrototypeChain</c> for the mechanism). A loop in the
/// base class can only run the <em>ordinary</em> algorithm, so the whole correctness of it is in when it
/// declines to walk a link and hands the rest of the operation over instead. These are the two halves of
/// that: the classification the walks key on, and the behaviour it buys.
/// </summary>
public class PrototypeChainWalkTests
{
    /// <summary>
    /// The <c>[[Set]]</c> and <c>[[HasProperty]]</c> walks read <c>InternalTypes.PlainObject</c> as
    /// "this object does not override a property internal method" — the flag's own documented meaning — and
    /// walk such a link themselves instead of calling its virtual. So an object that carried the flag
    /// <em>and</em> overrode one of the three would have that override silently skipped by every other
    /// object's walk: a wrong answer, not a slow one.
    /// <para>
    /// Nothing in the type system says that, so it is asserted over the objects a built engine can actually
    /// reach — every own property value and every prototype, transitively, from the global object and from a
    /// sample of every exotic shape script can construct. The direction that is safe needs no check: a type
    /// that does <em>not</em> carry the flag is merely handed over to, exactly as before.
    /// </para>
    /// </summary>
    [Test]
    public void NoObjectCarryingPlainObjectOverridesAPropertyInternalMethod()
    {
        using var engine = new Engine(options => options.AllowClr(typeof(List<>).Assembly));

        var samples = engine.Evaluate("""
            [
              {}, Object.create(null), [1, 2], new Date(), new Error('e'), /r/g, new Map(), new Set(),
              new WeakMap(), new WeakSet(), new Int8Array(2), new DataView(new ArrayBuffer(2)),
              new Proxy({}, {}), (function () { return arguments; })(1), function f() {}, (class C {}),
              new String('s'), new Number(1), new Boolean(true), Promise.resolve(1),
              (function* g() { yield 1; })(), new (class D extends Array {})(),
              Object.getOwnPropertyDescriptor(Map.prototype, 'size'), System.String, new System.Text.StringBuilder()
            ]
            """).AsObject();

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reached in Reachable(engine.Realm.GlobalObject, samples))
        {
            if ((reached._type & InternalTypes.PlainObject) == InternalTypes.Empty)
            {
                continue;
            }

            var overridden = OverriddenInternalMethod(reached.GetType());
            if (overridden is not null)
            {
                offenders.Add(overridden);
            }
        }

        offenders.Should().BeEmpty(
            "a type carrying InternalTypes.PlainObject promises it does not override Get, Set or HasProperty — "
            + "another object's chain walk resolves such a link itself and would skip the override");
    }

    /// <summary>
    /// The other half: a link the walk declines really does get to run its own algorithm. A Proxy is the
    /// sharpest statement of it, because its answer is observable — a trap that does not fire produced no
    /// log entry, and the value the read, the write and the <c>in</c> settle on could only have come from
    /// the trap. Two ordinary links sit above it so the operation is genuinely mid-walk when it hands over.
    /// </summary>
    [Test]
    public void AnOverrideMidChainStillRunsItsOwnAlgorithm()
    {
        using var engine = new Engine();

        var outcome = engine.Evaluate("""
            var log = [];
            var mid = new Proxy({}, {
              get: function (target, key, receiver) { log.push('get:' + key); return key === 'viaTrap' ? 'trapped' : undefined; },
              set: function (target, key, value, receiver) { log.push('set:' + key); return true; },
              has: function (target, key) { log.push('has:' + key); return key === 'present'; }
            });
            var leaf = Object.create(Object.create(mid));

            var read = leaf.viaTrap;
            leaf.written = 1;
            var has = ('present' in leaf) + '/' + ('absent' in leaf);

            // read through Object.prototype, not through leaf: a get trap answers for every name on the
            // chain, hasOwnProperty included, and asking leaf for it would only log the trap again
            read + '|' + has + '|' + Object.prototype.hasOwnProperty.call(leaf, 'written') + '|' + log.join(',');
            """).AsString();

        outcome.Should().Be("trapped|true/false|false|get:viaTrap,set:written,has:present,has:absent");
    }

    /// <summary>
    /// The same thing for an in-box exotic that is not a Proxy: an array mid-chain answers <c>length</c> from
    /// its own <c>[[Get]]</c> and an index from its own <c>[[HasProperty]]</c>, neither of which an ordinary
    /// probe of its property storage would have produced on its own.
    /// </summary>
    [Test]
    public void AnArrayMidChainStillAnswersForItsOwnIndices()
    {
        using var engine = new Engine();

        engine.Evaluate("""
            var arr = [10, 20, 30];
            var leaf = Object.create(Object.create(arr));
            leaf.length + '|' + ('2' in leaf) + '|' + ('3' in leaf) + '|' + leaf[1];
            """).AsString().Should().Be("3|true|false|20");
    }

    /// <summary>
    /// Every object reachable from <paramref name="roots"/> by own property values and prototypes. Accessors
    /// are not invoked — a census must not run script — but a lazily-materialized data property is read,
    /// which is what makes the walk see the built-ins rather than their unmaterialized slots.
    /// </summary>
    private static IEnumerable<ObjectInstance> Reachable(params ObjectInstance[] roots)
    {
        var seen = new HashSet<ObjectInstance>(ReferenceComparer.Instance);
        var pending = new Stack<ObjectInstance>(roots);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            yield return current;

            if (current.Prototype is { } prototype)
            {
                pending.Push(prototype);
            }

            foreach (var key in current.GetOwnPropertyKeys())
            {
                var descriptor = current.GetOwnProperty(key);
                if (descriptor == PropertyDescriptor.Undefined || descriptor.IsAccessorDescriptor())
                {
                    continue;
                }

                if (descriptor.Value is ObjectInstance value)
                {
                    pending.Push(value);
                }
            }
        }
    }

    /// <summary>
    /// The first of the three property internal methods <paramref name="type"/> overrides below
    /// <see cref="ObjectInstance"/>, or <see langword="null"/> if it overrides none.
    /// </summary>
    private static string? OverriddenInternalMethod(Type type)
    {
        (string Name, Type[] Parameters)[] methods =
        [
            (nameof(ObjectInstance.Get), [typeof(JsValue), typeof(JsValue)]),
            (nameof(ObjectInstance.Set), [typeof(JsValue), typeof(JsValue), typeof(JsValue)]),
            (nameof(ObjectInstance.HasProperty), [typeof(JsValue)]),
        ];

        foreach (var (name, parameters) in methods)
        {
            var method = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: parameters,
                modifiers: null);

            if (method?.DeclaringType is { } declaring && declaring != typeof(ObjectInstance) && declaring != typeof(JsValue))
            {
                return declaring.Name + "." + name;
            }
        }

        return null;
    }

    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

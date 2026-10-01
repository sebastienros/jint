#nullable enable

using System.Globalization;
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

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reached in Census(engine))
        {
            if ((reached._type & InternalTypes.PlainObject) == InternalTypes.Empty)
            {
                continue;
            }

            var overridden = FirstOverridden(reached.GetType(), WalkMethods);
            if (overridden is not null)
            {
                offenders.Add(overridden);
            }
        }

        string.Join(", ", offenders).Should().BeEmpty(
            "a type carrying InternalTypes.PlainObject promises it does not override Get, Set or HasProperty — "
            + "another object's chain walk resolves such a link itself and would skip the override");
    }

    /// <summary>
    /// The converse, and the direction that was missing when a shaped host prototype
    /// (<c>SharedShapeObject</c>) shipped without the flag: it overrides nothing at all, so every
    /// <c>[[Set]]</c> and <c>'x' in o</c> reaching it declined to walk it, handed the rest of the chain back
    /// to the recursive path <em>and</em> paid the new flag test at every level — the old recursion plus the
    /// new overhead, which a paired benchmark caught as a regression on shaped prototypes alone.
    /// <para>
    /// A missing flag is only ever a slow answer, never a wrong one, which is why it cannot be caught by
    /// any correctness test and needs a census instead. The set of methods is wider than the walk's three,
    /// because the flag is a <em>storage</em> claim as well: the lanes in <c>ObjectInstance.Get</c>,
    /// <c>Set</c> and <c>CreateDataProperty</c> read <c>_properties</c> (or the shape) without asking
    /// <c>GetOwnProperty</c>, so a type that projects its own properties from anywhere else must not take
    /// the flag however ordinary its <c>[[Get]]</c> is. Overriding <c>SetOwnProperty</c> or
    /// <c>DefineOwnProperty</c> is compatible with it and deliberately absent from the list —
    /// <c>ObjectPrototype</c> carries the flag and overrides both.
    /// </para>
    /// </summary>
    [Test]
    public void NoObjectOverridingNoPropertyInternalMethodLacksPlainObject()
    {
        using var engine = new Engine(options => options.AllowClr(typeof(List<>).Assembly));

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reached in Census(engine))
        {
            var type = reached.GetType();
            if ((reached._type & InternalTypes.PlainObject) != InternalTypes.Empty
                || FirstOverridden(type, StorageMethods) is not null
                || EligibleButUnflagged.Contains(type.Name))
            {
                continue;
            }

            offenders.Add(type.Name);
        }

        string.Join(", ", offenders).Should().BeEmpty(
            "a type that overrides none of the property internal methods is entitled to be walked, and every "
            + "[[Set]] and [[HasProperty]] that meets it without InternalTypes.PlainObject hands the rest of "
            + "the chain back to the recursive path instead — give it the flag, or add it to "
            + nameof(EligibleButUnflagged) + " with the reason");
    }

    /// <summary>
    /// The exceptions: in-box types that override none of the named methods, would be walked correctly with
    /// <c>InternalTypes.PlainObject</c>, and do not carry it. Each is a deliberate omission rather than a
    /// defect, for one reason that applies to all of them.
    /// <para>
    /// The flag is two claims in one. "Overrides no property internal method" is what the walks read. "Own
    /// string-keyed properties live in the base storage" is what <c>ObjectInstance.Get</c>, <c>Set</c> and
    /// <c>CreateDataProperty</c> read, and taking the flag switches those three fast lanes on as well —
    /// which is a change to measure on its own terms, not part of the shaped-prototype regression this
    /// census was written for (sebastienros/jint#4076). A <c>SharedShapeObject</c> is flagged and absent
    /// from this list precisely because for <em>it</em> the second half is free: all three lanes spell their
    /// test <c>== PlainObject</c> against <c>PlainObject | BuiltinShapeMode</c>, so a shaped object is
    /// excluded from them by construction while its shape is installed, and once a deopt has moved every
    /// slot into <c>_properties</c> it really is the dictionary they assume.
    /// </para>
    /// <para>
    /// A type belongs here only while that is the whole story. Anything whose own properties come from
    /// somewhere else overrides one of the storage methods above and never reaches this list.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> EligibleButUnflagged = new(StringComparer.Ordinal)
    {
        // Built-ins whose string-keyed own properties live in a shared BuiltinShape, like a shaped host
        // prototype — but built through the public ObjectInstance(Engine) constructor, so they derive
        // their access semantics instead of declaring them.
        "AtomicsInstance",
        "GeneratorPrototype",
        "JsonInstance",
        "MathInstance",
        "ReflectInstance",
        "TemporalNow",

        // Ordinary in-box objects: internal slots in fields, own properties in the base storage.
        "BooleanInstance",
        "BooleanPrototype",
        "ErrorPrototype",
        "GeneratorInstance",
        "IntlInstance",
        "JsDataView",
        "JsMap",
        "JsPromise",
        "JsSet",
        "JsWeakMap",
        "JsWeakSet",
        "NumberInstance",
        "TemporalInstance",
    };

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
    /// The shaped-prototype regression itself, stated as behaviour. A chain of
    /// <see cref="JsObjectShape"/> instances is what a Web IDL binding generator hands a host, and every
    /// link of it overrides nothing — so the walk must resolve it rather than hand it over, and must get
    /// the ordinary answers while doing so: an inherited method found at the deepest level, a name nothing
    /// declares refused, a write landing on the receiver and not on the prototype that was walked past, an
    /// inherited setter invoked with the receiver, and an inherited get-only accessor refusing a write.
    /// </summary>
    [Test]
    public void AShapedHostPrototypeChainIsWalkedAndAnswersCorrectly()
    {
        using var engine = new Engine();
        engine.SetValue("chainLeaf", ShapedChain(engine, levels: 3));

        engine.Evaluate("""
            var obj = Object.create(chainLeaf);

            var inHit = ('l0_op' in obj) + '/' + ('l2_CONST' in obj);
            var inMiss = String('__absent__' in obj);
            var ownNone = String(obj.hasOwnProperty('l0_op'));

            obj.l0_op = 'shadowed';
            var write = obj.l0_op + '/' + obj.hasOwnProperty('l0_op') + '/' + (typeof chainLeaf.l0_op);

            obj.l1_sink = 42;
            var setter = obj.seenBySetter + '/' + obj.hasOwnProperty('seenBySetter') + '/' + chainLeaf.hasOwnProperty('seenBySetter');

            var strictWrite;
            try {
              (function () { 'use strict'; obj.l2_attr = 1; })();
              strictWrite = 'no error';
            } catch (error) { strictWrite = error.name; }

            [inHit, inMiss, ownNone, write, setter, strictWrite].join('|');
            """).AsString().Should().Be("true/true|false|false|shadowed/true/function|42/true/false|TypeError");
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
    /// The three the walks run in place of a link's own virtual: overriding one of these while carrying
    /// <c>InternalTypes.PlainObject</c> is the wrong answer the first census rules out.
    /// </summary>
    private static readonly (string Name, Type[] Parameters)[] WalkMethods =
    [
        (nameof(ObjectInstance.Get), [typeof(JsValue), typeof(JsValue)]),
        (nameof(ObjectInstance.Set), [typeof(JsValue), typeof(JsValue), typeof(JsValue)]),
        (nameof(ObjectInstance.HasProperty), [typeof(JsValue)]),
    ];

    /// <summary>
    /// The whole contract: the three above plus the five that answer what this object's own properties
    /// <em>are</em>. A type overriding one of those five projects its own properties from somewhere the
    /// flag's storage lanes do not look, so the second census does not ask it to carry the flag.
    /// </summary>
    private static readonly (string Name, Type[] Parameters)[] StorageMethods =
    [
        .. WalkMethods,
        (nameof(ObjectInstance.GetOwnProperty), [typeof(JsValue)]),
        (nameof(ObjectInstance.GetOwnPropertyKeys), [typeof(Types)]),
        ("ProbeOwnProperty", [typeof(JsValue)]),
        ("TryGetOwnPropertyValue", [typeof(JsValue), typeof(JsValue), typeof(JsValue).MakeByRefType()]),
        ("GetInitialOwnStringPropertyKeys", []),
    ];

    /// <summary>
    /// The first of <paramref name="methods"/> that <paramref name="type"/> overrides below
    /// <see cref="ObjectInstance"/>, or <see langword="null"/> if it overrides none.
    /// </summary>
    private static string? FirstOverridden(Type type, (string Name, Type[] Parameters)[] methods)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var (name, parameters) in methods)
        {
            var method = type.GetMethod(name, Flags, binder: null, types: parameters, modifiers: null);

            if (method?.DeclaringType is { } declaring && declaring != typeof(ObjectInstance) && declaring != typeof(JsValue))
            {
                return declaring.Name + "." + name;
            }
        }

        return null;
    }

    /// <summary>
    /// The objects both censuses run over: everything reachable from a built engine's global object, from a
    /// sample of every exotic shape script can construct, and from a shaped host prototype chain — the shape
    /// a Web IDL binding generator emits, which a host reaches through <see cref="JsObjectShape"/> and which
    /// no engine builds on its own.
    /// </summary>
    private static IEnumerable<ObjectInstance> Census(Engine engine)
    {
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

        return Reachable(engine.Realm.GlobalObject, samples, ShapedChain(engine, levels: 3));
    }

    /// <summary>
    /// A shaped host prototype chain of <paramref name="levels"/> links, built the way a binding generator
    /// would: one process-shared <see cref="JsObjectShape"/> per level, instantiated into this engine on top
    /// of the level below it. Each level is touched once, because the storage representation settles on the
    /// first property access rather than at construction.
    /// </summary>
    private static ObjectInstance ShapedChain(Engine engine, int levels)
    {
        ObjectInstance? previous = null;
        for (var level = 0; level < levels; level++)
        {
            var prefix = "l" + level.ToString(CultureInfo.InvariantCulture) + "_";
            var shape = new JsObjectShape.Builder()
                .Method(prefix + "op", static (_, _) => JsValue.Undefined)
                .Accessor(prefix + "attr", static (_, _) => new JsString("attr"))
                .Accessor(
                    prefix + "sink",
                    static (_, _) => JsValue.Undefined,
                    static (thisObject, arguments) =>
                    {
                        // Proves the receiver semantics through the walk: an inherited setter runs with the
                        // object the write STARTED on, not with the prototype it was found on.
                        _ = ((ObjectInstance) thisObject).Set("seenBySetter", arguments[0]);
                        return JsValue.Undefined;
                    })
                .Constant(prefix + "CONST", new JsString(prefix))
                .Build();

            previous = previous is null ? shape.Instantiate(engine) : shape.Instantiate(engine, previous);
            previous.Get(prefix + "CONST");
        }

        return previous!;
    }

    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

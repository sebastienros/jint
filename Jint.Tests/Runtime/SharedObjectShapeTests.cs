#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.Runtime;

/// <summary>
/// The half of <see cref="JsObjectShape"/>'s contract that only the engine's own assembly can witness: that an
/// instantiated object really does carry the shared <c>BuiltinShape</c> storage, that it fills that storage one
/// member at a time, and — the critical one — that a process-held shape retains no engine.
/// <para>
/// The script-visible behaviour is pinned from outside the assembly instead, in
/// <c>Jint.Tests.PublicInterface.HostPrototypeShapeTests</c>, where a green test also proves reachability.
/// </para>
/// </summary>
// The GC test measures collectability, which nothing else running concurrently may perturb.
[NonParallelizable]
public class SharedObjectShapeTests
{
    private static readonly JsObjectShape MethodsOnlyShape = new JsObjectShape.Builder()
        .Method("a", static (t, args) => new JsString("a"))
        .Method("b", static (t, args) => new JsString("b"))
        .Method("c", static (t, args) => new JsString("c"))
        .Build();

    private static readonly JsObjectShape MixedShape = new JsObjectShape.Builder()
        .Method("m", static (t, args) => new JsString("m"))
        .Accessor("acc", getter: static (t, args) => new JsString("acc"))
        .Constant("K", JsNumber.Create(7))
        .PerRealmSlot("constructor", enumerable: false)
        .ToStringTag("Mixed")
        .Build();

    [Test]
    public void InstantiateAllocatesNoPerRealmStorageUntilTheObjectIsTouched()
    {
        var engine = new Engine();
        var proto = (IBuiltinShaped) MethodsOnlyShape.Instantiate(engine);

        proto.BuiltinDescriptors.Should().BeNull("instantiation must allocate nothing but the object itself");

        // Any property access initializes the object; the descriptor array appears, still entirely empty
        // because every slot of this shape is a lazily-materialized function.
        ((ObjectInstance) proto).Get("a");

        var descriptors = proto.BuiltinDescriptors;
        descriptors.Should().NotBeNull();
        descriptors!.Length.Should().Be(3);
        descriptors[0].Should().NotBeNull("the touched member is the one that materialized");
        descriptors[1].Should().BeNull();
        descriptors[2].Should().BeNull();

        ((ObjectInstance) proto).Get("c");
        proto.BuiltinDescriptors![1].Should().BeNull("an untouched member stays unmaterialized");
        proto.BuiltinDescriptors![2].Should().NotBeNull();
    }

    [Test]
    public void InitializationPreWiresOnlyConstantsAndPerRealmValueSlots()
    {
        var engine = new Engine();
        var proto = MixedShape.Instantiate(engine);

        // Touch a symbol so initialization runs without materializing any string-keyed member.
        proto.Get(GlobalSymbolRegistryToStringTag(engine));

        var descriptors = ((IBuiltinShaped) proto).BuiltinDescriptors;
        descriptors.Should().NotBeNull();
        descriptors![0].Should().BeNull("the method materializes on first access");
        descriptors[1].Should().BeNull("the accessor materializes on first access");
        descriptors[2].Should().NotBeNull("a constant points at the shape's shared descriptor from the start");
        descriptors[3].Should().NotBeNull("a value-form per-realm slot needs a live descriptor to be fillable");

        descriptors[3]!.Value.Should().Be(JsValue.Undefined);
    }

    [Test]
    public void EveryEngineGetsItsOwnDescriptorsWhileTheShapeStaysUntouched()
    {
        var engineA = new Engine();
        var protoA = MixedShape.Instantiate(engineA);
        var engineB = new Engine();
        var protoB = MixedShape.Instantiate(engineB);

        var constantBefore = protoA.GetOwnProperty("K");

        protoA.Get("m").Should().NotBeNull();
        protoB.Get("m").Should().NotBeNull();

        var descriptorsA = ((IBuiltinShaped) protoA).BuiltinDescriptors!;
        var descriptorsB = ((IBuiltinShaped) protoB).BuiltinDescriptors!;

        ReferenceEquals(descriptorsA, descriptorsB).Should().BeFalse();
        ReferenceEquals(descriptorsA[0], descriptorsB[0]).Should().BeFalse("materialized members are engine-affine");
        ReferenceEquals(descriptorsA[2], descriptorsB[2]).Should().BeTrue("a constant's descriptor is the shared one");

        // Mutating a materialized member and falling back to the dictionary in A leaves B — and the shared
        // shape both were built from — exactly as they were.
        engineA.SetValue("proto", protoA);
        engineA.Execute("proto.m = 1; delete proto.acc;");

        protoB.Get("m").Should().NotBe(JsValue.Undefined);
        engineB.SetValue("proto", protoB);
        engineB.Evaluate("typeof proto.m").Should().Be("function");
        engineB.Evaluate("proto.acc").Should().Be("acc");
        engineB.Diagnostics.GetObjectRepresentation(protoB).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

        // A third engine, instantiated after all that, is indistinguishable from the first.
        var engineC = new Engine();
        var protoC = MixedShape.Instantiate(engineC);
        engineC.SetValue("proto", protoC);
        engineC.Evaluate("typeof proto.m").Should().Be("function");
        engineC.Evaluate("proto.K").Should().Be(7);
        ReferenceEquals(protoC.GetOwnProperty("K"), constantBefore).Should().BeTrue("the shared constant descriptor is never replaced");
        constantBefore.Value.Should().Be(7);
        constantBefore.Writable.Should().BeFalse();
        constantBefore.Configurable.Should().BeFalse();
    }

    [Test]
    public void AskingWhetherAMemberExistsDoesNotCreateIt()
    {
        // The point of the probe lane: a page walking a Web IDL prototype (for-in, Object.keys,
        // hasOwnProperty) asks only about names and enumerability, and used to pay one function object per
        // member to read one flag off the descriptor it hung on.
        var shape = new JsObjectShape.Builder()
            .Method("touched", static (t, args) => new JsString("touched"))
            .Method("enumerableUntouched", static (t, args) => new JsString("e"))
            .Method("hiddenUntouched", static (t, args) => new JsString("h"), enumerable: false)
            .Accessor("accessorUntouched", getter: static (t, args) => new JsString("a"))
            .Constant("K", JsNumber.Create(1))
            .Build();

        var engine = new Engine();
        var proto = shape.Instantiate(engine);
        engine.SetValue("proto", proto);

        // One member is read for its value, which necessarily materializes it.
        engine.Evaluate("proto.touched()").Should().Be("touched");

        // Everything below asks only about names and enumerability.
        engine.Evaluate("Object.keys(proto).join(',')").Should().Be("touched,enumerableUntouched,accessorUntouched,K");
        engine.Evaluate("'hiddenUntouched' in proto").Should().Be(true);
        engine.Evaluate("proto.hasOwnProperty('accessorUntouched')").Should().Be(true);
        engine.Evaluate("proto.propertyIsEnumerable('hiddenUntouched')").Should().Be(false);
        engine.Evaluate("proto.propertyIsEnumerable('accessorUntouched')").Should().Be(true);
        engine.Evaluate("(function () { var k = []; for (var n in proto) { k.push(n); } return k.join(','); })()")
            .Should().Be("touched,enumerableUntouched,accessorUntouched,K");

        var descriptors = ((IBuiltinShaped) proto).BuiltinDescriptors!;
        descriptors[0].Should().NotBeNull("the member whose value was read had to materialize");
        descriptors[4].Should().NotBeNull("a constant points at the shape's shared descriptor from the start");

        if (HostContractVerificationSwitch.Enabled)
        {
            // Host-contract verification cross-checks every probe against GetOwnProperty, which materializes the
            // slot — the checker doing its job, and the reason the no-materialization figure is an unverified one.
            descriptors[1].Should().NotBeNull();
            descriptors[2].Should().NotBeNull();
            descriptors[3].Should().NotBeNull();
        }
        else
        {
            descriptors[1].Should().BeNull("an enumerable member's name and flag come from the shared layout");
            descriptors[2].Should().BeNull("so do a non-enumerable member's");
            descriptors[3].Should().BeNull("an accessor is not created to answer whether it is enumerable");
        }

        // Reading the values is what creates them, and the answers are unchanged.
        engine.Evaluate("Object.values(proto).length").Should().Be(4);
        ((IBuiltinShaped) proto).BuiltinDescriptors![1].Should().NotBeNull();
        ((IBuiltinShaped) proto).BuiltinDescriptors![3].Should().NotBeNull();
    }

    [Test]
    public void EnumeratingAnIntrinsicNamespaceDoesNotCreateItsFunctions()
    {
        // The same lane, on the built-ins it was already storing this way: Object.keys(Math) has to test
        // every one of Math's ~35 members for enumerability, and used to build every one of their function
        // objects to do it — for a result that is always empty, since built-in members are non-enumerable.
        var engine = new Engine();
        engine.Evaluate("Math.abs(-1)");

        var math = (IBuiltinShaped) engine.Realm.Intrinsics.Math;
        var materializedAfterOneCall = CountMaterialized(math);
        materializedAfterOneCall.Should().Be(1, "only the member that was called");

        engine.Evaluate("Object.keys(Math).length").Should().Be(0);
        engine.Evaluate("JSON.stringify(Math)").Should().Be("{}");
        engine.Evaluate("Math.hasOwnProperty('sqrt')").Should().Be(true);
        engine.Evaluate("Math.propertyIsEnumerable('sqrt')").Should().Be(false);
        engine.Evaluate("'cbrt' in Math").Should().Be(true);
        engine.Evaluate("(function () { var n = 0; for (var k in Math) { n++; } return n; })()").Should().Be(0);

        if (HostContractVerificationSwitch.Enabled)
        {
            CountMaterialized(math).Should().BeGreaterThan(materializedAfterOneCall, "the probe checker materializes what it re-reads");
        }
        else
        {
            CountMaterialized(math).Should().Be(materializedAfterOneCall, "no enumerability question created a function");
        }

        static int CountMaterialized(IBuiltinShaped shaped)
        {
            var descriptors = shaped.BuiltinDescriptors!;
            var count = 0;
            for (var i = 0; i < descriptors.Length; i++)
            {
                if (descriptors[i] is not null && shaped.BuiltinShape.Kinds[i] != BuiltinSlotKind.Constant)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// The probe contract is trusted and never re-verified at runtime, so it is swept here instead: every
    /// object a fresh engine reaches that stores its own properties as a shared layout is asked about each
    /// of its own string keys, and the answer must equal the one its <c>GetOwnProperty</c> gives.
    /// <para>
    /// Each key is probed <em>before</em> the comparison materializes it, so the untouched state is what gets
    /// checked; the whole set is then probed again once materialized. The sweep is what covers the shaped
    /// hosts whose <c>GetOwnProperty</c> is not the base one — a <c>Function</c>-derived constructor resolves
    /// <c>length</c>/<c>name</c>/<c>prototype</c> from its own fields, and <c>Array.prototype</c> resolves
    /// indices from element storage, both ahead of the shared layout.
    /// </para>
    /// </summary>
    [Test]
    public void EveryReachableSharedLayoutObjectProbesConsistentlyWithItsDescriptors()
    {
        var engine = new Engine();

        var roots = new List<ObjectInstance> { engine.Realm.GlobalObject };
        foreach (var source in new[]
                 {
                     "Object.getPrototypeOf(function* () {} ())",
                     "Object.getPrototypeOf(async function* () {} ())",
                     "Math", "JSON", "Reflect", "Atomics", "Temporal.Now",
                 })
        {
            if (engine.Evaluate(source) is ObjectInstance seed)
            {
                roots.Add(seed);
            }
        }

        var visited = new HashSet<ObjectInstance>(ReferenceComparer.Instance);
        var pending = new Queue<ObjectInstance>(roots);
        var shapedChecked = 0;
        var keysChecked = 0;

        while (pending.Count > 0 && visited.Count < 4000)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            // Asking for the keys is what runs a lazily-initialized built-in's Initialize, and therefore
            // what installs the shared layout — so the flag is only meaningful after this call. Collecting
            // keys materializes no slot; reading their values below is what does.
            var keys = current.GetOwnPropertyKeys(Jint.Runtime.Types.String);

            var shaped = (current._type & Jint.Runtime.InternalTypes.BuiltinShapeMode) != Jint.Runtime.InternalTypes.Empty;
            if (shaped)
            {
                shapedChecked++;
                keysChecked += AssertProbesAgree(current);
            }

            // Only data descriptors are followed: invoking a getter would run engine code (and some
            // intrinsic accessors throw by design), which this sweep has no business doing.
            foreach (var key in keys)
            {
                var descriptor = current.GetOwnProperty(key);
                if (descriptor.Value is ObjectInstance child)
                {
                    pending.Enqueue(child);
                }
            }

            if (current.Prototype is { } prototype)
            {
                pending.Enqueue(prototype);
            }

            if (shaped)
            {
                // ...and again now that walking the values above materialized every slot.
                AssertProbesAgree(current);
            }
        }

        // A fresh engine reaches 122 shared-layout objects carrying 1154 own string keys between them, plus
        // the nine tricky names probed on each of those objects — 2252 comparisons — each compared once
        // untouched and once materialized. The floors are deliberately loose: they exist to fail if the sweep
        // ever stops reaching the built-ins, not to track the exact intrinsic count.
        shapedChecked.Should().BeGreaterThan(100, "the sweep must keep reaching the engine's shared-layout built-ins");
        keysChecked.Should().BeGreaterThan(2000);

        static int AssertProbesAgree(ObjectInstance target)
        {
            var checkedKeys = 0;
            foreach (var key in target.GetOwnPropertyKeys(Jint.Runtime.Types.String))
            {
                AssertOneAgrees(target, key);
                checkedKeys++;
            }

            // ...and the other half of the contract: a name the object does NOT declare. The probe answers
            // an absent name from the shared layout index alone, so every shaped type has to be swept for
            // one that would disagree — a subclass resolving string names ahead of the layout, or a slot
            // the layout cannot answer without materializing it. Several of these names ARE declared on
            // some of the objects the sweep reaches (Function.prototype declares length/name, every
            // prototype declares constructor and toString); the assertion is the agreement, not the answer.
            foreach (var name in TrickyProbeNames)
            {
                AssertOneAgrees(target, JsString.Create(name));
                checkedKeys++;
            }

            return checkedKeys;
        }

        static void AssertOneAgrees(ObjectInstance target, JsValue key)
        {
            // Probe FIRST: GetOwnProperty may materialize the slot, which would hide a lane that only
            // answers correctly once a live descriptor exists.
            var probe = target.ProbeOwnProperty(key);
            var descriptor = target.GetOwnProperty(key);

            OwnPropertyProbe expected;
            if (ReferenceEquals(descriptor, Jint.Runtime.Descriptors.PropertyDescriptor.Undefined))
            {
                expected = OwnPropertyProbe.Missing;
            }
            else if (descriptor.Enumerable)
            {
                expected = OwnPropertyProbe.Enumerable;
            }
            else
            {
                expected = OwnPropertyProbe.NonEnumerable;
            }

            probe.Should().Be(expected, $"{target.GetType().Name}.{key} must probe as its descriptor reports");
        }
    }

    /// <summary>
    /// Names the shaped-object sweep probes on every object besides its own keys: one that nothing can
    /// possibly declare, the five that a <c>Function</c>-derived or otherwise field-backed host answers
    /// ahead of its shared layout, and two integer-like ones an array-backed host answers from element
    /// storage.
    /// </summary>
    private static readonly string[] TrickyProbeNames =
    [
        "__definitelyAbsentSweepName__", "length", "name", "prototype", "toString", "constructor", "hasOwnProperty", "0", "42",
    ];

    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    [Test]
    public void ASharedShapeDoesNotRetainEngines()
    {
        // The critical invariant of the whole feature: a host declares one shape per process and instantiates
        // it into every short-lived engine, so anything the shape held on to — an engine, a realm, a
        // materialized function, an instantiated prototype — would leak one of those per engine, forever.
        // Modelled on SharedPreparedScriptDoesNotRetainEngines in GarbageCollectionTests.
        var shape = new JsObjectShape.Builder()
            .Method("greet", static (t, args) => new JsString("hello"))
            .Accessor("tag", getter: static (t, args) => new JsString("tag"), setter: static (t, args) => JsValue.Undefined)
            .Constant("K", JsNumber.Create(1))
            .PerRealmSlot("constructor", enumerable: false)
            .PerRealmSlot("lazy", static o => new JsString("lazy"), enumerable: false)
            .ToStringTag("Retention")
            .Build();

        const int count = 20;
        var references = new List<WeakReference>(count);
        for (var i = 0; i < count; i++)
        {
            references.Add(RunOnceAndForget(shape));
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var aliveCount = references.Count(static r => r.IsAlive);
        GC.KeepAlive(shape);

        aliveCount.Should().Be(0, $"{aliveCount} of {count} engines were not collected — the shared shape still pins engines.");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static WeakReference RunOnceAndForget(JsObjectShape shape)
        {
            var engine = new Engine();
            var proto = shape.Instantiate(engine);
            proto.DefineOwnPropertyUnchecked("constructor", new Jint.Runtime.Descriptors.PropertyDescriptor(new JsString("ctor"), Jint.Runtime.Descriptors.PropertyFlag.NonEnumerable));
            engine.SetValue("proto", proto);
            // Materialize every kind of member, so anything a materialization could have parked on the shape
            // would be parked by the time the engine is dropped.
            engine.Execute("var el = Object.create(proto); el.greet(); el.tag; el.K; el.constructor; el.lazy; Object.keys(proto); String(proto);");
            return new WeakReference(engine);
        }
    }

    [Test]
    public void ASharedShapeDoesNotRetainHostStateOfDroppedEngines()
    {
        // Host state is the one thing a host is invited to hang off a shaped object, and the invitation is
        // only safe while the shape holds none of it. It is stored on the instantiated object, which is
        // engine-affine, so it must die with the engine — including when the state itself references that
        // engine, which is the case the API explicitly permits (a constructor, a JsValue, anything).
        var shape = new JsObjectShape.Builder()
            .Method("describe", static (t, args) => new JsString("described"))
            .Build();

        const int count = 20;
        var engines = new List<WeakReference>(count);
        var states = new List<WeakReference>(count);
        for (var i = 0; i < count; i++)
        {
            var (engine, state) = RunOnceAndForget(shape);
            engines.Add(engine);
            states.Add(state);
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        var enginesAlive = engines.Count(static r => r.IsAlive);
        var statesAlive = states.Count(static r => r.IsAlive);
        GC.KeepAlive(shape);

        enginesAlive.Should().Be(0, $"{enginesAlive} of {count} engines were not collected — attaching host state made the shape pin engines.");
        statesAlive.Should().Be(0, $"{statesAlive} of {count} host-state objects were not collected — the shape retains state of instances whose engine is gone.");

        // NoInlining so neither reference can be stack-rooted in this frame across the GC.Collect calls.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static (WeakReference Engine, WeakReference State) RunOnceAndForget(JsObjectShape shape)
        {
            var engine = new Engine();
            var proto = shape.Instantiate(engine);

            // Deliberately engine-affine and mutually referential: the state points at a value of this
            // engine, and the engine reaches the state through the prototype. Only the pair dying together
            // makes that safe.
            var state = new object[] { proto, new JsString("engine-affine") };
            JsObjectShape.SetHostState(proto, state);

            engine.SetValue("proto", proto);
            engine.Execute("var el = Object.create(proto); el.describe();");

            return (new WeakReference(engine), new WeakReference(state));
        }
    }

    private static JsValue GlobalSymbolRegistryToStringTag(Engine engine)
        => engine.Evaluate("Symbol.toStringTag");
}

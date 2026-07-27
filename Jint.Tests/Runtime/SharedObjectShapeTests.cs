#nullable enable

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
[CollectionDefinition(nameof(SharedObjectShapeTests), DisableParallelization = true)]
[Collection(nameof(SharedObjectShapeTests))]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
        engineB.Advanced.GetObjectRepresentation(protoB).Should().Be(ObjectRepresentation.SharedBuiltinLayout);

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

    [Fact]
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
            proto.FastSetProperty("constructor", new Jint.Runtime.Descriptors.PropertyDescriptor(new JsString("ctor"), Jint.Runtime.Descriptors.PropertyFlag.NonEnumerable));
            engine.SetValue("proto", proto);
            // Materialize every kind of member, so anything a materialization could have parked on the shape
            // would be parked by the time the engine is dropped.
            engine.Execute("var el = Object.create(proto); el.greet(); el.tag; el.K; el.constructor; el.lazy; Object.keys(proto); String(proto);");
            return new WeakReference(engine);
        }
    }

    private static JsValue GlobalSymbolRegistryToStringTag(Engine engine)
        => engine.Evaluate("Symbol.toStringTag");
}

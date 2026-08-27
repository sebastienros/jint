#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.AdvancedOperations.HasSharedShape"/> — the stable predicate a host may pin in its own
/// suite to assert that the shaping factories actually shaped, rather than falling back silently. These
/// tests live outside the Jint assembly on purpose: the project has no <c>InternalsVisibleTo</c> grant, so
/// every green assertion here is proof that a third-party embedder can write exactly the same one.
/// </summary>
public class SharedShapeTests
{
    private static readonly JsObjectLayout Layout = new("id", "name", "active");

    private static readonly JsObjectShape PrototypeShape = new JsObjectShape.Builder()
        .Method("greet", static (thisObject, args) => new JsString("hello"))
        .Constant("ANSWER", JsNumber.Create(42))
        .Build();

    private static JsObject CreateSample(Engine engine) => JsObject.Create(
        engine,
        Layout,
        [JsNumber.Create(1), new JsString("sample"), JsBoolean.True]);

    private static KeyValuePair<string, JsValue>[] SampleEntries() =>
    [
        new("id", JsNumber.Create(1)),
        new("name", new JsString("sample")),
        new("active", JsBoolean.True)
    ];

    /// <summary>A host object that answers its own properties instead of using Jint's storage.</summary>
    private sealed class HostRecord : ObjectInstance
    {
        public HostRecord(Engine engine) : base(engine)
        {
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            return property is JsString name && string.Equals(name.ToString(), "answer", StringComparison.Ordinal)
                ? new PropertyDescriptor(JsNumber.Create(42), writable: false, enumerable: true, configurable: false)
                : PropertyDescriptor.Undefined;
        }
    }

    // ---- reachability and guards ----

    [Test]
    public void ThePredicateIsReachableFromOutsideTheJintAssembly()
    {
        // This project has no InternalsVisibleTo, so the call below compiling at all is the guarantee. The
        // reflection assertion catches the accident of the surface being narrowed to internal + IVT later.
        var engine = new Engine();
        engine.Advanced.HasSharedShape(CreateSample(engine)).Should().BeTrue();

        typeof(Engine.AdvancedOperations).GetMethod(nameof(Engine.AdvancedOperations.HasSharedShape))
            .Should().NotBeNull();
    }

    [Test]
    public void NullIsRejected()
    {
        var engine = new Engine();
        Invoking(() => engine.Advanced.HasSharedShape(null!)).Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void AnObjectFromAnotherEngineIsRejected()
    {
        // Hidden classes are interned per engine, so asking one engine about another's object is a mistake
        // worth failing on rather than answering.
        var engine = new Engine();
        var other = new Engine();

        Invoking(() => engine.Advanced.HasSharedShape(CreateSample(other)))
            .Should().Throw<ArgumentException>().WithMessage("*different engine*");
    }

    // ---- the documented success cases a host may pin ----

    [Test]
    public void AnObjectBuiltFromALayoutHasASharedShape()
    {
        var engine = new Engine();
        engine.Advanced.HasSharedShape(JsObject.Create(engine, Layout, [JsNumber.Create(1), new JsString("a"), JsBoolean.True]))
            .Should().BeTrue();

        // Wider than the in-object slot capacity, so the values spill — still shared.
        var wide = JsObject.Create(engine, new JsObjectLayout("a", "b", "c", "d", "e", "f", "g"),
        [
            JsNumber.Create(1), JsNumber.Create(2), JsNumber.Create(3), JsNumber.Create(4),
            JsNumber.Create(5), JsNumber.Create(6), JsNumber.Create(7)
        ]);
        engine.Advanced.HasSharedShape(wide).Should().BeTrue();
    }

    [Test]
    public void ObjectsBuiltFromTheSameEntriesAllHaveASharedShape()
    {
        // The adoption assertion in one line, and the reason the answer is worth having: "shared" is a claim
        // about a batch, so every object of the batch has to answer the same way.
        var engine = new Engine();

        var first = JsObject.CreateFromEntries(engine, SampleEntries());
        var second = JsObject.CreateFromEntries(engine, SampleEntries());
        var third = JsObject.CreateFromEntries(engine, (IEnumerable<KeyValuePair<string, JsValue>>) SampleEntries());

        engine.Advanced.HasSharedShape(first).Should().BeTrue();
        engine.Advanced.HasSharedShape(second).Should().BeTrue();
        engine.Advanced.HasSharedShape(third).Should().BeTrue();
    }

    [Test]
    public void AnInstantiatedShapeObjectHasASharedShape()
    {
        var engine = new Engine();
        var prototype = PrototypeShape.Instantiate(engine);
        engine.SetValue("proto", prototype);

        // Like GetObjectRepresentation, the predicate observes without perturbing: an object that installs
        // its shared layout lazily reports it only once something has touched it.
        engine.Evaluate("proto.ANSWER").Should().Be(42);

        engine.Advanced.HasSharedShape(prototype).Should().BeTrue();
    }

    // ---- and the cases that are not shared ----

    [Test]
    public void AShapedObjectDeoptedByARawDescriptorWriteNoLongerHasASharedShape()
    {
        // Storing a raw descriptor under a string key is a dictionary-mode operation, so it permanently
        // deoptimizes a shape-mode receiver — the documented DefineOwnPropertyUnchecked caveat, witnessed.
        var engine = new Engine();
        var obj = CreateSample(engine);
        engine.Advanced.HasSharedShape(obj).Should().BeTrue();

        obj.DefineOwnPropertyUnchecked("extra", new PropertyDescriptor(JsNumber.Create(7), writable: true, enumerable: true, configurable: true));

        engine.Advanced.HasSharedShape(obj).Should().BeFalse();

        // ...and the object still behaves exactly as it did.
        engine.SetValue("o", obj);
        engine.Evaluate("o.id + ',' + o.extra").Should().Be("1,7");
    }

    [Test]
    public void MutationsAShapeCannotExpressDropTheSharedShape()
    {
        var engine = new Engine();
        var obj = CreateSample(engine);
        engine.SetValue("o", obj);

        engine.Advanced.HasSharedShape(obj).Should().BeTrue();
        engine.Execute("delete o.name;");
        engine.Advanced.HasSharedShape(obj).Should().BeFalse();
    }

    [Test]
    public void AHostObjectSubclassNeverHasASharedShape()
    {
        // A host subclass keeps its own-property set outside the engine's storage, so no shared layout
        // describes it — whatever the subclass does.
        var engine = new Engine();
        var host = new HostRecord(engine);
        engine.SetValue("host", host);

        engine.Evaluate("host.answer").Should().Be(42);
        engine.Advanced.HasSharedShape(host).Should().BeFalse();
    }

    [Test]
    public void SpecializedObjectTypesDoNotHaveASharedShape()
    {
        var engine = new Engine();

        engine.Advanced.HasSharedShape(engine.Evaluate("[1, 2, 3]").AsObject()).Should().BeFalse();
        engine.Advanced.HasSharedShape(engine.Evaluate("new Proxy({}, {})").AsObject()).Should().BeFalse();
    }

    [Test]
    public void AnObjectPopulatedThroughSetIsPinnedAsUnshared()
    {
        // PINNED BEHAVIOUR, NOT CONTRACT. A bare JsObject starts in the ordinary property dictionary and
        // ordinary Set writes do not start a hidden class for it, so it answers false today. Which
        // construction paths reach a shared shape is exactly the part that may improve release to release;
        // this test exists to make such a change visible, not to forbid it.
        var engine = new Engine();
        var obj = new JsObject(engine);
        obj.Set("id", JsNumber.Create(1));
        obj.Set("name", new JsString("sample"));

        engine.Advanced.HasSharedShape(obj).Should().BeFalse();

        // Whatever the representation, the object is a completely ordinary one.
        engine.SetValue("o", obj);
        engine.Evaluate("Object.keys(o).join()").Should().Be("id,name");
    }

    // ---- the predicate and the diagnostic can never disagree ----

    [Test]
    public void ThePredicateAgreesWithTheFinerGrainedDiagnostic()
    {
        var engine = new Engine();
        engine.Execute("Math.abs(1);");

        var samples = new ObjectInstance[]
        {
            CreateSample(engine),
            JsObject.CreateFromEntries(engine, SampleEntries()),
            JsObject.CreateFromEntries(engine, System.Array.Empty<KeyValuePair<string, JsValue>>()),
            engine.Evaluate("({ a: 1 })").AsObject(),
            engine.Evaluate("({})").AsObject(),
            engine.Evaluate("Math").AsObject(),
            engine.Evaluate("[1, 2]").AsObject(),
            new HostRecord(engine),
        };

        foreach (var sample in samples)
        {
            var representation = engine.Diagnostics.GetObjectRepresentation(sample);
            var shared = representation is ObjectRepresentation.HiddenClass or ObjectRepresentation.SharedBuiltinLayout;
            engine.Advanced.HasSharedShape(sample).Should().Be(shared, "representation was {0}", representation);
        }
    }
}

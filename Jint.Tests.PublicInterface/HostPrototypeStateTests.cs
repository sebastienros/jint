#nullable enable

// Reads a Jint diagnostic API declared outside the compatibility contract. Acknowledged the way an embedder
// acknowledges it; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="JsObjectShape.SetHostState"/> / <see cref="JsObjectShape.GetHostState"/> — the affordance that
/// lets a host keep a shaped prototype's own bookkeeping <em>on</em> the prototype instead of in a side table
/// keyed by it. Reported by an embedder as the one thing blocking adoption of <see cref="JsObjectShape"/>:
/// <c>Instantiate</c> hands back an object of a type they cannot derive from, so a type token, a constructor
/// back-reference or the collection an indexer projects had nowhere to live.
///
/// <para>
/// The state is per <em>instance</em>, which is what makes it safe to be engine-affine — unlike everything
/// placed into the shape itself, which is process-shared and must not be. That distinction is the point of
/// this file, and <c>ASharedShapeDoesNotRetainHostStateOfDroppedEngines</c> in Jint.Tests pins the other half
/// of it: the shape never retains state belonging to an engine that has been dropped.
/// </para>
/// </summary>
public class HostPrototypeStateTests
{
    /// <summary>
    /// The per-prototype bookkeeping the report described, in miniature: an engine-independent type token
    /// plus an engine-affine back-reference filled in after the fact.
    /// </summary>
    private sealed class NodeTypeInfo
    {
        public NodeTypeInfo(string interfaceName)
        {
            InterfaceName = interfaceName;
        }

        public string InterfaceName { get; }

        /// <summary>
        /// The constructor whose <c>prototype</c> is the object this state hangs on — which is exactly why
        /// state is attached after creation rather than passed to <c>Instantiate</c>: it does not exist yet
        /// when the prototype is built.
        /// </summary>
        public JsValue? Constructor { get; set; }
    }

    private static readonly JsObjectShape NodeShape = new JsObjectShape.Builder()
        .Method("describe", Describe)
        .Method("ctorName", ConstructorName)
        .Build();

    /// <summary>
    /// The retrieval pattern the XML docs describe: a member implementation receives the <em>instance</em>,
    /// and walks up to the prototype carrying the state. Engine-independent, as a shape's delegates must be —
    /// everything it needs arrives through the receiver.
    /// </summary>
    private static NodeTypeInfo? InfoFor(JsValue thisObject)
    {
        for (var o = (thisObject as ObjectInstance)?.Prototype; o is not null; o = o.Prototype)
        {
            if (JsObjectShape.GetHostState(o) is NodeTypeInfo info)
            {
                return info;
            }
        }

        return null;
    }

    private static JsValue Describe(JsValue thisObject, JsValue[] arguments)
    {
        var info = InfoFor(thisObject);
        return info is null ? JsValue.Undefined : new JsString(info.InterfaceName);
    }

    private static JsValue ConstructorName(JsValue thisObject, JsValue[] arguments)
    {
        var constructor = InfoFor(thisObject)?.Constructor;
        return constructor is null ? JsValue.Undefined : constructor.Get("name");
    }

    // ---- the capability ----

    [Fact]
    public void AMemberImplementationReachesTheStateAttachedToItsPrototype()
    {
        var engine = new Engine();
        var prototype = NodeShape.Instantiate(engine);
        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("Node"));

        engine.SetValue("proto", prototype);
        engine.Execute("var el = Object.create(proto);");

        engine.Evaluate("el.describe()").Should().Be("Node");
        // ...and the host can read it back from outside script just as directly.
        (JsObjectShape.GetHostState(prototype) as NodeTypeInfo)!.InterfaceName.Should().Be("Node");
    }

    [Fact]
    public void StateMayHoldEngineAffineValuesAttachedAfterTheObjectExists()
    {
        // The ordering the API is shaped around: the constructor cannot exist before the prototype, because
        // its `prototype` property is the prototype. State attached after creation closes that cycle without
        // a mutable placeholder.
        var engine = new Engine();
        var prototype = NodeShape.Instantiate(engine);
        var info = new NodeTypeInfo("Node");
        JsObjectShape.SetHostState(prototype, info);

        engine.SetValue("proto", prototype);
        engine.Execute("function Node() {} Node.prototype = proto; var el = Object.create(proto);");

        engine.Evaluate("el.ctorName()").Should().Be(JsValue.Undefined, "nothing has been wired yet");

        info.Constructor = engine.Evaluate("Node");
        engine.Evaluate("el.ctorName()").Should().Be("Node");
    }

    [Fact]
    public void StateIsPerInstanceAndIsolatedBetweenEngines()
    {
        var engineA = new Engine();
        var protoA = NodeShape.Instantiate(engineA);
        JsObjectShape.SetHostState(protoA, new NodeTypeInfo("FromA"));
        engineA.SetValue("proto", protoA);
        engineA.Execute("var el = Object.create(proto);");

        var engineB = new Engine();
        var protoB = NodeShape.Instantiate(engineB);
        JsObjectShape.SetHostState(protoB, new NodeTypeInfo("FromB"));
        engineB.SetValue("proto", protoB);
        engineB.Execute("var el = Object.create(proto);");

        engineA.Evaluate("el.describe()").Should().Be("FromA");
        engineB.Evaluate("el.describe()").Should().Be("FromB");

        // Two objects from one shape in the *same* engine are independent too.
        var second = NodeShape.Instantiate(engineA);
        JsObjectShape.SetHostState(second, new NodeTypeInfo("Second"));
        engineA.Evaluate("el.describe()").Should().Be("FromA");
        (JsObjectShape.GetHostState(second) as NodeTypeInfo)!.InterfaceName.Should().Be("Second");
    }

    [Fact]
    public void TheWalkFindsTheNearestPrototypeCarryingState()
    {
        // A deep prototype chain, only some levels of which carry state — the shape a host gets once it stops
        // flattening every member onto every leaf.
        var engine = new Engine();

        var baseProto = NodeShape.Instantiate(engine);
        JsObjectShape.SetHostState(baseProto, new NodeTypeInfo("Node"));

        var middle = NodeShape.Instantiate(engine, baseProto);
        var leaf = NodeShape.Instantiate(engine, middle);
        JsObjectShape.SetHostState(leaf, new NodeTypeInfo("HTMLDivElement"));

        engine.SetValue("baseProto", baseProto);
        engine.SetValue("middle", middle);
        engine.SetValue("leaf", leaf);
        engine.Execute("var fromBase = Object.create(baseProto), fromMiddle = Object.create(middle), fromLeaf = Object.create(leaf);");

        engine.Evaluate("fromLeaf.describe()").Should().Be("HTMLDivElement");
        engine.Evaluate("fromMiddle.describe()").Should().Be("Node", "the middle level carries none, so the walk continues");
        engine.Evaluate("fromBase.describe()").Should().Be("Node");

        // The intermediate object genuinely has none of its own.
        JsObjectShape.GetHostState(middle).Should().BeNull();
    }

    [Fact]
    public void OnlyTheInstantiatedObjectCarriesState()
    {
        var engine = new Engine();
        var prototype = NodeShape.Instantiate(engine);
        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("Node"));

        engine.SetValue("proto", prototype);
        var inheriting = engine.Evaluate("Object.create(proto)").AsObject();

        // Inheriting from a shaped prototype does not inherit its host state; the walk is the host's job.
        JsObjectShape.GetHostState(inheriting).Should().BeNull();
    }

    [Fact]
    public void StateCanBeReplacedAndDetached()
    {
        var engine = new Engine();
        var prototype = NodeShape.Instantiate(engine);

        JsObjectShape.GetHostState(prototype).Should().BeNull("nothing is attached until the host attaches it");

        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("First"));
        (JsObjectShape.GetHostState(prototype) as NodeTypeInfo)!.InterfaceName.Should().Be("First");

        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("Second"));
        (JsObjectShape.GetHostState(prototype) as NodeTypeInfo)!.InterfaceName.Should().Be("Second");

        JsObjectShape.SetHostState(prototype, null);
        JsObjectShape.GetHostState(prototype).Should().BeNull();
    }

    [Fact]
    public void StateSurvivesTheFallbackToTheOrdinaryRepresentation()
    {
        // Deleting a declared member drops the object out of the shared layout. The representation is an
        // implementation detail; the object's identity as one a shape created is not, so its state stays.
        var engine = new Engine();
        var prototype = NodeShape.Instantiate(engine);
        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("Node"));

        engine.SetValue("proto", prototype);
        engine.Execute("var el = Object.create(proto);");
        engine.Evaluate("el.describe()").Should().Be("Node");

        engine.Evaluate("delete proto.ctorName").Should().Be(true);
        engine.Advanced.GetObjectRepresentation(prototype).Should().Be(ObjectRepresentation.Specialized);

        engine.Evaluate("el.describe()").Should().Be("Node");
        (JsObjectShape.GetHostState(prototype) as NodeTypeInfo)!.InterfaceName.Should().Be("Node");

        // ...and it can still be replaced afterwards.
        JsObjectShape.SetHostState(prototype, new NodeTypeInfo("Rebound"));
        engine.Evaluate("el.describe()").Should().Be("Rebound");
    }

    // ---- what it refuses ----

    [Fact]
    public void GetHostStateAnswersNullForEverythingItDoesNotOwn()
    {
        var engine = new Engine();

        JsObjectShape.GetHostState(null).Should().BeNull("so a prototype walk can run off the end of the chain");
        JsObjectShape.GetHostState(engine.Evaluate("({})").AsObject()).Should().BeNull();
        JsObjectShape.GetHostState(engine.Evaluate("Object.prototype").AsObject()).Should().BeNull();
        JsObjectShape.GetHostState(engine.Evaluate("Math").AsObject()).Should().BeNull();
        JsObjectShape.GetHostState(new PlainHostObject(engine)).Should().BeNull();
    }

    [Fact]
    public void SetHostStateRejectsAnObjectItCannotHold()
    {
        var engine = new Engine();

        Invoking(() => JsObjectShape.SetHostState(null!, new NodeTypeInfo("x")))
            .Should().Throw<ArgumentNullException>();

        // Silently dropping the state would leave the host debugging a lookup that never had a chance.
        Invoking(() => JsObjectShape.SetHostState(engine.Evaluate("({})").AsObject(), new NodeTypeInfo("x")))
            .Should().Throw<ArgumentException>()
            .WithMessage("*created by JsObjectShape.Instantiate*");

        Invoking(() => JsObjectShape.SetHostState(new PlainHostObject(engine), new NodeTypeInfo("x")))
            .Should().Throw<ArgumentException>();
    }

    /// <summary>An ordinary host subclass — the thing a shaped object is deliberately not.</summary>
    private sealed class PlainHostObject : ObjectInstance
    {
        public PlainHostObject(Engine engine) : base(engine)
        {
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property) => PropertyDescriptor.Undefined;
    }
}

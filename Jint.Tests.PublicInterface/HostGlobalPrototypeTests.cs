using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A Proxy or a host object with a <c>get</c> hook installed as the prototype of <c>globalThis</c>. A bare
/// identifier resolves through the global's prototype chain, so a name such an object only produces from
/// its <c>[[Get]]</c> has to reach the identifier lane too — <c>[[GetOwnProperty]]</c> fires a Proxy's
/// <c>getOwnPropertyDescriptor</c> trap and never <c>get</c>, which would leave the read disagreeing with
/// <c>in</c>, with <c>typeof</c> and with the same read spelled <c>globalThis.name</c>.
/// </summary>
public class ExoticGlobalPrototypeTests
{
    /// <summary>
    /// Produces a value from <see cref="ObjectInstance.Get"/> for a name it owns no descriptor for — the
    /// derived <see cref="PropertyAccessSemantics.Exotic"/> shape, and exactly a Proxy's problem. It
    /// declares nothing: the engine derives the semantics from the override itself.
    /// </summary>
    private sealed class SynthesizingHost : ObjectInstance
    {
        private const string Virtual = "virt";

        public SynthesizingHost(Engine engine) : base(engine)
        {
        }

        public int Gets { get; private set; }

        public JsValue LastReceiver { get; private set; } = JsValue.Undefined;

        private static bool IsVirtual(JsValue property)
            => property.IsString() && string.Equals(property.AsString(), Virtual, StringComparison.Ordinal);

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (IsVirtual(property))
            {
                Gets++;
                LastReceiver = receiver;
                return "VIRTUAL";
            }

            return base.Get(property, receiver);
        }

        public override bool HasProperty(JsValue property) => IsVirtual(property) || base.HasProperty(property);
    }

    private const string InstallProxy = """
        var proxy = new Proxy({}, {
            has: function (t, k) { return k === 'virt' || Reflect.has(t, k); },
            get: function (t, k, r) { if (k === 'virt') { gets++; receiver = r; return 'VIRTUAL'; } return Reflect.get(t, k, r); },
            set: function (t, k, v, r) { sets++; setReceiver = r; return Reflect.set(t, k, v, r); }
        });
        var gets = 0, sets = 0, receiver = null, setReceiver = null;
        Object.setPrototypeOf(globalThis, proxy);
        """;

    [Test]
    public void AProxyPrototypeSeesItsGetTrapForABareIdentifier()
    {
        var engine = new Engine();
        engine.Execute(InstallProxy);

        engine.Evaluate("virt").AsString().Should().Be("VIRTUAL");
        engine.Evaluate("gets").AsNumber().Should().Be(1);
        engine.Evaluate("receiver === globalThis").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AProxyPrototypeAnswersTypeofTheReadAndTheMemberReadAlike()
    {
        var engine = new Engine();
        engine.Execute(InstallProxy);

        engine.Evaluate("typeof virt").AsString().Should().Be("string");
        engine.Evaluate("'virt' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("globalThis.virt").AsString().Should().Be("VIRTUAL");
        engine.Evaluate("virt").AsString().Should().Be("VIRTUAL");
    }

    [Test]
    public void AProxyPrototypeSeesItsSetTrapForABareAssignment()
    {
        var engine = new Engine();
        engine.Execute(InstallProxy);

        engine.Execute("virt = 12;");

        engine.Evaluate("sets").AsNumber().Should().Be(1);
        engine.Evaluate("setReceiver === globalThis").AsBoolean().Should().BeTrue();
        // Reflect.set forwarded onto the global receiver, so the name is now an own global that shadows
        engine.Evaluate("globalThis.hasOwnProperty('virt')").AsBoolean().Should().BeTrue();
        engine.Evaluate("virt").AsNumber().Should().Be(12);
    }

    /// <summary>
    /// staging/sm/Proxy/global-receiver.js from test262, which the generated suite does not cover because
    /// the harness only generates annexB, built-ins, intl402 and language.
    /// </summary>
    [Test]
    public void Test262StagingGlobalReceiver()
    {
        var engine = new Engine();
        engine.Execute("""
            var global = this;
            var proto = Object.getPrototypeOf(global);
            var gets = 0, sets = 0, getReceiver = null, setReceiver = null;

            Object.setPrototypeOf(global, new Proxy(proto, {
                has(t, id) { return id === "bareword" || Reflect.has(t, id); },
                get(t, id, r) { gets++; getReceiver = r; return Reflect.get(t, id, r); },
                set(t, id, v, r) { sets++; setReceiver = r; return Reflect.set(t, id, v, r); }
            }));
            """);

        engine.Evaluate("bareword").Should().Be(JsValue.Undefined);
        engine.Evaluate("gets").AsNumber().Should().Be(1);
        engine.Evaluate("getReceiver === global").AsBoolean().Should().BeTrue();

        engine.Execute("bareword = 12;");
        engine.Evaluate("sets").AsNumber().Should().Be(1);
        engine.Evaluate("setReceiver === global").AsBoolean().Should().BeTrue();
        engine.Evaluate("global.bareword").AsNumber().Should().Be(12);
    }

    [Test]
    public void AHostPrototypeThatOverridesGetIsAskedThroughGet()
    {
        var engine = new Engine();
        var host = new SynthesizingHost(engine);
        engine.Global.Prototype = host;

        engine.Evaluate("virt").AsString().Should().Be("VIRTUAL");
        engine.Evaluate("typeof virt").AsString().Should().Be("string");
        engine.Evaluate("'virt' in globalThis").AsBoolean().Should().BeTrue();
        engine.Evaluate("globalThis.virt").AsString().Should().Be("VIRTUAL");

        host.Gets.Should().BeGreaterThan(0);
        ReferenceEquals(host.LastReceiver, engine.Global).Should().BeTrue();
    }

    [Test]
    public void AProxyBelowTheDirectPrototypeStillResolves()
    {
        var engine = new Engine();
        engine.Execute(InstallProxy);
        engine.Execute("Object.setPrototypeOf(globalThis, Object.create(proxy));");

        engine.Evaluate("virt").AsString().Should().Be("VIRTUAL");
        engine.Evaluate("typeof virt").AsString().Should().Be("string");
    }

    [Test]
    public void AnOrdinaryPrototypeIsUnaffected()
    {
        var engine = new Engine();
        engine.Execute("""
            var plain = Object.create(null);
            plain.plainName = 'PLAIN';
            Object.defineProperty(plain, 'accessor', { get: function () { return this === globalThis ? 'RECEIVER' : 'OTHER'; } });
            Object.setPrototypeOf(globalThis, plain);
            """);

        engine.Evaluate("plainName").AsString().Should().Be("PLAIN");
        engine.Evaluate("typeof plainName").AsString().Should().Be("string");
        engine.Evaluate("globalThis.plainName").AsString().Should().Be("PLAIN");
        // an inherited accessor is still invoked with the global as its `this`
        engine.Evaluate("accessor").AsString().Should().Be("RECEIVER");
        // and a name absent from the whole chain is still unresolvable
        engine.Evaluate("typeof missing").AsString().Should().Be("undefined");
        Invoking(() => engine.Evaluate("missing")).Should().Throw<Jint.Runtime.JavaScriptException>();
    }

    [Test]
    public void AnOrdinaryPrototypeTwoLevelsDeepIsUnaffected()
    {
        var engine = new Engine();
        engine.Execute("""
            var base = Object.create(null);
            base.deepName = 'DEEP';
            Object.setPrototypeOf(globalThis, Object.create(base));
            """);

        engine.Evaluate("deepName").AsString().Should().Be("DEEP");
        engine.Evaluate("typeof deepName").AsString().Should().Be("string");
    }
}

/// <summary>
/// A wrapped CLR object installed as the prototype of <c>globalThis</c> (issue #2925). Only the global
/// object participates in bare-identifier resolution, so this is where the spec-shaped
/// GlobalEnvironmentRecord lanes (HasBinding/GetBindingValue walking the prototype chain) meet interop:
/// members inherited from the host must resolve as free identifiers, through <c>typeof</c>, and as calls.
/// </summary>
public class HostGlobalPrototypeTests
{
    public sealed class GlobalHost
    {
        public string Greeting => "hello";
        public int Compute(int a, int b) => a + b;
    }

    [Test]
    public void MembersInheritedFromAHostGlobalPrototypeResolveAsBareIdentifiers()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        engine.Execute("Object.setPrototypeOf(globalThis, host);");

        engine.Evaluate("Greeting").AsString().Should().Be("hello");
        engine.Evaluate("typeof Greeting").AsString().Should().Be("string");
        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
        engine.Evaluate("globalThis.Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Test]
    public void HostSidePrototypeAssignmentBehavesTheSame()
    {
        var engine = new Engine();
        var host = new GlobalHost();
        engine.Global.Prototype = (ObjectInstance) JsValue.FromObject(engine, host);

        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Test]
    public void HostMembersResolveFromDeeperInThePrototypeChain()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        // host sits one level below the direct prototype
        engine.Execute("Object.setPrototypeOf(globalThis, Object.create(host));");

        engine.Evaluate("Greeting").AsString().Should().Be("hello");
        engine.Evaluate("typeof Compute").AsString().Should().Be("function");
        engine.Evaluate("Compute(40, 2)").AsNumber().Should().Be(42);
    }

    [Test]
    public void OwnGlobalsAndDeclarationsStillShadowInheritedHostMembers()
    {
        var engine = new Engine();
        engine.SetValue("host", new GlobalHost());
        engine.Execute("Object.setPrototypeOf(globalThis, host);");

        // a getter-only CLR property surfaces as inherited non-writable data: sloppy assignment
        // is a silent no-op per OrdinarySet, so no shadowing own property appears
        engine.Evaluate("globalThis.Greeting = 'own'; Greeting").AsString().Should().Be("hello");
        engine.Evaluate("globalThis.hasOwnProperty('Greeting')").AsBoolean().Should().BeFalse();

        // defineProperty and var hoisting create own bindings that shadow the host
        engine.Evaluate("Object.defineProperty(globalThis, 'Greeting', { value: 'own', writable: true, configurable: true }); Greeting").AsString().Should().Be("own");
        engine.Execute("var Compute = 1;");
        engine.Evaluate("typeof Compute").AsString().Should().Be("number");
    }
}

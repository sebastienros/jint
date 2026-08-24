using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host function is a class a third party can derive. Before <c>HostFunction</c> existed, the only
/// accessible constructor on <see cref="Jint.Native.Function.Function"/> was
/// <c>protected Function(Engine, Realm, JsString?)</c> — and no public member anywhere returned a
/// <c>Realm</c>, so the type advertised an extension point nobody outside the assembly could reach. These
/// tests live in the one project without <c>InternalsVisibleTo</c>, so they compile only against what an
/// embedder can actually see.
/// </summary>
public class HostFunctionTests
{
    [Fact]
    public void AHostFunctionIsCallableFromScript()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("greet('world')").AsString().Should().Be("hello world");
    }

    [Fact]
    public void ItReportsItselfAsAFunction()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("typeof greet").AsString().Should().Be("function");
        engine.Evaluate("greet instanceof Function").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(greet) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(greet)").AsString().Should().Be("[object Function]");
    }

    [Fact]
    public void ItCarriesTheNameAndLengthItWasBuiltWith()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("greet.name").AsString().Should().Be("greet");
        engine.Evaluate("greet.length").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-built-in-function-objects">§10.3</see>: a built-in's
    /// <c>name</c> and <c>length</c> are <c>{ writable: false, enumerable: false, configurable: true }</c>.
    /// Configurable is the half that matters in practice — it is what lets a wrapper rename the function.
    /// </summary>
    [Fact]
    public void NameAndLengthCarryTheAttributesTheSpecificationGivesABuiltIn()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        foreach (var member in new[] { "name", "length" })
        {
            var descriptor = engine.Evaluate($"Object.getOwnPropertyDescriptor(greet, '{member}')").AsObject();
            descriptor.Get("writable").AsBoolean().Should().BeFalse(member);
            descriptor.Get("enumerable").AsBoolean().Should().BeFalse(member);
            descriptor.Get("configurable").AsBoolean().Should().BeTrue(member);
        }

        engine.Evaluate("Object.defineProperty(greet, 'name', { value: 'renamed' }); greet.name")
            .AsString().Should().Be("renamed");
    }

    [Fact]
    public void ItWorksAsACallback()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("['a', 'b'].map(greet).join('|')").AsString().Should().Be("hello a|hello b");
    }

    [Fact]
    public void CallApplyAndBindAllWork()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("greet.call(null, 'call')").AsString().Should().Be("hello call");
        engine.Evaluate("greet.apply(null, ['apply'])").AsString().Should().Be("hello apply");
        engine.Evaluate("greet.bind(null, 'bind')()").AsString().Should().Be("hello bind");
    }

    [Fact]
    public void TheHostCanInvokeItToo()
    {
        var engine = new Engine();
        var greet = new GreetFunction(engine);
        engine.SetValue("greet", greet);

        engine.Call(greet, "host").AsString().Should().Be("hello host");
        engine.Invoke("greet", "invoke").AsString().Should().Be("hello invoke");
    }

    /// <summary>
    /// A host function has no <c>[[Construct]]</c>, which is what the specification says for a built-in
    /// function object that is not a constructor. A host that wants <c>new</c> derives from
    /// <see cref="Constructor"/> instead.
    /// </summary>
    [Fact]
    public void ConstructingOneIsATypeError()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        Invoking(() => engine.Evaluate("new greet('x')")).Should().Throw<JavaScriptException>()
            .WithMessage("*is not a constructor*");
    }

    [Fact]
    public void ItIsAnObjectAndCanCarryItsOwnState()
    {
        var engine = new Engine();
        var counter = new CounterFunction(engine);
        engine.SetValue("next", counter);

        engine.Evaluate("next(); next(); next()").AsNumber().Should().Be(3);
        counter.Count.Should().Be(3);

        // ...and it is an ordinary object besides, so expandos work.
        engine.Evaluate("next.tag = 'x'; next.tag").AsString().Should().Be("x");
    }

    [Fact]
    public void ToStringReportsNativeCode()
    {
        var engine = new Engine();
        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("greet.toString()").AsString().Should().Contain("[native code]");
    }

    /// <summary>
    /// The same principal-realm rule <see cref="Jint.Runtime.Interop.ClrFunction"/> follows: a host function
    /// built after a <c>ShadowRealm</c> exists must still be a <c>Function</c> of the realm the surrounding
    /// script can reach.
    /// </summary>
    [Fact]
    public void ItBelongsToThePrincipalRealmEvenWhenBuiltAfterAShadowRealm()
    {
        var engine = new Engine();
        engine.Evaluate("new ShadowRealm()");

        engine.SetValue("greet", new GreetFunction(engine));

        engine.Evaluate("greet instanceof Function").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// A CLR exception escaping <c>Invoke</c> reaches the host untouched by default, and becomes a
    /// script-catchable error when the engine is configured to catch them — the same two behaviours a
    /// <c>ClrFunction</c> body has, so which of the two spellings a host chose is not observable.
    /// </summary>
    [Fact]
    public void AClrExceptionFromTheBodyPropagatesToTheHostByDefault()
    {
        var engine = new Engine();
        engine.SetValue("boom", new ThrowingFunction(engine));

        Invoking(() => engine.Evaluate("boom()")).Should().Throw<InvalidOperationException>()
            .WithMessage("from the host");
    }

    [Fact]
    public void AClrExceptionFromTheBodyIsCatchableWhenTheEngineCatchesThem()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("boom", new ThrowingFunction(engine));

        // The text itself is redacted by default (see the v5 migration guide, "Script-visible error text is
        // redacted"); what this pins is that the throw became a JavaScript error script could catch at all.
        engine.Evaluate("try { boom(); 'not reached'; } catch (e) { e instanceof Error ? 'caught' : 'other' }")
            .AsString().Should().Be("caught");
    }

    [Fact]
    public void TheConstructorValidatesItsArguments()
    {
        var engine = new Engine();

        Invoking(() => new GreetFunction(null!)).Should().Throw<ArgumentNullException>();
        Invoking(() => new GreetFunction(engine, name: null!)).Should().Throw<ArgumentNullException>();
        Invoking(() => new GreetFunction(engine, length: -1)).Should().Throw<ArgumentOutOfRangeException>();

        // An empty name is legal — that is what an anonymous built-in has.
        var anonymous = new GreetFunction(engine, name: "");
        engine.SetValue("anonymous", anonymous);
        engine.Evaluate("anonymous.name").AsString().Should().BeEmpty();
    }

    /// <summary>
    /// The other half of the derivable-callable surface. A host <see cref="Constructor"/> used to inherit
    /// from <c>Object.prototype</c> — the default every <c>ObjectInstance</c> starts with — so it was not
    /// <c>instanceof Function</c> and had no <c>call</c>, <c>apply</c> or <c>bind</c>.
    /// </summary>
    [Fact]
    public void AHostConstructorIsAFunctionToo()
    {
        var engine = new Engine();
        engine.SetValue("Box", new BoxConstructor(engine));

        engine.Evaluate("typeof Box").AsString().Should().Be("function");
        engine.Evaluate("Box instanceof Function").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(Box) === Function.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("typeof Box.call").AsString().Should().Be("function");
        engine.Evaluate("new Box().tag").AsString().Should().Be("box");
    }

    /// <summary>
    /// The three coercion helpers that used to take a <c>Realm</c> — which no public member returns — now
    /// take the <see cref="Engine"/> a host has. That this file compiles is the whole assertion.
    /// </summary>
    [Fact]
    public void TheCoercionHelpersTakeAnEngineAHostCanActuallyHold()
    {
        var engine = new Engine();

        TypeConverter.ToObject(engine, new JsString("s")).Get("length").AsNumber().Should().Be(1);
        TypeConverter.ToIndex(engine, new JsNumber(7)).Should().Be(7u);

        var descriptor = PropertyDescriptor.ToPropertyDescriptor(
            engine,
            engine.Evaluate("({ value: 42, enumerable: true })"));
        descriptor.Value.AsNumber().Should().Be(42);
        descriptor.Enumerable.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="Realm"/> stays public, and after this change every remaining place it appears in the
    /// surface is a <see cref="Host"/> virtual — where the engine <i>hands</i> the realm to the host rather
    /// than asking it for one it has no way to obtain. That this subclass compiles and runs is the assertion;
    /// <c>CreateRealm</c> is the only one that has to produce a realm, and <c>base</c> produces it.
    /// </summary>
    [Fact]
    public void TheRealmsLeftInTheSurfaceAreOnesAHostIsHandedRatherThanAskedFor()
    {
        var host = new RealmObservingHost();
        var engine = new Engine(options => options.UseHostFactory(_ => host));

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
        host.CreatedRealms.Should().BeGreaterThan(0);

        engine.Evaluate("new ShadowRealm()");
        host.InitializedShadowRealms.Should().Be(1);
    }
}

file sealed class RealmObservingHost : Host
{
    public int CreatedRealms { get; private set; }

    public int InitializedShadowRealms { get; private set; }

    protected override Realm CreateRealm()
    {
        CreatedRealms++;
        return base.CreateRealm();
    }

    public override void InitializeShadowRealm(Realm realm)
    {
        InitializedShadowRealms++;
        realm.HostDefined = "shadow";
        base.InitializeShadowRealm(realm);
    }
}

file sealed class GreetFunction : HostFunction
{
    public GreetFunction(Engine engine, string name = "greet", int length = 1) : base(engine, name, length)
    {
    }

    protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments)
        => new JsString("hello " + TypeConverter.ToString(arguments.At(0)));
}

file sealed class CounterFunction : HostFunction
{
    public CounterFunction(Engine engine) : base(engine, "next")
    {
    }

    public int Count { get; private set; }

    protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments) => new JsNumber(++Count);
}

file sealed class ThrowingFunction : HostFunction
{
    public ThrowingFunction(Engine engine) : base(engine, "boom")
    {
    }

    protected override JsValue Invoke(JsValue thisObject, JsValue[] arguments)
        => throw new InvalidOperationException("from the host");
}

file sealed class BoxConstructor : Constructor
{
    public BoxConstructor(Engine engine) : base(engine, "Box")
    {
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        var box = new JsObject(_engine);
        box.Set("tag", new JsString("box"));
        return box;
    }
}

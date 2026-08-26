#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The eleven §5.1 interface objects that used not to be globals, seen from outside the assembly: the eight
/// Streams interfaces a script never constructs by name, and <c>Crypto</c>, <c>SubtleCrypto</c> and
/// <c>Performance</c>, which had no interface object at all.
/// </summary>
/// <remarks>
/// The promise a host can build on is not that the names are defined — it is that each one <b>is the object
/// its instances inherit from</b>, so <c>x instanceof Name</c> and
/// <c>Object.getPrototypeOf(x) === Name.prototype</c> cannot disagree. Everything here is script-visible, so
/// it holds for an embedder with no access to the engine's internals.
/// </remarks>
public class WebApiInterfaceObjectTests
{
    /// <summary>Every one of the eleven, with the flag that provides it.</summary>
    public static TestCases<string, WebApiFeatures> Exposed => new()
    {
        { "ReadableStreamDefaultReader", WebApiFeatures.Streams },
        { "ReadableStreamBYOBReader", WebApiFeatures.Streams },
        { "ReadableStreamDefaultController", WebApiFeatures.Streams },
        { "ReadableByteStreamController", WebApiFeatures.Streams },
        { "ReadableStreamBYOBRequest", WebApiFeatures.Streams },
        { "WritableStreamDefaultWriter", WebApiFeatures.Streams },
        { "WritableStreamDefaultController", WebApiFeatures.Streams },
        { "TransformStreamDefaultController", WebApiFeatures.Streams },
        { "Crypto", WebApiFeatures.Crypto },
        { "SubtleCrypto", WebApiFeatures.Crypto },
        { "Performance", WebApiFeatures.Performance },
    };

    [TestCaseSource(nameof(Exposed))]
    public void ArrivesWithItsFeatureAndNeverOnItsOwn(string name, WebApiFeatures feature)
    {
        new Engine(options => options.UseWebApis(feature))
            .Evaluate("typeof " + name).AsString().Should().Be("function", name);

        // Not on an engine that asked for nothing, nor for something else.
        new Engine().Evaluate("typeof " + name).AsString().Should().Be("undefined", name);
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof " + name).AsString().Should().Be("undefined", name);
    }

    /// <summary>
    /// A <c>ShadowRealm</c> carries none of the web APIs, which is deliberately more conservative than a
    /// browser — and widening the exposure did not widen that.
    /// </summary>
    [TestCaseSource(nameof(Exposed))]
    public void DoesNotReachIntoAShadowRealm(string name, WebApiFeatures feature)
    {
        new Engine(options => options.UseWebApis(feature))
            .Evaluate("new ShadowRealm().evaluate('typeof " + name + "')").AsString().Should().Be("undefined", name);
    }

    /// <summary>
    /// A host that registered a global of its own keeps it. The install probes rather than reads, so a host's
    /// own lazy global is not forced into existence merely by our looking.
    /// </summary>
    [TestCaseSource(nameof(Exposed))]
    public void LeavesAGlobalTheHostAlreadyOwns(string name, WebApiFeatures feature)
    {
        var built = 0;
        var engine = new Engine(options => options
            .AddLazyGlobal(name, _ => { built++; return new JsString("host's own"); })
            .UseWebApis(feature));

        built.Should().Be(0, name);
        engine.Evaluate(name).AsString().Should().Be("host's own", name);
        built.Should().Be(1, name);
    }

    [TestCase("ReadableStreamDefaultReader", "new ReadableStream().getReader()")]
    [TestCase("ReadableStreamBYOBReader", "new ReadableStream({ type: 'bytes' }).getReader({ mode: 'byob' })")]
    [TestCase("WritableStreamDefaultWriter", "new WritableStream().getWriter()")]
    [TestCase("ReadableStreamDefaultController", "captured(c => new ReadableStream({ start: c }))")]
    [TestCase("ReadableByteStreamController", "captured(c => new ReadableStream({ type: 'bytes', start: c }))")]
    [TestCase("WritableStreamDefaultController", "captured(c => new WritableStream({ start: c }))")]
    [TestCase("TransformStreamDefaultController", "captured(c => new TransformStream({ start: c }))")]
    [TestCase("Crypto", "crypto")]
    [TestCase("SubtleCrypto", "crypto.subtle")]
    [TestCase("Performance", "performance")]
    public void IsTheObjectItsInstancesInheritFrom(string name, string instance)
    {
        var engine = new Engine(options => options.UseWebApis());
        engine.Execute("function captured(build) { var seen; build(c => { seen = c; }); return seen; }");

        engine.Evaluate(instance + " instanceof " + name).AsBoolean().Should().BeTrue(name);
        engine.Evaluate("Object.getPrototypeOf(" + instance + ") === " + name + ".prototype").AsBoolean().Should().BeTrue(name);
        engine.Evaluate(name + ".prototype.constructor === " + name).AsBoolean().Should().BeTrue(name);

        // Not a Symbol.hasInstance shim: the chain answers, and nothing was planted on the interface object.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(" + name + ", Symbol.hasInstance)")
            .AsBoolean().Should().BeFalse(name);
    }

    /// <summary>
    /// Which of them a script may construct, and which refuse — https://webidl.spec.whatwg.org/#es-interface-call
    /// gives an interface object a <c>[[Construct]]</c> only when the interface declares a constructor
    /// operation.
    /// </summary>
    [TestCase("ReadableStreamDefaultReader", true)]
    [TestCase("ReadableStreamBYOBReader", true)]
    [TestCase("WritableStreamDefaultWriter", true)]
    [TestCase("ReadableStreamDefaultController", false)]
    [TestCase("ReadableByteStreamController", false)]
    [TestCase("ReadableStreamBYOBRequest", false)]
    [TestCase("WritableStreamDefaultController", false)]
    [TestCase("TransformStreamDefaultController", false)]
    [TestCase("Crypto", false)]
    [TestCase("SubtleCrypto", false)]
    [TestCase("Performance", false)]
    public void IsConstructibleOnlyWhereTheStandardSaysSo(string name, bool constructible)
    {
        var engine = new Engine(options => options.UseWebApis());

        // The three constructible ones all take the stream they lock, so a bare `new` is a TypeError for a
        // different reason; give each of them the argument its IDL asks for.
        var argument = name switch
        {
            "ReadableStreamDefaultReader" => "new ReadableStream()",
            "ReadableStreamBYOBReader" => "new ReadableStream({ type: 'bytes' })",
            "WritableStreamDefaultWriter" => "new WritableStream()",
            _ => "",
        };

        var answer = engine.Evaluate(
            "(function () { try { return new " + name + "(" + argument + ") instanceof " + name + " ? 'built' : 'other'; }"
            + " catch (e) { return e.constructor.name; } })()").AsString();

        answer.Should().Be(constructible ? "built" : "TypeError", name);
    }

    /// <summary>
    /// The three singletons keep their members on the interface prototype, which is where a browser's are —
    /// so the object itself has no own property, and <c>Object.keys</c> answers the empty array as it always
    /// did.
    /// </summary>
    [TestCase("crypto", "Crypto", "randomUUID")]
    [TestCase("crypto.subtle", "SubtleCrypto", "digest")]
    [TestCase("performance", "Performance", "now")]
    public void KeepsTheSingletonsMembersOnTheInterfacePrototype(string instance, string name, string member)
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("Object.getOwnPropertyNames(" + instance + ").length").AsNumber().Should().Be(0, instance);
        engine.Evaluate("Object.prototype.hasOwnProperty.call(" + name + ".prototype, '" + member + "')")
            .AsBoolean().Should().BeTrue(name);
        engine.Evaluate("typeof " + instance + "." + member).AsString().Should().Be("function", instance);
        engine.Evaluate("Object.prototype.toString.call(" + instance + ")").AsString().Should().Be("[object " + name + "]");

        // Still one object per realm, and still reached only the way it always was.
        engine.Evaluate(instance + " === " + instance).AsBoolean().Should().BeTrue();
    }
}
#endif

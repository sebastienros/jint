#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The §5.1 interface objects that used not to be globals: the eight Streams interfaces a script never
/// constructs by name, and the three — <c>Crypto</c>, <c>SubtleCrypto</c> and <c>Performance</c> — that had no
/// interface object at all.
/// </summary>
/// <remarks>
/// <para>
/// The rule these are held to is that <c>instanceof</c> answers from a <b>real prototype chain</b>. An
/// interface object whose <c>prototype</c> is not what its instances actually inherit from would make
/// <c>x instanceof Crypto</c> true while <c>Object.getPrototypeOf(x)</c> said otherwise, which is a half-truth
/// a script can see through — so every assertion here asks both questions.
/// </para>
/// <para>
/// Everything else about the install is the family's: a lazy, non-clobbering descriptor on the principal
/// realm's global object, with WebIDL's interface-object attributes (writable and configurable, not
/// enumerable) and never inside a <c>ShadowRealm</c>.
/// </para>
/// </remarks>
public class InterfaceObjectExposureTests
{
    /// <summary>Every interface object this exposure decision added, with the flag that provides it.</summary>
    public static TheoryData<string, WebApiFeatures> Exposed => new()
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

    [Theory]
    [MemberData(nameof(Exposed))]
    public void IsALazyNonEnumerableGlobalBehindItsOwnFlag(string name, WebApiFeatures feature)
    {
        var engine = new Engine(options => options.UseWebApis(feature));

        engine.Evaluate("typeof " + name).AsString().Should().Be("function");

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        descriptor.Enumerable.Should().BeFalse();
        descriptor.Writable.Should().BeTrue();
        descriptor.Configurable.Should().BeTrue();

        // Only behind the flag.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof " + name).AsString().Should().Be("undefined");

        // Never inside a shadow realm.
        engine.Evaluate("new ShadowRealm().evaluate('typeof " + name + "')").AsString().Should().Be("undefined");
    }

    [Theory]
    [MemberData(nameof(Exposed))]
    public void CostsNothingUntilItIsRead(string name, WebApiFeatures feature)
    {
        var engine = new Engine(options => options.UseWebApis(feature));

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);

        // Still flagged CustomJsValue and holding no value means the factory has not run.
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(Exposed))]
    public void LeavesAGlobalTheHostAlreadyOwns(string name, WebApiFeatures feature)
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue(name, marker))
            .UseWebApis(feature));

        engine.Evaluate(name).Should().BeSameAs(marker);
    }

    [Theory]
    [MemberData(nameof(Exposed))]
    public void LeavesAHostLazyGlobalUnmaterialized(string name, WebApiFeatures feature)
    {
        var built = 0;
        var engine = new Engine(options => options
            .AddLazyGlobal(name, _ => { built++; return new JsString("host's own"); })
            .UseWebApis(feature));

        built.Should().Be(0);
        engine.Evaluate(name).AsString().Should().Be("host's own");
        built.Should().Be(1);
    }

    /// <summary>
    /// The whole point: the global names the very object an instance inherits from, so <c>instanceof</c> and
    /// <c>Object.getPrototypeOf</c> agree.
    /// </summary>
    [Theory]
    [InlineData("ReadableStreamDefaultReader", "new ReadableStream().getReader()")]
    [InlineData("ReadableStreamBYOBReader", "new ReadableStream({ type: 'bytes' }).getReader({ mode: 'byob' })")]
    [InlineData("WritableStreamDefaultWriter", "new WritableStream().getWriter()")]
    [InlineData("ReadableStreamDefaultController", "captured(c => new ReadableStream({ start: c }))")]
    [InlineData("ReadableByteStreamController", "captured(c => new ReadableStream({ type: 'bytes', start: c }))")]
    [InlineData("WritableStreamDefaultController", "captured(c => new WritableStream({ start: c }))")]
    [InlineData("TransformStreamDefaultController", "captured(c => new TransformStream({ start: c }))")]
    public void AStreamInstanceInheritsFromTheGlobalOfThatName(string name, string instance)
    {
        var engine = new Engine(options => options.UseWebApis());
        engine.Execute("function captured(build) { var seen; build(c => { seen = c; }); return seen; }");

        engine.Evaluate(instance + " instanceof " + name).AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(" + instance + ") === " + name + ".prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(" + instance + ").constructor === " + name).AsBoolean().Should().BeTrue();
        engine.Evaluate(name + ".name").AsString().Should().Be(name);
    }

    [Fact]
    public void AByobRequestInheritsFromTheGlobalOfThatName()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Execute("""
            var seen = false;
            var proto = false;
            var stream = new ReadableStream({
                type: 'bytes',
                autoAllocateChunkSize: 16,
                pull(controller) {
                    var request = controller.byobRequest;
                    if (request) {
                        seen = request instanceof ReadableStreamBYOBRequest;
                        proto = Object.getPrototypeOf(request) === ReadableStreamBYOBRequest.prototype;
                        request.view[0] = 1;
                        request.respond(1);
                    }
                }
            });
            stream.getReader().read();
            """);

        engine.Evaluate("seen").AsBoolean().Should().BeTrue();
        engine.Evaluate("proto").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// WebIDL gives an interface object a <c>[[Construct]]</c> only when the interface declares a constructor
    /// operation; every other interface object is a function that refuses to construct
    /// (https://webidl.spec.whatwg.org/#es-interface-call).
    /// </summary>
    [Theory]
    [InlineData("ReadableStreamDefaultController")]
    [InlineData("ReadableByteStreamController")]
    [InlineData("ReadableStreamBYOBRequest")]
    [InlineData("WritableStreamDefaultController")]
    [InlineData("TransformStreamDefaultController")]
    [InlineData("Crypto")]
    [InlineData("SubtleCrypto")]
    [InlineData("Performance")]
    public void ANonConstructibleInterfaceObjectRefusesNew(string name)
    {
        var engine = new Engine(options => options.UseWebApis());

        Throws(engine, "new " + name + "()").Should().Be("TypeError");
    }

    /// <summary>
    /// The three interfaces WinterTC §5.1 lists that had no interface object at all. They are real now: the
    /// members live on the interface prototype object, the instance inherits from it, and the brand check is
    /// what it always was.
    /// </summary>
    [Theory]
    [InlineData("crypto", "Crypto", "randomUUID")]
    [InlineData("crypto.subtle", "SubtleCrypto", "digest")]
    [InlineData("performance", "Performance", "now")]
    public void TheThreeSingletonsAreRealInstancesOfTheirInterface(string instance, string name, string member)
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate(instance + " instanceof " + name).AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(" + instance + ") === " + name + ".prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(" + name + ".prototype) === Object.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate(name + ".prototype.constructor === " + name).AsBoolean().Should().BeTrue();
        engine.Evaluate(name + ".name").AsString().Should().Be(name);

        // The members are the prototype's, which is what WebIDL says and what a browser shows.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(" + name + ".prototype, '" + member + "')").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.hasOwnProperty.call(" + instance + ", '" + member + "')").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof " + instance + "." + member).AsString().Should().Be("function");

        // @@toStringTag rides the prototype too.
        engine.Evaluate("Object.prototype.toString.call(" + instance + ")").AsString().Should().Be("[object " + name + "]");

        // The object is still its own singleton and enumerates as empty, exactly as in a browser.
        engine.Evaluate("Object.keys(" + instance + ").length").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The brand check every member of those three performs: an extracted member called on something else
    /// raises a <c>TypeError</c>, which is what stops the prototype being usable on an ordinary object.
    /// </summary>
    [Theory]
    [InlineData("Crypto.prototype.randomUUID.call({})")]
    [InlineData("Object.getOwnPropertyDescriptor(Crypto.prototype, 'subtle').get.call({})")]
    [InlineData("Performance.prototype.now.call({})")]
    [InlineData("Object.getOwnPropertyDescriptor(Performance.prototype, 'timeOrigin').get.call({})")]
    public void APrototypeMemberBrandChecksItsReceiver(string expression)
    {
        var engine = new Engine(options => options.UseWebApis());

        Throws(engine, expression).Should().Be("TypeError");
    }

    /// <summary>
    /// <c>crypto.subtle</c> is still one object per realm, reached only through <c>crypto</c> — the interface
    /// object being nameable does not make a second one constructible.
    /// </summary>
    [Fact]
    public void SubtleCryptoIsStillOneObjectPerRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("crypto.subtle === crypto.subtle").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto.subtle instanceof SubtleCrypto").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ThePerformanceTimelineStillWorksThroughThePrototype()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Execute("performance.mark('a'); performance.measure('m', 'a');");
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(2);
        engine.Evaluate("performance.getEntriesByName('a')[0] instanceof PerformanceMark").AsBoolean().Should().BeTrue();

        engine.Execute("performance.clearMarks(); performance.clearMeasures();");
        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(0);
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis());

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var reader = new ReadableStream().getReader();");
        engine.Evaluate("reader instanceof ReadableStreamDefaultReader").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto instanceof Crypto").AsBoolean().Should().BeTrue();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("ReadableStreamDefaultReader");
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        descriptor._value.Should().BeNull();

        engine.Evaluate("new ReadableStream().getReader() instanceof ReadableStreamDefaultReader").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto instanceof Crypto").AsBoolean().Should().BeTrue();
        engine.Evaluate("performance instanceof Performance").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>Engine.Advanced.EnableWebApis</c> is the same install on a live engine, so it grants the same
    /// interface objects.
    /// </summary>
    [Fact]
    public void ArrivesThroughTheLiveDoorToo()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof ReadableStreamDefaultReader").AsString().Should().Be("undefined");
        engine.Evaluate("typeof Crypto").AsString().Should().Be("undefined");

        engine.Advanced.EnableWebApis(WebApiFeatures.Streams | WebApiFeatures.Crypto | WebApiFeatures.Performance);

        engine.Evaluate("new ReadableStream().getReader() instanceof ReadableStreamDefaultReader").AsBoolean().Should().BeTrue();
        engine.Evaluate("crypto instanceof Crypto").AsBoolean().Should().BeTrue();
        engine.Evaluate("performance instanceof Performance").AsBoolean().Should().BeTrue();
    }

    /// <summary>The constructor name of whatever <paramref name="expression"/> throws, or "no throw".</summary>
    private static string Throws(Engine engine, string expression)
    {
        return engine.Evaluate(
            "(function () { try { " + expression + "; return 'no throw'; } catch (e) { return e.constructor.name; } })()")
            .AsString();
    }
}
#endif

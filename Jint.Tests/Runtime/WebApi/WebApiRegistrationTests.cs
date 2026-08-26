#if NET8_0_OR_GREATER
#nullable enable

using System.Text.Json;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// How the opt-in web globals are installed: what they are made of, who does not get them, and what a global
/// snapshot restore does to them.
/// </summary>
public class WebApiRegistrationTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis());

    [Test]
    public void InstallsUnmaterializedLazyDescriptors()
    {
        var engine = WebEngine();

        foreach (var name in new[] { "console", "DOMException", "QuotaExceededError" })
        {
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);

            descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
            // Still flagged CustomJsValue means the factory has not run: enabling a feature nobody uses costs
            // one descriptor and nothing else.
            (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
            descriptor._value.Should().BeNull();
        }
    }

    [Test]
    public void GivesEachGlobalTheAttributesWebIdlAsksFor()
    {
        var engine = WebEngine();

        // An interface object is writable and configurable but not enumerable.
        var domException = engine.Realm.GlobalObject.GetOwnProperty("DOMException");
        domException.Writable.Should().BeTrue();
        domException.Configurable.Should().BeTrue();
        domException.Enumerable.Should().BeFalse();

        // console is an ordinary enumerable data property — a documented simplification of the WebIDL
        // namespace-object accessor pair.
        var console = engine.Realm.GlobalObject.GetOwnProperty("console");
        console.Writable.Should().BeTrue();
        console.Configurable.Should().BeTrue();
        console.Enumerable.Should().BeTrue();
    }

    [Test]
    public void InstallsDomExceptionForAnyFeatureButNotForNone()
    {
        // DOMException has no flag of its own: it exists whenever any web API does. Neither has the one
        // interface WebIDL derives from it, https://webidl.spec.whatwg.org/#quotaexceedederror, and for the
        // same reason — several features report a refusal with it.
        var enabled = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        enabled.Evaluate("typeof DOMException").AsString().Should().Be("function");
        enabled.Evaluate("typeof QuotaExceededError").AsString().Should().Be("function");

        var none = new Engine(options => options.WebApi.Features = WebApiFeatures.None);
        none.Evaluate("typeof DOMException").AsString().Should().Be("undefined");
        none.Evaluate("typeof QuotaExceededError").AsString().Should().Be("undefined");
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = WebEngine();

        // Only the principal realm's global object is touched. This is deliberately more conservative than a
        // browser, where these APIs are [Exposed=*].
        engine.Evaluate("new ShadowRealm().evaluate('typeof console')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof DOMException')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof QuotaExceededError')").AsString().Should().Be("undefined");

        // ... and the outer realm still has them.
        engine.Evaluate("typeof console").AsString().Should().Be("object");
    }

    [Test]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = WebEngine();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("console.log('first'); var e = new DOMException('x');");
        engine.Realm.GlobalObject.GetOwnProperty("console")._value.Should().NotBeNull();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The descriptor is back to the unmaterialized state it was captured in, so the next read runs the
        // factory again — which does NOT mean a new object. See the three tests below for what it does mean.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("console");
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        descriptor._value.Should().BeNull();

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("new DOMException('y', 'AbortError').code").AsNumber().Should().Be(20);
        engine.Evaluate("new QuotaExceededError('y', { quota: 1, requested: 2 }).requested").AsNumber().Should().Be(2);
        engine.Evaluate("typeof e").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// What a restore actually does to a web-API global, stated as an honesty pin rather than as the
    /// reassurance three sites used to give. The descriptor is reverted; the object behind it is not,
    /// because the factory is a read of a realm intrinsic that memoizes — the same factory shape, over the
    /// same memo, that <c>Math</c> and <c>JSON</c> have. A pooled host therefore gets the previous cycle's
    /// <c>console</c>, with the previous cycle's monkey-patches on it.
    /// </summary>
    /// <remarks>
    /// The pin is deliberately two-sided: object identity AND the surviving mutation. Asserting only that
    /// <c>typeof console === "object"</c> after a restore is what let the claim drift in the first place.
    /// </remarks>
    [Test]
    public void AWebApiSingletonIsTheSameObjectAfterARestoreAndKeepsWhatTheScriptDidToIt()
    {
        var engine = WebEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var before = engine.Evaluate("console");
        engine.Execute("console.scribble = 1; console.log = function () { return 'hijacked'; };");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        var after = engine.Evaluate("console");

        after.Should().BeSameAs(before);
        engine.Evaluate("console.scribble").AsNumber().Should().Be(1);
        engine.Evaluate("console.log()").AsString().Should().Be("hijacked");
    }

    /// <summary>
    /// The same for a singleton whose members live on an interface prototype rather than on itself, which
    /// is the case that makes "rebuild the singleton" an answer that would not help even if it were taken:
    /// the patch is on <c>Crypto.prototype</c>, a separately memoized intrinsic, so a freshly built
    /// <c>crypto</c> would inherit it anyway.
    /// </summary>
    [Test]
    public void APatchOnAnInterfacePrototypeSurvivesARestoreToo()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Crypto));
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("Crypto.prototype.getRandomValues = function () { return 'hijacked'; };");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("String(crypto.getRandomValues(new Uint8Array(1)))").AsString().Should().Be("hijacked");
        engine.Evaluate("crypto instanceof Crypto").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The exhaustive form, so the claim cannot drift again on one global while the others are checked.
    /// Every string-keyed global of a fully-featured engine whose value is an object is read, mutated,
    /// restored and read again, and the set that came back as a <em>different</em> object has to be exactly
    /// <see cref="RebuiltByARestore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two-sided on purpose, like the WPT exclusion table: a web-API global that starts being rebuilt fails
    /// this, and so does one of the nine below that stops being. Either is a deliberate decision to make,
    /// not a table to widen.
    /// </para>
    /// <para>
    /// The nine that are rebuilt are the <c>[JsFunction]</c> slots of the global object's own builtin
    /// shape, and they are the reason the rule has to be phrased about <em>where the value lives</em>
    /// rather than about laziness: those slots are the only place their function object exists, so putting
    /// the slot back to unmaterialized really does mean the next read builds a new one. Every other global
    /// here is a lazy descriptor in front of a realm intrinsic that outlives it, so reverting the descriptor
    /// reverts a cache and nothing else.
    /// </para>
    /// </remarks>
    [Test]
    public void OnlyTheGlobalObjectsOwnFunctionSlotsAreRebuiltByARestore()
    {
        var probe = FullyFeaturedEngine();
        var names = new List<string>();
        foreach (var key in probe.Realm.GlobalObject.GetOwnPropertyKeys(Types.String))
        {
            names.Add(key.ToString());
        }

        names.Sort(StringComparer.Ordinal);
        names.Should().HaveCountGreaterThan(100, "the engine under test should carry the whole web-API surface");

        var rebuilt = new List<string>();
        var examined = 0;

        foreach (var name in names)
        {
            // `globalThis` is the global object itself, so writing to it writes a global binding — which a
            // restore does revert. It is the binding table, not a value in it.
            if (name == "globalThis")
            {
                continue;
            }

            var engine = FullyFeaturedEngine();
            var snapshot = engine.Advanced.CaptureGlobalSnapshot();

            var read = $"globalThis[{JsonSerializer.Serialize(name)}]";
            var before = engine.Evaluate(read);
            if (before is not ObjectInstance)
            {
                continue;
            }

            examined++;
            engine.Execute($"{read}.__scribble__ = 1;");
            engine.Advanced.RestoreGlobalSnapshot(snapshot);

            if (!ReferenceEquals(engine.Evaluate(read), before))
            {
                rebuilt.Add(name);
            }
        }

        examined.Should().BeGreaterThan(100);
        rebuilt.Should().BeEquivalentTo(
            RebuiltByARestore,
            "a restore reverts a descriptor, never whatever the descriptor's factory reads from — so only a "
            + "global whose value has nowhere else to live comes back fresh");
    }

    /// <summary>
    /// The global object's own <c>[JsFunction]</c> slots: the only globals on this engine whose value lives
    /// nowhere but the slot a restore reverts. <c>parseInt</c> and <c>parseFloat</c> are deliberately absent
    /// — they are <c>Intrinsics.ParseInt</c>/<c>ParseFloat</c>, the very function objects
    /// <c>Number.parseInt</c>/<c>parseFloat</c> are, so they memoize like every other intrinsic.
    /// </summary>
    private static readonly string[] RebuiltByARestore =
    [
        "decodeURI", "decodeURIComponent", "encodeURI", "encodeURIComponent",
        "escape", "isFinite", "isNaN", "toString", "unescape",
    ];

    private static Engine FullyFeaturedEngine() => new(options => options
        .UseWebApis(WebApiFeatures.Default)
        .UseStorage()
        .UseCacheApi());

    [Test]
    public void InstallsTheEventAndAbortInterfaceObjectsBehindTheEventsFlag()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        foreach (var name in new[] { "Event", "CustomEvent", "EventTarget", "AbortController", "AbortSignal" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");

            // Interface objects: writable and configurable, but not enumerable.
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();

            // ... and only behind the flag.
            new Engine(options => options.UseWebApis(WebApiFeatures.Console))
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined");

            // ... and never inside a shadow realm.
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Test]
    public void InstallsTheFetchInterfaceObjectsBehindTheFetchFlag()
    {
        var engine = new Engine(options => options.UseFetch());

        foreach (var name in new[] { "Headers", "Request", "Response" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");

            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);
            descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
            descriptor.Enumerable.Should().BeFalse();
            descriptor.Writable.Should().BeTrue();
            descriptor.Configurable.Should().BeTrue();

            // ... and only behind the flag: the default set never carries it.
            new Engine(options => options.UseWebApis())
                .Evaluate($"typeof {name}").AsString().Should().Be("undefined");

            // ... and never inside a shadow realm.
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Test]
    public void FetchBringsTheFeaturesItsSurfaceIsBuiltFrom()
    {
        // The closure is computed at install, so it catches a host that assigned Features directly rather
        // than calling UseFetch.
        foreach (var options in new[] { new Options().UseFetch(), Assigned() })
        {
            var engine = new Engine(options);

            engine.Evaluate("typeof AbortController").AsString().Should().Be("function");
            engine.Evaluate("typeof URL").AsString().Should().Be("function");
            engine.Evaluate("typeof Blob").AsString().Should().Be("function");

            // ... and the option value still reads back exactly what the host asked for.
            options.WebApi.Features.Should().Be(WebApiFeatures.Fetch);
        }

        static Options Assigned()
        {
            var options = new Options();
            options.WebApi.Features = WebApiFeatures.Fetch;
            return options;
        }
    }

    [Test]
    public void LeavesAGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("console", marker))
            .UseWebApis());

        engine.Evaluate("console").Should().BeSameAs(marker);
    }

    /// <summary>
    /// The non-clobbering probe needs a <see cref="JsString"/> for each name it asks about, and
    /// <c>JsString.Create</c> allocates a fresh one for anything longer than a character — so an engine that
    /// enabled the web APIs used to build one throwaway object per installed global. They are interned
    /// process-wide instead, which measured at 2,208 bytes off the construction allocation of a
    /// <see cref="WebApiFeatures.Default"/> engine.
    /// </summary>
    [Test]
    public void InternsTheGlobalNamesItProbesWith()
    {
        WebApiRegistration.NameOf("console").Should().BeSameAs(WebApiRegistration.NameOf("console"));

        // A JsString is immutable and realm-independent, which is what makes one table safe for every engine
        // in the process — unlike the objects the descriptors produce, which stay per engine.
        var first = new Engine(options => options.UseWebApis());
        var second = new Engine(options => options.UseWebApis());
        first.Evaluate("console").Should().NotBeSameAs(second.Evaluate("console"));
    }

    [Test]
    public void LeavesAHostLazyGlobalUnmaterialized()
    {
        var built = 0;
        var engine = new Engine(options => options
            .AddLazyGlobal("console", _ => { built++; return new JsString("host's own"); })
            .UseWebApis());

        // The non-clobbering check probes rather than reads, so looking does not force the host's factory.
        built.Should().Be(0);
        engine.Evaluate("console").AsString().Should().Be("host's own");
        built.Should().Be(1);
    }
}
#endif

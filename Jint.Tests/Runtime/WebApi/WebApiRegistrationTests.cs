#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// How the opt-in web globals are installed: what they are made of, who does not get them, and what a global
/// snapshot restore does to them.
/// </summary>
public class WebApiRegistrationTests
{
    private static Engine WebEngine() => new(options => options.UseWebApis());

    [Fact]
    public void InstallsUnmaterializedLazyDescriptors()
    {
        var engine = WebEngine();

        foreach (var name in new[] { "console", "DOMException" })
        {
            var descriptor = engine.Realm.GlobalObject.GetOwnProperty(name);

            descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
            // Still flagged CustomJsValue means the factory has not run: enabling a feature nobody uses costs
            // one descriptor and nothing else.
            (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
            descriptor._value.Should().BeNull();
        }
    }

    [Fact]
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

    [Fact]
    public void InstallsDomExceptionForAnyFeatureButNotForNone()
    {
        // DOMException has no flag of its own: it exists whenever any web API does.
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof DOMException").AsString().Should().Be("function");

        new Engine(options => options.WebApi.Features = WebApiFeatures.None)
            .Evaluate("typeof DOMException").AsString().Should().Be("undefined");
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = WebEngine();

        // Only the principal realm's global object is touched. This is deliberately more conservative than a
        // browser, where these APIs are [Exposed=*].
        engine.Evaluate("new ShadowRealm().evaluate('typeof console')").AsString().Should().Be("undefined");
        engine.Evaluate("new ShadowRealm().evaluate('typeof DOMException')").AsString().Should().Be("undefined");

        // ... and the outer realm still has them.
        engine.Evaluate("typeof console").AsString().Should().Be("object");
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = WebEngine();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("console.log('first'); var e = new DOMException('x');");
        engine.Realm.GlobalObject.GetOwnProperty("console")._value.Should().NotBeNull();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The descriptor is back to the unmaterialized state it was captured in, so the next read rebuilds
        // the object rather than serving the previous cycle's.
        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("console");
        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        descriptor._value.Should().BeNull();

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("new DOMException('y', 'AbortError').code").AsNumber().Should().Be(20);
        engine.Evaluate("typeof e").AsString().Should().Be("undefined");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void LeavesAGlobalTheHostAlreadyOwns()
    {
        var marker = new JsString("host's own");
        var engine = new Engine(options => options
            .Configure(e => e.SetValue("console", marker))
            .UseWebApis());

        engine.Evaluate("console").Should().BeSameAs(marker);
    }

    [Fact]
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

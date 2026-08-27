#nullable enable

using System.Globalization;
using Jint.Constraints;
using Jint.Runtime.Modules;
#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.WebApi;
#endif

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host can read back the configuration of an engine it was handed.
/// </summary>
/// <remarks>
/// <para>
/// <c>Engine.Options</c> was internal through 4.16.x, so a component given nothing but an <see cref="Engine"/>
/// had no supported way to answer "what is this engine allowed to do". The options are frozen once an engine
/// has read them, so handing the instance back is a read and nothing else.
/// </para>
/// <para>
/// This project has no <c>InternalsVisibleTo</c>, so everything asserted here is reachable by a third party.
/// </para>
/// </remarks>
public class HostEngineConfigurationTests
{
    [Test]
    public void AHostReadsBackWhatItConfigured()
    {
        var loader = new StubModuleLoader();
        var limits = new ResultLimits { MaxDepth = 7 };

        var options = new Options();
        options.Strict = true;
        options.Culture = CultureInfo.InvariantCulture;
        options.TimeZone = TimeZoneInfo.Utc;
        options.ResultLimits = limits;
        options.Constraints.MaxRecursionDepth = 12;
        options.Interop.Enabled = true;
        options.Interop.AllowWrite = true;
        options.Json.MaxParseDepth = 9;
        options.UseModules(loader);
        options.AddImmutableCrossing(typeof(Version));

        var engine = new Engine(options);

        engine.Options.Strict.Should().BeTrue();
        engine.Options.Culture.Should().BeSameAs(CultureInfo.InvariantCulture);
        engine.Options.TimeZone.Should().Be(TimeZoneInfo.Utc);
        engine.Options.ResultLimits.Should().BeSameAs(limits);
        engine.Options.Constraints.MaxRecursionDepth.Should().Be(12);
        engine.Options.Interop.Enabled.Should().BeTrue();
        engine.Options.Interop.AllowWrite.Should().BeTrue();
        engine.Options.Json.MaxParseDepth.Should().Be(9);
        engine.Options.Modules.ModuleLoader.Should().BeSameAs(loader);
        engine.Options.Interop.ImmutableCrossingTypes.Should().ContainSingle().Which.Should().Be(typeof(Version));
    }

    /// <summary>
    /// The engine keeps the caller's own instance rather than a copy, which is what makes reading it back the
    /// same answer as reading the object the host still holds.
    /// </summary>
    [Test]
    public void TheEngineHandsBackTheInstanceItWasBuiltFrom()
    {
        var options = new Options();
        var engine = new Engine(options);

        engine.Options.Should().BeSameAs(options);
        engine.Options.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void AConfigurationCallbackIsVisibleOnTheEngineItConfigured()
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 1234);

        engine.Options.Constraints.MaxArraySize.Should().Be(1234u);
    }

    [Test]
    public void ASetterOnAReadBackInstanceThrows()
    {
        var engine = new Engine();

        Invoking(() => engine.Options.Strict = true)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Options.Strict cannot be changed*");

        Invoking(() => engine.Options.Interop.AllowWrite = true)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Options.Interop.AllowWrite cannot be changed*");

        // Including the half of the configuration that grows.
        Invoking(() => engine.Options.Interop.AllowedAssemblies.Add(typeof(string).Assembly))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => engine.Options.AddLazyGlobal("x", static _ => Native.JsValue.Undefined))
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Two engines built with a callback each configure their own instance, so what one is allowed to do says
    /// nothing about what the other is.
    /// </summary>
    [Test]
    public void TwoEnginesConfiguredByCallbackDoNotShareOneConfiguration()
    {
        var first = new Engine(options => options.Interop.Enabled = true);
        var second = new Engine(options => options.Strict = true);

        first.Options.Should().NotBeSameAs(second.Options);
        first.Options.Interop.Enabled.Should().BeTrue();
        first.Options.Strict.Should().BeFalse();
        second.Options.Interop.Enabled.Should().BeFalse();
        second.Options.Strict.Should().BeTrue();
    }

    /// <summary>
    /// <c>new Engine()</c> reads one process-wide instance of the defaults, which is now something a host can
    /// see rather than something it has to infer.
    /// </summary>
    /// <remarks>
    /// It is the sharing behind the cross-tenant leak in #3331. What that issue's fix closed is the writing —
    /// the one door that configures a live engine takes it a copy of its own first — and not the sharing,
    /// which carries nothing tenant-specific: the instance is frozen by the first engine built from it and
    /// every setting on it is a default.
    /// </remarks>
    [Test]
    public void TwoDefaultBuiltEnginesReadBackOneSharedConfigurationOfDefaults()
    {
        var first = new Engine();
        var second = new Engine();

        first.Options.Should().BeSameAs(second.Options);
        first.Options.IsReadOnly.Should().BeTrue();
        first.Options.Interop.Enabled.Should().BeFalse();
        first.Options.Strict.Should().BeFalse();
    }

    /// <summary>
    /// A hardened engine keeps a private copy, so reading it back answers what the engine runs under rather
    /// than what the host declared.
    /// </summary>
    [Test]
    public void AnUntrustedProfileReadsBackAsTheEnginesOwnHardenedCopy()
    {
        var options = new Options();
        options.Interop.Enabled = true;
        options.ForUntrustedCode(new UntrustedCodeLimits
        {
            TimeoutInterval = TimeSpan.FromSeconds(5),
            MaxStatements = 500,
            MemoryLimit = 16_000_000,
            MaxRecursionDepth = 64,
            MaxArraySize = 10_000,
            RegexTimeout = TimeSpan.FromMilliseconds(100),
            PromiseTimeout = TimeSpan.FromMilliseconds(100),
            MaxOperationDuration = TimeSpan.FromSeconds(10),
        });

        var engine = new Engine(options);

        engine.Options.Should().NotBeSameAs(options);

        // The grant the host declared is revoked on the engine's copy, and left alone on the host's.
        engine.Options.Interop.Enabled.Should().BeFalse();
        options.Interop.Enabled.Should().BeTrue();

        // ... and what the profile imposed is readable, rather than only observable by tripping it.
        engine.Options.Constraints.MaxRecursionDepth.Should().Be(64);
        engine.Options.Constraints.MaxArraySize.Should().Be(10_000u);
        engine.Options.Host.StringCompilationAllowed.Should().BeFalse();

        // The copy is frozen too.
        engine.Options.IsReadOnly.Should().BeTrue();
        Invoking(() => engine.Options.Interop.Enabled = true).Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A constraint reads back from where it was registered: an instance from the configuration, a factory
    /// registration only from the engine that built one.
    /// </summary>
    [Test]
    public void AConstraintReadsBackFromWhereItWasRegistered()
    {
        var instance = new NoopConstraint();

        var options = new Options();
        options.AddConstraint(instance);
        options.LimitStatements(1000);

        var engine = new Engine(options);

        engine.Options.Constraints.Constraints.Should().ContainSingle().Which.Should().BeSameAs(instance);

        // The budget was registered as a factory, so the instance enforcing it is the engine's own and the
        // configuration never held one. engine.Constraints.Find<T>() is what answers for the live set, and
        // the two questions stay different questions.
        engine.Constraints.Find<MaxStatementsConstraint>().Should().NotBeNull();
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The shape of #3331, read back rather than inferred: a live web-API configuration lands on the engine
    /// that asked for it, and the sibling sharing the defaults is untouched.
    /// </summary>
    [Test]
    public void ALiveWebApiConfigurationIsReadBackOnTheEngineThatAskedForItAndNoOther()
    {
        var tenant = new Engine();
        var other = new Engine();

        // They start out reading one instance...
        tenant.Options.Should().BeSameAs(other.Options);

        var client = new HttpClient(new StubHandler());
        tenant.WebApi.Enable(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = client);

        // ... and the engine that was configured took a copy of its own.
        tenant.Options.Should().NotBeSameAs(other.Options);
        tenant.Options.WebApi.Fetch.HttpClient.Should().BeSameAs(client);
        other.Options.WebApi.Fetch.HttpClient.Should().BeNull();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
#endif

    private sealed class NoopConstraint : Constraint
    {
        public override void Check()
        {
        }

        public override void Reset()
        {
        }
    }

    private sealed class StubModuleLoader : IModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => throw new NotSupportedException();

        public ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new NotSupportedException();
    }
}

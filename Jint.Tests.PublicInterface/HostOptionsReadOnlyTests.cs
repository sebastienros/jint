#nullable enable

using System.Globalization;
using System.Reflection;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Options"/> is configuration until an engine reads it, and a frozen record afterwards.
/// </summary>
/// <remarks>
/// <para>
/// The engine keeps the very instance it was handed — <c>Options.CreateEngineOptions</c> returns <c>this</c>
/// unless a hardened profile made a private clone — and reads about thirty of its settings live, on hot
/// paths: <c>Interop.AllowWrite</c> on every projected write, <c>Interop.ExceptionHandler</c> on every CLR
/// call. Six other properties were read once and documented as having no effect afterwards. Which of the two
/// a given property was could only be learnt by reading its documentation, and the first kind meant a host
/// could grant CLR writes to a running engine with an ordinary assignment.
/// </para>
/// <para>
/// So the engine makes the options read-only when it has finished reading them, following
/// <c>JsonSerializerOptions.MakeReadOnly()</c>, and every setter and every registry refuses from that point.
/// This suite is in the project without <c>InternalsVisibleTo</c> because it is a promise to third parties.
/// </para>
/// </remarks>
public class HostOptionsReadOnlyTests
{
    [Test]
    public void AnEngineMakesTheOptionsItWasBuiltFromReadOnly()
    {
        var options = new Options();
        options.IsReadOnly.Should().BeFalse();

        _ = new Engine(options);

        options.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void TheRefusalNamesTheSettingAndIsAnInvalidOperation()
    {
        var options = new Options();
        _ = new Engine(options);

        Invoking(() => options.Interop.AllowWrite = true)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Options.Interop.AllowWrite cannot be changed*");
    }

    /// <summary>
    /// The write that motivated all of this: <c>Interop.AllowWrite</c> is read on every projected write, so
    /// setting it after construction used to hand a running engine the ability to mutate host objects.
    /// </summary>
    [Test]
    public void GrantingClrWritesToALiveEngineIsRefused()
    {
        var options = new Options();
        options.Interop.Enabled = true;
        var target = new Box();
        var engine = new Engine(options);
        engine.SetValue("box", target);

        engine.Execute("box.Value = 1");
        target.Value.Should().Be(0, "writes through projected CLR objects are denied by default");

        Invoking(() => options.Interop.AllowWrite = true).Should().Throw<InvalidOperationException>();

        engine.Execute("box.Value = 2");
        target.Value.Should().Be(0, "and the refused write cannot have granted the engine anything");
    }

    [TestCaseSource(nameof(EveryGroup))]
    public void EveryOptionGroupRefusesAWrite(string name, Action<Options> write)
    {
        var options = new Options();
        _ = new Engine(options);

        Invoking(() => write(options))
            .Should().Throw<InvalidOperationException>($"{name} belongs to a frozen Options")
            .WithMessage($"{name} cannot be changed*");
    }

    /// <summary>
    /// Two registries sit side by side on <c>Options.Constraints</c>: the public <c>Constraints</c> list of
    /// live instances, and the internal factory list that <c>AddConstraint(Func&lt;Constraint&gt;)</c> writes
    /// to. They were constructed with the same name string, so a refused factory registration named the
    /// registry the host had not touched.
    /// </summary>
    /// <remarks>
    /// The <c>Limit*</c> methods do not reach this: each begins with <c>RemoveConstraints</c>, which is
    /// refused first and names the live-instances list correctly. <c>AddConstraint</c> with a factory is
    /// the only way to the second registry, and so the only way to observe the collision.
    /// </remarks>
    [Test]
    public void ARefusedConstraintFactoryNamesItsOwnRegistry()
    {
        var options = new Options();
        _ = new Engine(options);

        Invoking(() => options.AddConstraint(static () => new InertConstraint()))
            .Should().Throw<InvalidOperationException>("the options are frozen")
            .WithMessage("*cannot be changed*")
            .And.Message.Should().NotContain(
                "Options.Constraints.Constraints",
                "the factory registry is not the list of live instances, and naming that one sends the host to a line it never wrote");
    }

    /// <summary>
    /// A constraint that bounds nothing: this fixture only cares which registry refuses the registration.
    /// </summary>
    private sealed class InertConstraint : Constraint
    {
        public override void Check()
        {
        }

        public override void Reset()
        {
        }
    }

    public static TestCases<string, Action<Options>> EveryGroup()
    {
        var data = new TestCases<string, Action<Options>>
        {
            { "Options.Strict", static o => o.Strict = true },
            { "Options.Culture", static o => o.Culture = CultureInfo.InvariantCulture },
            { "Options.TimeZone", static o => o.TimeZone = TimeZoneInfo.Utc },
            { "Options.TimeSystem", static o => o.TimeSystem = null! },
            { "Options.ResultLimits", static o => o.ResultLimits = ResultLimits.Conservative },
            { "Options.AgentCanSuspend", static o => o.AgentCanSuspend = true },
            { "Options.ExperimentalFeatures", static o => o.ExperimentalFeatures = ExperimentalFeature.All },
            { "Options.RetainFunctionSourceText", static o => o.RetainFunctionSourceText = true },
            { "Options.ReferenceResolver", static o => o.SetReferenceResolver(NullPropagatingReferenceResolver.Instance) },
            { "Options.Constraints.MaxRecursionDepth", static o => o.Constraints.MaxRecursionDepth = 8 },
            { "Options.Parsing.MaxNodeCount", static o => o.Parsing.MaxNodeCount = 8 },
            { "Options.Interop.Enabled", static o => o.Interop.Enabled = true },
            { "Options.Debugger.Enabled", static o => o.Debugger.Enabled = true },
            { "Options.Coverage.Enabled", static o => o.Coverage.Enabled = true },
            { "Options.Host.StringCompilationAllowed", static o => o.Host.StringCompilationAllowed = false },
            { "Options.Modules.RegisterRequire", static o => o.Modules.RegisterRequire = true },
            { "Options.Json.MaxParseDepth", static o => o.Json.MaxParseDepth = 8 },
            { "Options.Intl.CldrProvider", static o => o.Intl.CldrProvider = null! },
            { "Options.Temporal.TimeZoneProvider", static o => o.Temporal.TimeZoneProvider = null! },
            { "Options.Profiling.Enabled", static o => o.Profiling.Enabled = true },
            { "Options.Profiling.MaxEvents", static o => o.Profiling.MaxEvents = 8 },
        };

#if NET8_0_OR_GREATER
        data.Add("Options.WebApi.Features", static o => o.WebApi.Features = WebApiFeatures.Console);
        data.Add("Options.WebApi.Console.Sink", static o => o.WebApi.Console.Sink = Jint.WebApi.ConsoleSink.Null);
        data.Add("Options.WebApi.Timers.MaxActiveTimers", static o => o.WebApi.Timers.MaxActiveTimers = 8);
        data.Add("Options.WebApi.Fetch.MaxRedirects", static o => o.WebApi.Fetch.MaxRedirects = 8);
        data.Add("Options.WebApi.Diagnostics.Sink", static o => o.WebApi.Diagnostics.Sink = null);
        data.Add("Options.WebApi.Storage.MaxTotalBytes", static o => o.WebApi.Storage.MaxTotalBytes = 8);
        data.Add("Options.WebApi.Cache.Provider", static o => o.WebApi.Cache.Provider = null);
        data.Add("Options.WebApi.Messaging.Broker", static o => o.WebApi.Messaging.Broker = null);
        data.Add("Options.WebApi.Workers.MaxWorkers", static o => o.WebApi.Workers.MaxWorkers = 8);
#endif

        return data;
    }

    /// <summary>
    /// A group is allocated on first touch, so most of them do not exist when the freeze happens. One
    /// materialized afterwards has to be born frozen, or the very write that was meant to be refused would
    /// be the one that creates a fresh, mutable group — and the engine reads <c>Intl</c> and <c>Temporal</c>
    /// lazily, long after construction, so the write would have landed.
    /// </summary>
    [Test]
    public void AGroupMaterializedAfterTheFreezeIsBornFrozen()
    {
        var options = new Options();
        options.MakeReadOnly();

        Invoking(() => options.Json.MaxParseDepth = 8).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Intl.CldrProvider = null!).Should().Throw<InvalidOperationException>();
        Invoking(() => options.Temporal.CalendarProvider = null!).Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A registry is the half of the configuration that grows, and a plain <see cref="List{T}"/> cannot
    /// refuse. Every <c>Add*</c> verb and every list behind one has to say no as loudly as an assignment.
    /// </summary>
    [TestCaseSource(nameof(EveryRegistryMutation))]
    public void EveryRegistryRefusesAChange(string description, Action<Options> mutate)
    {
        var options = new Options();
        _ = new Engine(options);

        Invoking(() => mutate(options))
            .Should().Throw<InvalidOperationException>($"{description} changes a frozen Options");
    }

    public static TestCases<string, Action<Options>> EveryRegistryMutation()
    {
        var data = new TestCases<string, Action<Options>>
        {
            { "AddConstraint(instance)", static o => o.AddConstraint(new NoopConstraint()) },
            { "AddConstraint(factory)", static o => o.AddConstraint(static () => new NoopConstraint()) },
            { "RemoveConstraints", static o => o.RemoveConstraints(static _ => true) },
            { "LimitStatements", static o => o.LimitStatements(1) },
            { "LimitMemory", static o => o.LimitMemory(1024) },
            { "LimitExecutionTime", static o => o.LimitExecutionTime(TimeSpan.FromSeconds(1)) },
            { "ObserveCancellation", static o => o.ObserveCancellation(new CancellationTokenSource().Token) },
            { "Constraints.Constraints.Add", static o => o.Constraints.Constraints.Add(new NoopConstraint()) },
            { "Constraints.Constraints.Clear", static o => o.Constraints.Constraints.Clear() },
            { "AddObjectConverter", static o => o.AddObjectConverter(new NoopConverter()) },
            { "Interop.ObjectConverters.Add", static o => o.Interop.ObjectConverters.Add(new NoopConverter()) },
            { "AddExtensionMethods", static o => o.AddExtensionMethods(typeof(string)) },
            { "Interop.ExtensionMethodTypes.Add", static o => o.Interop.ExtensionMethodTypes.Add(typeof(string)) },
            { "AddImmutableCrossing", static o => o.AddImmutableCrossing(typeof(string)) },
            { "Interop.ImmutableCrossingTypes.AddRange", static o => o.Interop.ImmutableCrossingTypes.AddRange([typeof(string)]) },
            { "AllowClr()", static o => o.AllowClr() },
            { "AllowClr(assemblies)", static o => o.AllowClr(typeof(string).Assembly) },
            { "Interop.AllowedAssemblies.Add", static o => o.Interop.AllowedAssemblies.Add(typeof(string).Assembly) },
            { "Interop.AllowedAssemblies[0] =", static o => o.Interop.AllowedAssemblies.Insert(0, typeof(string).Assembly) },
            { "AddLazyGlobal", static o => o.AddLazyGlobal("x", static _ => Native.JsValue.Undefined) },
            { "Configure", static o => o.Configure(static _ => { }) },
            { "SetTypeConverter", static o => o.SetTypeConverter(static engine => new DefaultTypeConverter(engine)) },
            { "SetReferenceResolver", static o => o.SetReferenceResolver(NullPropagatingReferenceResolver.Instance) },
            { "UseHostFactory", static o => o.UseHostFactory(static _ => new Jint.Runtime.Host()) },
            { "UseModules", static o => o.UseModules(new StubModuleLoader()) },
            { "CatchClrExceptions", static o => o.CatchClrExceptions() },
            { "ExposeDetailedErrors", static o => o.ExposeDetailedErrors() },
            { "ForUntrustedCode", static o => o.ForUntrustedCode(Limits) },
            { "UseNodeBuiltinModules", static o => o.UseNodeBuiltinModules() },
            { "UseNodeProcess", static o => o.UseNodeProcess() },
        };

#if NET8_0_OR_GREATER
        data.Add("WebApi.Fetch.AllowedSchemes.Add", static o => o.WebApi.Fetch.AllowedSchemes.Add("ftp"));
        data.Add("WebApi.Fetch.AllowedSchemes.Clear", static o => o.WebApi.Fetch.AllowedSchemes.Clear());
        data.Add("UseWebApis", static o => o.UseWebApis());
#endif

        return data;
    }

    /// <summary>
    /// Sharing one configured <see cref="Options"/> between engines is a documented embedding pattern, so a
    /// second freeze has to be a no-op rather than a refusal.
    /// </summary>
    [Test]
    public void TheSameOptionsInstanceStillBuildsASecondEngine()
    {
        var options = new Options();
        options.Strict = true;
        options.AddLazyGlobal("answer", static _ => 42);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Evaluate("answer").AsNumber().Should().Be(42);
        second.Evaluate("answer").AsNumber().Should().Be(42);
        options.IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void MakeReadOnlyIsIdempotentAndAvailableToTheHost()
    {
        var options = new Options();
        options.Interop.Enabled = true;

        options.MakeReadOnly();
        options.MakeReadOnly();
        options.IsReadOnly.Should().BeTrue();

        Invoking(() => options.Interop.Enabled = false).Should().Throw<InvalidOperationException>();

        // A frozen instance is still perfectly good engine input, and the engine reads it as it stands.
        var engine = new Engine(options);
        options.Interop.Enabled.Should().BeTrue();
        engine.Evaluate("typeof importNamespace").AsString().Should().Be("function");
    }

    /// <summary>
    /// The hardened profile is the one case where the engine keeps a private clone rather than the caller's
    /// instance. The clone has to be mutable while the profile is expanded onto it, and frozen once the
    /// engine has read it — and the caller's own object is frozen too, so that "an engine makes the options
    /// you handed it read-only" needs no exception for the profiled case.
    /// </summary>
    [Test]
    public void AnUntrustedProfileStillAppliesAndBothInstancesEndUpReadOnly()
    {
        var options = new Options();
        options.Interop.Enabled = true;
        options.ForUntrustedCode(Limits with { MaxStatements = 500 });

        var engine = new Engine(options);

        options.IsReadOnly.Should().BeTrue();

        // The profile revoked the CLR grant on the engine's own copy, and left the caller's reading alone.
        engine.Evaluate("typeof importNamespace").AsString().Should().Be("undefined");
        options.Interop.Enabled.Should().BeTrue();

        // The same thing said of the configuration rather than of its effect, which is only sayable because
        // the engine hands its own copy back.
        engine.Options.Should().NotBeSameAs(options);
        engine.Options.Interop.Enabled.Should().BeFalse();
        engine.Options.IsReadOnly.Should().BeTrue();

        // ... and the budget it registered is the engine's, not a value nobody enforces.
        Invoking(() => engine.Execute("for (let i = 0; i < 1000000; i++) {}"))
            .Should().Throw<StatementsCountOverflowException>();

        // The same declaration still hardens the next engine built from it.
        var second = new Engine(options);
        second.Evaluate("typeof importNamespace").AsString().Should().Be("undefined");
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// <c>Engine.WebApi.Enable</c> is the one door that writes to a frozen <see cref="Options"/>, and
    /// the suspension it opens is scoped to the group it is configuring — not to web-API groups at large, and
    /// not to the registries.
    /// </summary>
    /// <remarks>
    /// The group it hands the callback is the engine's own copy of the web-API subtree, so the host's own
    /// instance is not written to at all; <see cref="HostWebApiOptionsIsolationTests"/> is where that is
    /// pinned from the outside.
    /// </remarks>
    [Test]
    public void TheLiveWebApiDoorSuspendsTheGuardForTheGroupItIsConfiguringAndNothingElse()
    {
        var other = new Options();
        _ = new Engine(other);

        var options = new Options();
        var engine = new Engine(options);
        var ran = false;

        engine.WebApi.Enable(WebApiFeatures.Timers, webApi =>
        {
            ran = true;

            // The group being configured: a value setting is writable for the duration of the callback.
            webApi.Timers.MaxActiveTimers = 3;

            // A different frozen Options is not opened by it, even on this thread.
            Invoking(() => other.WebApi.Timers.MaxActiveTimers = 3)
                .Should().Throw<InvalidOperationException>();

            // Nor is a registry, on either instance: a live enable sets a value, it never grows a registry.
            Invoking(() => webApi.Fetch.AllowedSchemes.Add("ftp"))
                .Should().Throw<InvalidOperationException>();
        });

        ran.Should().BeTrue();

        // The engine got the setting, on the copy of the subtree it took for itself...
        engine.WebApi.Features.Should().HaveFlag(WebApiFeatures.Timers);

        // ... and this instance, which the host may be sharing with any number of other engines, was not
        // written to at all.
        options.WebApi.Timers.MaxActiveTimers.Should().Be(1000);

        // ... and the suspension ends with the call.
        Invoking(() => options.WebApi.Timers.MaxActiveTimers = 4)
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// The engine's private copy of the web-API subtree is frozen around registries that are frozen too.
    /// </summary>
    /// <remarks>
    /// The only public door onto a group cloned from a frozen source. <c>WebApiOptions.Clone</c> copies each
    /// sub-group with <c>MemberwiseClone</c>, which carries the frozen state, but a registry has to be a new
    /// list or the copy would share the host's — and a new <see cref="OptionsList{T}"/> used to be born
    /// writable, so this copy came back frozen around an <c>AllowedSchemes</c> that still accepted an
    /// <c>Add</c>. The live door is for setting a value on the subtree, never for growing a registry on it,
    /// and <see cref="TheLiveWebApiDoorSuspendsTheGuardForTheGroupItIsConfiguringAndNothingElse"/> pins the
    /// same refusal for the sub-group nobody had materialized before the freeze — which reaches the copy
    /// through the accessor rather than through the clone.
    /// </remarks>
    [Test]
    public void TheEnginesPrivateWebApiCopyIsFrozenAroundFrozenRegistries()
    {
        var options = new Options();

        // Materialized before the freeze, so the engine's copy of it comes from Clone rather than from the
        // accessor that would have made it born frozen.
        options.WebApi.Fetch.MaxRedirects = 5;

        var engine = new Engine(options);
        options.WebApi.Fetch.AllowedSchemes.IsReadOnly.Should().BeTrue();

        Options.WebApiOptions? privateCopy = null;
        engine.WebApi.Enable(WebApiFeatures.Timers, webApi =>
        {
            privateCopy = webApi;
            webApi.Should().NotBeSameAs(options.WebApi);
            webApi.Fetch.Should().NotBeSameAs(options.WebApi.Fetch);

            webApi.Fetch.AllowedSchemes.IsReadOnly.Should().BeTrue(
                "the copy is frozen, and a registry inside a frozen group is frozen too");
            Invoking(() => webApi.Fetch.AllowedSchemes.Add("ftp"))
                .Should().Throw<InvalidOperationException>();
        });

        privateCopy.Should().NotBeNull();

        // And the host's own instance was not written to at all.
        options.WebApi.Fetch.AllowedSchemes.Should().NotContain("ftp");
    }
#endif

    /// <summary>
    /// A configuration callback runs while the engine is being built, which is before the freeze — otherwise
    /// the documented way to finish configuring an engine would have stopped working.
    /// </summary>
    [Test]
    public void AConfigurationCallbackStillWritesWhileTheEngineIsBeingBuilt()
    {
        var options = new Options();
        options.Configure(_ => options.Interop.AllowGetType = true);

        _ = new Engine(options);

        options.Interop.AllowGetType.Should().BeTrue();
        Invoking(() => options.Interop.AllowGetType = false).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ReadingIsUnaffected()
    {
        var options = new Options();
        options.Constraints.MaxRecursionDepth = 12;
        options.Interop.AllowedAssemblies.Add(typeof(string).Assembly);

        _ = new Engine(options);

        options.Constraints.MaxRecursionDepth.Should().Be(12);
        options.Interop.AllowedAssemblies.Should().ContainSingle();
        options.Interop.AllowedAssemblies[0].Should().BeSameAs(typeof(string).Assembly);
        options.Interop.AllowedAssemblies.IsReadOnly.Should().BeTrue();
        options.Interop.ObjectConverters.IsReadOnly.Should().BeTrue();
    }

    private static readonly UntrustedCodeLimits Limits = new()
    {
        TimeoutInterval = TimeSpan.FromSeconds(5),
        MaxStatements = 100_000,
        MemoryLimit = 16_000_000,
        MaxRecursionDepth = 64,
        MaxArraySize = 10_000,
        RegexTimeout = TimeSpan.FromMilliseconds(100),
        PromiseTimeout = TimeSpan.FromMilliseconds(100),
        MaxOperationDuration = TimeSpan.FromSeconds(10),
    };

    private sealed class Box
    {
        public int Value { get; set; }
    }

    private sealed class NoopConverter : ObjectConverter
    {
        public override bool TryConvert(Engine engine, object value, out Native.JsValue result)
        {
            result = Native.JsValue.Undefined;
            return false;
        }
    }

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

        public Jint.Runtime.Modules.ModuleRecord LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new NotSupportedException();
    }
}

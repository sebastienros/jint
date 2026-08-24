#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers declaring the CLR <em>target</em> types a <see cref="ClrTypeConverter"/> converts to, which lets the
/// engine keep its compiled member-write and method-invoker lanes — and its share of the accessor cache — for
/// everything the declaration excludes. These live in the public-interface suite on purpose: the project
/// references Jint without any internals access, so the registration overload and every observable consequence
/// of it are proven reachable by a third-party host.
/// <para>
/// The defect this fixes: the lanes used to gate on <c>converter.GetType() == typeof(DefaultTypeConverter)</c>,
/// an exact type test, so <c>class Mine : DefaultTypeConverter</c> — the obvious way to adjust one conversion —
/// disarmed all of it.
/// </para>
/// <para>
/// Whether a lane ran is asserted through a converter that <b>counts the target types it is consulted with</b>
/// and otherwise behaves exactly as <see cref="DefaultTypeConverter"/> does. Counting is not behaviour, so the
/// probe never steps outside its own declaration and the assertions stay valid with host-contract verification
/// on. The two probes below are the two places the reflection path really does consult the converter for a
/// value the compiled lane would have handled itself.
/// </para>
/// </summary>
public class TypeConverterRegistrationTests
{
    #region hosts and probes

    public sealed class Host
    {
        /// <summary>
        /// An integral <c>JsNumber</c> outside the <see cref="int"/> range is the one write the coercion path
        /// cannot finish on its own: it reaches <c>ConvertValueToSet</c> with a target of <see cref="long"/>,
        /// while the compiled write lane takes it directly.
        /// </summary>
        public long Big { get; set; }

        public object? Boxed { get; set; }

        /// <summary>
        /// A member type no compiled lane covers, so a converter is consulted for it whatever it declared —
        /// which is what makes a drifted declaration observable at all.
        /// </summary>
        public float Ratio { get; set; }

        public Dictionary<string, int> Dict { get; } = new() { ["k"] = 1 };

        /// <summary>
        /// <see cref="bool"/> arguments are the ones the reflection binding path hands to the converter (value
        /// coercion for booleans is off by default), while the compiled invoker binds them directly.
        /// </summary>
        public bool And(bool a, bool b) => a && b;
    }

    /// <summary>
    /// A real string-keyed indexer, whose accessor bakes in the key the engine's converter produced from the
    /// member name — the one resolution artefact a host converter steers.
    /// </summary>
    public sealed class StringIndexed
    {
        public string this[string key] => key + "!";
    }

    /// <summary>
    /// Behaves exactly like the stock converter and records the target type of every conversion it is asked
    /// for, so "did this conversion reach the converter?" can be asserted without misdeclaring anything.
    /// </summary>
    private sealed class CountingConverter : DefaultTypeConverter
    {
        public CountingConverter(Engine engine) : base(engine)
        {
        }

        public readonly List<Type> Targets = new();

        public override object? Convert(object? value, Type type, IFormatProvider formatProvider)
        {
            Targets.Add(type);
            return base.Convert(value, type, formatProvider);
        }

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            Targets.Add(type);
            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    /// <summary>A converter that declares its own target types instead of being told them at registration.</summary>
    private sealed class SelfDeclaringConverter : DefaultTypeConverter
    {
        public SelfDeclaringConverter(Engine engine) : base(engine)
        {
        }

        public readonly List<Type> Targets = new();

        protected override Type[] HandledTargetTypes => [typeof(long)];

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            Targets.Add(type);
            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    /// <summary>
    /// Really does answer two target types differently from the stock converter. One of them,
    /// <see cref="bool"/>, is covered by a compiled lane and the other, <see cref="float"/>, is not — which is
    /// exactly the inconsistency a drifted declaration produces.
    /// </summary>
    private sealed class MeddlingConverter : DefaultTypeConverter
    {
        public MeddlingConverter(Engine engine) : base(engine)
        {
        }

        public override bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
        {
            if (type == typeof(bool) && value is bool b)
            {
                converted = !b;
                return true;
            }

            if (type == typeof(float) && value is double d)
            {
                converted = (float) (d * 2);
                return true;
            }

            return base.TryConvert(value, type, formatProvider, out converted);
        }
    }

    private static CountingConverter Create(out Engine engine, params Type[] declared)
    {
        CountingConverter? converter = null;
        engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            if (declared.Length == 0)
            {
                options.SetTypeConverter(e => converter = new CountingConverter(e));
            }
            else
            {
                options.SetTypeConverter(e => converter = new CountingConverter(e), declared);
            }
        });
        engine.SetValue("host", new Host());
        return converter!;
    }

    /// <summary>
    /// The compiled lanes are built from expression trees on net8.0+ and need dynamic code, so on the other
    /// targets there is no lane to arm and every conversion reaches the converter however it was registered.
    /// </summary>
    private static readonly bool _compiledLanesAvailable =
#if NET8_0_OR_GREATER
        System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
#else
        false;
#endif

    /// <inheritdoc cref="ObjectConverterRegistrationTests.Verifying"/>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    /// <inheritdoc cref="Verifying" />
    public static bool NotVerifying => !Verifying;

    private static void ShouldBeOffTheConverter(CountingConverter converter, Type target)
    {
        if (_compiledLanesAvailable)
        {
            converter.Targets.Should().NotContain(target, "the compiled lane produces this itself");
        }
        else
        {
            converter.Targets.Should().Contain(target, "there is no compiled lane on this target framework");
        }
    }

    #endregion

    #region 1. registration surface

    [Fact]
    public void RegistrationRequiresAtLeastOneDeclaredTargetType()
    {
        var options = new Options();
        var act = () => options.SetTypeConverter(static e => new DefaultTypeConverter(e), []);
        act.Should().Throw<ArgumentException>().WithParameterName("handledTargetTypes");
    }

    [Fact]
    public void RegistrationRejectsNullDeclaredTargetType()
    {
        var options = new Options();
        var act = () => options.SetTypeConverter(static e => new DefaultTypeConverter(e), [typeof(long), null!]);
        act.Should().Throw<ArgumentException>().WithParameterName("handledTargetTypes");
    }

    [Fact]
    public void RegistrationRejectsNullFactory()
    {
        var options = new Options();
        var act = () => options.SetTypeConverter(null!, typeof(long));
        act.Should().Throw<ArgumentNullException>().WithParameterName("typeConverterFactory");
    }

    [Fact]
    public void TheEngineHandsBackExactlyTheConverterTheFactoryProduced()
    {
        var converter = Create(out var engine, typeof(TimeSpan));
        engine.TypeConverter.Should().BeSameAs(converter);
    }

    #endregion

    #region 2. the defect: deriving from the shipped converter used to disarm the lanes

    [Fact]
    public void AnUndeclaredSubclassOfTheStockConverterDisarmsBothCompiledLanes()
    {
        var converter = Create(out var engine);

        engine.Evaluate("host.Big = 4294967296");
        converter.Targets.Should().Contain(typeof(long), "an undeclared converter must be offered every conversion");

        converter.Targets.Clear();
        engine.Evaluate("host.And(true, true)").Should().Be(true);
        converter.Targets.Should().Contain(typeof(bool));
    }

    [Fact]
    public void ADeclaredSubclassOfTheStockConverterKeepsTheCompiledWriteLane()
    {
        var converter = Create(out var engine, typeof(TimeSpan));

        engine.Evaluate("host.Big = 4294967296");
        ShouldBeOffTheConverter(converter, typeof(long));
        engine.Evaluate("host.Big").Should().Be(4294967296d);
    }

    [Fact]
    public void ADeclaredSubclassOfTheStockConverterKeepsTheCompiledInvokerLane()
    {
        var converter = Create(out var engine, typeof(TimeSpan));

        engine.Evaluate("host.And(true, true)").Should().Be(true);
        ShouldBeOffTheConverter(converter, typeof(bool));
    }

    [Fact]
    public void ADeclaredTargetTypeStillReachesTheConverter()
    {
        var converter = Create(out var engine, typeof(long));

        engine.Evaluate("host.Big = 4294967296");
        converter.Targets.Should().Contain(typeof(long), "this is exactly what the declaration asked for");

        // ...and the parameter type it did not declare keeps its own lane
        converter.Targets.Clear();
        engine.Evaluate("host.And(true, true)").Should().Be(true);
        ShouldBeOffTheConverter(converter, typeof(bool));
    }

    [Fact]
    public void AConverterMayDeclareItsOwnTargetTypesInsteadOfBeingToldThem()
    {
        SelfDeclaringConverter? converter = null;
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.SetTypeConverter(e => converter = new SelfDeclaringConverter(e));
        });
        engine.SetValue("host", new Host());

        engine.Evaluate("host.Big = 4294967296");
        converter!.Targets.Should().Contain(typeof(long));

        converter.Targets.Clear();
        engine.Evaluate("host.And(true, true)").Should().Be(true);
        if (_compiledLanesAvailable)
        {
            converter.Targets.Should().NotContain(typeof(bool), "the member declaration narrows exactly as the registration would");
        }
    }

    [Fact]
    public void ARegistrationWidensAConverterOwnDeclarationRatherThanReplacingIt()
    {
        SelfDeclaringConverter? converter = null;
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            options.SetTypeConverter(e => converter = new SelfDeclaringConverter(e), typeof(bool));
        });
        engine.SetValue("host", new Host());

        // declared at the registration
        engine.Evaluate("host.And(true, true)").Should().Be(true);
        converter!.Targets.Should().Contain(typeof(bool));

        // still declared by the converter itself - a registration cannot take that away
        converter.Targets.Clear();
        engine.Evaluate("host.Big = 4294967296");
        converter.Targets.Should().Contain(typeof(long));
    }

    #endregion

    #region 3. the target type is exact, unlike an object converter's value

    /// <summary>
    /// The difference the target-driven signature makes, in the direction it is observable. An
    /// <see cref="ObjectConverter"/> is handed a <em>value</em>, so its filter has to claim in both directions
    /// and a member typed <see cref="object"/> can never be excluded — it could hold anything. A
    /// <see cref="ClrTypeConverter"/> is handed the target <see cref="Type"/> itself, so a declared type claims
    /// itself and its subtypes and nothing above them: declaring <see cref="Enum"/> leaves a
    /// <see cref="long"/> write alone, and no downward guess is needed to stay sound.
    /// </summary>
    [Fact]
    public void ADeclaredBaseTypeCoversEverythingUnderItAndNothingAboveIt()
    {
        // long is not an Enum and never will be, so declaring Enum leaves the long write alone
        var narrow = Create(out var narrowEngine, typeof(Enum));
        narrowEngine.Evaluate("host.Big = 4294967296");
        ShouldBeOffTheConverter(narrow, typeof(long));

        // ...while declaring a supertype of long does claim it
        var wide = Create(out var wideEngine, typeof(ValueType));
        wideEngine.Evaluate("host.Big = 4294967296");
        wide.Targets.Should().Contain(typeof(long));
    }

    /// <summary>
    /// A member typed <see cref="object"/> is <em>not</em> claimed by a converter that declared something else
    /// — the exact question can answer that, where the object-converter filter cannot — but no compiled lane
    /// covers an <see cref="object"/>-typed member either, so the converter is consulted regardless. Pinned so
    /// the two facts are not confused: the filter's precision buys a lane only where a lane exists.
    /// </summary>
    [Fact]
    public void AnObjectTypedMemberHasNoCompiledLaneToKeep()
    {
        var converter = Create(out var engine, typeof(TimeSpan));
        engine.Evaluate("host.Boxed = 'text'");
        converter.Targets.Should().Contain(typeof(object));
    }

    #endregion

    #region 4. the accessor cache the converter used to partition

    /// <summary>
    /// The third thing the exact type test cost: the installed converter was part of
    /// <c>InteropResolutionProfile</c>, which keys the accessor cache a <see cref="TypeResolver"/> shares, so an
    /// engine with any custom converter re-resolved every CLR member from scratch and shared nothing with its
    /// stock siblings. The engagement probe is the resolver's own <see cref="TypeResolver.MemberFilter"/>, which
    /// is consulted only while a member is being resolved.
    /// </summary>
    [Fact]
    public void AnEngineWithACustomConverterSharesTheStockAccessorCache()
    {
        var calls = 0;
        var resolver = new TypeResolver { MemberFilter = _ => { calls++; return true; } };

        var stock = new Engine(options => options.Interop.TypeResolver = resolver);
        stock.SetValue("host", new Host());
        stock.Evaluate("host.And(true, true)").Should().Be(true);
        calls.Should().BeGreaterThan(0, "the first engine has to resolve the member");

        calls = 0;
        var custom = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.SetTypeConverter(e => new CountingConverter(e), typeof(TimeSpan));
        });
        custom.SetValue("host", new Host());
        custom.Evaluate("host.And(true, true)").Should().Be(true);
        calls.Should().Be(0, "the converter is no longer part of what partitions the cache");
    }

    /// <summary>
    /// The one artefact it still costs, and only for the engines it could actually affect: an indexer accessor
    /// bakes in a key the converter produced from the member name.
    /// </summary>
    [Fact]
    public void AConverterThatCouldKeyAnIndexerDifferentlyResolvesItsOwn()
    {
        var calls = 0;
        var resolver = new TypeResolver { MemberFilter = _ => { calls++; return true; } };

        Engine Create(Action<Options>? configure = null)
        {
            var engine = new Engine(options =>
            {
                options.Interop.TypeResolver = resolver;
                configure?.Invoke(options);
            });
            engine.SetValue("indexed", new StringIndexed());
            return engine;
        }

        var stock = Create();
        stock.Evaluate("indexed.k").Should().Be("k!");
        calls.Should().BeGreaterThan(0);

        calls = 0;
        var unrelated = Create(options => options.SetTypeConverter(e => new CountingConverter(e), typeof(TimeSpan)));
        unrelated.Evaluate("indexed.k").Should().Be("k!");
        calls.Should().Be(0, "this converter cannot produce a string key, so the stock entry is still correct for it");

        calls = 0;
        var claiming = Create(options => options.SetTypeConverter(e => new CountingConverter(e), typeof(string)));
        claiming.Evaluate("indexed.k").Should().Be("k!");
        calls.Should().BeGreaterThan(0, "this one may key the indexer differently, so it must not be served the stock entry");
    }

    #endregion

    #region 5. the declaration and the converter's switch drifting apart

    /// <summary>
    /// Declaring target types is a promise that every other conversion matches
    /// <see cref="DefaultTypeConverter"/>'s, and nothing links it to the <c>switch</c> inside the converter. With
    /// host-contract verification on, the stock conversion is run beside the converter's for every undeclared
    /// target and a disagreement says so.
    /// </summary>
    private static Engine CreateMeddling(params Type[] declared)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = true;
            if (declared.Length == 0)
            {
                options.SetTypeConverter(static e => new MeddlingConverter(e));
            }
            else
            {
                options.SetTypeConverter(e => new MeddlingConverter(e), declared);
            }
        });
        engine.SetValue("host", new Host());
        return engine;
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationCatchesAConverterAnsweringAnUndeclaredTargetType()
    {
        var engine = CreateMeddling(typeof(TimeSpan));

        var act = () => engine.Evaluate("host.Ratio = 1.5");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MeddlingConverter*Single*TimeSpan*");
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationIsSilentForAConverterThatStaysInsideItsDeclaration()
    {
        var engine = CreateMeddling(typeof(float), typeof(bool));

        engine.Evaluate("host.Ratio = 1.5");
        engine.Evaluate("host.Ratio").Should().Be(3d, "the converter doubles floats, as it declared it would");
        engine.Evaluate("host.And(true, true)").Should().Be(false, "and flips booleans, likewise declared");
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationLeavesAnUndeclaredRegistrationAlone()
    {
        // registering without declared target types claims everything, so nothing can be out of scope
        var engine = CreateMeddling();

        engine.Evaluate("host.Ratio = 1.5");
        engine.Evaluate("host.Ratio").Should().Be(3d);
    }

    /// <summary>
    /// The same drift with nothing verifying: the converter is skipped on exactly the conversions its
    /// declaration excluded and a lane covers, and honoured everywhere else. Pinned so the damage is on the
    /// record — the inconsistency is invisible from script and from the host.
    /// </summary>
    [Fact(Skip = "host-contract verification is on in this run", SkipUnless = nameof(NotVerifying))]
    public void WithoutVerificationADriftedDeclarationIsSilentlyInconsistent()
    {
        var engine = CreateMeddling(typeof(TimeSpan));

        // float has no compiled lane, so the undeclared case really does run here...
        engine.Evaluate("host.Ratio = 1.5");
        engine.Evaluate("host.Ratio").Should().Be(3d);

        // ...while the equally undeclared bool case is skipped wherever the invoker lane exists
        engine.Evaluate("host.And(true, true)").Should().Be(_compiledLanesAvailable);
    }

    #endregion
}

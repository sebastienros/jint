#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers declaring the CLR types an <see cref="IObjectConverter"/> handles, which lets the engine keep the
/// compiled member-read fast lanes for members no registered converter can ever be handed. These live in the
/// public-interface suite on purpose: the project references Jint without any internals access, so the
/// registration overload and every observable consequence of it are proven reachable by a third-party host.
/// <para>
/// The behavioral tests below deliberately use a converter that would change the value it sees: whether the
/// change shows up is the only black-box evidence of whether the fast lane ran, so the assertions double as
/// documentation of the contract (declaring types is a promise — a misdeclared converter really is skipped).
/// </para>
/// <para>
/// The assignability rules the engine derives from a declaration are decided by an internal filter type and
/// are unit-tested next to it, in <c>Jint.Tests.Runtime.InteropObjectConverterFilterTests</c>.
/// </para>
/// </summary>
public class ObjectConverterRegistrationTests
{
    #region hosts

    public enum Level
    {
        Zero = 0,
        One = 1,
    }

    public sealed class Host
    {
        public bool Flag { get; set; } = true;
        public int Number { get; set; } = 1;
        public string Text { get; set; } = "text";
        public Level EnumValue { get; set; } = Level.One;
        public object? Boxed { get; set; }

        public bool GetFlag() => Flag;
        public int GetNumber() => Number;
        public string GetText() => Text;
        public Level GetEnumValue() => EnumValue;
        public object? GetBoxed() => Boxed;
    }

    private static Engine CreateEngine(Host host, Action<Options> configure)
    {
        var engine = new Engine(configure);
        engine.SetValue("host", host);
        return engine;
    }

    /// <summary>
    /// Declaring the types a converter handles only has an observable effect where the compiled member-read
    /// lane exists in the first place; on the other targets, and without dynamic code, every member keeps
    /// going through the converter exactly as before.
    /// </summary>
    private static readonly bool _compiledReadLaneAvailable =
#if NET8_0_OR_GREATER
        System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
#else
        false;
#endif

    /// <summary>
    /// Every caller of this helper reads a member through a <b>deliberately misdeclared</b> converter — one
    /// whose <c>TryConvert</c> would change the value but whose registration does not mention its type — since
    /// that is the only way to observe from the outside whether the fast lane ran. Where the lane exists the
    /// converter is skipped and the raw value comes back. Where it does not, the converter really is consulted,
    /// and converting an undeclared type is exactly the drift the claim verifier reports: with host-contract
    /// verification on, the read therefore throws rather than quietly returning the converted value.
    /// </summary>
    /// <summary>
    /// Whether Jint's host-contract verifiers are running: always in a Debug build, and in Release when
    /// <c>Jint.EnableHostContractVerification</c> was set before the first use of any Jint type — which is what
    /// this repository's Release verification leg does (<c>JINT_HOST_CONTRACT_VERIFICATION=1</c>). Public and
    /// static so xUnit can read it for <c>SkipUnless</c>.
    /// </summary>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    /// <inheritdoc cref="Verifying" />
    public static bool NotVerifying => !Verifying;

    private static void ShouldRead(Engine engine, string expression, JsValue whenBypassed, JsValue whenConverted)
    {
        if (_compiledReadLaneAvailable)
        {
            engine.Evaluate(expression).Should().Be(whenBypassed);
            return;
        }

        if (Verifying)
        {
            ((Action) (() => engine.Evaluate(expression))).Should().Throw<InvalidOperationException>(
                "the converter is reached here, and it converts a type its registration did not declare");
            return;
        }

        engine.Evaluate(expression).Should().Be(whenConverted);
    }

    /// <summary>Turns every bool it sees into its negation, and every enum into its name.</summary>
    private sealed class MeddlingConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            if (value is bool b)
            {
                result = !b ? JsBoolean.True : JsBoolean.False;
                return true;
            }

            if (value is Enum e)
            {
                result = new JsString(e.ToString());
                return true;
            }

            result = null;
            return false;
        }
    }

    /// <summary>
    /// Records every value it is offered and converts nothing, so "did this value reach the converters?" can be
    /// asserted directly — without the converter having to step outside its own declaration to make the answer
    /// visible.
    /// </summary>
    private sealed class RecordingConverter : IObjectConverter
    {
        public readonly List<object> Seen = new();

        public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            Seen.Add(value);
            result = null;
            return false;
        }
    }

    private sealed class NeverConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            result = null;
            return false;
        }
    }

    #endregion

    #region 1. registration surface

    [Fact]
    public void RegistrationWithoutDeclaredTypesIsUnchanged()
    {
        var converter = new NeverConverter();
        var options = new Options();
        options.AddObjectConverter(converter);

        // the overload without declared types must keep storing the converter itself
        options.Interop.ObjectConverters.Should().ContainSingle().Which.Should().BeSameAs(converter);
    }

    [Fact]
    public void RegistrationRequiresAtLeastOneDeclaredType()
    {
        var options = new Options();
        var act = () => options.AddObjectConverter(new NeverConverter(), []);
        act.Should().Throw<ArgumentException>().WithParameterName("handledTypes");
    }

    [Fact]
    public void RegistrationRejectsNullDeclaredType()
    {
        var options = new Options();
        var act = () => options.AddObjectConverter(new NeverConverter(), [typeof(bool), null!]);
        act.Should().Throw<ArgumentException>().WithParameterName("handledTypes");
    }

    [Fact]
    public void RegistrationRejectsNullConverter()
    {
        var options = new Options();
        var act = () => options.AddObjectConverter(null!, typeof(bool));
        act.Should().Throw<ArgumentNullException>().WithParameterName("objectConverter");
    }

    #endregion

    #region 2. behavior of a declared converter

    [Fact]
    public void UndeclaredConverterSeesEveryMember()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter()));

        engine.Evaluate("host.Flag").Should().Be(false);
        engine.Evaluate("host.EnumValue").Should().Be("One");
    }

    [Fact]
    public void ConverterDeclaringOnlyUnrelatedTypesDoesNotSeeTheMember()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        // bool cannot be a Guid, so the member never reaches the converter
        ShouldRead(engine, "host.Flag", whenBypassed: true, whenConverted: false);
        engine.Evaluate("host.Number").Should().Be(1);
        engine.Evaluate("host.Text").Should().Be("text");
    }

    [Fact]
    public void ConverterDeclaringTheMemberTypeStillSeesIt()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(bool)));

        engine.Evaluate("host.Flag").Should().Be(false);
    }

    [Fact]
    public void DeclaringEnumLeavesOtherMembersOnTheFastLane()
    {
        // the case this feature exists for: a converter registered purely to render enums keeps doing so,
        // and no longer costs every other member its fast lane
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(Enum)));

        engine.Evaluate("host.EnumValue").Should().Be("One");
        ShouldRead(engine, "host.Flag", whenBypassed: true, whenConverted: false);
        engine.Evaluate("host.Number").Should().Be(1);
    }

    [Fact]
    public void ObjectTypedMemberIsAlwaysOfferedToConverters()
    {
        var recorder = new RecordingConverter();
        var engine = CreateEngine(new Host { Boxed = true }, options => options.AddObjectConverter(recorder, typeof(Guid)));

        // the declared type says nothing about what the member actually holds, so no declaration can keep a
        // member typed `object` off the converters
        engine.Evaluate("host.Boxed").Should().Be(true);
        recorder.Seen.Should().Contain(true);
    }

    [Fact]
    public void OneUndeclaredConverterDisablesTheNarrowing()
    {
        var engine = CreateEngine(new Host(), options => options
            .AddObjectConverter(new NeverConverter(), typeof(Guid))
            .AddObjectConverter(new MeddlingConverter()));

        engine.Evaluate("host.Flag").Should().Be(false);
    }

    [Fact]
    public void SharedAccessorsStillFollowEachEnginesOwnConverters()
    {
        // the accessor for Host.Flag is resolved once and reused by both engines; the converter decision is
        // taken per read, from the reading engine, so it must not be baked into the shared accessor
        var resolver = new TypeResolver();
        var withConverter = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.AddObjectConverter(new MeddlingConverter(), typeof(bool));
        });
        withConverter.SetValue("host", new Host());
        var withoutConverter = new Engine(options => options.Interop.TypeResolver = resolver);
        withoutConverter.SetValue("host", new Host());

        withConverter.Evaluate("host.Flag").Should().Be(false);
        withoutConverter.Evaluate("host.Flag").Should().Be(true);
        withConverter.Evaluate("host.Flag").Should().Be(false);
    }

    #endregion

    #region 3. the same question, asked for a method's return value

    /// <summary>
    /// The compiled method-invoker lane produces the <see cref="JsValue"/> for a return value itself, so it
    /// asks the identical question of the identical filter. It is sound for the same reason: every return
    /// type the lane covers and a converter could observe (<see cref="int"/>, <see cref="long"/>,
    /// <see cref="double"/>, <see cref="bool"/>, <see cref="string"/>) is sealed, so the value handed to
    /// <c>FromObjectWithType</c> has exactly that runtime type — the very type the filter was asked about.
    /// </summary>
    [Fact]
    public void UndeclaredConverterSeesEveryReturnValue()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter()));

        engine.Evaluate("host.GetFlag()").Should().Be(false);
        engine.Evaluate("host.GetEnumValue()").Should().Be("One");
    }

    [Fact]
    public void ConverterDeclaringOnlyUnrelatedTypesDoesNotSeeTheReturnValue()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        ShouldRead(engine, "host.GetFlag()", whenBypassed: true, whenConverted: false);
        engine.Evaluate("host.GetNumber()").Should().Be(1);
        engine.Evaluate("host.GetText()").Should().Be("text");
    }

    [Fact]
    public void ConverterDeclaringTheReturnTypeStillSeesIt()
    {
        var engine = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(bool)));

        engine.Evaluate("host.GetFlag()").Should().Be(false);
    }

    [Fact]
    public void ReturnTypesOutsideTheLaneAlwaysReachTheConverter()
    {
        // an enum return type is not one the compiled invoker can produce, so the whole call keeps the
        // reflection path and the converter sees the value however it was registered
        var declared = CreateEngine(new Host(), options => options.AddObjectConverter(new MeddlingConverter(), typeof(Enum)));
        declared.Evaluate("host.GetEnumValue()").Should().Be("One");

        // and a method declared to return object can hand back anything at all
        var recorder = new RecordingConverter();
        var boxed = CreateEngine(new Host { Boxed = true }, options => options.AddObjectConverter(recorder, typeof(Guid)));
        boxed.Evaluate("host.GetBoxed()").Should().Be(true);
        recorder.Seen.Should().Contain(true);
    }

    [Fact]
    public void OneUndeclaredConverterDisablesTheNarrowingForReturnValuesToo()
    {
        var engine = CreateEngine(new Host(), options => options
            .AddObjectConverter(new NeverConverter(), typeof(Guid))
            .AddObjectConverter(new MeddlingConverter()));

        engine.Evaluate("host.GetFlag()").Should().Be(false);
    }

    #endregion

    #region 4. writes

    [Fact]
    public void ASharedAccessorAnswersTheConverterQuestionPerFilterNotOnce()
    {
        // Same shape as above, but both engines have a converter - only one of them declares bool. The
        // accessor remembers which filter last told it "this member type is not claimed" so it need not
        // re-derive a constant answer per read; that memo is keyed on the filter, so the engine whose
        // converter does claim bool must never be served the other's verdict. Interleaved in both orders,
        // and repeated, so a memo that ignored the filter would show up whichever engine warmed it.
        var resolver = new TypeResolver();

        Engine Create(Type declared)
        {
            var engine = new Engine(options =>
            {
                options.Interop.TypeResolver = resolver;
                options.AddObjectConverter(new MeddlingConverter(), declared);
            });
            engine.SetValue("host", new Host());
            return engine;
        }

        var claiming = Create(typeof(bool));
        var unrelated = Create(typeof(Guid));

        for (var i = 0; i < 3; i++)
        {
            ShouldRead(unrelated, "host.Flag", whenBypassed: true, whenConverted: false);
            claiming.Evaluate("host.Flag").Should().Be(false);
            claiming.Evaluate("host.Flag").Should().Be(false);
            ShouldRead(unrelated, "host.Flag", whenBypassed: true, whenConverted: false);
        }
    }

    [Fact]
    public void ASharedMethodDescriptorAnswersTheConverterQuestionPerFilterNotOnce()
    {
        // The method-invoker twin of the accessor memo test above: descriptors are shared through
        // the resolver, so the engine whose converter claims the return type must never be served
        // the other's "not claimed" verdict. Interleaved in both orders, and repeated.
        var resolver = new TypeResolver();

        Engine Create(Type declared)
        {
            var engine = new Engine(options =>
            {
                options.Interop.TypeResolver = resolver;
                options.AddObjectConverter(new MeddlingConverter(), declared);
            });
            engine.SetValue("host", new Host());
            return engine;
        }

        var claiming = Create(typeof(bool));
        var unrelated = Create(typeof(Guid));

        for (var i = 0; i < 3; i++)
        {
            ShouldRead(unrelated, "host.GetFlag()", whenBypassed: true, whenConverted: false);
            claiming.Evaluate("host.GetFlag()").Should().Be(false);
            claiming.Evaluate("host.GetFlag()").Should().Be(false);
            ShouldRead(unrelated, "host.GetFlag()", whenBypassed: true, whenConverted: false);
        }
    }

    [Fact]
    public void DeclaredConverterDoesNotAffectWrites()
    {
        var host = new Host();
        var engine = CreateEngine(host, options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        engine.Evaluate("host.Flag = false; host.Number = 7; host.Text = 'written';");

        host.Flag.Should().BeFalse();
        host.Number.Should().Be(7);
        host.Text.Should().Be("written");
    }

    #endregion

    #region 5. the declaration and the converter's switch drifting apart

    /// <summary>
    /// Declaring types is a promise, and nothing links it to the <c>switch</c> inside the converter: add a case
    /// to <c>TryConvert</c> and forget the registration, and the engine keeps the fast lanes for every member
    /// that cannot produce the declared types — so the new case is silently skipped on exactly those members,
    /// and works everywhere else. With host-contract verification on, converting an undeclared type says so.
    /// </summary>
    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationCatchesAConverterConvertingAnUndeclaredType()
    {
        // Boxed is declared `object`, so no declaration can exclude it and the converter is always offered its
        // value — a bool, which the registration below does not mention.
        var engine = CreateEngine(
            new Host { Boxed = true },
            options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        var act = () => engine.Evaluate("host.Boxed");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MeddlingConverter*Boolean*Guid*");
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationIsSilentForAConverterThatStaysInsideItsDeclaration()
    {
        var engine = CreateEngine(
            new Host { Boxed = Level.One },
            options => options.AddObjectConverter(new MeddlingConverter(), typeof(Enum), typeof(bool)));

        engine.Evaluate("host.Boxed").Should().Be("One");
        engine.Evaluate("host.Flag").Should().Be(false);
        engine.Evaluate("host.EnumValue").Should().Be("One");
    }

    [Fact(Skip = "host-contract verification is off in this run", SkipUnless = nameof(Verifying))]
    public void VerificationLeavesAnUndeclaredRegistrationAlone()
    {
        // registering without declared types claims everything, so nothing can be out of scope
        var engine = CreateEngine(new Host { Boxed = true }, options => options.AddObjectConverter(new MeddlingConverter()));

        engine.Evaluate("host.Boxed").Should().Be(false);
    }

    /// <summary>
    /// The same drift with nothing verifying: the converter runs on the members whose declared type happens to
    /// be wide enough to reach it, and is skipped on the rest. Pinned so the damage is on the record — the
    /// inconsistency is invisible from script and from the host.
    /// </summary>
    [Fact(Skip = "host-contract verification is on in this run", SkipUnless = nameof(NotVerifying))]
    public void WithoutVerificationADriftedDeclarationIsSilentlyInconsistent()
    {
        var engine = CreateEngine(
            new Host { Boxed = true },
            options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        // an `object`-typed member cannot be excluded, so the undeclared bool case does run here...
        engine.Evaluate("host.Boxed").Should().Be(false);
        // ...while the same value read through a `bool`-typed member skips it, where the lane exists
        ShouldRead(engine, "host.Flag", whenBypassed: true, whenConverted: false);
    }

    #endregion
}

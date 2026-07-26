#nullable enable
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Exercises declaring the CLR types an <see cref="IObjectConverter"/> handles, which lets the engine keep
/// the compiled member-read fast lanes for members no registered converter can ever be handed.
/// <para>
/// The behavioral tests below deliberately use a converter that would change the value it sees: whether the
/// change shows up is the only black-box evidence of whether the fast lane ran, so the assertions double as
/// documentation of the contract (declaring types is a promise — a misdeclared converter really is skipped).
/// </para>
/// </summary>
public class InteropObjectConverterFilterTests
{
    #region hosts

    public enum Level
    {
        Zero = 0,
        One = 1,
    }

    public interface IMarker;

    public sealed class Marked : IMarker;

    public class OpenBase;

    public sealed class SealedDerived : OpenBase;

    public class UnrelatedOpen;

    public sealed class Host
    {
        public bool Flag { get; set; } = true;
        public int Number { get; set; } = 1;
        public string Text { get; set; } = "text";
        public Level EnumValue { get; set; } = Level.One;
        public object? Boxed { get; set; }
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

    private static void ShouldRead(Engine engine, string expression, JsValue whenBypassed, JsValue whenConverted)
    {
        engine.Evaluate(expression).Should().Be(_compiledReadLaneAvailable ? whenBypassed : whenConverted);
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
                result = JsString.Create(e.ToString());
                return true;
            }

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
        var host = new Host { Boxed = true };
        var engine = CreateEngine(host, options => options.AddObjectConverter(new MeddlingConverter(), typeof(Guid)));

        // the declared type says nothing about what the member actually holds
        engine.Evaluate("host.Boxed").Should().Be(false);
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

    #region 3. assignability rules

    private static ObjectConverterTypeFilter Filter(params Type[] handledTypes)
    {
        return ObjectConverterTypeFilter.Create([new TypedObjectConverter(new NeverConverter(), handledTypes)])!;
    }

    [Fact]
    public void NoConvertersMeansNoFilter()
    {
        ObjectConverterTypeFilter.Create(null).Should().BeNull();
        ObjectConverterTypeFilter.Create([]).Should().BeNull();
    }

    [Fact]
    public void AnUndeclaredConverterClaimsEverything()
    {
        var filter = ObjectConverterTypeFilter.Create([new NeverConverter()])!;

        filter.Claims(typeof(bool)).Should().BeTrue();
        filter.Claims(typeof(string)).Should().BeTrue();
        filter.Claims(typeof(Guid)).Should().BeTrue();
    }

    [Fact]
    public void DeclaredTypesFromAllConvertersAreUnioned()
    {
        var filter = ObjectConverterTypeFilter.Create(
        [
            new TypedObjectConverter(new NeverConverter(), [typeof(bool)]),
            new TypedObjectConverter(new NeverConverter(), [typeof(string)]),
        ])!;

        filter.Claims(typeof(bool)).Should().BeTrue();
        filter.Claims(typeof(string)).Should().BeTrue();
        filter.Claims(typeof(int)).Should().BeFalse();
    }

    [Fact]
    public void ExactAndUnrelatedTypes()
    {
        var filter = Filter(typeof(string));

        filter.Claims(typeof(string)).Should().BeTrue();
        filter.Claims(typeof(int)).Should().BeFalse();
        filter.Claims(typeof(bool)).Should().BeFalse();
        filter.Claims(typeof(Level)).Should().BeFalse();
    }

    [Fact]
    public void UnknownMemberTypeIsClaimed()
    {
        Filter(typeof(string)).Claims(null).Should().BeTrue();
    }

    [Fact]
    public void ObjectTypedMemberIsClaimedByAnything()
    {
        Filter(typeof(Guid)).Claims(typeof(object)).Should().BeTrue();
    }

    [Fact]
    public void ConverterDeclaringObjectClaimsEverything()
    {
        var filter = Filter(typeof(object));

        filter.Claims(typeof(int)).Should().BeTrue();
        filter.Claims(typeof(string)).Should().BeTrue();
        filter.Claims(typeof(Level)).Should().BeTrue();
    }

    [Fact]
    public void DeclaredEnumClaimsConcreteEnums()
    {
        var filter = Filter(typeof(Enum));

        filter.Claims(typeof(Level)).Should().BeTrue();
        filter.Claims(typeof(Level?)).Should().BeTrue();
        filter.Claims(typeof(int)).Should().BeFalse();
        filter.Claims(typeof(string)).Should().BeFalse();
        filter.Claims(typeof(bool)).Should().BeFalse();
    }

    [Fact]
    public void DeclaredInterfaceClaimsImplementers()
    {
        var filter = Filter(typeof(IMarker));

        filter.Claims(typeof(Marked)).Should().BeTrue();
        filter.Claims(typeof(IMarker)).Should().BeTrue();

        // a sealed type that does not implement it can never be one
        filter.Claims(typeof(string)).Should().BeFalse();
        filter.Claims(typeof(int)).Should().BeFalse();

        // but a non-sealed class can have a subtype that implements it
        filter.Claims(typeof(OpenBase)).Should().BeTrue();
    }

    [Fact]
    public void DeclaredBaseClassClaimsSubtypesAndSupertypes()
    {
        var filter = Filter(typeof(OpenBase));

        filter.Claims(typeof(OpenBase)).Should().BeTrue();
        filter.Claims(typeof(SealedDerived)).Should().BeTrue();

        // a member typed as an unrelated non-sealed class: single inheritance rules out a common subtype
        filter.Claims(typeof(UnrelatedOpen)).Should().BeFalse();

        // ... unless the member is typed as an interface, which the declared class could implement
        filter.Claims(typeof(IMarker)).Should().BeTrue();
    }

    [Fact]
    public void DeclaredSealedTypeIsClaimedThroughABaseTypedMember()
    {
        var filter = Filter(typeof(SealedDerived));

        filter.Claims(typeof(OpenBase)).Should().BeTrue();
        filter.Claims(typeof(SealedDerived)).Should().BeTrue();
        filter.Claims(typeof(UnrelatedOpen)).Should().BeFalse();
    }

    [Fact]
    public void NullableMembersAreClaimedThroughTheirUnderlyingType()
    {
        var filter = Filter(typeof(int));

        filter.Claims(typeof(int?)).Should().BeTrue();
        filter.Claims(typeof(long?)).Should().BeFalse();
    }

    [Fact]
    public void OpenGenericDeclarationIsTreatedAsClaimingEverything()
    {
        var filter = Filter(typeof(List<>));

        filter.Claims(typeof(int)).Should().BeTrue();
        filter.Claims(typeof(string)).Should().BeTrue();
    }

    [Fact]
    public void RepeatedQueriesAreStable()
    {
        var filter = Filter(typeof(Enum));

        for (var i = 0; i < 3; i++)
        {
            filter.Claims(typeof(Level)).Should().BeTrue();
            filter.Claims(typeof(int)).Should().BeFalse();
        }
    }

    #endregion
}

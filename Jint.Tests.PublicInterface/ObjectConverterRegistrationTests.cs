#nullable enable

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
                result = new JsString(e.ToString());
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
}

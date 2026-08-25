#nullable enable

using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Tests.Runtime;

/// <summary>
/// The one thing <c>Jint.Tests.PublicInterface</c> cannot assert about <c>[JsAccessible]</c>. Every
/// behavioural claim about the feature is made there, from outside the assembly, and is deliberately made
/// against the reflected path rather than against a hand-written expectation — which means a generator that
/// silently registered nothing at all would pass all of it. This suite is what says the lane is engaged: it
/// asks the resolver which accessor a member of an annotated type lands on.
/// </summary>
public class JsAccessibleAccessorSelectionTests
{
    static JsAccessibleAccessorSelectionTests()
    {
        JsAccessibleRegistration.RegisterAll();
    }

    [JsAccessible]
    public sealed class Annotated
    {
        public int Score { get; set; }
        public string? Name { get; set; }
        public JsValue? Payload { get; set; }
        public int Field;
        public int ReadOnly => 1;
        public JsValue Echo(JsValue value) => value;
        public string Shout(string text) => text;
        public int Overloaded(int value) => value;
        public int Overloaded(int first, int second) => first + second;
    }

    public sealed class NotAnnotated
    {
        public int Score { get; set; }
    }

    private static ReflectionAccessor Resolve<T>(string member, TypeResolver? resolver = null, Action<Options>? configure = null)
    {
        var engine = new Engine(options =>
        {
            if (resolver is not null)
            {
                options.Interop.TypeResolver = resolver;
            }

            configure?.Invoke(options);
        });

        return (resolver ?? engine.Options.Interop.TypeResolver).GetAccessor(
            engine,
            typeof(T),
            member,
            MemberResolutionRequirement.None);
    }

    [Theory]
    [InlineData("Score")]
    [InlineData("score")]
    [InlineData("Name")]
    [InlineData("Payload")]
    [InlineData("Field")]
    [InlineData("ReadOnly")]
    public void AnAnnotatedMemberResolvesToTheGeneratedAccessor(string member)
    {
        Resolve<Annotated>(member).Should().BeOfType<GeneratedMemberAccessor>();
    }

    [Fact]
    public void AnAnnotatedMethodResolvesToTheGeneratedAccessor()
    {
        Resolve<Annotated>("Echo").Should().BeOfType<GeneratedMethodAccessor>();
    }

    [Theory]
    // a non-JsValue parameter is a conversion the generator will not reproduce
    [InlineData("Shout")]
    // an overloaded name is what MethodInfoFunction exists for
    [InlineData("Overloaded")]
    public void AMemberTheGeneratorDeclinesKeepsItsReflectedAccessor(string member)
    {
        Resolve<Annotated>(member).Should().BeOfType<MethodAccessor>();
    }

    [Fact]
    public void AnUnannotatedTypeIsUntouched()
    {
        Resolve<NotAnnotated>("Score").Should().BeOfType<PropertyAccessor>();
    }

    /// <summary>
    /// A host that installed one of the settings steering member resolution keeps the generated lane. The
    /// registry's own name-keyed lookup is skipped — it is only equivalent to the reflected selection while
    /// nothing steers that selection — and the reflected selection runs instead, with the generated accessor
    /// swapped in for whatever it landed on. What the host configured decides which member that is; the lane
    /// it is read through is unaffected.
    /// </summary>
    [Theory]
    [InlineData("Score")]
    [InlineData("Field")]
    [InlineData("Echo")]
    public void AMemberAHostFilterAllowsKeepsItsGeneratedAccessor(string member)
    {
        var resolver = new TypeResolver { MemberFilter = static _ => true };
        var accessor = Resolve<Annotated>(member, resolver);

        (accessor is GeneratedMemberAccessor or GeneratedMethodAccessor).Should().BeTrue("{0} resolved to {1}", member, accessor.GetType().Name);
    }

    [Fact]
    public void AMemberAHostFilterHidesResolvesToNothingRatherThanToItsGeneratedAccessor()
    {
        var resolver = new TypeResolver { MemberFilter = static m => !string.Equals(m.Name, "Score", StringComparison.Ordinal) };

        Resolve<Annotated>("Score", resolver).Should().BeSameAs(ConstantValueAccessor.NullAccessor);
        Resolve<Annotated>("Name", resolver).Should().BeOfType<GeneratedMemberAccessor>();
    }

    [Fact]
    public void AHostNameCreatorRenamesTheGeneratedMemberRatherThanRemovingIt()
    {
        var resolver = new TypeResolver { MemberNameCreator = static m => ["js_" + m.Name] };

        Resolve<Annotated>("js_Score", resolver).Should().BeOfType<GeneratedMemberAccessor>();
        Resolve<Annotated>("js_Echo", resolver).Should().BeOfType<GeneratedMethodAccessor>();
        Resolve<Annotated>("Score", resolver).Should().BeSameAs(ConstantValueAccessor.NullAccessor);
    }

    [Fact]
    public void AHostNameComparerDecidesWhichNamesReachTheGeneratedMember()
    {
        var resolver = new TypeResolver { MemberNameComparer = StringComparer.Ordinal };

        Resolve<Annotated>("Score", resolver).Should().BeOfType<GeneratedMemberAccessor>();

        // the default comparer ignores the first character's casing; an ordinal one does not
        Resolve<Annotated>("score", resolver).Should().BeSameAs(ConstantValueAccessor.NullAccessor);
    }

    /// <summary>
    /// Binding flags that no longer report a member hide it from both lanes — and cost nothing to the lanes
    /// they do not narrow, which is what the blanket skip used to get wrong.
    /// </summary>
    [Fact]
    public void ANarrowedPropertyBindingProfileHidesThePropertiesAndLeavesTheFieldsAlone()
    {
        static Action<Options> NonPublicPropertiesOnly()
            => options => options.Interop.ObjectWrapperReportedPropertyBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        Resolve<Annotated>("Score", new TypeResolver(), NonPublicPropertiesOnly()).Should().BeSameAs(ConstantValueAccessor.NullAccessor);
        Resolve<Annotated>("Field", new TypeResolver(), NonPublicPropertiesOnly()).Should().BeOfType<GeneratedMemberAccessor>();
    }
}

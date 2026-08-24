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

    [Fact]
    public void AHostMemberFilterTakesTheWholeTypeBackToReflection()
    {
        var resolver = new TypeResolver { MemberFilter = static _ => true };
        Resolve<Annotated>("Score", resolver).Should().BeOfType<PropertyAccessor>();
    }

    [Fact]
    public void ANarrowedPropertyBindingProfileTakesTheWholeTypeBackToReflection()
    {
        var accessor = Resolve<Annotated>(
            "Score",
            new TypeResolver(),
            options => options.Interop.ObjectWrapperReportedPropertyBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        accessor.Should().NotBeOfType<GeneratedMemberAccessor>();
    }
}

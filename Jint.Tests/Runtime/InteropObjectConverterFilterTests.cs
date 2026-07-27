#nullable enable
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// Unit-tests the assignability rules <see cref="ObjectConverterTypeFilter"/> derives from the CLR types an
/// <see cref="IObjectConverter"/> declares it handles — "could a value of this member's static type ever be
/// one of the declared types?". Both the filter and the typed-converter wrapper are internal, which is why
/// this file lives here rather than in the public-interface suite.
/// <para>
/// The registration overload itself, and every consequence of a declaration that a third party can observe,
/// are covered from outside the assembly by
/// <c>Jint.Tests.PublicInterface.ObjectConverterRegistrationTests</c>.
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

    private sealed class NeverConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
        {
            result = null;
            return false;
        }
    }

    #endregion

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
}

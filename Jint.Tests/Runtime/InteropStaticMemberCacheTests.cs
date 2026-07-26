using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// The accessors backing static member access on a type reference live in a cache shared by the whole
/// process and keyed only by (declaring type, member name). Entries must therefore capture nothing that
/// belongs to a single engine. A nested type is the exception: it resolves to an accessor holding a
/// <see cref="TypeReference"/>, which is an object owned by the engine that created it.
/// </summary>
public class InteropStaticMemberCacheTests
{
    [Fact]
    public void NestedTypeIsResolvedPerEngine()
    {
        var first = new Engine();
        first.SetValue("Holder", TypeReference.CreateTypeReference<CrossEngineHolder>(first));
        var firstInner = first.Evaluate("Holder.Inner").Should().BeOfType<TypeReference>().Which;

        var second = new Engine();
        second.SetValue("Holder", TypeReference.CreateTypeReference<CrossEngineHolder>(second));
        var secondInner = second.Evaluate("Holder.Inner").Should().BeOfType<TypeReference>().Which;

        ReferenceEquals(firstInner, secondInner).Should().BeFalse(
            "an object created by one engine must never be handed to another one.");

        firstInner.Engine.Should().BeSameAs(first);
        secondInner.Engine.Should().BeSameAs(second);

        firstInner.ReferenceType.Should().Be<CrossEngineHolder.Inner>();
        secondInner.ReferenceType.Should().Be<CrossEngineHolder.Inner>();
    }

    [Fact]
    public void OrdinaryStaticMemberResolutionIsStillCached()
    {
        // The cache is keyed by (type, member name) alone, so an engine that finds an entry never runs its
        // own resolution. A second engine performing no member name lookups at all is therefore proof that
        // the entry the first one produced was reused, and that excluding nested types did not quietly turn
        // the cache off for everything else.

        var firstLookups = 0;
        var secondLookups = 0;

        ReadStaticMember(() => firstLookups++).Should().Be(42);
        ReadStaticMember(() => secondLookups++).Should().Be(42);

        firstLookups.Should().BeGreaterThan(0, "the first engine has to resolve the member itself.");
        secondLookups.Should().Be(0, "the second engine must be served from the shared accessor cache.");

        static double ReadStaticMember(Action onLookup)
        {
            var resolver = new TypeResolver
            {
                MemberNameCreator = member =>
                {
                    onLookup();
                    return new[] { member.Name };
                }
            };

            var engine = new Engine(options => options.SetTypeResolver(resolver));
            engine.SetValue("Holder", TypeReference.CreateTypeReference<CachedMemberHolder>(engine));
            return engine.Evaluate("Holder.Value").AsNumber();
        }
    }

    /// <summary>
    /// Only used by <see cref="NestedTypeIsResolvedPerEngine"/>: the cache is process-wide, so a type
    /// another test also resolves would make the outcome depend on which test ran first.
    /// </summary>
    private sealed class CrossEngineHolder
    {
        public sealed class Inner
        {
        }
    }

    /// <summary>Only used by <see cref="OrdinaryStaticMemberResolutionIsStillCached"/>, same reason.</summary>
    private sealed class CachedMemberHolder
    {
        public static int Value => 42;
    }
}

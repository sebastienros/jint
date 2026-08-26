using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// The accessors backing static member access on a type reference live in the configured
/// <see cref="TypeResolver"/> and are partitioned by the resolution profile. Entries must capture nothing that
/// belongs to a single engine. A nested type is the exception: it resolves to an accessor holding a
/// <see cref="TypeReference"/>, which is an object owned by the engine that created it and is not cached.
/// </summary>
public class InteropStaticMemberCacheTests
{
    [Test]
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

    [Test]
    public void OrdinaryStaticMemberResolutionIsStillCached()
    {
        var firstLookups = 0;
        var secondLookups = 0;
        Action onLookup = () => firstLookups++;
        var resolver = new TypeResolver
        {
            MemberNameCreator = member =>
            {
                onLookup();
                return new[] { member.Name };
            }
        };

        ReadStaticMember(resolver).Should().Be(42);
        onLookup = () => secondLookups++;
        ReadStaticMember(resolver).Should().Be(42);

        firstLookups.Should().BeGreaterThan(0, "the first engine has to resolve the member itself.");
        secondLookups.Should().Be(0, "the second engine must be served from the shared accessor cache.");

        static double ReadStaticMember(TypeResolver resolver)
        {
            var engine = new Engine(options => options.Interop.TypeResolver = resolver);
            engine.SetValue("Holder", TypeReference.CreateTypeReference<CachedMemberHolder>(engine));
            return engine.Evaluate("Holder.Value").AsNumber();
        }
    }

    [Test]
    public void StaticAllowGetTypePolicyPartitionsTheCache()
    {
        var resolver = new TypeResolver();
        var restricted = new Engine(options => options.Interop.TypeResolver = resolver);
        restricted.SetValue("Holder", TypeReference.CreateTypeReference<StaticGetTypeHost>(restricted));
        var permissive = new Engine(options =>
        {
            options.Interop.TypeResolver = resolver;
            options.Interop.AllowGetType = true;
        });
        permissive.SetValue("Holder", TypeReference.CreateTypeReference<StaticGetTypeHost>(permissive));

        restricted.Evaluate("typeof Holder.GetType").Should().Be("undefined");
        permissive.Evaluate("typeof Holder.GetType").Should().Be("function");
        restricted.Evaluate("typeof Holder.GetType").Should().Be("undefined");
    }

    [Test]
    public void ConstructorCacheBelongsToTheResolver()
    {
        var denied = new TypeResolver { MemberFilter = static _ => false };
        var deniedEngine = new Engine(options => options.Interop.TypeResolver = denied);
        deniedEngine.SetValue("Host", TypeReference.CreateTypeReference<ConstructorHost>(deniedEngine));
        Invoking(() => deniedEngine.Evaluate("new Host()")).Should().Throw<Jint.Runtime.JavaScriptException>();

        var allowed = new TypeResolver();
        var allowedEngine = new Engine(options => options.Interop.TypeResolver = allowed);
        allowedEngine.SetValue("Host", TypeReference.CreateTypeReference<ConstructorHost>(allowedEngine));
        allowedEngine.Evaluate("new Host().Value").Should().Be(42);
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

    private sealed class StaticGetTypeHost
    {
        public static new Type GetType() => typeof(StaticGetTypeHost);
    }

    private sealed class ConstructorHost
    {
        public int Value => 42;
    }
}

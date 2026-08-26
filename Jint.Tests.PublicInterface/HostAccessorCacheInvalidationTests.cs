#nullable enable

using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins how far a <c>[JsAccessible]</c> registration reaches into the accessor cache a
/// <see cref="TypeResolver"/> holds. <see cref="JsAccessibleRegistry"/> is process-wide and every registration
/// bumps one process-wide counter, so the reach is a question about every engine in the process — including
/// the ones using a resolver the caller constructed privately, which the counter does not distinguish.
///
/// <para>
/// The answer is that a registration reaches exactly the registered type. Every consult of the registry is
/// keyed on the exact target type — <c>TryGetMember</c>, and <c>TryGetAccessorForDeclaredMember</c>, which
/// additionally requires the reflected member to be declared on that very type — so nothing else can resolve
/// differently because of it. Dropping the whole cache instead was measurable from another test entirely:
/// a <c>RegisterAll()</c> landing between two resolutions of an unrelated type made the second one
/// re-resolve, and the assertions that count resolutions failed intermittently (#3368, and the two failures
/// reported in #3363).
/// </para>
///
/// <para>
/// The probe is the resolver's own <see cref="TypeResolver.MemberFilter"/>, which is consulted only while a
/// member is being resolved, so a zero says the cache answered.
/// </para>
/// </summary>
public class HostAccessorCacheInvalidationTests
{
    private sealed class Unregistered
    {
        public int Value => 1;
    }

    private sealed class RegisteredElsewhere
    {
        public int Value => 1;
    }

    private sealed class RegisteredLate
    {
        public int Value => 1;
    }

    private static Engine CreateEngine(TypeResolver resolver, object host)
    {
        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("host", host);
        return engine;
    }

    /// <summary>
    /// The row #3368 is about: one registration must not cost every other type in the process the
    /// resolutions it already paid for. Deterministic — the registration happens between the two engines on
    /// this thread — where the failure it stands for was an interleaving.
    /// </summary>
    [Test]
    public void RegisteringOneTypeKeepsWhatEveryOtherTypeResolved()
    {
        var calls = 0;
        var resolver = new TypeResolver { MemberFilter = _ => { calls++; return true; } };

        CreateEngine(resolver, new Unregistered()).Evaluate("host.Value").Should().Be(1);
        calls.Should().BeGreaterThan(0, "the first engine has to resolve the member");

        calls = 0;
        JsAccessibleRegistry.Register(
            typeof(RegisteredElsewhere),
            builder => builder.AddMember("Value", typeof(int), static _ => JsNumber.Create(7), static _ => 7, null, null));

        CreateEngine(resolver, new Unregistered()).Evaluate("host.Value").Should().Be(1);
        calls.Should().Be(0, "a registration can only change how the registered type resolves");
    }

    /// <summary>
    /// The other direction, which the narrowing must not lose: a registration landing after the type's
    /// members were already resolved still takes effect, or a late <c>RegisterAll()</c> would be silently
    /// ineffective rather than merely late (#3333).
    /// </summary>
    [Test]
    public void RegisteringATypeStillDropsWhatWasResolvedForIt()
    {
        var calls = 0;
        var resolver = new TypeResolver { MemberFilter = _ => { calls++; return true; } };

        CreateEngine(resolver, new RegisteredLate()).Evaluate("host.Value").Should().Be(1);
        calls.Should().BeGreaterThan(0);

        calls = 0;
        JsAccessibleRegistry.Register(
            typeof(RegisteredLate),
            builder => builder.AddMember("Value", typeof(int), static _ => JsNumber.Create(42), static _ => 42, null, null));

        // 42 rather than 1 is the whole assertion: the reflected accessor cached a moment ago would still
        // have read the CLR property
        CreateEngine(resolver, new RegisteredLate()).Evaluate("host.Value").Should().Be(42);
        calls.Should().BeGreaterThan(0, "the registered type's entry had to be resolved again");
    }
}

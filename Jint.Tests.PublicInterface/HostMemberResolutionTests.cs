using System.Reflection;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The two ways a host steers CLR member resolution from outside the engine — the settings on a
/// <see cref="TypeResolver"/>, and handing <see cref="ObjectWrapper.GetPropertyDescriptor"/> a
/// <see cref="MemberInfo"/> of its own — checked against the accessor cache a resolver shares between the
/// engines using it. Both have to keep working once that cache is warm, and neither may make it answer an
/// engine with something that engine would not have resolved for itself.
/// </summary>
public class HostMemberResolutionTests
{
    public sealed class Host
    {
        public int Value => 1;

        public string Describe(int a) => $"one:{a}";

        public string Describe(int a, string b) => $"two:{a}{b}";
    }

    private static Engine CreateEngine(TypeResolver resolver, Host host = null)
    {
        var engine = new Engine(options => options.Interop.TypeResolver = resolver);
        engine.SetValue("host", host ?? new Host());
        return engine;
    }

    private static MethodInfo DescribeOverload(params Type[] parameterTypes)
        => typeof(Host).GetMethod(nameof(Host.Describe), parameterTypes);

    #region resolver settings mutated after a resolution

    [Fact]
    public void TighteningTheMemberFilterAppliesToLaterResolutions()
    {
        var resolver = new TypeResolver();
        CreateEngine(resolver).Evaluate("host.Value").Should().Be(1);

        resolver.MemberFilter = m => !string.Equals(m.Name, nameof(Host.Value), StringComparison.Ordinal);

        CreateEngine(resolver).Evaluate("typeof host.Value").Should().Be("undefined");
    }

    [Fact]
    public void RelaxingTheMemberFilterAppliesToLaterResolutions()
    {
        var resolver = new TypeResolver
        {
            MemberFilter = m => !string.Equals(m.Name, nameof(Host.Value), StringComparison.Ordinal),
        };
        CreateEngine(resolver).Evaluate("typeof host.Value").Should().Be("undefined");

        resolver.MemberFilter = _ => true;

        CreateEngine(resolver).Evaluate("host.Value").Should().Be(1);
    }

    [Fact]
    public void ChangingTheNameCreatorAppliesToLaterResolutions()
    {
        var resolver = new TypeResolver();
        CreateEngine(resolver).Evaluate("host.Value").Should().Be(1);

        resolver.MemberNameCreator = m => new[] { "js_" + m.Name };

        var engine = CreateEngine(resolver);
        engine.Evaluate("host.js_Value").Should().Be(1);
        engine.Evaluate("typeof host.Value").Should().Be("undefined");
    }

    [Fact]
    public void ChangingTheNameComparerAppliesToLaterResolutions()
    {
        var resolver = new TypeResolver();
        CreateEngine(resolver).Evaluate("typeof host.VALUE").Should().Be("undefined");

        resolver.MemberNameComparer = StringComparer.OrdinalIgnoreCase;

        CreateEngine(resolver).Evaluate("host.VALUE").Should().Be(1);
    }

    [Fact]
    public void AssigningTheSameSettingBackKeepsWhatWasResolved()
    {
        // Only a real change may discard the resolutions; re-assigning what is already there is a no-op, so
        // a host that rebuilds its options per engine does not throw the shared cache away every time.
        var filterCalls = 0;
        Predicate<MemberInfo> filter = _ =>
        {
            filterCalls++;
            return true;
        };

        var resolver = new TypeResolver { MemberFilter = filter };
        CreateEngine(resolver).Evaluate("host.Value").Should().Be(1);
        filterCalls.Should().BeGreaterThan(0);
        filterCalls = 0;

        resolver.MemberFilter = filter;

        CreateEngine(resolver).Evaluate("host.Value").Should().Be(1);
        filterCalls.Should().Be(0);
    }

    #endregion

    #region a host-supplied MemberInfo

    [Fact]
    public void AHostSuppliedMemberInfoDoesNotNarrowOrdinaryResolutions()
    {
        // The accessor built from the host's MemberInfo covers exactly that member — one overload out of a
        // set, here — which is not what ordinary resolution produces for the same name. It must not be left
        // behind for the next ordinary resolution to find, in this engine or in another one on the same
        // resolver. Note that the descriptor read below is what shares the key: GetPropertyDescriptor
        // resolves with no readable/writable requirement, and so does GetOwnProperty.
        var resolver = new TypeResolver();
        var host = new Host();
        var producer = CreateEngine(resolver, host);
        var consumer = CreateEngine(resolver, host);

        ObjectWrapper.GetPropertyDescriptor(producer, host, DescribeOverload(typeof(int)))
            .Should().NotBeNull();

        producer.Evaluate("Object.getOwnPropertyDescriptor(host, 'Describe').value.call(host, 1, 'x')").Should().Be("two:1x");
        consumer.Evaluate("Object.getOwnPropertyDescriptor(host, 'Describe').value.call(host, 1, 'x')").Should().Be("two:1x");

        // the plain member read, which resolves under a readable requirement, is unaffected either way
        consumer.Evaluate("host.Describe(1, 'x')").Should().Be("two:1x");
    }

    [Fact]
    public void AHostSuppliedMemberInfoIsHonouredAfterAnOrdinaryResolution()
    {
        // The converse direction: once ordinary resolution has cached the whole overload set under that
        // same key, answering from the cache would silently ignore the member the host named.
        var resolver = new TypeResolver();
        var host = new Host();
        var engine = CreateEngine(resolver, host);

        engine.Evaluate("Object.getOwnPropertyDescriptor(host, 'Describe').value.call(host, 1, 'x')").Should().Be("two:1x");

        var descriptor = ObjectWrapper.GetPropertyDescriptor(engine, host, DescribeOverload(typeof(int)));
        engine.SetValue("describeOne", descriptor.Value);

        engine.Evaluate("describeOne(1)").Should().Be("one:1");
        Invoking(() => engine.Evaluate("describeOne(1, 'x')"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("No public methods with the specified arguments were found.");
    }

    [Fact]
    public void AHostSuppliedMemberInfoStillResolvesOnAFreshResolver()
    {
        // The plain case the API exists for, with nothing warm anywhere.
        var host = new Host();
        var engine = CreateEngine(new TypeResolver(), host);

        var descriptor = ObjectWrapper.GetPropertyDescriptor(engine, host, typeof(Host).GetProperty(nameof(Host.Value)));

        descriptor.Value.Should().Be(1);
    }

    #endregion
}

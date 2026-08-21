#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

/// <summary>
/// The resolved CLR member accessors live on the <see cref="TypeResolver"/>, so every engine configured with
/// the same resolver resolves each member — and compiles the delegates that read it — exactly once.
/// <para>
/// The counting <see cref="TypeResolver.MemberFilter"/> below is the engagement probe: it is only consulted
/// while a member is being resolved, so a zero count proves the second engine reused the first engine's work
/// rather than merely arriving at the same answer. The rest of the file guards the other half of the
/// bargain — that an entry is never served to an engine whose configuration would have resolved it
/// differently, and that nothing engine-affine gets shared.
/// </para>
/// </summary>
public class InteropAccessorCacheSharingTests
{
    #region hosts

    public sealed class Host
    {
        public Host(int value) => Value = value;

        public int Value { get; }

        public string Name { get; set; } = "name";
    }

    public sealed class PrivateMemberHost
    {
        public int Visible => 1;

        private int Hidden => 42;
    }

    public class Outer
    {
        public sealed class Inner
        {
            public int Value => 5;
        }
    }

    public sealed class StringIndexed
    {
        public string this[string key] => key + "!";
    }

    private sealed class CountingResolver
    {
        private int _memberFilterCalls;

        public TypeResolver Resolver { get; }

        public CountingResolver()
        {
            Resolver = new TypeResolver
            {
                MemberFilter = _ =>
                {
                    _memberFilterCalls++;
                    return true;
                },
            };
        }

        public int Reset()
        {
            var calls = _memberFilterCalls;
            _memberFilterCalls = 0;
            return calls;
        }
    }

    private static Engine CreateEngine(TypeResolver resolver, object host, Action<Options>? configure = null)
    {
        var engine = new Engine(options =>
        {
            options.AllowClrWrite();
            options.Interop.TypeResolver = resolver;
            configure?.Invoke(options);
        });
        engine.SetValue("host", host);
        return engine;
    }

    #endregion

    #region 1. sharing

    [Fact]
    public void SecondEngineReusesTheResolutionOfTheFirst()
    {
        var counting = new CountingResolver();
        var engine1 = CreateEngine(counting.Resolver, new Host(1));
        var engine2 = CreateEngine(counting.Resolver, new Host(2));
        counting.Reset();

        engine1.Evaluate("host.Value").Should().Be(1);
        counting.Reset().Should().BeGreaterThan(0, "the first engine has to resolve the member");

        engine2.Evaluate("host.Value").Should().Be(2);
        counting.Reset().Should().Be(0, "the resolution is shared through the resolver");
    }

    [Fact]
    public void SharingCoversMethodsAndWrites()
    {
        var counting = new CountingResolver();
        var host1 = new Host(1);
        var host2 = new Host(2);
        var engine1 = CreateEngine(counting.Resolver, host1);
        var engine2 = CreateEngine(counting.Resolver, host2);

        engine1.Evaluate("host.Name = 'first'; host.Name.toUpperCase()").Should().Be("FIRST");
        counting.Reset();

        engine2.Evaluate("host.Name = 'second'; host.Name.toUpperCase()").Should().Be("SECOND");
        counting.Reset().Should().Be(0);

        host1.Name.Should().Be("first");
        host2.Name.Should().Be("second");
    }

    [Fact]
    public void UnresolvedMembersAreSharedToo()
    {
        var counting = new CountingResolver();
        var engine1 = CreateEngine(counting.Resolver, new Host(1));
        var engine2 = CreateEngine(counting.Resolver, new Host(2));

        engine1.Evaluate("typeof host.NoSuchMember").Should().Be("undefined");
        counting.Reset();

        engine2.Evaluate("typeof host.NoSuchMember").Should().Be("undefined");
        counting.Reset().Should().Be(0);
    }

    [Fact]
    public void ResolversOfTheirOwnDoNotShare()
    {
        var counting1 = new CountingResolver();
        var counting2 = new CountingResolver();

        CreateEngine(counting1.Resolver, new Host(1)).Evaluate("host.Value").Should().Be(1);
        counting1.Reset().Should().BeGreaterThan(0);

        CreateEngine(counting2.Resolver, new Host(2)).Evaluate("host.Value").Should().Be(2);
        counting2.Reset().Should().BeGreaterThan(0);
    }

    [Fact]
    public void EnginesOnTheDefaultResolverStayCorrect()
    {
        // no explicit resolver: both engines land on TypeResolver.Default, the process-wide one
        var engine1 = new Engine();
        engine1.SetValue("host", new Host(11));
        var engine2 = new Engine();
        engine2.SetValue("host", new Host(22));

        engine1.Evaluate("host.Value").Should().Be(11);
        engine2.Evaluate("host.Value").Should().Be(22);
        engine1.Evaluate("host.Value").Should().Be(11);
    }

    #endregion

    #region 2. partitioning by interop configuration

    [Fact]
    public void BindingFlagsPartitionTheCache()
    {
        var resolver = new TypeResolver();
        var restricted = CreateEngine(resolver, new PrivateMemberHost());
        var permissive = CreateEngine(resolver, new PrivateMemberHost(), options =>
            options.Interop.ObjectWrapperReportedPropertyBindingFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // the restricted engine resolves first, so a shared entry would hide the member from the other one
        restricted.Evaluate("typeof host.Hidden").Should().Be("undefined");
        permissive.Evaluate("host.Hidden").Should().Be(42);

        // ... and the other way around
        var resolverReversed = new TypeResolver();
        var permissiveFirst = CreateEngine(resolverReversed, new PrivateMemberHost(), options =>
            options.Interop.ObjectWrapperReportedPropertyBindingFlags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var restrictedSecond = CreateEngine(resolverReversed, new PrivateMemberHost());

        permissiveFirst.Evaluate("host.Hidden").Should().Be(42);
        restrictedSecond.Evaluate("typeof host.Hidden").Should().Be("undefined");
    }

    [Fact]
    public void AllowGetTypePartitionsTheCache()
    {
        var resolver = new TypeResolver();
        var restricted = CreateEngine(resolver, new Host(1));
        var permissive = CreateEngine(resolver, new Host(1), options => options.Interop.AllowGetType = true);

        restricted.Evaluate("typeof host.GetType").Should().Be("undefined");
        permissive.Evaluate("typeof host.GetType").Should().Be("function");
        restricted.Evaluate("typeof host.GetType").Should().Be("undefined");
    }

    [Fact]
    public void ExtensionMethodsPartitionTheCache()
    {
        var resolver = new TypeResolver();
        var without = CreateEngine(resolver, new Host(1));
        var with = CreateEngine(resolver, new Host(1), options => options.Interop.ExtensionMethodTypes.Add(typeof(HostExtensions)));

        without.Evaluate("typeof host.Doubled").Should().Be("undefined");
        with.Evaluate("host.Doubled()").Should().Be(2);
        without.Evaluate("typeof host.Doubled").Should().Be("undefined");
    }

    [Fact]
    public void EnginesRegisteringTheSameExtensionMethodsShareTheResolution()
    {
        // Partitioning by extension method configuration is done by the *identity* of the built lookup, so
        // two engines registering the same containers only share the cache if they end up holding the same
        // lookup instance. Without that, every extension method host resolves everything for itself.
        var counting = new CountingResolver();
        var engine1 = CreateEngine(counting.Resolver, new Host(1), options => options.AddExtensionMethods(typeof(HostExtensions)));
        var engine2 = CreateEngine(counting.Resolver, new Host(2), options => options.AddExtensionMethods(typeof(HostExtensions)));
        counting.Reset();

        engine1.Evaluate("host.Doubled()").Should().Be(2);
        counting.Reset().Should().BeGreaterThan(0, "the first engine has to resolve the extension method");

        engine2.Evaluate("host.Doubled()").Should().Be(4);
        counting.Reset().Should().Be(0, "the resolution is shared through the resolver");
    }

    [Fact]
    public void ExtensionMethodEnginesDoNotGrowTheSharedCachePerEngine()
    {
        // The cache never evicts and lives as long as the resolver, so an entry set per engine is a leak for
        // the process. Churning identically configured engines must add nothing after the first one has
        // resolved the member.
        var resolver = new TypeResolver();

        Engine Create() => CreateEngine(resolver, new Host(1), options => options.AddExtensionMethods(typeof(HostExtensions)));

        var first = Create();
        first.Evaluate("host.Doubled()").Should().Be(2);
        first.Evaluate("host.Value").Should().Be(1);
        var countAfterFirstEngine = resolver.ResolvedAccessorCount;
        countAfterFirstEngine.Should().BeGreaterThan(0);

        for (var i = 0; i < 20; i++)
        {
            var engine = Create();
            engine.Evaluate("host.Doubled()").Should().Be(2);
            engine.Evaluate("host.Value").Should().Be(1);
        }

        resolver.ResolvedAccessorCount.Should().Be(countAfterFirstEngine);
    }

    [Fact]
    public void IdenticallyConfiguredExtensionMethodEnginesShareOneLookup()
    {
        var resolver = new TypeResolver();
        var engine1 = CreateEngine(resolver, new Host(1), options => options.AddExtensionMethods(typeof(HostExtensions)));
        var engine2 = CreateEngine(resolver, new Host(2), options => options.AddExtensionMethods(typeof(HostExtensions)));

        engine2._extensionMethods.Should().BeSameAs(engine1._extensionMethods);
        engine2._interopResolutionProfile.Should().Be(engine1._interopResolutionProfile);
    }

    [Fact]
    public void RegistrationOrderKeepsTheLookupsApart()
    {
        // Order decides which container's overloads are considered first, so it is part of the lookup's
        // identity - two orders must not collapse onto one interned instance.
        var engine1 = new Engine(options => options.AddExtensionMethods(typeof(HostExtensions), typeof(OtherHostExtensions)));
        var engine2 = new Engine(options => options.AddExtensionMethods(typeof(OtherHostExtensions), typeof(HostExtensions)));
        var engine3 = new Engine(options => options.AddExtensionMethods(typeof(HostExtensions), typeof(OtherHostExtensions)));

        engine2._extensionMethods.Should().NotBeSameAs(engine1._extensionMethods);
        engine3._extensionMethods.Should().BeSameAs(engine1._extensionMethods);
    }

    [Fact]
    public void CustomTypeConvertersPartitionTheCache()
    {
        var resolver = new TypeResolver();
        var stock = CreateEngine(resolver, new StringIndexed());
        var custom = CreateEngine(resolver, new StringIndexed(), options => options.SetTypeConverter(engine => new WrappingTypeConverter(engine)));

        stock.Evaluate("host.key").Should().Be("key!");
        custom.Evaluate("host.key").Should().Be("key!");
    }

    #endregion

    #region 3. nothing engine-affine is shared

    [Fact]
    public void NestedTypeReferencesStayWithTheirOwnEngine()
    {
        var resolver = new TypeResolver();
        var engine1 = new Engine(options => options.Interop.TypeResolver = resolver);
        engine1.SetValue("Outer", TypeReference.CreateTypeReference<Outer>(engine1));
        var engine2 = new Engine(options => options.Interop.TypeResolver = resolver);
        engine2.SetValue("Outer", TypeReference.CreateTypeReference<Outer>(engine2));

        var inner1 = engine1.Evaluate("Outer.Inner");
        var inner2 = engine2.Evaluate("Outer.Inner");

        inner1.Should().BeOfType<TypeReference>().Which.Engine.Should().BeSameAs(engine1);
        inner2.Should().BeOfType<TypeReference>().Which.Engine.Should().BeSameAs(engine2);

        engine1.Evaluate("new Outer.Inner().Value").Should().Be(5);
        engine2.Evaluate("new Outer.Inner().Value").Should().Be(5);
    }

    #endregion

    /// <summary>
    /// Behaves exactly like the stock converter but is not it, so the engine counts as having a
    /// host-installed <see cref="ITypeConverter"/>.
    /// </summary>
    private sealed class WrappingTypeConverter : ITypeConverter
    {
        private readonly ITypeConverter _inner;

        public WrappingTypeConverter(Engine engine) => _inner = new DefaultTypeConverter(engine);

        public object? Convert(object? value, Type type, IFormatProvider formatProvider)
            => _inner.Convert(value, type, formatProvider);

        public bool TryConvert(object? value, Type type, IFormatProvider formatProvider, [NotNullWhen(true)] out object? converted)
            => _inner.TryConvert(value, type, formatProvider, out converted);
    }
}

internal static class HostExtensions
{
    public static int Doubled(this InteropAccessorCacheSharingTests.Host host) => host.Value * 2;
}

internal static class OtherHostExtensions
{
    public static int Tripled(this InteropAccessorCacheSharingTests.Host host) => host.Value * 3;
}

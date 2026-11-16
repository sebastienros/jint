using System.Reflection;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A wrapped CLR object lists its reflected members in declaration order, the most-derived type first, on every
/// runtime. .NET Framework's reflection cache is filled in the order members are asked for, so a host or the
/// engine looking one up by name first used to move it to the front of <c>Reflect.ownKeys</c>.
/// </summary>
public sealed class HostWrapperKeyOrderTests
{
    private const string DeclarationOrder =
        "Zebra,Apple,BaseZed,BaseAlpha,Second,First,Zulu,Alpha,Middle,BaseZulu,BaseAlpha2";

    [Test]
    public void ReflectedMembersAreListedInDeclarationOrder()
    {
        var engine = new Engine();
        engine.SetValue("o", new OrderedHost());

        engine.Evaluate("Object.getOwnPropertyNames(o).join()").AsString().Should().Be(DeclarationOrder);
    }

    /// <summary>
    /// <see cref="LookedUpHost"/> is reflected here and nowhere else, so its cache is fresh when these by-name
    /// lookups fill it; on .NET Framework they used to reorder every listing that followed.
    /// </summary>
    [Test]
    public void ALookupByNameDoesNotReorderTheListing()
    {
        var type = typeof(LookedUpHost);
        type.GetMethod(nameof(LookedUpHost.Middle), BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();
        type.GetMethod(nameof(LookedUpBase.BaseAlpha2), BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();
        type.GetProperty(nameof(LookedUpHost.Apple), BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();
        type.GetField(nameof(LookedUpHost.First), BindingFlags.Public | BindingFlags.Instance).Should().NotBeNull();

        var engine = new Engine();
        engine.SetValue("o", new LookedUpHost());
        const string Keys = "Object.getOwnPropertyNames(o).join()";

        engine.Evaluate(Keys).AsString().Should().Be(DeclarationOrder);
        engine.Evaluate("o.Middle(); o.BaseZulu(); o.Apple").AsNumber().Should().Be(2);
        engine.Evaluate(Keys).AsString().Should().Be(DeclarationOrder);
    }

#pragma warning disable CA1822 // the members are the subject, instance on purpose
    private class OrderedBase
    {
        public int BaseZed => 1;
        public int BaseAlpha => 2;
        public void BaseZulu() { }
        public void BaseAlpha2() { }
    }

    private sealed class OrderedHost : OrderedBase
    {
        public int Zebra => 1;
        public int Apple => 2;
        public int Second = 1;
        public int First = 2;
        public void Zulu() { }
        public void Alpha() { }
        public void Middle() { }
    }

    private class LookedUpBase
    {
        public int BaseZed => 1;
        public int BaseAlpha => 2;
        public void BaseZulu() { }
        public void BaseAlpha2() { }
    }

    private sealed class LookedUpHost : LookedUpBase
    {
        public int Zebra => 1;
        public int Apple => 2;
        public int Second = 1;
        public int First = 2;
        public void Zulu() { }
        public void Alpha() { }
        public void Middle() { }
    }
#pragma warning restore CA1822
}

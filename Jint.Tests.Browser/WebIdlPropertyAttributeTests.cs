using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;

namespace Jint.Tests.Browser;

/// <summary>
/// Every member the generator emits, held to the property attributes WebIDL gives its kind — which are the
/// opposite of ECMAScript's for an operation, and therefore the opposite of a generator's natural default.
/// </summary>
/// <remarks>
/// <para>The table, and the clause each row comes from:</para>
/// <list type="table">
///   <item>
///     <term><a href="https://webidl.spec.whatwg.org/#es-operations">operation</a></term>
///     <description><c>{ writable: true, enumerable: true, configurable: true }</c></description>
///   </item>
///   <item>
///     <term><a href="https://webidl.spec.whatwg.org/#es-attributes">attribute</a></term>
///     <description>an accessor, <c>{ enumerable: true, configurable: true }</c></description>
///   </item>
///   <item>
///     <term><a href="https://webidl.spec.whatwg.org/#es-constants">constant</a></term>
///     <description><c>{ writable: false, enumerable: true, configurable: false }</c>, on both the prototype and the interface object</description>
///   </item>
///   <item>
///     <term><a href="https://webidl.spec.whatwg.org/#interface-prototype-object"><c>constructor</c></a></term>
///     <description><c>{ writable: true, enumerable: false, configurable: true }</c></description>
///   </item>
///   <item>
///     <term><c>Symbol.toStringTag</c></term>
///     <description><c>{ writable: false, enumerable: false, configurable: true }</c></description>
///   </item>
///   <item>
///     <term><a href="https://webidl.spec.whatwg.org/#js-iterable"><c>Symbol.iterator</c></a></term>
///     <description><c>{ writable: true, enumerable: false, configurable: true }</c>, on the prototype of an interface that supports indexed properties</description>
///   </item>
/// </list>
/// <para>
/// It walks every interface rather than a sample, because the attributes come from one call site per kind in
/// the emitter and a mistake there is a mistake everywhere at once — which is exactly the shape of bug a
/// spot-check misses.
/// </para>
/// </remarks>
public sealed class WebIdlPropertyAttributeTests
{
    [Test]
    public void EveryGeneratedMemberCarriesItsKindsAttributes()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        var realm = DomRealm.Of(fixture.Engine);
        var violations = new List<string>();

        foreach (var definition in DomInterfaces.All)
        {
            var prototype = realm.PrototypeOf(definition);

            foreach (var key in prototype.GetOwnPropertyKeys())
            {
                var descriptor = prototype.GetOwnProperty(key);
                var name = definition.Name + ".prototype[" + key + "]";

                if (key.IsSymbol())
                {
                    // Two symbol-keyed members are emitted and they differ in one attribute: @@iterator is a
                    // writable data property (Web IDL gives it an operation's writability), @@toStringTag is
                    // not.
                    var writableSymbol = ReferenceEquals(key, GlobalSymbolRegistry.Iterator);
                    Check(violations, name, descriptor, writable: writableSymbol, enumerable: false, configurable: true);
                    continue;
                }

                if (key.AsString() == "constructor")
                {
                    Check(violations, name, descriptor, writable: true, enumerable: false, configurable: true);
                    continue;
                }

                // Membership in the definition's own constant list, not a naming heuristic: `Document.URL`
                // is an IDL attribute whose name happens to be all upper case.
                if (Array.Exists(definition.Constants, c => c.Name == key.AsString()))
                {
                    Check(violations, name, descriptor, writable: false, enumerable: true, configurable: false);
                    continue;
                }

                if (descriptor.Get is not null || descriptor.Set is not null)
                {
                    Check(violations, name, descriptor, writable: null, enumerable: true, configurable: true);
                    continue;
                }

                Check(violations, name, descriptor, writable: true, enumerable: true, configurable: true);
            }
        }

        violations.Should().BeEmpty();
    }

    [Test]
    public void EveryConstantIsAnOwnPropertyOfTheInterfaceObjectToo()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        var realm = DomRealm.Of(fixture.Engine);
        var violations = new List<string>();

        foreach (var definition in DomInterfaces.All.Where(d => d.Constants.Length > 0))
        {
            var interfaceObject = (ObjectInstance) realm.InterfaceObjectOf(definition);

            foreach (var constant in definition.Constants)
            {
                var descriptor = interfaceObject.GetOwnProperty(constant.Name);
                if (descriptor == PropertyDescriptorPlaceholder)
                {
                    violations.Add(definition.Name + "." + constant.Name + " is missing from the interface object");
                    continue;
                }

                Check(violations, definition.Name + "." + constant.Name, descriptor, writable: false, enumerable: true, configurable: false);
                descriptor.Value.AsNumber().Should().Be(constant.Value);
            }
        }

        violations.Should().BeEmpty();
    }

    [Test]
    public void ToStringTagIsDeclaredOnEveryPrototype()
    {
        using var fixture = DomTestFixture.Create("<div></div>");
        var realm = DomRealm.Of(fixture.Engine);

        foreach (var definition in DomInterfaces.All)
        {
            var prototype = realm.PrototypeOf(definition);
            prototype.Get(GlobalSymbolRegistry.ToStringTag).AsString()
                .Should().Be(definition.Name, "{0}.prototype declares its own @@toStringTag", definition.Name);
        }
    }

    private static Jint.Runtime.Descriptors.PropertyDescriptor PropertyDescriptorPlaceholder
        => Jint.Runtime.Descriptors.PropertyDescriptor.Undefined;

    private static void Check(
        List<string> violations,
        string name,
        Jint.Runtime.Descriptors.PropertyDescriptor descriptor,
        bool? writable,
        bool enumerable,
        bool configurable)
    {
        if (writable is { } expected && descriptor.Writable != expected)
        {
            violations.Add(name + " must be writable: " + expected);
        }

        if (descriptor.Enumerable != enumerable)
        {
            violations.Add(name + " must be enumerable: " + enumerable);
        }

        if (descriptor.Configurable != configurable)
        {
            violations.Add(name + " must be configurable: " + configurable);
        }
    }
}

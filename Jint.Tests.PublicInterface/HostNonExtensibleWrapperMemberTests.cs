#nullable enable

using System.Collections;
using System.Reflection;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what a wrapped CLR object's reflected members are once the wrapper is non-extensible: a member <c>delete</c>
/// removes stays removed, and <c>[[OwnPropertyKeys]]</c> lists exactly the members <c>[[GetOwnProperty]]</c> describes.
/// </summary>
/// <remarks>
/// <para>
/// A reflected member is what the wrapper resolves from the CLR type for a name: a method such as <c>Contains</c>, a
/// property such as <c>Count</c>. A method is configurable and a property is not. Two things broke the invariants of
/// the essential internal methods
/// (<see href="https://tc39.es/ecma262/#sec-invariants-of-the-essential-internal-methods"/>) once the wrapper was
/// non-extensible. <c>delete</c> of a method answered <see langword="true"/> while the wrapper resolved the method
/// again on the next <c>[[GetOwnProperty]]</c>, so a non-extensible object kept a property it said it had removed,
/// which a <c>Proxy</c>'s <c>deleteProperty</c> check rejects. And a collection view or a dictionary never listed its
/// members, so <c>[[OwnPropertyKeys]]</c> left out properties <c>[[GetOwnProperty]]</c> describes, which a
/// <c>Proxy</c>'s <c>ownKeys</c> check rejects.
/// </para>
/// <para>
/// The expected values are what Node gives for an ordinary object holding the member as an own property:
/// <c>Object.preventExtensions</c> then <c>delete</c> of a configurable property leaves no own property, so a read
/// reaches the prototype, and <c>Reflect.ownKeys</c> lists every own property. On an extensible wrapper nothing
/// changes: a deleted member is resolved again on the next read, and a view lists only its positions.
/// </para>
/// </remarks>
public class HostNonExtensibleWrapperMemberTests
{
    /// <summary>
    /// The issue's table: a method deleted from a non-extensible wrapper stays deleted, whether or not it was read
    /// first, and every lane agrees that it is gone.
    /// </summary>
    [TestCase("Poco", "Hello", true)]
    [TestCase("Poco", "Hello", false)]
    [TestCase("List", "Contains", true)]
    [TestCase("List", "Contains", false)]
    [TestCase("List", "Add", true)]
    [TestCase("Array", "Clone", true)]
    [TestCase("Dictionary", "ContainsKey", true)]
    [TestCase("Dictionary", "ContainsKey", false)]
    [TestCase("IntDictionary", "ContainsKey", true)]
    [TestCase("Hashtable", "ContainsKey", true)]
    public void AMemberDeletedFromANonExtensibleWrapperStaysDeleted(string shape, string member, bool readFirst)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            const read = {{(readFirst ? "typeof host[m]" : "'function'")}};
            Object.preventExtensions(host);
            const deleted = delete host[m];
            [
              read,
              deleted,
              Object.getOwnPropertyDescriptor(host, m) === undefined,
              typeof host[m],
              Object.prototype.hasOwnProperty.call(host, m),
              m in host,
              Reflect.ownKeys(host).includes(m),
              Object.getOwnPropertyNames(host).includes(m),
            ].join()
            """).AsString().Should().Be("function,true,true,undefined,false,false,false,false");
    }

    /// <summary>
    /// A member read and called at a call site warmed before the <c>delete</c> sees the deletion: the site's cached
    /// descriptor is the one the deletion removed.
    /// </summary>
    [TestCase("Poco", "Hello", "'a'")]
    [TestCase("List", "Contains", "1")]
    [TestCase("Dictionary", "ContainsKey", "'a'")]
    public void AWarmedCallSiteSeesTheDeletion(string shape, string member, string argument)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            function read() { return typeof host.{{member}}; }
            function call() { try { host.{{member}}({{argument}}); return 'called'; } catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } }
            for (let i = 0; i < 3; i++) { read(); call(); }
            const before = [read(), call()].join();
            Object.preventExtensions(host);
            delete host.{{member}};
            [before, read(), call(), read()].join(';')
            """).AsString().Should().Be("function,called;undefined;TypeError;undefined");
    }

    /// <summary>
    /// The issue's control row, on every shape: a member reported non-configurable is not deleted.
    /// </summary>
    [TestCase("Poco", "Name", "string")]
    [TestCase("List", "Count", "number")]
    [TestCase("Dictionary", "Count", "number")]
    public void ANonConfigurableMemberIsNotDeleted(string shape, string member, string type)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            Object.preventExtensions(host);
            [delete host[m], Object.getOwnPropertyDescriptor(host, m) !== undefined, typeof host[m], Reflect.ownKeys(host).includes(m)].join()
            """).AsString().Should().Be("false,true," + type + ",true");
    }

    /// <summary>
    /// A deleted member cannot be defined or written back onto the non-extensible wrapper, and freezing it afterwards
    /// leaves it frozen without the member.
    /// </summary>
    [TestCase("Poco", "Hello")]
    [TestCase("List", "Contains")]
    [TestCase("Dictionary", "ContainsKey")]
    public void ADeletedMemberCannotComeBack(string shape, string member)
    {
        var engine = CreateEngine(CreateShape(shape), allowWrite: true);

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            Object.preventExtensions(host);
            delete host[m];
            const redefined = Reflect.defineProperty(host, m, { value: 1, configurable: true });
            const written = Reflect.set(host, m, 1);
            Object.freeze(host);
            [redefined, written, Object.isFrozen(host), m in host, Reflect.ownKeys(host).includes(m)].join()
            """).AsString().Should().Be("false,false,true,false,false");
    }

    /// <summary>
    /// Once the wrapper is non-extensible, every member <c>Object.getOwnPropertyDescriptor</c> describes is listed,
    /// every listed key is described, and no key is listed twice. The names asked are the CLR type's public
    /// instance members.
    /// </summary>
    [TestCase("Poco", "preventExtensions")]
    [TestCase("Poco", "seal")]
    [TestCase("Poco", "freeze")]
    [TestCase("List", "preventExtensions")]
    [TestCase("List", "seal")]
    [TestCase("List", "freeze")]
    [TestCase("Array", "preventExtensions")]
    [TestCase("Array", "freeze")]
    [TestCase("Dictionary", "preventExtensions")]
    [TestCase("Dictionary", "seal")]
    [TestCase("Dictionary", "freeze")]
    [TestCase("IntDictionary", "preventExtensions")]
    [TestCase("IntDictionary", "freeze")]
    [TestCase("Hashtable", "preventExtensions")]
    [TestCase("Hashtable", "freeze")]
    public void ANonExtensibleWrapperListsExactlyTheMembersItDescribes(string shape, string integrityLevel)
    {
        var host = CreateShape(shape);
        var engine = CreateEngine(host);

        engine.Evaluate(
            $$"""
            const names = {{MemberNamesOf(host.GetType())}};
            Object.{{integrityLevel}}(host);
            const keys = Reflect.ownKeys(host);
            const strings = keys.filter(k => typeof k === 'string');
            const missing = names.filter(n => Object.getOwnPropertyDescriptor(host, n) !== undefined && !keys.includes(n));
            const phantom = keys.filter(k => Object.getOwnPropertyDescriptor(host, k) === undefined).map(String);
            const duplicated = strings.filter((k, i) => strings.indexOf(k) !== i);
            JSON.stringify({ described: names.some(n => Object.getOwnPropertyDescriptor(host, n) !== undefined), missing, phantom, duplicated })
            """).AsString().Should().Be("""{"described":true,"missing":[],"phantom":[],"duplicated":[]}""");
    }

    /// <summary>
    /// <c>Object.freeze</c> and <c>Object.seal</c> reach every member, so a sealed or frozen wrapper's methods are
    /// non-configurable and are not deleted; <c>Object.isSealed</c> and <c>Object.isFrozen</c> answer having looked
    /// at them. (A sealed wrapper may be frozen as well: a method is a non-writable data property, and a property an
    /// accessor.)
    /// </summary>
    [TestCase("Poco", "Hello", "seal")]
    [TestCase("Poco", "Hello", "freeze")]
    [TestCase("List", "Contains", "seal")]
    [TestCase("List", "Contains", "freeze")]
    [TestCase("Dictionary", "ContainsKey", "seal")]
    [TestCase("Dictionary", "ContainsKey", "freeze")]
    public void ASealedOrFrozenWrappersMembersAreNotDeleted(string shape, string member, string integrityLevel)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            Object.{{integrityLevel}}(host);
            const descriptor = Object.getOwnPropertyDescriptor(host, m);
            [
              descriptor.configurable,
              delete host[m],
              typeof host[m],
              Object.isSealed(host),
              '{{integrityLevel}}' === 'seal' || Object.isFrozen(host),
            ].join()
            """).AsString().Should().Be("false,false,function,true,true");
    }

    /// <summary>
    /// A proxy's <c>ownKeys</c> trap over a non-extensible target must report exactly the target's keys
    /// (<see href="https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-ownpropertykeys"/>),
    /// so a trap reporting a member the target describes, and a trap forwarding the target's own list, both succeed.
    /// </summary>
    [TestCase("Poco", "Hello")]
    [TestCase("List", "Contains")]
    [TestCase("Array", "Clone")]
    [TestCase("Dictionary", "ContainsKey")]
    public void AProxysOwnKeysTrapMayReportTheMembersOfANonExtensibleWrapper(string shape, string member)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            Object.preventExtensions(host);
            const outcome = trap => {
              try { return Reflect.ownKeys(new Proxy(host, { ownKeys: trap })).includes(m); }
              catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }
            };
            [
              outcome(t => { const keys = Reflect.ownKeys(t); return keys.includes(m) ? keys : [...keys, m]; }),
              outcome(t => Reflect.ownKeys(t)),
              outcome(undefined),
            ].join()
            """).AsString().Should().Be("true,true,true");
    }

    /// <summary>
    /// A proxy's <c>deleteProperty</c> trap may not report a property of a non-extensible target deleted while the
    /// target still has it
    /// (<see href="https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-delete-p"/>), so a
    /// trap forwarding to the wrapper threw while the wrapper kept the member it said it had deleted.
    /// </summary>
    [TestCase("Poco", "Hello")]
    [TestCase("List", "Contains")]
    [TestCase("Dictionary", "ContainsKey")]
    public void AProxyForwardingDeleteOverANonExtensibleWrapperSucceeds(string shape, string member)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            Object.preventExtensions(host);
            const proxy = new Proxy(host, { deleteProperty: (t, k) => Reflect.deleteProperty(t, k) });
            let deleted;
            try { deleted = delete proxy[m]; }
            catch (e) { deleted = e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; }
            [deleted, m in host].join()
            """).AsString().Should().Be("true,false");
    }

    /// <summary>
    /// A view lists its members after its positions and ahead of its <c>length</c> forwarder, as a plain wrapper lists
    /// its members ahead of its projections; none of them is enumerable, so what enumerates a frozen view is still
    /// its positions.
    /// </summary>
    [Test]
    public void ANonExtensibleViewListsItsMembersAfterItsPositions()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });

        engine.Evaluate(
            """
            Object.preventExtensions(host);
            const names = Object.getOwnPropertyNames(host);
            const seen = [];
            for (const k in host) seen.push(k);
            [
              names.slice(0, 2).join(),
              names[names.length - 1],
              names.indexOf('Count') > 1,
              names.indexOf('Contains') > 1,
              Object.keys(host).join(),
              seen.join(),
              Object.keys({ ...host }).join(),
              JSON.stringify(host),
            ].join(';')
            """).AsString().Should().Be("0,1;length;true;true;0,1;0,1;0,1;[1,2]");
    }

    /// <summary>
    /// A frozen dictionary lists its members, and still enumerates and serializes its entries alone. A key spelled
    /// like a member, which a <see cref="Hashtable"/> answers from its entry, is that entry and is enumerable.
    /// </summary>
    [TestCase("Dictionary", "a", "{\"a\":1}")]
    [TestCase("IntDictionary", "1", "{\"1\":\"one\"}")]
    [TestCase("Hashtable", "Values", "{\"Values\":1}")]
    public void AFrozenDictionaryEnumeratesOnlyItsEntries(string shape, string keys, string json)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            """
            Object.freeze(host);
            const seen = [];
            for (const k in host) seen.push(k);
            [Reflect.ownKeys(host).includes('Count'), Object.keys(host).join(), seen.join(), JSON.stringify(host)].join(';')
            """).AsString().Should().Be($"true;{keys};{keys};{json}");
    }

    /// <summary>
    /// The members of a view or a dictionary are non-enumerable: a view enumerates its positions and a dictionary its
    /// entries.
    /// </summary>
    [TestCase("List", "Count")]
    [TestCase("List", "Capacity")]
    [TestCase("List", "Contains")]
    [TestCase("Array", "Length")]
    [TestCase("Dictionary", "Count")]
    [TestCase("IntDictionary", "Count")]
    [TestCase("Hashtable", "Count")]
    public void TheMembersOfAViewOrADictionaryAreNotEnumerable(string shape, string member)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            [Object.getOwnPropertyDescriptor(host, m).enumerable, Object.prototype.propertyIsEnumerable.call(host, m)].join()
            """).AsString().Should().Be("false,false");
    }

    /// <summary>
    /// On an extensible wrapper nothing changes: a deleted method is resolved again on the next read, and a view
    /// lists its positions and none of its members.
    /// </summary>
    [TestCase("Poco", "Hello", "Name,Hello")]
    [TestCase("List", "Contains", "0,1")]
    [TestCase("Dictionary", "ContainsKey", "a")]
    public void AnExtensibleWrapperResolvesADeletedMemberAgain(string shape, string member, string keys)
    {
        var engine = CreateEngine(CreateShape(shape));

        engine.Evaluate(
            $$"""
            const m = '{{member}}';
            typeof host[m];
            const deleted = delete host[m];
            [deleted, typeof host[m], Object.getOwnPropertyDescriptor(host, m) !== undefined, Reflect.ownKeys(host).map(String).join()].join(';')
            """).AsString().Should().Be("true;function;true;" + keys);
    }

    /// <summary>
    /// The host side reads the same list: <see cref="ObjectInstance.GetOwnPropertyKeys"/> on a non-extensible view
    /// includes its members, and leaves out the one the script deleted.
    /// </summary>
    [Test]
    public void TheHostSeesTheSameKeys()
    {
        var engine = CreateEngine(new List<int> { 1, 2 });
        var wrapper = (ObjectInstance) engine.GetValue("host");

        wrapper.GetOwnPropertyKeys().Select(key => key.ToString()).Should().Equal("0", "1");

        engine.Execute("Object.preventExtensions(host); delete host.Contains;");

        var keys = wrapper.GetOwnPropertyKeys().Select(key => key.ToString()).ToList();
        keys.Should().Contain("Add").And.Contain("Count").And.NotContain("Contains");
    }

    /// <summary>
    /// Each member name is listed once and in declaration order, extensible or not: a method's overloads declared
    /// apart, and a property a derived type hides with <c>new</c>, keep the place of the first one listed.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void AnOverloadedOrHiddenMemberIsListedOnceInItsPlace(bool preventExtensions)
    {
        typeof(ShadowingRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.Name == nameof(ShadowingRecord.Shadowed))
            .Should().Be(2, "reflection returns the hidden property as well, which is what the listing must not repeat");

        var engine = CreateEngine(new ShadowingRecord());
        if (preventExtensions)
        {
            engine.Execute("Object.preventExtensions(host);");
        }

        engine.Evaluate("Reflect.ownKeys(host).join()").AsString().Should().Be("Shadowed,First,Last,Hello,Middle,Before");
    }

    private static Engine CreateEngine(object host, bool allowWrite = false)
    {
        var engine = new Engine(options =>
        {
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
            options.Interop.AllowWrite = allowWrite;
        });
        engine.SetValue("host", host);
        return engine;
    }

    private static object CreateShape(string kind) => kind switch
    {
        "Poco" => new Greeter(),
        "List" => new List<int> { 1, 2 },
        "Array" => new[] { 1, 2 },
        "Dictionary" => new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 },
        "IntDictionary" => new Dictionary<int, string> { [1] = "one" },
        "Hashtable" => new Hashtable { ["Values"] = 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape"),
    };

    /// <summary>
    /// The public instance properties and methods of <paramref name="type"/>, as a JavaScript array literal.
    /// </summary>
    private static string MemberNamesOf(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName)
            .Select(m => m.Name);

        return "[" + string.Join(",", properties.Concat(methods).Distinct(StringComparer.Ordinal).Select(name => "'" + name + "'")) + "]";
    }

    /// <summary>
    /// A plain host object with a method, an overload of it, and a property.
    /// </summary>
    public sealed class Greeter
    {
        public string Name { get; set; } = "n";

        public string Hello() => "hi";

        public string Hello(string who) => "hi " + who;
    }

    /// <summary>
    /// A base type whose property <see cref="ShadowingRecord"/> hides.
    /// </summary>
    public class ShadowingBase
    {
        public string Shadowed { get; set; } = "base";

        public int Before() => 1;
    }

    /// <summary>
    /// A host object hiding its base type's property with one of another type, and declaring a method's overloads
    /// with another method between them.
    /// </summary>
    public sealed class ShadowingRecord : ShadowingBase
    {
        public new int Shadowed { get; set; } = 2;

        public string First { get; set; } = "f";

        public string Hello() => "hi";

        public int Middle() => 0;

        public string Hello(string who) => who;

        public string Last { get; set; } = "l";
    }
}

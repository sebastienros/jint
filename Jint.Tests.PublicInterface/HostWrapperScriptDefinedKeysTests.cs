#nullable enable

using System.Collections;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Dynamic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interop;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that a wrapped CLR object lists the properties a script defined on the wrapper itself, after the target's
/// own keys (#4503).
/// </summary>
/// <remarks>
/// <para>
/// <c>GetOwnPropertyKeys</c> is the one enumeration hook: <c>Reflect.ownKeys</c>, <c>Object.getOwnPropertyNames</c>
/// and <c>getOwnPropertySymbols</c>, <c>Object.keys</c>, <c>Object.assign</c>, <c>for..in</c>, <c>JSON.stringify</c>,
/// <see href="https://tc39.es/ecma262/#sec-setintegritylevel">SetIntegrityLevel</see> (<c>Object.freeze</c> /
/// <c>seal</c>) and <see href="https://tc39.es/ecma262/#sec-testintegritylevel">TestIntegrityLevel</see>
/// (<c>isFrozen</c> / <c>isSealed</c>) all read it. A wrapper used to list only its target's keys, so each of them
/// missed a property the script had created on the wrapper.
/// </para>
/// <para>
/// The order is <see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see> over
/// the target's keys and the script's, with the target's taken as the older ones. The expected values are what Node
/// gives for the same script on an ordinary object or array holding the same keys. What a wrapper projects itself
/// (a CLR member, an element, a dictionary entry) is listed once, by the target, however the script redefines it.
/// </para>
/// </remarks>
public class HostWrapperScriptDefinedKeysTests
{
    /// <summary>
    /// One shape per wrapper class: the array-like views, read-only and dictionary-shaped variants, the plain
    /// wrappers over a countable collection, the dictionaries and an <see cref="ExpandoObject"/>, and a plain object.
    /// </summary>
    private static readonly string[] _shapes =
    [
        "List",
        "ReadOnlyCollection",
        "Array",
        "ImmutableArray",
        "ArrayList",
        "ReadOnlyList",
        "JObject",
        "HashSet",
        "Dictionary",
        "IntDictionary",
        "Expando",
        "Object",
    ];

    /// <summary>
    /// The shapes whose own keys are all non-configurable when <c>AllowWrite</c> is off, so that a configurable
    /// property of the script's is the one thing standing between a non-extensible wrapper and
    /// <c>Object.isSealed</c>. <c>HashSet</c> lists its methods, which are configurable; an
    /// <c>ExpandoObject</c> stores what the script assigns.
    /// </summary>
    private static readonly string[] _sealableShapes =
    [
        "List",
        "ReadOnlyCollection",
        "Array",
        "ImmutableArray",
        "ArrayList",
        "ReadOnlyList",
        "JObject",
        "Dictionary",
        "IntDictionary",
        "Object",
    ];

    public static TestCases<string, bool> ShapesAndSettings
    {
        get
        {
            var data = new TestCases<string, bool>();
            foreach (var shape in _shapes)
            {
                data.Add(shape, false);
                data.Add(shape, true);
            }

            return data;
        }
    }

    public static TestCases<string> SealableShapes
    {
        get
        {
            var data = new TestCases<string>();
            foreach (var shape in _sealableShapes)
            {
                data.Add(shape);
            }

            return data;
        }
    }

    /// <summary>
    /// The script's keys follow the target's: strings in creation order, then symbols in creation order.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndSettings))]
    public void TheScriptsKeysFollowTheTargetsKeys(string shape, bool allowWrite)
    {
        var (host, read) = CreateShape(shape);
        var before = read();
        var engine = CreateEngine(host, allowWrite);

        var outcome = engine.Evaluate(
            """
            const target = Reflect.ownKeys(host).map(String);
            const later = Symbol('later');
            Object.defineProperty(host, later, { value: 4, enumerable: true, configurable: true, writable: true });
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true, writable: true });
            Object.defineProperty(host, 'hidden', { value: 2, enumerable: false, configurable: true, writable: true });
            Object.defineProperty(host, Symbol('sym'), { value: 3, enumerable: true, configurable: true, writable: true });
            const expected = target.concat(['tag', 'hidden', 'Symbol(later)', 'Symbol(sym)']);
            const names = Object.getOwnPropertyNames(host);
            const symbols = Object.getOwnPropertySymbols(host).map(String);
            [
              Reflect.ownKeys(host).map(String).join() === expected.join(),
              names.join() === target.concat(['tag', 'hidden']).join(),
              symbols.join(),
            ].join(';')
            """).AsString();

        outcome.Should().Be("true;true;Symbol(later),Symbol(sym)");
        read().Should().Be(before, "listing the wrapper's own keys leaves the CLR object alone");
    }

    /// <summary>
    /// The issue's own reproduction, on a list view and on a plain object: the key is listed, the symbol a write
    /// may add is listed, and <c>Object.freeze</c> now reaches both.
    /// </summary>
    [TestCase("List")]
    [TestCase("Object")]
    public void TheIssuesReproduction(string shape)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true);

        var outcome = engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 1, configurable: true, writable: true, enumerable: true });
            host[Symbol.for('s')] = 1;
            const listed = Reflect.ownKeys(host).includes('tag') && Reflect.ownKeys(host).includes(Symbol.for('s'));
            Object.freeze(host);
            const tag = Object.getOwnPropertyDescriptor(host, 'tag');
            const symbol = Object.getOwnPropertyDescriptor(host, Symbol.for('s'));
            [listed, Object.isFrozen(host), tag.configurable, tag.writable, symbol.configurable, delete host.tag, delete host[Symbol.for('s')], host.tag].join()
            """).AsString();

        outcome.Should().Be("true,true,false,false,false,false,false,1");
    }

    /// <summary>
    /// A symbol written by assignment is created only where <c>AllowWrite</c> permits it, and is listed exactly then.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ASymbolTheScriptAssignsIsListedWhereTheWriteIsAllowed(string shape, bool allowWrite)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Evaluate("host[Symbol.for('assigned')] = 1; Object.getOwnPropertySymbols(host).map(String).join()")
            .AsString().Should().Be(allowWrite ? "Symbol(assigned)" : "");
    }

    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ObjectFreezeMakesTheScriptsPropertiesNonConfigurable(string shape, bool allowWrite)
    {
        var (host, read) = CreateShape(shape);
        var before = read();
        var engine = CreateEngine(host, allowWrite);

        var outcome = engine.Evaluate(Run(strict: false,
            """
            const symbol = Symbol('tag');
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true, writable: true });
            Object.defineProperty(host, symbol, { get() { return 2; }, configurable: true });
            Object.freeze(host);
            const tag = Object.getOwnPropertyDescriptor(host, 'tag');
            const accessor = Object.getOwnPropertyDescriptor(host, symbol);
            return [Object.isFrozen(host), tag.configurable, tag.writable, accessor.configurable, delete host.tag, delete host[symbol], host.tag, host[symbol]].join();
            """)).AsString();

        outcome.Should().Be("true,false,false,false,false,false,1,2");
        engine.Evaluate(Run(strict: true, "delete host.tag; return 'no-throw';")).AsString()
            .Should().Be("TypeError", "a strict-mode delete of a frozen property throws");
        engine.Evaluate(Run(strict: true, "host.tag = 5; return 'no-throw';")).AsString()
            .Should().Be("TypeError", "a strict-mode write to a frozen data property throws");
        engine.Evaluate("host.tag").Should().Be(1);
        read().Should().Be(before);
    }

    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ObjectSealMakesTheScriptsPropertiesNonConfigurableAndLeavesThemWritable(string shape, bool allowWrite)
    {
        var (host, read) = CreateShape(shape);
        var before = read();
        var engine = CreateEngine(host, allowWrite);

        var outcome = engine.Evaluate(Run(strict: true,
            """
            const symbol = Symbol('tag');
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true, writable: true });
            Object.defineProperty(host, symbol, { value: 2, configurable: true, writable: true });
            Object.seal(host);
            host.tag = 3;
            const tag = Object.getOwnPropertyDescriptor(host, 'tag');
            const sym = Object.getOwnPropertyDescriptor(host, symbol);
            return [Object.isSealed(host), tag.configurable, tag.writable, sym.configurable, sym.writable, host.tag, host[symbol]].join();
            """)).AsString();

        // a symbol-keyed write is AllowWrite's to refuse on any wrapper, so only the string property is written
        outcome.Should().Be("true,false,true,false,true,3,2");
        engine.Evaluate(Run(strict: true, "delete host.tag; return 'no-throw';")).AsString().Should().Be("TypeError");
        read().Should().Be(before);
    }

    /// <summary>
    /// <c>Object.isSealed</c> and <c>isFrozen</c> answered <see langword="true"/> for a non-extensible wrapper whose
    /// script-defined property was still configurable, because they never saw it. TestIntegrityLevel must check
    /// every own key.
    /// </summary>
    [TestCaseSource(nameof(SealableShapes))]
    public void IsSealedSeesAConfigurablePropertyOfTheScripts(string shape)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 1, configurable: true });
            Object.preventExtensions(host);
            [Object.isSealed(host), Object.isFrozen(host), delete host.tag, Object.isSealed(host)].join()
            """).AsString().Should().Be("false,false,true,true");
    }

    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ObjectAssignCopiesTheScriptsEnumerableProperties(string shape, bool allowWrite)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Evaluate(
            """
            const symbol = Symbol('tag');
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true });
            Object.defineProperty(host, 'hidden', { value: 2, enumerable: false, configurable: true });
            Object.defineProperty(host, symbol, { value: 3, enumerable: true, configurable: true });
            const copy = Object.assign({}, host);
            [copy.tag, 'hidden' in copy, copy[symbol], Object.keys(host).includes('tag'), Object.keys(host).includes('hidden')].join()
            """).AsString().Should().Be("1,false,3,true,false");
    }

    [TestCaseSource(nameof(ShapesAndSettings))]
    public void ForInVisitsTheScriptsEnumerableProperty(string shape, bool allowWrite)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true });
            Object.defineProperty(host, 'hidden', { value: 2, enumerable: false, configurable: true });
            const seen = [];
            for (const key in host) {
              if (key === 'tag' || key === 'hidden') seen.push(key + '=' + host[key]);
            }
            seen.join()
            """).AsString().Should().Be("tag=1");
    }

    /// <summary>
    /// A wrapper that serializes as an object (not as an array) carries the script's enumerable property, as an
    /// ordinary object's <see href="https://tc39.es/ecma262/#sec-serializejsonobject">SerializeJSONObject</see> does.
    /// </summary>
    [TestCase("Dictionary", "{\"b\":1,\"1\":2,\"tag\":7}")]
    [TestCase("Expando", "{\"a\":1,\"tag\":7}")]
    [TestCase("JObject", "{\"a\":1,\"tag\":7}")]
    [TestCase("Object", "{\"Value\":1,\"tag\":7}")]
    public void JsonStringifyCarriesTheScriptsEnumerableProperty(string shape, string expected)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 7, enumerable: true, configurable: true });
            Object.defineProperty(host, 'hidden', { value: 8, enumerable: false, configurable: true });
            JSON.stringify(host)
            """).AsString().Should().Be(expected);
    }

    /// <summary>
    /// Integer indices first and ascending, then strings in creation order, then symbols in creation order. A plain
    /// wrapper's member names are never indices, so the script's indices lead the list.
    /// </summary>
    [Test]
    public void APlainWrapperListsInOrdinaryOwnPropertyKeysOrder()
    {
        var engine = CreateEngine(new OrderedRecord(), allowWrite: false);

        engine.Evaluate(
            """
            const define = key => Object.defineProperty(host, key, { value: 0, enumerable: true, configurable: true });
            define('5'); define('tag'); define(Symbol('a')); define('0'); define('x'); define(Symbol('b')); define('1.5');
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,5,First,Second,Describe,tag,x,1.5,Symbol(a),Symbol(b)");
    }

    /// <summary>
    /// A dictionary that enumerates its numeric keys ascending and first is in OrdinaryOwnPropertyKeys order
    /// already, and the script's indices join that run.
    /// </summary>
    [Test]
    public void TheScriptsIndicesJoinADictionarysAscendingIndices()
    {
        var host = new Dictionary<int, string> { [1] = "a", [2] = "b", [4] = "c" };
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            const define = key => Object.defineProperty(host, key, { value: 0, enumerable: true, configurable: true });
            define('tag'); define('5'); define('3'); define('0');
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,1,2,3,4,5,tag");
        host.Keys.Should().Equal(1, 2, 4);
    }

    /// <summary>
    /// A dictionary that enumerates its keys in some other order keeps that order, as it did before; the script's
    /// indices lead a list whose first key is not an index.
    /// </summary>
    [Test]
    public void ADictionaryKeepsItsOwnOrder()
    {
        var host = new Dictionary<string, int>(StringComparer.Ordinal) { ["b"] = 1, ["1"] = 2 };
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate("Reflect.ownKeys(host).map(String).join()").AsString().Should().Be("b,1");
        engine.Evaluate(
            """
            const define = key => Object.defineProperty(host, key, { value: 0, enumerable: true, configurable: true });
            define('tag'); define('5'); define('0');
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,5,b,1,tag");
    }

    /// <summary>
    /// Deleting every property the script defined leaves only the target's keys, and the next one it defines is
    /// listed again.
    /// </summary>
    [TestCase("List")]
    [TestCase("Object")]
    public void DeletingEveryScriptPropertyAndDefiningOneAgain(string shape)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            const target = Reflect.ownKeys(host).map(String).join();
            const symbol = Symbol('s');
            Object.defineProperty(host, 'a', { value: 0, configurable: true });
            Object.defineProperty(host, symbol, { value: 0, configurable: true });
            delete host.a;
            delete host[symbol];
            const cleared = Reflect.ownKeys(host).map(String).join() === target;
            Object.defineProperty(host, 'b', { value: 0, configurable: true });
            [cleared, Reflect.ownKeys(host).map(String).join() === target + ',b'].join()
            """).AsString().Should().Be("true,true");
    }

    /// <summary>
    /// Two wrappers over one CLR object (they are <c>Equals</c> by target) each keep the properties the script
    /// defined on them.
    /// </summary>
    [Test]
    public void TwoWrappersOverOneObjectKeepTheirOwnKeys()
    {
        var engine = new Engine();
        var target = new HostRecord();
        engine.SetValue("first", ObjectWrapper.Create(engine, target));
        engine.SetValue("second", ObjectWrapper.Create(engine, target));

        engine.Evaluate(
            """
            Object.defineProperty(first, 'tag', { value: 1, enumerable: true, configurable: true });
            [Reflect.ownKeys(first).join(), Reflect.ownKeys(second).join()].join(';')
            """).AsString().Should().Be("Value,tag;Value");
    }

    /// <summary>
    /// A property deleted and defined again is the newest one.
    /// </summary>
    [TestCase("List")]
    [TestCase("Object")]
    [TestCase("Dictionary")]
    public void ADeletedPropertyDefinedAgainMovesToTheEnd(string shape)
    {
        var (host, _) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            const target = Reflect.ownKeys(host).length;
            const define = key => Object.defineProperty(host, key, { value: 0, enumerable: true, configurable: true });
            define('p'); define('q'); define('r');
            delete host.p;
            const afterDelete = Reflect.ownKeys(host).slice(target).join();
            define('p');
            afterDelete + ';' + Reflect.ownKeys(host).slice(target).join()
            """).AsString().Should().Be("q,r;q,r,p");
    }

    /// <summary>
    /// A CLR member the script redefines is still the target's: listed once, where the target lists it, and on a view
    /// (which lists only its positions) not at all. A method's descriptor is configurable, so it can be redefined as
    /// data and then as an accessor.
    /// </summary>
    [Test]
    public void ARedefinedClrMemberIsListedOnce()
    {
        var engine = CreateEngine(new OrderedRecord(), allowWrite: true);

        engine.Evaluate(
            """
            const before = Reflect.ownKeys(host).map(String).join();
            Object.defineProperty(host, 'Describe', { value: 'data', configurable: true });
            const afterData = Reflect.ownKeys(host).map(String).join();
            Object.defineProperty(host, 'Describe', { get() { return 'accessor'; }, configurable: true });
            const afterAccessor = Reflect.ownKeys(host).map(String).join();
            [before === afterData, before === afterAccessor, afterAccessor, host.Describe].join(';')
            """).AsString().Should().Be("true;true;First,Second,Describe;accessor");
    }

    [Test]
    public void AViewsRedefinedMemberIsStillNotListed()
    {
        var (host, _) = CreateShape("List");
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'Contains', { value: 'redefined', configurable: true });
            Object.defineProperty(host, 'tag', { value: 1, configurable: true });
            Reflect.ownKeys(host).map(String).join()
            """).AsString().Should().Be("0,1,tag");
    }

    /// <summary>
    /// A key the script defined that the target comes to report as well is listed once, in the target's place.
    /// </summary>
    [Test]
    public void AKeyTheDictionaryGainsLaterIsListedOnce()
    {
        var host = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 };
        var engine = CreateEngine(host, allowWrite: false);

        engine.Execute("Object.defineProperty(host, 'tag', { value: 'script', enumerable: true, configurable: true }); Object.defineProperty(host, 'z', { value: 0, configurable: true });");
        host["tag"] = 7;

        engine.Evaluate("Reflect.ownKeys(host).join()").AsString().Should().Be("a,tag,z");
        engine.Evaluate("host.tag").AsString().Should().Be("script", "the property the script defined still outranks the entry");
    }

    /// <summary>
    /// Reading, caching and freezing what a wrapper projects never adds a key: only a property the script creates does.
    /// </summary>
    [Test]
    public void ReadingTheTargetsMembersAddsNoKey()
    {
        var engine = CreateEngine(new OrderedRecord(), allowWrite: false);

        engine.Evaluate(
            """
            const before = Reflect.ownKeys(host).map(String).join();
            host.First; host.Describe; host.ToString(); host.GetHashCode(); String(host); host[Symbol.iterator];
            Object.getOwnPropertyDescriptor(host, 'GetType');
            Object.freeze(host);
            const after = Reflect.ownKeys(host).map(String).join();
            [before === after, after].join(';')
            """).AsString().Should().Be("true;First,Second,Describe");
    }

    [Test]
    public void FreezingADictionaryListsEachEntryOnce()
    {
        var host = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 2 };
        var engine = CreateEngine(host, allowWrite: true);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 0, configurable: true });
            Object.freeze(host);
            [Reflect.ownKeys(host).join(), Object.isFrozen(host)].join(';')
            """).AsString().Should().Be("a,b,tag;true");
    }

    /// <summary>
    /// <c>for..of</c> over a dictionary iterates the dictionary, not the wrapper's own keys.
    /// </summary>
    [Test]
    public void ForOfOverADictionaryYieldsOnlyItsEntries()
    {
        var host = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1, ["b"] = 2 };
        var engine = CreateEngine(host, allowWrite: false);

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 0, enumerable: true, configurable: true });
            const seen = [];
            for (const [key, value] of host) seen.push(key + '=' + value);
            seen.join()
            """).AsString().Should().Be("a=1,b=2");
    }

    /// <summary>
    /// The host side reads the same hook: <see cref="ObjectInstance.GetOwnProperties"/> is derived from it.
    /// </summary>
    [Test]
    public void GetOwnPropertiesIncludesTheScriptsProperties()
    {
        var (host, _) = CreateShape("Object");
        var engine = CreateEngine(host, allowWrite: false);
        engine.Execute("Object.defineProperty(host, 'tag', { value: 'script', enumerable: true, configurable: true });");

        var wrapper = (ObjectInstance) engine.GetValue("host");
        wrapper.GetOwnProperties().Select(pair => pair.Key + "=" + pair.Value.Value).Should().Equal("Value=1", "tag=script");
    }

    /// <summary>
    /// A script-created property is reported even under a host's own reported keys, after them.
    /// </summary>
    [Test]
    public void TheScriptsKeysFollowAHostsReportedKeys()
    {
        var engine = new Engine(options =>
        {
            options.Interop.ObjectWrapperReportedPropertyKeys = static (_, target) => target is OrderedRecord ? [JsString.Create("Second"), JsString.Create("tag")] : null;
        });
        engine.SetValue("host", new OrderedRecord());

        engine.Evaluate(
            """
            Object.defineProperty(host, 'tag', { value: 1, enumerable: true, configurable: true });
            Object.defineProperty(host, 'other', { value: 1, enumerable: true, configurable: true });
            Reflect.ownKeys(host).join()
            """).AsString().Should().Be("Second,tag,other");
    }

    private static Engine CreateEngine(object host, bool allowWrite)
    {
        var engine = new Engine(options =>
        {
            options.Interop.AllowWrite = allowWrite;
            options.Interop.ArrayConversion = ArrayConversionMode.LiveView;
        });
        engine.SetValue("host", host);
        return engine;
    }

    private static string Run(bool strict, string body)
    {
        var prologue = strict ? "'use strict';\n" : "";
        return "(function () { " + prologue + "try {\n" + body + "\n} catch (e) { return e instanceof TypeError ? 'TypeError' : 'unexpected: ' + e; } })()";
    }

    private static (object Host, Func<string> Read) CreateShape(string kind)
    {
        switch (kind)
        {
            case "List":
            {
                var items = new List<int> { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "ReadOnlyCollection":
            {
                var items = new List<int> { 1, 2 };
                return (new ReadOnlyCollection<int>(items), () => string.Join(",", items));
            }
            case "Array":
            {
                var items = new[] { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "ImmutableArray":
            {
                var items = ImmutableArray.Create(1, 2);
                return (items, () => string.Join(",", items));
            }
            case "ArrayList":
            {
                var items = new ArrayList { 1, 2 };
                return (items, () => string.Join(",", items.Cast<object?>()));
            }
            case "ReadOnlyList":
            {
                var items = new List<int> { 1, 2 };
                return (new HostReadOnlySequence(items), () => string.Join(",", items));
            }
            case "JObject":
            {
                var items = new JObject { ["a"] = 1 };
                return (items, () => items.ToString(Newtonsoft.Json.Formatting.None));
            }
            case "HashSet":
            {
                var items = new HashSet<int> { 1, 2 };
                return (items, () => string.Join(",", items));
            }
            case "Dictionary":
            {
                var items = new Dictionary<string, int>(StringComparer.Ordinal) { ["b"] = 1, ["1"] = 2 };
                return (items, () => string.Join(",", items.Select(pair => pair.Key + "=" + pair.Value)));
            }
            case "IntDictionary":
            {
                var items = new Dictionary<int, string> { [1] = "a" };
                return (items, () => string.Join(",", items.Select(pair => pair.Key + "=" + pair.Value)));
            }
            case "Expando":
            {
                IDictionary<string, object?> items = new ExpandoObject();
                items["a"] = 1;
                return (items, () => string.Join(",", items.Select(pair => pair.Key + "=" + pair.Value)));
            }
            case "Object":
            {
                var item = new HostRecord();
                return (item, () => item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown shape");
        }
    }

    public sealed class HostRecord
    {
        public int Value { get; set; } = 1;
    }

    /// <summary>
    /// Two properties and a method, so a plain wrapper lists three member names.
    /// </summary>
    public sealed class OrderedRecord
    {
        public int First { get; set; } = 1;

        public string Second { get; set; } = "two";

        public string Describe() => First + Second;
    }

    /// <summary>
    /// A collection offering only <see cref="IReadOnlyList{T}"/>, which is what the read-only list view wraps.
    /// </summary>
    private sealed class HostReadOnlySequence(List<int> items) : IReadOnlyList<int>
    {
        public int Count => items.Count;

        public int this[int index] => items[index];

        public IEnumerator<int> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

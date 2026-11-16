#nullable enable

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what a script's <c>[[Set]]</c> does on a wrapped dictionary whose key type is not <see cref="string"/>
/// when the key it names is one the dictionary cannot hold (#4518).
/// </summary>
/// <remarks>
/// <para>
/// <c>dictInt.expando = 2</c> on a <c>Dictionary&lt;int, string&gt;</c> under
/// <see cref="Options.InteropOptions.AllowWrite"/> used to raise the dictionary's own
/// <see cref="ArgumentException"/> out of <c>Evaluate</c>. A name that does not convert to the key type
/// reached the dictionary through its non-generic <see cref="IDictionary"/> indexer, which takes any object
/// and then rejects every one that is not a <c>TKey</c>.
/// </para>
/// <para>
/// Such a key cannot be an entry. It is now what a name the CLR type has no member for is on any wrapper: a
/// property of the wrapper itself (https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor, which ends in
/// CreateDataProperty on the receiver). It is gated on <c>AllowWrite</c> exactly as a symbol key is on
/// the same wrapper, so the two dictionary shapes answer <c>[[Set]]</c> alike: a string-keyed dictionary takes
/// every name as an entry under <c>AllowWrite</c> and refuses every one without it.
/// </para>
/// <para>
/// The oracle is the host dictionary, read from C#, and the descriptor the script reads back, compared with
/// what Node reports for an ordinary object: <c>{"value":2,"writable":true,"enumerable":true,"configurable":true}</c>.
/// A dictionary that implements only the generic <see cref="IDictionary{TKey,TValue}"/> already behaved this
/// way and is the reference row.
/// </para>
/// </remarks>
public class HostDictionaryForeignKeyWriteTests
{
    private const string Sentinel = "one";

    /// <summary>
    /// The shapes that implement the non-generic <see cref="IDictionary"/> beside the generic contract, which
    /// is every dictionary the base class library ships.
    /// </summary>
    private static readonly string[] _nonGenericIndexerShapes =
    [
        "Dictionary<int, string>",
        "Dictionary<long, string>",
        "Dictionary<Guid, string>",
        "Dictionary<DayOfWeek, string>",
        "Dictionary<DateTime, string>",
        "SortedDictionary<int, string>",
        "SortedList<int, string>",
        "ConcurrentDictionary<int, string>",
        "ReadOnlyDictionary<int, string>",
    ];

    private static readonly string[] _shapes = [.. _nonGenericIndexerShapes, nameof(GenericOnlyDictionary)];

    public static TestCases<string> Shapes => new(_shapes);

    public static TestCases<string> NonGenericIndexerShapes => new(_nonGenericIndexerShapes);

    public static TestCases<string, bool> ShapesAndModes => WithModes(_shapes);

    public static TestCases<string, bool> NonGenericIndexerShapesAndModes => WithModes(_nonGenericIndexerShapes);

    private static TestCases<string, bool> WithModes(string[] shapes)
    {
        var data = new TestCases<string, bool>();
        foreach (var shape in shapes)
        {
            data.Add(shape, false);
            data.Add(shape, true);
        }

        return data;
    }

    /// <summary>
    /// Names that are neither an entry key nor a CLR member, per key type. <c>"1.5"</c> is index-shaped but
    /// not an integer, so an integral key type cannot hold it; a <see cref="DateTime"/> key can (it parses
    /// as a date), which is why it is not listed there.
    /// </summary>
    public static TestCases<string, string> ShapesAndForeignKeys => new()
    {
        { "Dictionary<int, string>", "expando" },
        { "Dictionary<int, string>", "1.5" },
        { "Dictionary<int, string>", "Monday" },
        { "Dictionary<int, string>", "" },
        { "Dictionary<long, string>", "expando" },
        { "Dictionary<long, string>", "1.5" },
        { "Dictionary<Guid, string>", "expando" },
        { "Dictionary<Guid, string>", "5" },
        { "Dictionary<DayOfWeek, string>", "expando" },
        { "Dictionary<DayOfWeek, string>", "1.5" },
        { "Dictionary<DateTime, string>", "expando" },
        { "Dictionary<DateTime, string>", "-1" },
        { "SortedDictionary<int, string>", "expando" },
        { "SortedList<int, string>", "expando" },
        { "ConcurrentDictionary<int, string>", "expando" },
        { "ReadOnlyDictionary<int, string>", "expando" },
        { nameof(GenericOnlyDictionary), "expando" },
        { nameof(GenericOnlyDictionary), "1.5" },
    };

    private static (object Host, Func<int> Count) CreateShape(string shape)
    {
        switch (shape)
        {
            case "Dictionary<int, string>":
            {
                var d = new Dictionary<int, string> { [1] = Sentinel };
                return (d, () => d.Count);
            }
            case "Dictionary<long, string>":
            {
                var d = new Dictionary<long, string> { [1] = Sentinel };
                return (d, () => d.Count);
            }
            case "Dictionary<Guid, string>":
            {
                var d = new Dictionary<Guid, string> { [Guid.Empty] = Sentinel };
                return (d, () => d.Count);
            }
            case "Dictionary<DayOfWeek, string>":
            {
                var d = new Dictionary<DayOfWeek, string> { [DayOfWeek.Sunday] = Sentinel };
                return (d, () => d.Count);
            }
            case "Dictionary<DateTime, string>":
            {
                var d = new Dictionary<DateTime, string> { [DateTime.MinValue] = Sentinel };
                return (d, () => d.Count);
            }
            case "SortedDictionary<int, string>":
            {
                var d = new SortedDictionary<int, string> { [1] = Sentinel };
                return (d, () => d.Count);
            }
            case "SortedList<int, string>":
            {
                var d = new SortedList<int, string> { [1] = Sentinel };
                return (d, () => d.Count);
            }
            case "ConcurrentDictionary<int, string>":
            {
                var d = new ConcurrentDictionary<int, string>();
                d[1] = Sentinel;
                return (d, () => d.Count);
            }
            case "ReadOnlyDictionary<int, string>":
            {
                var d = new ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [1] = Sentinel });
                return (d, () => d.Count);
            }
            case nameof(GenericOnlyDictionary):
            {
                var d = new GenericOnlyDictionary { [1] = Sentinel };
                return (d, () => d.Count);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown shape");
        }
    }

    private static Engine CreateEngine(object host, bool allowWrite, bool strict = false, bool throwOnUnresolvedMember = false)
    {
        var engine = new Engine(options =>
        {
            options.Strict = strict;
            options.Interop.AllowWrite = allowWrite;
            options.Interop.ThrowOnUnresolvedMember = throwOnUnresolvedMember;
        });
        engine.SetValue("d", host);
        return engine;
    }

    private static string Literal(string key) => "'" + key + "'";

    /// <summary>
    /// The reported case, verbatim: under <c>AllowWrite</c> the write no longer escapes as the dictionary's
    /// <see cref="ArgumentException"/>, and lands on the wrapper.
    /// </summary>
    [Test]
    public void TheReportedWriteKeepsThePropertyOnTheWrapper()
    {
        var dictInt = new Dictionary<int, string> { [1] = "a" };
        var engine = new Engine(options => options.Interop.AllowWrite = true);
        engine.SetValue("dictInt", dictInt);

        var thrown = Caught.Exception(() => engine.Execute("dictInt.expando = 2;"));

        thrown.Should().BeNull();
        engine.Evaluate("dictInt.expando").AsNumber().Should().Be(2);
        engine.Evaluate("dictInt[1]").AsString().Should().Be("a");
        dictInt.Should().HaveCount(1).And.ContainKey(1);

        // a second write updates the wrapper's own property, as it does on any object
        engine.Execute("dictInt.expando = 3;");
        engine.Evaluate("dictInt.expando").AsNumber().Should().Be(3);
        dictInt.Should().HaveCount(1);
    }

    [TestCaseSource(nameof(ShapesAndForeignKeys))]
    public void AKeyTheDictionaryCannotHoldBecomesAPropertyOfTheWrapper(string shape, string key)
    {
        foreach (var strict in new[] { false, true })
        {
            var (host, count) = CreateShape(shape);
            var engine = CreateEngine(host, allowWrite: true, strict);

            var thrown = Caught.Exception(() => engine.Execute($"d[{Literal(key)}] = 2;"));

            thrown.Should().BeNull($"strict: {strict}");
            engine.Evaluate($"d[{Literal(key)}]").AsNumber().Should().Be(2);
            engine.Evaluate($"JSON.stringify(Object.getOwnPropertyDescriptor(d, {Literal(key)}))").AsString()
                .Should().Be("""{"value":2,"writable":true,"enumerable":true,"configurable":true}""");
            engine.Evaluate($"{Literal(key)} in d").AsBoolean().Should().BeTrue();
            engine.Evaluate($"d.hasOwnProperty({Literal(key)})").AsBoolean().Should().BeTrue();
            count().Should().Be(1, "the key cannot be an entry, so the dictionary is untouched");
        }
    }

    [TestCaseSource(nameof(Shapes))]
    public void ReflectSetReportsTheWriteItMade(string shape)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true);

        engine.Evaluate("Reflect.set(d, 'expando', 2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.expando").AsNumber().Should().Be(2);
        count().Should().Be(1);
    }

    /// <summary>
    /// Without <c>AllowWrite</c> the write is refused, as it was before and as it is on a string-keyed
    /// dictionary: <c>false</c> from <c>[[Set]]</c>, which is a <c>TypeError</c> in strict mode
    /// (https://tc39.es/ecma262/#sec-putvalue). The reference shape is not a row here, and never refused: with
    /// no non-generic indexer it resolves the name as a plain object does, which keeps a property whatever
    /// <c>AllowWrite</c> says.
    /// </summary>
    [TestCaseSource(nameof(NonGenericIndexerShapesAndModes))]
    public void WithoutAllowWriteTheWriteIsRefused(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false, strict);

        engine.Evaluate("Reflect.set(d, 'expando', 2)").AsBoolean().Should().BeFalse();
        engine.Evaluate("try { d.expando = 2; 'no error' } catch (e) { e.constructor.name }").AsString()
            .Should().Be(strict ? "TypeError" : "no error");
        engine.Evaluate("typeof d.expando").AsString().Should().Be("undefined");
        engine.Evaluate("'expando' in d").AsBoolean().Should().BeFalse();
        count().Should().Be(1);
    }

    /// <summary>
    /// A name the dictionary cannot hold that is a CLR member is that member, and a read-only one refuses the
    /// write: Node's answer for an accessor with no setter, <c>false</c> from <c>Reflect.set</c> and a
    /// <c>TypeError</c> in strict mode. It used to reach the non-generic indexer too, because the
    /// writable-filtered resolution skips a member that cannot be written.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void AReadOnlyMemberNameIsRefusedRatherThanStoredAnywhere(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        engine.Evaluate("Reflect.set(d, 'Count', 5)").AsBoolean().Should().BeFalse();
        engine.Evaluate("try { d.Count = 5; 'no error' } catch (e) { e.constructor.name }").AsString()
            .Should().Be(strict ? "TypeError" : "no error");
        engine.Evaluate("d.Count").AsNumber().Should().Be(1);
        engine.Evaluate("Object.getOwnPropertyDescriptor(d, 'Count') === undefined || typeof Object.getOwnPropertyDescriptor(d, 'Count').get === 'function'")
            .AsBoolean().Should().BeTrue("the CLR member is not shadowed by a data property");
        count().Should().Be(1);
    }

    [TestCaseSource(nameof(ShapesAndModes))]
    public void AMethodNameIsRefusedAndTheMethodStillAnswers(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        engine.Evaluate("try { d.ContainsKey = 5; 'no error' } catch (e) { e.constructor.name }").AsString()
            .Should().Be(strict ? "TypeError" : "no error");
        engine.Evaluate("typeof d.ContainsKey").AsString().Should().Be("function");
        count().Should().Be(1);
    }

    /// <summary>
    /// An index-shaped name the key type parses is an entry, as before: <c>"5"</c> and <c>"-1"</c> on an
    /// integral key (the dictionary holds negative keys), a member name on an enum key, a date on a
    /// <see cref="DateTime"/> key.
    /// </summary>
    [Test]
    public void ANameTheKeyTypeParsesIsStillAnEntry()
    {
        var ints = new Dictionary<int, string> { [1] = Sentinel };
        var engine = CreateEngine(ints, allowWrite: true);
        engine.Execute("d['5'] = 'five'; d['-1'] = 'minus one';");
        ints.Should().HaveCount(3);
        ints[5].Should().Be("five");
        ints[-1].Should().Be("minus one");
        engine.Evaluate("d[5] + '/' + d['-1']").AsString().Should().Be("five/minus one");
        engine.Evaluate("d.hasOwnProperty('5')").AsBoolean().Should().BeTrue();

        var longs = new Dictionary<long, string> { [1] = Sentinel };
        engine = CreateEngine(longs, allowWrite: true);
        engine.Execute("d['5'] = 'five'; d['-1'] = 'minus one';");
        longs.Should().HaveCount(3);
        longs[5].Should().Be("five");
        longs[-1].Should().Be("minus one");

        var days = new Dictionary<DayOfWeek, string> { [DayOfWeek.Sunday] = Sentinel };
        engine = CreateEngine(days, allowWrite: true);
        engine.Execute("d.Monday = 'm';");
        days.Should().HaveCount(2);
        days[DayOfWeek.Monday].Should().Be("m");

        var dates = new Dictionary<DateTime, string> { [DateTime.MinValue] = Sentinel };
        engine = CreateEngine(dates, allowWrite: true);
        engine.Execute("d['2020-01-01'] = 'new year';");
        dates.Should().HaveCount(2);
        dates[new DateTime(2020, 1, 1)].Should().Be("new year");
    }

    /// <summary>
    /// <c>Object.defineProperty</c> with the same key always defined a property of the wrapper, whatever
    /// <c>AllowWrite</c> says; it still does, and an assignment then updates that property.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void DefinePropertyWithTheSameKeyDefinesItOnTheWrapper(string shape, bool allowWrite)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        engine.Execute("Object.defineProperty(d, 'expando', { value: 2, writable: true, enumerable: true, configurable: true });");

        engine.Evaluate("d.expando").AsNumber().Should().Be(2);
        engine.Execute("d.expando = 3;");
        engine.Evaluate("d.expando").AsNumber().Should().Be(3);
        engine.Evaluate("Reflect.defineProperty(d, 'expando', { value: 4 })").AsBoolean().Should().BeTrue();
        engine.Evaluate("d.expando").AsNumber().Should().Be(4);
        count().Should().Be(1);
    }

    /// <summary>
    /// <c>delete</c> with the same key removes the wrapper's property and touches no entry, in both modes;
    /// with nothing to delete it answers <c>true</c>, as for any absent property.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void DeleteWithTheSameKeyRemovesTheWrappersProperty(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        engine.Evaluate("delete d.expando").AsBoolean().Should().BeTrue();

        engine.Execute("d.expando = 2;");
        engine.Evaluate("delete d.expando").AsBoolean().Should().BeTrue();
        engine.Evaluate("'expando' in d").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof d.expando").AsString().Should().Be("undefined");
        count().Should().Be(1);
    }

    [TestCaseSource(nameof(ShapesAndModes))]
    public void DeleteWithoutAllowWriteAnswersAsForAnAbsentProperty(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false, strict);

        engine.Evaluate("delete d.expando").AsBoolean().Should().BeTrue();
        count().Should().Be(1);
    }

    /// <summary>
    /// A non-extensible wrapper refuses the new property, as an ordinary object does: Node answers
    /// <c>false</c> from <c>Reflect.set</c> and, in strict mode, a <c>TypeError</c>.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void ANonExtensibleWrapperRefusesTheProperty(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        engine.Execute("Object.preventExtensions(d);");

        engine.Evaluate("Reflect.set(d, 'expando', 2)").AsBoolean().Should().BeFalse();
        engine.Evaluate("try { d.expando = 2; 'no error' } catch (e) { e.constructor.name }").AsString()
            .Should().Be(strict ? "TypeError" : "no error");
        engine.Evaluate("typeof d.expando").AsString().Should().Be("undefined");
        count().Should().Be(1);
    }

    /// <summary>
    /// <see cref="Options.InteropOptions.ThrowOnUnresolvedMember"/> does not turn the key into a CLR
    /// exception either: a dictionary's reads were already exempt from it, and the write now agrees with them.
    /// The reference shape is not a row here: with no non-generic indexer it resolves the name as a plain object
    /// does, and that option reports such a name on a plain object with its own exception, before and after.
    /// </summary>
    [TestCaseSource(nameof(NonGenericIndexerShapes))]
    public void ThrowOnUnresolvedMemberDoesNotTurnTheWriteIntoAClrException(string shape)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, throwOnUnresolvedMember: true);

        engine.Evaluate("'expando' in d").AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof d.expando").AsString().Should().Be("undefined");

        var thrown = Caught.Exception(() => engine.Execute("d.expando = 2;"));

        thrown.Should().BeNull();
        engine.Evaluate("d.expando").AsNumber().Should().Be(2);
        count().Should().Be(1);
    }

    /// <summary>
    /// The string-keyed counterpart the semantics are chosen to match: under <c>AllowWrite</c> every name is
    /// an entry, and without it every name is refused. A symbol, the one key it cannot hold, is a property of
    /// the wrapper under <c>AllowWrite</c>, which is what a non-string-keyed dictionary now does with a name.
    /// </summary>
    [Test]
    public void AStringKeyedDictionaryIsTheCounterpart()
    {
        var strings = new Dictionary<string, string> { ["1"] = Sentinel };
        var engine = CreateEngine(strings, allowWrite: true);
        engine.Execute("d.expando = '2'; var s = Symbol('s'); d[s] = 3;");
        strings.Should().HaveCount(2);
        strings["expando"].Should().Be("2");
        engine.Evaluate("d[s]").AsNumber().Should().Be(3);

        var ints = new Dictionary<int, string> { [1] = Sentinel };
        engine = CreateEngine(ints, allowWrite: true);
        engine.Execute("d.expando = 2; var s = Symbol('s'); d[s] = 3;");
        ints.Should().HaveCount(1);
        engine.Evaluate("d.expando + d[s]").AsNumber().Should().Be(5);

        engine = CreateEngine(new Dictionary<string, string>(), allowWrite: false, strict: true);
        engine.Evaluate("try { d.expando = 2; 'no error' } catch (e) { e.constructor.name }").AsString().Should().Be("TypeError");
    }

    // How the kept key is enumerated (#4503, migration guide 4.223). A property the script creates on a wrapper is
    // listed after the target's own keys because ObjectWrapper.SetOwnProperty marks a creation. The key this class
    // keeps on the wrapper reaches SetOwnProperty through [[Set]]'s ordinary algorithm
    // (https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor, CreateDataProperty on the receiver), so it is
    // such a creation, and every test below fails if it stops being one. Each expectation is relative to the list
    // the same enumeration gave before the script wrote anything, so it holds for a shape whose target lists
    // entries (int keys), none it can name (a Guid key has no string spelling the wrapper reports), or its CLR
    // members (the reference shape). The values are Node's for an ordinary object holding the same keys.

    /// <summary>The enumerations that yield the string keys, each of which reads the one hook.</summary>
    private static readonly string[] _stringKeyEnumerations =
    [
        "Reflect.ownKeys(d).filter(k => typeof k === 'string')",
        "Object.getOwnPropertyNames(d)",
        "Object.keys(d)",
        "(() => { const r = []; for (const k in d) r.push(k); return r; })()",
        "Object.entries(d).map(e => e[0])",
        "Object.keys({ ...d })",
        "Object.keys(Object.assign({}, d))",
        "Object.keys(JSON.parse(JSON.stringify(d)))",
    ];

    private static (string Enumeration, string Expected)[] AllStringKeys(string expected)
        => [.. _stringKeyEnumerations.Select(enumeration => (enumeration, expected))];

    /// <summary>
    /// Takes every enumeration in <paramref name="checks"/> before and after <paramref name="script"/> runs, and
    /// answers which of them is not what its expectation (a function of the list taken before) says; empty when
    /// all agree.
    /// </summary>
    /// <remarks>
    /// The reference shape lists its CLR members, and the order reflection returns them in is not stable: on .NET
    /// Framework it changes as members are resolved, in this process and not only in this engine. For that shape the
    /// members the engine listed at first are compared as interchangeable, so that the keys the script created are
    /// still compared by position among them and after them.
    /// </remarks>
    private static string Disagreements(Engine engine, string shape, string script, params (string Enumeration, string Expected)[] checks)
    {
        var enumerations = string.Join(",\n", checks.Select(check => $"() => {check.Enumeration}"));
        var expectations = string.Join(",\n", checks.Select(check => check.Expected));
        var reflected = shape == nameof(GenericOnlyDictionary) ? "true" : "false";
        engine.Execute($"var enumerations = [{enumerations}]; var expectations = [{expectations}];");
        engine.Execute("var before = enumerations.map(e => e()); globalThis.members ??= new Set(before.flat());");
        engine.Execute(script);
        return engine.Evaluate(
            $$"""
            enumerations.map((e, i) => {
              const canonical = list => JSON.stringify({{reflected}} ? list.map(k => members.has(k) ? '#' : k) : list);
              const got = canonical(e());
              const want = canonical(expectations[i](before[i]));
              return got === want ? '' : e.toString() + ' gave ' + got + ', wanted ' + want;
            }).filter(x => x).join('; ')
            """).AsString();
    }

    private static string Listed(string key)
        => $"b => /^(0|[1-9][0-9]*)$/.test({Literal(key)}) && Number({Literal(key)}) < 4294967295 ? [{Literal(key)}, ...b] : [...b, {Literal(key)}]";

    /// <summary>
    /// The key follows what the dictionary lists and is listed by every enumeration, in either mode. Where the key is
    /// an array index that the key type cannot hold (<c>"5"</c> on a <see cref="Guid"/> key), it leads the list, as
    /// an index does on an ordinary object.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndForeignKeys))]
    public void AForeignNameIsListedAfterTheTargetsKeys(string shape, string key)
    {
        foreach (var strict in new[] { false, true })
        {
            var (host, count) = CreateShape(shape);
            var engine = CreateEngine(host, allowWrite: true, strict);

            Disagreements(engine, shape, $"d[{Literal(key)}] = 2;", AllStringKeys(Listed(key))).Should().BeEmpty($"strict: {strict}");
            count().Should().Be(1);
        }
    }

    /// <summary>
    /// A read that answers "absent" before the write (<c>in</c>, <c>hasOwnProperty</c>, the descriptor, the value)
    /// leaves nothing on the wrapper that would stand between the write and the listing.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void AReadBeforeTheWriteStillLeavesTheKeyListed(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        Disagreements(
            engine,
            shape,
            """
            void d.expando;
            'expando' in d;
            d.hasOwnProperty('expando');
            Object.getOwnPropertyDescriptor(d, 'expando');
            d.expando = 2;
            d.expando = 3;
            """,
            AllStringKeys("b => [...b, 'expando']")).Should().BeEmpty();
        engine.Evaluate("d.expando").AsNumber().Should().Be(3);
        count().Should().Be(1);
    }

    /// <summary>
    /// A symbol is the one key a dictionary cannot hold under any key type, so it is a property of the wrapper
    /// whatever <c>AllowWrite</c> is allowed to do to a name. It is listed after the strings, and is no string key.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void ASymbolIsListedAfterTheStrings(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        Disagreements(
            engine,
            shape,
            "d[Symbol.for('s')] = 3; d.expando = 2;",
            [
                .. AllStringKeys("b => [...b, 'expando']"),
                ("Reflect.ownKeys(d).map(String)", "b => [...b, 'expando', 'Symbol(s)']"),
                ("Object.getOwnPropertySymbols(d).map(String)", "b => ['Symbol(s)']"),
            ]).Should().BeEmpty();
        engine.Evaluate("d[Symbol.for('s')]").AsNumber().Should().Be(3);
        count().Should().Be(1);
    }

    /// <summary>
    /// The order is OrdinaryOwnPropertyKeys over the target's keys and the script's, the target's being the older
    /// (https://tc39.es/ecma262/#sec-ordinaryownpropertykeys): array indices ascending, with the dictionary's own
    /// leading ones, then strings in creation order, then symbols in creation order. Every index below is
    /// larger than an <see cref="int"/> key can be, so it cannot be an entry. A key deleted and written again
    /// is the newest, and the dictionary is untouched throughout. Node gives the same order for
    /// <c>{ 1: 'a', 2: 'b' }</c> given the same writes.
    /// </summary>
    [TestCase("Dictionary<int, string>")]
    [TestCase("SortedDictionary<int, string>")]
    [TestCase("SortedList<int, string>")]
    public void TheKeysKeepOrdinaryOwnPropertyKeysOrder(string shape)
    {
        foreach (var strict in new[] { false, true })
        {
            var (host, count) = CreateShape(shape);
            switch (host)
            {
                case Dictionary<int, string> dictionary:
                    dictionary[2] = "b";
                    break;
                case SortedDictionary<int, string> sorted:
                    sorted[2] = "b";
                    break;
                case SortedList<int, string> list:
                    list[2] = "b";
                    break;
            }

            var engine = CreateEngine(host, allowWrite: true, strict);

            engine.Execute(
                """
                d.foo = 1;
                d['3000000000'] = 2;
                d['4294967295'] = 3;
                d.bar = 4;
                d['1.5'] = 5;
                d[Symbol.for('s1')] = 6;
                d['2147483648'] = 7;
                d[Symbol.for('s2')] = 8;
                """);

            engine.Evaluate("Reflect.ownKeys(d).map(String).join()").AsString()
                .Should().Be("1,2,2147483648,3000000000,foo,4294967295,bar,1.5,Symbol(s1),Symbol(s2)", $"strict: {strict}");
            engine.Evaluate("Object.keys(d).join()").AsString()
                .Should().Be("1,2,2147483648,3000000000,foo,4294967295,bar,1.5");

            engine.Execute("delete d.foo; d.foo = 9; delete d[Symbol.for('s1')]; d[Symbol.for('s1')] = 10;");

            engine.Evaluate("Reflect.ownKeys(d).map(String).join()").AsString()
                .Should().Be("1,2,2147483648,3000000000,4294967295,bar,1.5,foo,Symbol(s2),Symbol(s1)");
            count().Should().Be(2, "no key of the script's was ever an entry");
        }
    }

    /// <summary>
    /// <c>delete</c> forgets the key everywhere, and writing it again lists it after what was written in between.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void ADeletedKeyIsNoLongerListed(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        Disagreements(
            engine,
            shape,
            "d.expando = 2; d.other = 3; d[Symbol.for('s')] = 4;",
            [
                .. AllStringKeys("b => [...b, 'expando', 'other']"),
                ("Reflect.ownKeys(d).map(String)", "b => [...b, 'expando', 'other', 'Symbol(s)']"),
                ("Object.getOwnPropertySymbols(d).map(String)", "b => ['Symbol(s)']"),
            ]).Should().BeEmpty("they are listed before they are deleted");

        Disagreements(
            engine,
            shape,
            "delete d.expando; delete d[Symbol.for('s')];",
            [
                .. AllStringKeys("b => b.filter(k => k !== 'expando')"),
                ("Reflect.ownKeys(d).map(String)", "b => b.filter(k => k !== 'expando' && k !== 'Symbol(s)')"),
                ("Object.getOwnPropertySymbols(d).map(String)", "b => []"),
            ]).Should().BeEmpty();
        engine.Evaluate("'expando' in d || d.hasOwnProperty('expando') || Symbol.for('s') in d").AsBoolean().Should().BeFalse();

        Disagreements(
            engine,
            shape,
            "d.expando = 5;",
            [
                .. AllStringKeys("b => [...b, 'expando']"),
                ("Reflect.ownKeys(d).map(String)", "b => [...b, 'expando']"),
            ]).Should().BeEmpty("the key is written again after 'other'");
        count().Should().Be(1);
    }

    /// <summary>
    /// A write the wrapper refuses lists nothing: without <c>AllowWrite</c> a name and a symbol are both refused,
    /// as Node refuses them on a frozen object. The reference shape is not a row: with no non-generic indexer it
    /// keeps the name as a plain object does, <c>AllowWrite</c> or not.
    /// </summary>
    [TestCaseSource(nameof(NonGenericIndexerShapesAndModes))]
    public void WithoutAllowWriteARefusedWriteListsNothing(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: false, strict);

        Disagreements(
            engine,
            shape,
            """
            Reflect.set(d, 'expando', 2);
            Reflect.set(d, Symbol.for('s'), 3);
            try { d.expando = 2; } catch (e) { }
            try { d[Symbol.for('s')] = 3; } catch (e) { }
            """,
            [
                .. AllStringKeys("b => b"),
                ("Reflect.ownKeys(d).map(String)", "b => b"),
            ]).Should().BeEmpty();
        count().Should().Be(1);
    }

    /// <summary>A non-extensible wrapper refuses the new key (<c>Reflect.set</c> answers <c>false</c>), so it is not listed.</summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void ANonExtensibleWrapperListsNothingItRefused(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        Disagreements(
            engine,
            shape,
            """
            Object.preventExtensions(d);
            Reflect.set(d, 'expando', 2);
            Reflect.set(d, Symbol.for('s'), 3);
            try { d.expando = 2; } catch (e) { }
            try { d[Symbol.for('s')] = 3; } catch (e) { }
            """,
            [
                .. AllStringKeys("b => b"),
                ("Reflect.ownKeys(d).map(String)", "b => b"),
            ]).Should().BeEmpty();
        count().Should().Be(1);
    }

    /// <summary>A CLR member the dictionary reads but cannot write refuses the name, so nothing is stored or listed under it.</summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void ARefusedMemberWriteListsNothing(string shape, bool strict)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite: true, strict);

        Disagreements(
            engine,
            shape,
            """
            Reflect.set(d, 'Count', 5);
            Reflect.set(d, 'ContainsKey', 5);
            try { d.Count = 5; } catch (e) { }
            try { d.ContainsKey = 5; } catch (e) { }
            """,
            [
                .. AllStringKeys("b => b"),
                ("Reflect.ownKeys(d).map(String)", "b => b"),
            ]).Should().BeEmpty();
        count().Should().Be(1);
    }

    /// <summary>
    /// <c>Object.defineProperty</c> defines the key on the wrapper in both modes, and redefining it, as an accessor
    /// here, lists it once and leaves it in its place.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void DefinePropertyListsTheKeyInBothModes(string shape, bool allowWrite)
    {
        var (host, count) = CreateShape(shape);
        var engine = CreateEngine(host, allowWrite);

        Disagreements(
            engine,
            shape,
            """
            Object.defineProperty(d, 'expando', { value: 2, writable: true, enumerable: true, configurable: true });
            Object.defineProperty(d, 'later', { value: 3, writable: true, enumerable: true, configurable: true });
            Object.defineProperty(d, 'expando', { get() { return 7; }, enumerable: true, configurable: true });
            """,
            AllStringKeys("b => [...b, 'expando', 'later']")).Should().BeEmpty();
        engine.Evaluate("d.expando").AsNumber().Should().Be(7);
        count().Should().Be(1);
    }

    /// <summary>
    /// Because the key is listed, <c>Object.seal</c> and <c>Object.freeze</c> reach it
    /// (https://tc39.es/ecma262/#sec-setintegritylevel): it becomes non-configurable, so <c>delete</c> refuses it,
    /// as Node refuses it on an ordinary object.
    /// </summary>
    [TestCaseSource(nameof(ShapesAndModes))]
    public void SealingAndFreezingReachTheKey(string shape, bool freeze)
    {
        foreach (var strict in new[] { false, true })
        {
            var (host, count) = CreateShape(shape);
            var engine = CreateEngine(host, allowWrite: true, strict);

            engine.Execute($"d.expando = 2; Object.{(freeze ? "freeze" : "seal")}(d);");

            engine.Evaluate("Object.getOwnPropertyDescriptor(d, 'expando').configurable").AsBoolean().Should().BeFalse($"strict: {strict}");
            engine.Evaluate("Object.getOwnPropertyDescriptor(d, 'expando').writable").AsBoolean().Should().Be(!freeze);
            engine.Evaluate("Reflect.deleteProperty(d, 'expando')").AsBoolean().Should().BeFalse();
            engine.Evaluate("try { delete d.expando; 'no error' } catch (e) { e.constructor.name }").AsString()
                .Should().Be(strict ? "TypeError" : "no error");
            engine.Evaluate("d.hasOwnProperty('expando')").AsBoolean().Should().BeTrue();
            count().Should().Be(1);
        }
    }

    /// <summary>
    /// The reference shape: a dictionary implementing only <see cref="IDictionary{TKey,TValue}"/>, so that no
    /// non-generic indexer stands in for a name its key type cannot hold.
    /// </summary>
    public sealed class GenericOnlyDictionary : IDictionary<int, string>
    {
        private readonly Dictionary<int, string> _inner = new();

        public string this[int key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        public ICollection<int> Keys => _inner.Keys;
        public ICollection<string> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;
        public void Add(int key, string value) => _inner.Add(key, value);
        public void Add(KeyValuePair<int, string> item) => _inner.Add(item.Key, item.Value);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<int, string> item) => ((ICollection<KeyValuePair<int, string>>) _inner).Contains(item);
        public bool ContainsKey(int key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<int, string>[] array, int arrayIndex) => ((ICollection<KeyValuePair<int, string>>) _inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => _inner.GetEnumerator();
        public bool Remove(int key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<int, string> item) => ((ICollection<KeyValuePair<int, string>>) _inner).Remove(item);
        public bool TryGetValue(int key, out string value) => _inner.TryGetValue(key, out value!);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

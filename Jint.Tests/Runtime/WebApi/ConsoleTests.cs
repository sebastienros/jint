#if NET8_0_OR_GREATER
#nullable enable

using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>console</c> object against the Console Standard — https://console.spec.whatwg.org/.
/// </summary>
/// <remarks>
/// Everything is asserted through a recording sink rather than through captured standard output, because
/// what the engine promises a host is exactly the sequence of
/// <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> calls: one per emitted record, already formatted
/// and already indented.
/// </remarks>
public class ConsoleTests
{
    private sealed class RecordingSink : ConsoleSink
    {
        internal List<(ConsoleLogLevel Level, string Message)> Records { get; } = new();

        internal List<string> Messages => Records.ConvertAll(r => r.Message);

        public override void Write(ConsoleLogLevel level, string message)
        {
            Records.Add((level, message));
        }
    }

    private static (Engine Engine, RecordingSink Sink) Recording()
    {
        var sink = new RecordingSink();
        var engine = new Engine(options => options.UseConsole(sink));
        return (engine, sink);
    }

    private static List<string> Run(string script)
    {
        var (engine, sink) = Recording();
        engine.Execute(script);
        return sink.Messages;
    }

    [Test]
    public void SubstitutesTheFormatSpecifiers()
    {
        Run("console.log('hello %s', 'world')").Should().Equal("hello world");
        Run("console.log('%d apples', 3.7)").Should().Equal("3 apples");
        Run("console.log('%i apples', 3.7)").Should().Equal("3 apples");
        Run("console.log('%f apples', 3.5)").Should().Equal("3.5 apples");
        Run("console.log('%o', {a:1})").Should().Equal("{ a: 1 }");
        Run("console.log('%O', [1,'x'])").Should().Equal("[ 1, 'x' ]");
    }

    [Test]
    public void CoercesAwkwardValuesTheWayTheSpecifierSays()
    {
        Run("console.log('%d', Symbol('s'))").Should().Equal("NaN");
        Run("console.log('%f', Symbol('s'))").Should().Equal("NaN");
        Run("console.log('%s', Symbol('s'))").Should().Equal("Symbol(s)");
        Run("console.log('%d', {})").Should().Equal("NaN");
        Run("console.log('%d', 10n)").Should().Equal("10");
    }

    [Test]
    public void ConsumesPercentCWithoutEmittingStyling()
    {
        Run("console.log('%cstyled', 'color: red')").Should().Equal("styled");
        Run("console.log('%ca%cb', 'x', 'y')").Should().Equal("ab");
    }

    [Test]
    public void CollapsesADoubledPercent()
    {
        // Formatter step 1 returns a single argument untouched, so substitution — including %% — only ever
        // happens when there is something to substitute. This is what a browser does too.
        Run("console.log('100%% sure')").Should().Equal("100%% sure");
        Run("console.log('100%% sure of %s', 'it')").Should().Equal("100% sure of it");
    }

    [Test]
    public void LeavesASpecifierWithNoArgumentAlone()
    {
        Run("console.log('a %s %s', 'b')").Should().Equal("a b %s");
    }

    [Test]
    public void AppendsExtraArgumentsSeparatedBySpaces()
    {
        Run("console.log('a', 'b', 'c')").Should().Equal("a b c");
        Run("console.log('n=%d', 1, 'and', 2)").Should().Equal("n=1 and 2");
        Run("console.log(1, 2)").Should().Equal("1 2");
    }

    [Test]
    public void DoesNotTreatANonStringFirstArgumentAsAFormatString()
    {
        Run("console.log({a:'%s'}, 'b')").Should().Equal("{ a: '%s' } b");
    }

    [Test]
    public void EmitsNothingWhenThereIsNothingToLog()
    {
        // https://console.spec.whatwg.org/#logger step 1.
        Run("console.log()").Should().BeEmpty();
        Run("console.warn(); console.error(); console.info(); console.debug()").Should().BeEmpty();
    }

    [Test]
    public void MakesExactlyOneWritePerRecord()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.log('a\\nb', {x:1}); console.warn('c')");

        sink.Records.Should().HaveCount(2);
        sink.Records[0].Message.Should().Be("a\nb { x: 1 }");
    }

    [Test]
    public void MapsEachMethodToItsLevel()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.debug('d'); console.log('l'); console.info('i'); console.warn('w'); console.error('e');");

        sink.Records.ConvertAll(r => r.Level).Should().Equal(
            ConsoleLogLevel.Debug,
            ConsoleLogLevel.Log,
            ConsoleLogLevel.Info,
            ConsoleLogLevel.Warn,
            ConsoleLogLevel.Error);
    }

    [Test]
    public void IndentsWhatFollowsAGroupLabel()
    {
        var messages = Run(@"
            console.log('before');
            console.group('outer');
            console.log('one');
            console.groupCollapsed('inner');
            console.log('two');
            console.groupEnd();
            console.log('three');
            console.groupEnd();
            console.log('after');");

        messages.Should().Equal(
            "before",
            "outer",
            "  one",
            "  inner",
            "    two",
            "  three",
            "after");
    }

    [Test]
    public void IndentsEveryLineOfAMultiLineRecord()
    {
        Run("console.group('g'); console.log('a\\nb')").Should().Equal("g", "  a\n  b");
    }

    [Test]
    public void SurvivesAnUnbalancedGroupEnd()
    {
        Run("console.groupEnd(); console.groupEnd(); console.log('flat')").Should().Equal("flat");
    }

    [Test]
    public void CountsPerLabel()
    {
        Run("console.count(); console.count(); console.count('a'); console.count()")
            .Should().Equal("default: 1", "default: 2", "a: 1", "default: 3");
    }

    [Test]
    public void ResetsACounter()
    {
        Run("console.count('a'); console.countReset('a'); console.count('a')")
            .Should().Equal("a: 1", "a: 1");
    }

    [Test]
    public void WarnsWhenResettingACounterThatDoesNotExist()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.countReset('nope')");

        sink.Records.Should().HaveCount(1);
        sink.Records[0].Level.Should().Be(ConsoleLogLevel.Warn);
        sink.Records[0].Message.Should().Be("Count for 'nope' does not exist");
    }

    [Test]
    public void ReportsTimersByLabelWithADuration()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.time(); console.timeLog(); console.timeLog(undefined, 'mid'); console.timeEnd();");

        // Durations are wall-clock, so only the shape is asserted.
        sink.Records.Should().HaveCount(3);
        sink.Records[0].Message.Should().MatchRegex(@"^default: [0-9.]+ms$");
        sink.Records[1].Message.Should().MatchRegex(@"^default: [0-9.]+ms mid$");
        sink.Records[2].Message.Should().MatchRegex(@"^default: [0-9.]+ms$");
        sink.Records.TrueForAll(r => r.Level == ConsoleLogLevel.Log).Should().BeTrue();
    }

    [Test]
    public void WarnsAboutADuplicateOrMissingTimer()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.time('t'); console.time('t'); console.timeEnd('t'); console.timeEnd('t'); console.timeLog('t');");

        sink.Records[0].Message.Should().Be("Timer 't' already exists");
        sink.Records[0].Level.Should().Be(ConsoleLogLevel.Warn);
        sink.Records[1].Message.Should().MatchRegex(@"^t: [0-9.]+ms$");
        sink.Records[2].Message.Should().Be("Timer 't' does not exist");
        sink.Records[3].Message.Should().Be("Timer 't' does not exist");
    }

    [Test]
    public void AssertsOnlyOnAFalsyCondition()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.assert(true, 'never'); console.assert(1, 'never'); console.assert('x', 'never');");
        sink.Records.Should().BeEmpty();

        engine.Execute("console.assert(false)");
        engine.Execute("console.assert(0, 'bad %s', 'thing')");
        engine.Execute("console.assert(undefined, {a:1})");
        engine.Execute("console.assert()");

        sink.Records.ConvertAll(r => r.Message).Should().Equal(
            "Assertion failed",
            "Assertion failed: bad thing",
            "Assertion failed { a: 1 }",
            "Assertion failed");
        sink.Records.TrueForAll(r => r.Level == ConsoleLogLevel.Error).Should().BeTrue();
    }

    [Test]
    public void TracesTheCallStack()
    {
        var (engine, sink) = Recording();

        engine.Execute("function outer() { inner(); } function inner() { console.trace('why'); } outer();");

        sink.Records.Should().HaveCount(1);
        sink.Records[0].Level.Should().Be(ConsoleLogLevel.Error);
        sink.Records[0].Message.Should().StartWith("Trace: why");
        sink.Records[0].Message.Should().Contain("at inner");
        sink.Records[0].Message.Should().Contain("at outer");

        // The dispatcher's own frame is excluded, exactly as `new Error().stack` excludes the Error constructor.
        sink.Records[0].Message.Should().NotContain("at trace");
    }

    [Test]
    public void InspectsBoundedAndCycleSafely()
    {
        Run("var o = {}; o.self = o; console.log(o)").Should().Equal("{ self: [Circular] }");
        Run("console.log({a:{b:{c:{d:1}}}})").Should().Equal("{ a: { b: { c: [Object] } } }");
        Run("console.log([])").Should().Equal("[]");
        Run("console.log({})").Should().Equal("{}");
        Run("console.log({ 'not-an-identifier': 1 })").Should().Equal("{ 'not-an-identifier': 1 }");

        // An accessor is named, never invoked: a console must not be a way to run script.
        Run("console.log({ get x() { throw new Error('nope'); } })").Should().Equal("{ x: [Getter] }");

        // Non-enumerable own properties are skipped, the way Object.keys skips them.
        Run("var o = {}; Object.defineProperty(o, 'hidden', { value: 1 }); console.log(o)").Should().Equal("{}");
    }

    [Test]
    public void QuotesStringsOnlyBelowTheTopLevel()
    {
        Run("console.log('x')").Should().Equal("x");
        Run("console.log(['x'])").Should().Equal("[ 'x' ]");
        Run("console.dir('x')").Should().Equal("'x'");
    }

    [Test]
    public void RendersFunctionsSymbolsAndErrors()
    {
        Run("console.log(Symbol('s'))").Should().Equal("Symbol(s)");
        Run("console.log(10n)").Should().Equal("10n");
        Run("console.log(new TypeError('bad'))").Should().Equal("TypeError: bad");
    }

    /// <summary>
    /// https://github.com/sebastienros/jint/issues/3316. A promise owns no enumerable property, so walking
    /// it as an ordinary object rendered the empty one every other engine renders as its state.
    /// </summary>
    [Test]
    public void RendersAPromiseInEachOfItsThreeStates()
    {
        Run("console.log(new Promise(() => {}))").Should().Equal("Promise { <pending> }");
        Run("console.log(Promise.resolve(42))").Should().Equal("Promise { 42 }");
        Run("var p = Promise.reject(new TypeError('bad')); p.catch(() => {}); console.log(p)")
            .Should().Equal("Promise { <rejected> TypeError: bad }");

        // The issue's own shape: the promise reached through an array.
        Run("console.log([new Promise(() => {})])").Should().Equal("[ Promise { <pending> } ]");

        // What the specification decides was never wrong and does not move. Only the rendering the Console
        // Standard leaves to the implementation changed.
        Run("console.log(String(Promise.resolve()))").Should().Equal("[object Promise]");
        Run("console.log(Object.prototype.toString.call(Promise.resolve()))").Should().Equal("[object Promise]");
    }

    /// <summary>
    /// The rest of the family the promise belongs to. Each rendering is read from the object's internal
    /// slots, never through the prototype accessor of the same name — <c>source</c>, <c>flags</c>,
    /// <c>byteLength</c> and <c>toISOString</c> are configurable on every one of these.
    /// </summary>
    [Test]
    public void RendersTheWellKnownExoticObjects()
    {
        Run("console.log(new Map())").Should().Equal("Map(0) {}");
        Run("console.log(new Map([['a', 1], [2, 'b']]))").Should().Equal("Map(2) { 'a' => 1, 2 => 'b' }");
        Run("console.log(new Set())").Should().Equal("Set(0) {}");
        Run("console.log(new Set([1, 'two']))").Should().Equal("Set(2) { 1, 'two' }");

        // Nothing enumerates a weak collection, and a WeakRef is not dereferenced: reaching its target is
        // what WeakRef.prototype.deref exists to gate.
        Run("console.log(new WeakMap())").Should().Equal("WeakMap { <items unknown> }");
        Run("console.log(new WeakSet())").Should().Equal("WeakSet { <items unknown> }");
        Run("console.log(new WeakRef({}))").Should().Equal("WeakRef { <target unknown> }");

        Run("console.log(new Date(Date.UTC(2020, 0, 1)))").Should().Equal("2020-01-01T00:00:00.000Z");
        Run("console.log(new Date(NaN))").Should().Equal("Invalid Date");
        Run("console.log(/ab+c/gi)").Should().Equal("/ab+c/gi");

        Run("console.log(new Uint8Array([1, 2, 3]))").Should().Equal("Uint8Array(3) [ 1, 2, 3 ]");
        Run("console.log(new BigInt64Array([1n, 2n]))").Should().Equal("BigInt64Array(2) [ 1n, 2n ]");
        Run("console.log(new Uint8Array(0))").Should().Equal("Uint8Array(0) []");
        Run("console.log(new ArrayBuffer(8))").Should().Equal("ArrayBuffer { byteLength: 8 }");
        Run("console.log(new DataView(new ArrayBuffer(8), 2, 4))")
            .Should().Equal("DataView { byteLength: 4, byteOffset: 2, buffer: ArrayBuffer { byteLength: 8 } }");

        // A detached buffer has no length to read, which is what keeps the walk off bytes that are gone.
        Run("var b = new ArrayBuffer(8); var v = new Uint8Array(b); b.transfer(); console.log(v); console.log(b)")
            .Should().Equal("Uint8Array(0) []", "ArrayBuffer { (detached), byteLength: 0 }");

        Run("console.log(new String('x'))").Should().Equal("[String: 'x']");
        Run("console.log(new Number(5))").Should().Equal("[Number: 5]");
        Run("console.log(new Boolean(true))").Should().Equal("[Boolean: true]");
        Run("console.log(Object(Symbol('s')))").Should().Equal("[Symbol: Symbol(s)]");
        Run("console.log(Object(10n))").Should().Equal("[BigInt: 10n]");

        Run("(function () { console.log(arguments); })(1, 'a')").Should().Equal("[Arguments] { '0': 1, '1': 'a' }");

        // Object.create(null) inherits no toString, so it is labelled rather than left to read as a literal.
        Run("console.log(Object.create(null))").Should().Equal("[Object: null prototype] {}");
        Run("var o = Object.create(null); o.a = 1; console.log(o)").Should().Equal("[Object: null prototype] { a: 1 }");
    }

    /// <summary>
    /// A function is named, not printed: <c>Function.prototype.toString</c> answers the whole source text
    /// once the engine retains it, and one console record carrying a function body is exactly the unbounded
    /// output the rest of the renderer is written to avoid.
    /// </summary>
    [Test]
    public void NamesAFunctionInsteadOfPrintingIt()
    {
        Run("console.log(function foo() {})").Should().Equal("[Function: foo]");
        Run("console.log(function () {})").Should().Equal("[Function (anonymous)]");
        Run("console.log(() => {})").Should().Equal("[Function (anonymous)]");
        Run("console.log(async function bar() {})").Should().Equal("[AsyncFunction: bar]");
        Run("console.log(function* gen() {})").Should().Equal("[GeneratorFunction: gen]");
        Run("console.log(async function* agen() {})").Should().Equal("[AsyncGeneratorFunction: agen]");
        Run("console.log(class Foo {})").Should().Equal("[class Foo]");
        Run("console.log(class {})").Should().Equal("[class (anonymous)]");
        Run("console.log(Math.max)").Should().Equal("[Function: max]");

        // `name` is configurable on every function, so a script can make reading it observable. Such a
        // function reports as anonymous rather than turning the console into a way to run the accessor.
        Run("var f = function foo() {}; Object.defineProperty(f, 'name', { get() { throw new Error('name ran'); } }); console.log(f)")
            .Should().Equal("[Function (anonymous)]");

        var sink = new RecordingSink();
        var engine = new Engine(options =>
        {
            options.RetainFunctionSourceText = true;
            options.UseConsole(sink);
        });

        engine.Execute("function withABody() { return 'a body nobody asked the console for'; } console.log(withABody)");
        sink.Messages.Should().Equal("[Function: withABody]");
    }

    /// <summary>
    /// The class promises it is not a way to run script. A proxy is where that promise was untrue: walking
    /// one calls its <c>ownKeys</c> and <c>getOwnPropertyDescriptor</c> traps, so it renders as its target.
    /// </summary>
    [Test]
    public void NeverRunsScriptWhileInspecting()
    {
        Run(@"var p = new Proxy({ a: 1 }, {
                  ownKeys() { throw new Error('ownKeys ran'); },
                  getOwnPropertyDescriptor() { throw new Error('getOwnPropertyDescriptor ran'); },
                  get() { throw new Error('get ran'); },
              });
              console.log(p)").Should().Equal("{ a: 1 }");

        // A revoked proxy has no target at all, and every trap on it throws.
        Run("var r = Proxy.revocable({ a: 1 }, {}); r.revoke(); console.log(r.proxy)").Should().Equal("<Revoked Proxy>");

        // Symbol.toStringTag is an ordinary accessor a script may install, and nothing consults it.
        Run("console.log({ get [Symbol.toStringTag]() { throw new Error('tag ran'); }, a: 1 })")
            .Should().Equal("{ a: 1 }");
    }

    /// <summary>
    /// The same promise, for the one value a console is most often handed. <c>name</c> and <c>message</c>
    /// are both configurable on every error and definable on any subclass, so rendering an error through
    /// <c>Get</c> made <c>console.log(err)</c> a way to run script — and to throw out of a log statement.
    /// https://github.com/sebastienros/jint/issues/3598.
    /// </summary>
    [Test]
    public void NeverRunsScriptWhileInspectingAnError()
    {
        var (engine, sink) = Recording();
        engine.Execute("globalThis.ran = 0;");

        // An own accessor on the instance, on the property the renderer wants most.
        engine.Execute("""
            var e = new Error('the message');
            Object.defineProperty(e, 'message', { get() { ran++; throw new Error('message ran'); }, configurable: true });
            console.log(e);
            """);

        // A getter on a subclass prototype, which is where a script most naturally puts one.
        engine.Execute("""
            class Bad extends Error { get name() { ran++; throw new Error('name ran'); } }
            console.log(new Bad('boom'));
            """);

        // An error whose prototype chain is a proxy: the walk stops at it rather than reaching a trap.
        engine.Execute("""
            var behindProxy = new Error('inherited from a proxy');
            Object.setPrototypeOf(behindProxy, new Proxy(Error.prototype, {
                get() { ran++; throw new Error('get ran'); },
                getOwnPropertyDescriptor() { ran++; throw new Error('gopd ran'); },
            }));
            console.log(behindProxy);
            """);

        // A proxy around the error itself, which #3316 already pinned for an ordinary object. Through
        // console.error, because every level shares one formatter and the error one is where an error goes.
        engine.Execute("""
            console.error(new Proxy(new Error('behind a proxy'), {
                get() { ran++; throw new Error('get ran'); },
                ownKeys() { ran++; throw new Error('ownKeys ran'); },
            }));
            """);

        engine.Evaluate("ran").AsNumber().Should().Be(0);
        sink.Messages.Should().Equal(
            // The refused message is absent rather than guessed at, so the name stands alone.
            "Error",
            // A refused `name` falls back to the constructor's, read as a descriptor off the same chain.
            "Bad: boom",
            "Error: inherited from a proxy",
            "Error: behind a proxy");
    }

    /// <summary>
    /// A <c>DOMException</c> keeps its text, which is the reason a getter-free read is not a one-liner: its
    /// <c>name</c> and <c>message</c> are WebIDL prototype accessors by design, so a renderer that refuses
    /// every accessor would turn <c>AbortError: x</c> into <c>Error</c>.
    /// </summary>
    [Test]
    public void RendersADomExceptionByItsSlots()
    {
        Run("console.log(new DOMException('aborted', 'AbortError'))").Should().Equal("AbortError: aborted");
        Run("console.log(new DOMException())").Should().Equal("Error");
        Run("console.log(new DOMException('just a message'))").Should().Equal("Error: just a message");
        Run("console.log(new QuotaExceededError('too much'))").Should().Equal("QuotaExceededError: too much");

        // A WebIDL attribute is configurable like any other, so an own data property defined over one is
        // what the property is — and the walk, which reaches it first, is what prints it.
        Run("""
            var e = new DOMException('aborted', 'AbortError');
            Object.defineProperty(e, 'name', { value: 'Renamed' });
            console.log(e);
            """).Should().Equal("Renamed: aborted");
    }

    [Test]
    public void AnExoticContainerIsDepthCappedAndCycleSafe()
    {
        Run("var m = new Map(); m.set('self', m); console.log(m)").Should().Equal("Map(1) { 'self' => [Circular] }");
        Run("var s = new Set(); s.add(s); console.log(s)").Should().Equal("Set(1) { [Circular] }");

        Run("console.log({ a: { b: { c: new Map([[1, 2]]) } } })").Should().Equal("{ a: { b: { c: [Map] } } }");
        Run("console.log({ a: { b: { c: new Set([1]) } } })").Should().Equal("{ a: { b: { c: [Set] } } }");
        Run("console.log({ a: { b: { c: Promise.resolve(1) } } })").Should().Equal("{ a: { b: { c: [Promise] } } }");
        Run("console.log({ a: { b: { c: new Uint8Array(1) } } })").Should().Equal("{ a: { b: { c: [Uint8Array] } } }");

        Run("var s = new Set(); for (var i = 0; i < 105; i++) s.add(i); console.log(s)")
            .Should().ContainSingle().Which.Should().EndWith("99, ... 5 more items }");

        Run("var m = new Map(); for (var i = 0; i < 105; i++) m.set(i, i); console.log(m)")
            .Should().ContainSingle().Which.Should().EndWith("99 => 99, ... 5 more items }");
    }

    [Test]
    public void TablesAnArrayOfPrimitivesThroughTheValuesColumn()
    {
        Run("console.table(['a', 'b'])").Should().Equal(
            """
            +---------+--------+
            | (index) | Values |
            +---------+--------+
            | 0       | 'a'    |
            | 1       | 'b'    |
            +---------+--------+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TablesAnArrayOfObjectsWithTheUnionOfTheirKeys()
    {
        // Columns are unioned across rows in first-seen order, and a row that lacks one renders an empty cell.
        Run("console.table([{ a: 1, b: 2 }, { a: 3, c: 4 }])").Should().Equal(
            """
            +---------+---+---+---+
            | (index) | a | b | c |
            +---------+---+---+---+
            | 0       | 1 | 2 |   |
            | 1       | 3 |   | 4 |
            +---------+---+---+---+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TablesAPlainObjectByItsKeys()
    {
        Run("console.table({ first: { n: 1 }, second: { n: 2 } })").Should().Equal(
            """
            +---------+---+
            | (index) | n |
            +---------+---+
            | first   | 1 |
            | second  | 2 |
            +---------+---+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TheColumnsArgumentReplacesTheDerivedColumnSet()
    {
        Run("console.table([{ a: 1, b: 2 }], ['b'])").Should().Equal(
            """
            +---------+---+
            | (index) | b |
            +---------+---+
            | 0       | 2 |
            +---------+---+
            """.ReplaceLineEndings("\n"));

        // A named column no row has is still a column, with empty cells.
        Run("console.table([{ a: 1 }], ['zzz'])").Should().Equal(
            """
            +---------+-----+
            | (index) | zzz |
            +---------+-----+
            | 0       |     |
            +---------+-----+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void MixesObjectRowsAndPrimitiveRowsInOneTable()
    {
        Run("console.table([{ a: 1 }, 'plain'])").Should().Equal(
            """
            +---------+---+---------+
            | (index) | a | Values  |
            +---------+---+---------+
            | 0       | 1 |         |
            | 1       |   | 'plain' |
            +---------+---+---------+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TableNeverInvokesAnAccessor()
    {
        // The same rule the rest of the formatter follows: a console must not be a way to run script.
        Run("console.table([{ get x() { throw new Error('nope'); } }])").Should().Equal(
            """
            +---------+----------+
            | (index) | x        |
            +---------+----------+
            | 0       | [Getter] |
            +---------+----------+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TablesAnEmptyCollectionAsAnEmptyTable()
    {
        // An object with rows is tabular whether or not it has any, so this is a table and not the fallback.
        var expected =
            """
            +---------+
            | (index) |
            +---------+
            """.ReplaceLineEndings("\n");

        Run("console.table([])").Should().Equal(expected);
        Run("console.table({})").Should().Equal(expected);
    }

    [Test]
    public void TableFallsBackToLoggingWhatCannotBeParsedAsTabular()
    {
        // "Fall back to just logging the argument if it can't be parsed as tabular."
        Run("console.table('hello')").Should().Equal("hello");
        Run("console.table(42)").Should().Equal("42");
        Run("console.table(null)").Should().Equal("null");
        Run("console.table()").Should().Equal("undefined");
        Run("console.table(function foo() {})").Should().Equal("[Function: foo]");
    }

    [Test]
    public void TableEmitsExactlyOneRecordAtLogLevel()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.group('g'); console.table(['a']); console.groupEnd();");

        sink.Records.Should().HaveCount(2);
        sink.Records[1].Level.Should().Be(ConsoleLogLevel.Log);

        // A table is a single record however many lines it occupies, so the group indents every one of them.
        sink.Records[1].Message.Should().Be(
            """
              +---------+--------+
              | (index) | Values |
              +---------+--------+
              | 0       | 'a'    |
              +---------+--------+
            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void TableBoundsTheRowsItRenders()
    {
        var messages = Run("console.table(Array.from({ length: 105 }, (_, i) => i))");

        messages.Should().HaveCount(1);

        // Three border lines, one header, one row per rendered entry, and the trailing note.
        messages[0].Split('\n').Should().HaveCount(3 + 1 + 100 + 1);
        messages[0].Should().Contain("| 99 ");
        messages[0].Should().NotContain("| 100 ");
        messages[0].Should().EndWith("... 5 more rows");
    }

    [Test]
    public void TheColumnsArgumentIsAWebIdlSequence()
    {
        // sequence<DOMString> is iterated with the iterator protocol and each element stringified, so a Set
        // works and a non-iterable is a TypeError before anything is logged.
        var (engine, sink) = Recording();
        engine.Execute("console.table([{ 1: 'x' }], new Set([1]));");
        sink.Messages[0].Should().Contain("| 'x' |");

        var (other, otherSink) = Recording();
        Assert.Throws<Jint.Runtime.JavaScriptException>(() => other.Execute("console.table([{a:1}], 5)"));
        otherSink.Records.Should().BeEmpty();
    }

    [Test]
    public void TimeStampEmitsItsLabel()
    {
        // Not a Console Standard method — a browser drops a marker on a profiler timeline and prints nothing.
        // There is no timeline here, so the marker is the record.
        Run("console.timeStamp('checkpoint')").Should().Equal("checkpoint");
        Run("console.timeStamp()").Should().Equal("default");
        Run("console.timeStamp(7)").Should().Equal("7");
    }

    [Test]
    public void IsIdentityStableAndTagged()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("console === console").AsBoolean().Should().BeTrue();
        engine.Evaluate("console === globalThis.console").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(console)").AsString().Should().Be("[object console]");
        engine.Evaluate("console[Symbol.toStringTag]").AsString().Should().Be("console");
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#console-namespace — "For historical web-compatibility reasons, the
    /// namespace object for console must have as its [[Prototype]] an empty object, created as if by
    /// ObjectCreate(%ObjectPrototype%), instead of %ObjectPrototype%."
    /// </summary>
    /// <remarks>
    /// The three columns are the three the rule constrains, and Node 24 answers <c>false</c>, <c>0</c> and
    /// <c>true</c> to them. <c>idlharness.js</c> asserts exactly this, for <c>console</c> and for nothing
    /// else.
    /// </remarks>
    [Test]
    public void SitsOnAPrivateEmptyPrototypeRatherThanOnObjectPrototype()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("Object.getPrototypeOf(console) === Object.prototype").AsBoolean().Should().BeFalse();
        engine.Evaluate("Reflect.ownKeys(Object.getPrototypeOf(console)).length").AsNumber().Should().Be(0);
        engine.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(console)) === Object.prototype").AsBoolean().Should().BeTrue();

        // "an empty object, created as if by ObjectCreate(%ObjectPrototype%)": an ordinary, extensible object
        // and not a second exotic one, and the same object every time it is asked for.
        engine.Evaluate("Object.getPrototypeOf(console) === Object.getPrototypeOf(console)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isExtensible(Object.getPrototypeOf(console))").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(Object.getPrototypeOf(console))").AsString().Should().Be("[object Object]");

        // The chain still ends at %Object.prototype%, so everything an ordinary object inherits is reachable
        // through console exactly as before.
        engine.Evaluate("typeof console.hasOwnProperty").AsString().Should().Be("function");
        engine.Evaluate("console.hasOwnProperty('log')").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Where the members live is not what the rule changes: the operations stay own properties of the
    /// namespace object, which is what a WebIDL namespace object carries
    /// (https://webidl.spec.whatwg.org/#es-namespaces) and what Node 24 answers — its
    /// <c>Object.getOwnPropertyNames(console)</c> lists every method, and the object above it lists nothing.
    /// </summary>
    [Test]
    public void KeepsItsMembersOnTheNamespaceObjectItself()
    {
        var engine = new Engine(options => options.UseWebApis());

        var members = new[]
        {
            "log", "info", "warn", "error", "debug", "trace", "assert", "dir", "table", "timeStamp",
            "group", "groupCollapsed", "groupEnd", "count", "countReset", "time", "timeLog", "timeEnd",
        };

        foreach (var member in members)
        {
            engine.Evaluate($"Object.prototype.hasOwnProperty.call(console, '{member}')").AsBoolean().Should().BeTrue(member);
            engine.Evaluate($"Object.prototype.hasOwnProperty.call(Object.getPrototypeOf(console), '{member}')").AsBoolean().Should().BeFalse(member);
        }

        // The tag is an own symbol of the namespace object too, which is what keeps `[object console]` true.
        engine.Evaluate("Object.getOwnPropertySymbols(console).indexOf(Symbol.toStringTag) >= 0").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(console)").AsString().Should().Be("[object console]");
    }

    /// <summary>
    /// The web-compatibility reason the rule exists, and the containment consequence of getting it wrong: a
    /// library decorating logging through the object above <c>console</c> must reach a throwaway object, not
    /// every object in the realm.
    /// </summary>
    [Test]
    public void PatchingConsolesPrototypeDoesNotReachObjectPrototype()
    {
        var engine = new Engine(options => options.UseWebApis());

        // The idiom itself, verbatim: this is what the patching libraries the rule was written for do.
        engine.Execute("console.__proto__.decorated = 42;");

        // It still works on console, which is the whole point of allowing it.
        engine.Evaluate("console.decorated").AsNumber().Should().Be(42);

        // And it reaches nothing else.
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'decorated')").AsBoolean().Should().BeFalse();
        engine.Evaluate("'decorated' in {}").AsBoolean().Should().BeFalse();
        engine.Evaluate("({}).decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("[].decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("(function () {}).decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("'s'.decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("JSON.stringify(Object.keys({ a: 1 }))").AsString().Should().Be("[\"a\"]");

        // Object.setPrototypeOf(console, x) was always contained; assert it stays so, and that replacing the
        // prototype leaves %Object.prototype% untouched as well.
        engine.Execute("Object.setPrototypeOf(console, { replaced: 1 });");
        engine.Evaluate("console.replaced").AsNumber().Should().Be(1);
        engine.Evaluate("console.decorated").IsUndefined().Should().BeTrue();
        engine.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'replaced')").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// The prototype is per realm, like the namespace object it sits under, so one engine's patch is not
    /// another's.
    /// </summary>
    [Test]
    public void GivesEachEngineItsOwnConsolePrototype()
    {
        var options = new Options().UseWebApis();

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("console.__proto__.decorated = 42;");

        first.Evaluate("console.decorated").AsNumber().Should().Be(42);
        second.Evaluate("console.decorated").IsUndefined().Should().BeTrue();
        second.Evaluate("Object.prototype.hasOwnProperty.call(Object.prototype, 'decorated')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void CountsAndTimersAreOwnedByTheEngine()
    {
        var sink = new RecordingSink();
        var options = new Options().UseConsole(sink);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("console.count()");
        second.Execute("console.count()");

        sink.Messages.Should().Equal("default: 1", "default: 1");
    }
}
#endif

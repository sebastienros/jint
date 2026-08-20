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

    [Fact]
    public void SubstitutesTheFormatSpecifiers()
    {
        Run("console.log('hello %s', 'world')").Should().Equal("hello world");
        Run("console.log('%d apples', 3.7)").Should().Equal("3 apples");
        Run("console.log('%i apples', 3.7)").Should().Equal("3 apples");
        Run("console.log('%f apples', 3.5)").Should().Equal("3.5 apples");
        Run("console.log('%o', {a:1})").Should().Equal("{ a: 1 }");
        Run("console.log('%O', [1,'x'])").Should().Equal("[ 1, 'x' ]");
    }

    [Fact]
    public void CoercesAwkwardValuesTheWayTheSpecifierSays()
    {
        Run("console.log('%d', Symbol('s'))").Should().Equal("NaN");
        Run("console.log('%f', Symbol('s'))").Should().Equal("NaN");
        Run("console.log('%s', Symbol('s'))").Should().Equal("Symbol(s)");
        Run("console.log('%d', {})").Should().Equal("NaN");
        Run("console.log('%d', 10n)").Should().Equal("10");
    }

    [Fact]
    public void ConsumesPercentCWithoutEmittingStyling()
    {
        Run("console.log('%cstyled', 'color: red')").Should().Equal("styled");
        Run("console.log('%ca%cb', 'x', 'y')").Should().Equal("ab");
    }

    [Fact]
    public void CollapsesADoubledPercent()
    {
        // Formatter step 1 returns a single argument untouched, so substitution — including %% — only ever
        // happens when there is something to substitute. This is what a browser does too.
        Run("console.log('100%% sure')").Should().Equal("100%% sure");
        Run("console.log('100%% sure of %s', 'it')").Should().Equal("100% sure of it");
    }

    [Fact]
    public void LeavesASpecifierWithNoArgumentAlone()
    {
        Run("console.log('a %s %s', 'b')").Should().Equal("a b %s");
    }

    [Fact]
    public void AppendsExtraArgumentsSeparatedBySpaces()
    {
        Run("console.log('a', 'b', 'c')").Should().Equal("a b c");
        Run("console.log('n=%d', 1, 'and', 2)").Should().Equal("n=1 and 2");
        Run("console.log(1, 2)").Should().Equal("1 2");
    }

    [Fact]
    public void DoesNotTreatANonStringFirstArgumentAsAFormatString()
    {
        Run("console.log({a:'%s'}, 'b')").Should().Equal("{ a: '%s' } b");
    }

    [Fact]
    public void EmitsNothingWhenThereIsNothingToLog()
    {
        // https://console.spec.whatwg.org/#logger step 1.
        Run("console.log()").Should().BeEmpty();
        Run("console.warn(); console.error(); console.info(); console.debug()").Should().BeEmpty();
    }

    [Fact]
    public void MakesExactlyOneWritePerRecord()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.log('a\\nb', {x:1}); console.warn('c')");

        sink.Records.Should().HaveCount(2);
        sink.Records[0].Message.Should().Be("a\nb { x: 1 }");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void IndentsEveryLineOfAMultiLineRecord()
    {
        Run("console.group('g'); console.log('a\\nb')").Should().Equal("g", "  a\n  b");
    }

    [Fact]
    public void SurvivesAnUnbalancedGroupEnd()
    {
        Run("console.groupEnd(); console.groupEnd(); console.log('flat')").Should().Equal("flat");
    }

    [Fact]
    public void CountsPerLabel()
    {
        Run("console.count(); console.count(); console.count('a'); console.count()")
            .Should().Equal("default: 1", "default: 2", "a: 1", "default: 3");
    }

    [Fact]
    public void ResetsACounter()
    {
        Run("console.count('a'); console.countReset('a'); console.count('a')")
            .Should().Equal("a: 1", "a: 1");
    }

    [Fact]
    public void WarnsWhenResettingACounterThatDoesNotExist()
    {
        var (engine, sink) = Recording();

        engine.Execute("console.countReset('nope')");

        sink.Records.Should().HaveCount(1);
        sink.Records[0].Level.Should().Be(ConsoleLogLevel.Warn);
        sink.Records[0].Message.Should().Be("Count for 'nope' does not exist");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void QuotesStringsOnlyBelowTheTopLevel()
    {
        Run("console.log('x')").Should().Equal("x");
        Run("console.log(['x'])").Should().Equal("[ 'x' ]");
        Run("console.dir('x')").Should().Equal("'x'");
    }

    [Fact]
    public void RendersFunctionsSymbolsAndErrors()
    {
        Run("console.log(Symbol('s'))").Should().Equal("Symbol(s)");
        Run("console.log(10n)").Should().Equal("10n");
        Run("console.log(new TypeError('bad'))").Should().Equal("TypeError: bad");
        Run("console.log(function foo() {})").Should().Equal("function foo() { [native code] }");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void TableFallsBackToLoggingWhatCannotBeParsedAsTabular()
    {
        // "Fall back to just logging the argument if it can't be parsed as tabular."
        Run("console.table('hello')").Should().Equal("hello");
        Run("console.table(42)").Should().Equal("42");
        Run("console.table(null)").Should().Equal("null");
        Run("console.table()").Should().Equal("undefined");
        Run("console.table(function foo() {})").Should().Equal("function foo() { [native code] }");
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void TimeStampEmitsItsLabel()
    {
        // Not a Console Standard method — a browser drops a marker on a profiler timeline and prints nothing.
        // There is no timeline here, so the marker is the record.
        Run("console.timeStamp('checkpoint')").Should().Equal("checkpoint");
        Run("console.timeStamp()").Should().Equal("default");
        Run("console.timeStamp(7)").Should().Equal("7");
    }

    [Fact]
    public void IsIdentityStableAndTagged()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("console === console").AsBoolean().Should().BeTrue();
        engine.Evaluate("console === globalThis.console").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(console)").AsString().Should().Be("[object console]");
        engine.Evaluate("console[Symbol.toStringTag]").AsString().Should().Be("console");
    }

    [Fact]
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

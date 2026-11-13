#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>String.prototype.split</c> converts a separator that has no <c>@@split</c> method to a string before it
/// looks at the limit: step 6 is <c>? ToString(separator)</c> and step 7 is the zero-limit return
/// (https://tc39.es/ecma262/#sec-string.prototype.split). A RegExp whose <c>@@split</c> is gone took a shortcut
/// that skipped the conversion, so for a zero limit its <c>toString</c>, and the <c>source</c> and
/// <c>flags</c> reads inside <c>RegExp.prototype.toString</c>, never ran. Every expectation here was read off
/// node v26.13.0, each script in a fresh realm.
/// </summary>
public class StringSplitSeparatorConversionTests
{
    private static string Run(string script) => new Engine().Evaluate(script).ToString();

    [Fact]
    public void ARegExpSeparatorWithoutSplitIsConvertedToAStringWhenTheLimitIsZero()
    {
        // String.prototype.split step 6 (ToString(separator)) runs before step 7 returns [] for a zero limit.
        // RegExp.prototype.toString reads "source" and then "flags", so the getters show the conversion ran.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/g;
            Object.defineProperty(re, 'source', { get() { log.push('source'); return 'b'; } });
            Object.defineProperty(re, 'flags', { get() { log.push('flags'); return 'i'; } });
            var result = 'a/b/ic'.split(re, 0);
            JSON.stringify([log, result])
            """).Should().Be("""[["source","flags"],[]]""");
    }

    [Theory]
    [InlineData("1", """[["source","flags"],["a"]]""")]
    [InlineData("undefined", """[["source","flags"],["a","c"]]""")]
    [InlineData("4294967295", """[["source","flags"],["a","c"]]""")]
    public void ARegExpSeparatorWithoutSplitIsConvertedToAStringWhenTheLimitIsNotZero(string limit, string expected)
    {
        // The same conversion, once, for a limit that is not zero and for no limit at all.
        Run($$"""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/g;
            Object.defineProperty(re, 'source', { get() { log.push('source'); return 'b'; } });
            Object.defineProperty(re, 'flags', { get() { log.push('flags'); return 'i'; } });
            var result = 'a/b/ic'.split(re, {{limit}});
            JSON.stringify([log, result])
            """).Should().Be(expected);
    }

    [Fact]
    public void ARegExpSeparatorWithoutSplitIsConvertedToAStringWhenThereIsNoLimit()
    {
        // No limit argument at all: the conversion still happens once, source before flags.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/g;
            Object.defineProperty(re, 'source', { get() { log.push('source'); return 'b'; } });
            Object.defineProperty(re, 'flags', { get() { log.push('flags'); return 'i'; } });
            var result = 'a/b/ic'.split(re);
            JSON.stringify([log, result])
            """).Should().Be("""[["source","flags"],["a","c"]]""");
    }

    [Theory]
    [InlineData("undefined", "0", """[["toString"],[]]""")]
    [InlineData("null", "0", """[["toString"],[]]""")]
    [InlineData("undefined", "4294967295", """[["toString"],["a","b"]]""")]
    [InlineData("null", "4294967295", """[["toString"],["a","b"]]""")]
    public void AnOwnUndefinedOrNullSplitSymbolFallsThroughToTheStringConversion(string value, string limit, string expected)
    {
        // GetMethod (https://tc39.es/ecma262/#sec-getmethod) treats undefined and null alike: no splitter, so step 6 applies.
        Run($$"""
            var log = [];
            var re = /a/;
            re[Symbol.split] = {{value}};
            re.toString = function () { log.push('toString'); return 'x'; };
            var result = 'axb'.split(re, {{limit}});
            JSON.stringify([log, result])
            """).Should().Be(expected);
    }

    [Fact]
    public void TheSeparatorIsConvertedAfterTheLimitWhenTheLimitIsZero()
    {
        // Steps 4, 5, 6 and then 7: ToString(O), ToUint32(limit), ToString(separator), and only then the zero-limit return.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/;
            re.toString = function () { log.push('separator'); return 'x'; };
            var limit = { valueOf() { log.push('limit'); return 0; } };
            var receiver = { toString() { log.push('this'); return 'abc'; } };
            var result = String.prototype.split.call(receiver, re, limit);
            JSON.stringify([log, result])
            """).Should().Be("""[["this","limit","separator"],[]]""");
    }

    [Fact]
    public void TheSeparatorIsConvertedAfterTheLimitWhenTheLimitIsNotZero()
    {
        // The order is the same when step 7 does not return early.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/;
            re.toString = function () { log.push('separator'); return 'b'; };
            var limit = { valueOf() { log.push('limit'); return 2; } };
            var receiver = { toString() { log.push('this'); return 'abcbd'; } };
            var result = String.prototype.split.call(receiver, re, limit);
            JSON.stringify([log, result])
            """).Should().Be("""[["this","limit","separator"],["a","c"]]""");
    }

    [Fact]
    public void AnAbruptLimitConversionStopsBeforeTheSeparatorIsConverted()
    {
        // Step 5 is abrupt, so step 6 never runs.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var log = [];
            var re = /a/;
            re.toString = function () { log.push('separator'); return 'b'; };
            var limit = { valueOf() { log.push('limit'); throw new EvalError('limit'); } };
            var outcome;
            try { 'abc'.split(re, limit); outcome = 'returned'; } catch (e) { outcome = e.name; }
            JSON.stringify([log, outcome])
            """).Should().Be("""[["limit"],"EvalError"]""");
    }

    [Theory]
    [InlineData("0", "RangeError:boom")]
    [InlineData("5", "RangeError:boom")]
    [InlineData("undefined", "RangeError:boom")]
    public void AnAbruptSeparatorConversionPropagatesWhateverTheLimit(string limit, string expected)
    {
        // Step 6 is `? ToString(separator)`: a throwing toString must surface for a zero limit as well as for any other.
        Run($$"""
            delete RegExp.prototype[Symbol.split];
            var re = /a/;
            re.toString = function () { throw new RangeError('boom'); };
            var outcome;
            try { 'abc'.split(re, {{limit}}); outcome = 'returned'; } catch (e) { outcome = e.name + ':' + e.message; }
            outcome
            """).Should().Be(expected);
    }

    [Fact]
    public void AnAbruptSourceGetterPropagatesWhenTheLimitIsZero()
    {
        // RegExp.prototype.toString reads "source" first; its abrupt completion is the result of step 6.
        Run("""
            delete RegExp.prototype[Symbol.split];
            var re = /a/;
            Object.defineProperty(re, 'source', { get() { throw new SyntaxError('source'); } });
            var outcome;
            try { 'abc'.split(re, 0); outcome = 'returned'; } catch (e) { outcome = e.name + ':' + e.message; }
            outcome
            """).Should().Be("SyntaxError:source");
    }

    [Theory]
    [InlineData("0", "[1,[]]")]
    [InlineData("'0'", "[1,[]]")]
    [InlineData("-0", "[1,[]]")]
    [InlineData("NaN", "[1,[]]")]
    [InlineData("4294967296", "[1,[]]")]
    [InlineData("-4294967296", "[1,[]]")]
    [InlineData("0.9", "[1,[]]")]
    [InlineData("false", "[1,[]]")]
    [InlineData("null", "[1,[]]")]
    [InlineData("({ valueOf() { return 0; } })", "[1,[]]")]
    public void EveryFormOfZeroConvertsTheRegExpSeparatorExactlyOnce(string limit, string expected)
    {
        // ToUint32 maps all of these to 0; the conversion must still happen, and only once.
        Run($$"""
            delete RegExp.prototype[Symbol.split];
            var calls = 0;
            var re = /a/;
            re.toString = function () { calls++; return 'x'; };
            var result = 'abc'.split(re, {{limit}});
            JSON.stringify([calls, result])
            """).Should().Be(expected);
    }

    [Theory]
    [InlineData("1", """[1,["a"]]""")]
    [InlineData("2", """[1,["a","c"]]""")]
    [InlineData("undefined", """[1,["a","c","d"]]""")]
    [InlineData("4294967295", """[1,["a","c","d"]]""")]
    public void TheRegExpSeparatorIsConvertedExactlyOnceForANonZeroLimit(string limit, string expected)
    {
        // The string-separator step that follows must receive the converted string, not convert the regexp again.
        Run($$"""
            delete RegExp.prototype[Symbol.split];
            var calls = 0;
            var re = /a/;
            re.toString = function () { calls++; return 'b'; };
            var result = 'abcbd'.split(re, {{limit}});
            JSON.stringify([calls, result])
            """).Should().Be(expected);
    }

    [Fact]
    public void ARegExpSubclassSeparatorWithoutSplitIsConvertedWhenTheLimitIsZero()
    {
        // A subclass instance is still a RegExp, so it took the same shortcut.
        Run("""
            delete RegExp.prototype[Symbol.split];
            class Pattern extends RegExp {}
            var calls = 0;
            var re = new Pattern('a');
            re.toString = function () { calls++; return 'x'; };
            var result = 'abc'.split(re, 0);
            JSON.stringify([calls, result])
            """).Should().Be("[1,[]]");
    }

    [Fact]
    public void ARegExpWithItsSplitMethodIsNeverConvertedToAString()
    {
        // Step 3 hands the work to @@split, so toString stays unread for a zero limit and for any other.
        Run("""
            var calls = 0;
            var re = /b/;
            re.toString = function () { calls++; return 'x'; };
            var zero = 'abc'.split(re, 0);
            var all = 'abc'.split(re);
            JSON.stringify([calls, zero, all])
            """).Should().Be("""[0,[],["a","c"]]""");
    }

    [Fact]
    public void AStringSeparatorStillSplitsAndHonoursTheLimit()
    {
        // The plain-string path is unchanged: a zero limit gives [], a limit truncates.
        Run("""
            JSON.stringify([
              'a,b'.split(',', 0), 'a,b'.split(',', '0'), 'a,b'.split(',', -0),
              'a,b,c'.split(',', 2), 'a,b,c'.split(','), 'abc'.split(''), 'abc'.split('', 2)
            ])
            """).Should().Be("""[[],[],[],["a","b"],["a","b","c"],["a","b","c"],["a","b"]]""");
    }

    [Fact]
    public void AnObjectSeparatorWithoutSplitIsConvertedExactlyOnce()
    {
        // A non-RegExp object separator is converted once whether or not the limit is zero.
        Run("""
            var calls = 0;
            var separator = { toString() { calls++; return ','; } };
            var result = 'a,b'.split(separator);
            var afterFirst = calls;
            var empty = 'a,b'.split(separator, 0);
            JSON.stringify([afterFirst, calls, result, empty])
            """).Should().Be("""[1,2,["a","b"],[]]""");
    }

    [Fact]
    public void NullAndUndefinedSeparatorsKeepTheirMeaning()
    {
        // null converts to "null" (step 6), undefined stays undefined (step 8) and a zero limit still wins over it (step 7).
        Run("""
            JSON.stringify([
              'anullb'.split(null), 'anullb'.split(null, 1), 'anullb'.split(null, 0),
              'abc'.split(undefined), 'abc'.split(undefined, 0), 'abc'.split(undefined, 1)
            ])
            """).Should().Be("""[["a","b"],["a"],[],["abc"],[],["abc"]]""");
    }
}

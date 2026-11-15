#nullable enable

using Jint.Constraints;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>String.prototype.split("")</c> answers the code units of the first <c>lim</c> characters of the subject, one
/// element each. The result is built in one pass over a pre-sized store, so these pin what that must keep doing: the
/// code-unit semantics (a surrogate pair is two elements, a lone surrogate is one), the <c>ToUint32</c> limit, the
/// array limit, the constraint checks that bound a very long subject, and the realm the result belongs to.
/// https://tc39.es/ecma262/#sec-string.prototype.split
/// </summary>
public class StringSplitEmptySeparatorTests
{
    private static Engine NewEngine() => new();

    // The subject is a script literal and the expectation its code units in hex, so no control character is in a test's
    // name or arguments. U+007E is the last single-character string the engine caches and U+007F the first it does not.
    [TestCase("''", "")]
    [TestCase("'a'", "61")]
    [TestCase("'hello'", "68,65,6c,6c,6f")]
    [TestCase("'a b\\tc\\n'", "61,20,62,9,63,a")]
    [TestCase("'~\\u007f\\u0080\\u00ff'", "7e,7f,80,ff")]
    [TestCase("'\\u0000a'", "0,61")]
    [TestCase("'αβγ'", "3b1,3b2,3b3")]
    [TestCase("'日本語'", "65e5,672c,8a9e")]
    public void ASubjectSplitsIntoOneStringPerCodeUnit(string subject, string expectedCodeUnits)
    {
        var engine = NewEngine();
        engine.Execute("var subject = " + subject + "; var parts = subject.split('');");

        engine.Evaluate("parts.map(function (c) { return typeof c === 'string' && c.length === 1 ? c.charCodeAt(0).toString(16) : 'bad'; }).join(',')")
            .AsString().Should().Be(expectedCodeUnits);
        engine.Evaluate("parts.length === subject.length").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ASurrogatePairIsTwoElementsAndALoneSurrogateIsOne()
    {
        var engine = NewEngine();

        engine.Evaluate("'a\\u{1F600}b'.split('').length").AsNumber().Should().Be(4);
        engine.Evaluate("""
            var p = 'a\u{1F600}b'.split('');
            [p[0], p[1].charCodeAt(0).toString(16), p[2].charCodeAt(0).toString(16), p[3]].join('|')
            """).AsString().Should().Be("a|d83d|de00|b");

        // A lone high surrogate, then a lone low surrogate: neither is joined to the other or dropped.
        engine.Evaluate("""
            var l = '\uDE00a\uD83D'.split('');
            [l.length, l[0].charCodeAt(0).toString(16), l[1], l[2].charCodeAt(0).toString(16)].join('|')
            """).AsString().Should().Be("3|de00|a|d83d");

        // Every element is a string of length 1 whose code unit is the subject's.
        engine.Evaluate("""
            var s = 'x\u{1F600}\uD83D\uDE00y', parts = s.split('');
            parts.every(function (c, i) { return typeof c === 'string' && c.length === 1 && c === s[i]; })
            """).AsBoolean().Should().BeTrue();
    }

    [TestCase("undefined", 5)]
    [TestCase("5", 5)]
    [TestCase("6", 5)]
    [TestCase("4", 4)]
    [TestCase("1", 1)]
    [TestCase("0", 0)]
    [TestCase("-1", 5)]
    [TestCase("1.9", 1)]
    [TestCase("'2'", 2)]
    [TestCase("true", 1)]
    [TestCase("false", 0)]
    [TestCase("null", 0)]
    [TestCase("NaN", 0)]
    [TestCase("Infinity", 0)]
    [TestCase("4294967296", 0)]
    [TestCase("4294967297", 1)]
    [TestCase("4294967295", 5)]
    [TestCase("-4294967295", 1)]
    public void TheLimitIsConvertedWithToUint32(string limit, int expectedLength)
    {
        var engine = NewEngine();

        var result = engine.Evaluate("'hello'.split('', " + limit + ")");

        result.AsArray().Length.Should().Be((uint) expectedLength);
        engine.Evaluate("'hello'.split('', " + limit + ").join('')").AsString().Should().Be("hello".Substring(0, expectedLength));
    }

    [Test]
    public void TheArgumentsAreConvertedInSpecificationOrder()
    {
        var engine = NewEngine();

        // ToString(this), then ToUint32(limit), then ToString(separator): the log is the only thing to see the order.
        // https://tc39.es/ecma262/#sec-string.prototype.split steps 4-6
        engine.Evaluate("""
            var log = [];
            var subject = { toString() { log.push('this'); return 'abc'; } };
            var limit = { valueOf() { log.push('limit'); return 2; } };
            var separator = { toString() { log.push('separator'); return ''; } };
            var r = String.prototype.split.call(subject, separator, limit);
            log.join(',') + '|' + r.join('')
            """).AsString().Should().Be("this,limit,separator|ab");
    }

    [Test]
    public void ARegExpThatMatchesTheEmptyStringSplitsTheSameWay()
    {
        var engine = NewEngine();

        engine.Evaluate("JSON.stringify('abc'.split(/(?:)/))").AsString().Should().Be("[\"a\",\"b\",\"c\"]");
        engine.Evaluate("JSON.stringify('abc'.split(/(?:)/, 2))").AsString().Should().Be("[\"a\",\"b\"]");
        engine.Evaluate("JSON.stringify('abc'.split(new RegExp('')))").AsString().Should().Be("[\"a\",\"b\",\"c\"]");
        engine.Evaluate("'a\\u{1F600}b'.split(/(?:)/).length").AsNumber().Should().Be(4);
        engine.Evaluate("'a\\u{1F600}b'.split(/(?:)/u).length").AsNumber().Should().Be(3);
    }

    [Test]
    public void TheResultIsAnOrdinaryExtensibleArray()
    {
        var engine = NewEngine();

        engine.Evaluate("Array.isArray('abc'.split(''))").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf('abc'.split('')) === Array.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("""
            var a = 'abc'.split('');
            var d = Object.getOwnPropertyDescriptor(a, '1');
            var l = Object.getOwnPropertyDescriptor(a, 'length');
            [d.value, d.writable, d.enumerable, d.configurable, l.value, l.writable, l.enumerable, l.configurable, Object.keys(a).join(','), Object.isExtensible(a)].join('|')
            """).AsString().Should().Be("b|true|true|true|3|true|false|false|0,1,2|true");

        // It is a fresh array every call and it can be written to, grown and truncated like any other.
        engine.Evaluate("""
            var x = 'abc'.split(''), y = 'abc'.split('');
            x.push('d'); x[0] = 'z'; y.length = 1;
            [x.join(''), y.join(''), 'abc'.split('').join('')].join('|')
            """).AsString().Should().Be("zbcd|a|abc");
    }

    [Test]
    public void TheResultBelongsToTheCalleesRealm()
    {
        var engine = NewEngine();
        Test262Object.Install(engine);

        engine.Evaluate("""
            var other = $262.createRealm().global;
            var r = other.String.prototype.split.call('abc', '');
            [r instanceof other.Array, r instanceof Array, r.length].join('|')
            """).AsString().Should().Be("true|false|3");
    }

    // The two sizes either side of every multiple of the constraint interval, where the fill checks the constraints,
    // plus a size past the dense-array limit's neighbourhood is left to the dedicated test below.
    [TestCase(9_999)]
    [TestCase(10_000)]
    [TestCase(10_001)]
    [TestCase(19_999)]
    [TestCase(20_000)]
    [TestCase(20_001)]
    [TestCase(30_000)]
    public void ALongSubjectKeepsEveryCodeUnitInOrder(int length)
    {
        var engine = NewEngine();
        engine.SetValue("length", length);

        engine.Evaluate("""
            var s = Array.from({ length: length }, function (_, i) { return String.fromCharCode(32 + (i % 90)); }).join('');
            var parts = s.split('');
            var ok = parts.length === length;
            for (var i = 0; ok && i < length; i++) { if (parts[i] !== s[i]) ok = false; }
            ok
            """).AsBoolean().Should().BeTrue();

        // And with a limit that ends inside a checked stretch.
        engine.Evaluate("""
            var cut = length - 3;
            var head = s.split('', cut);
            head.length === cut && head.join('') === s.slice(0, cut)
            """).AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ANonAsciiLongSubjectAllocatesItsElementsAndKeepsThemInOrder()
    {
        var engine = NewEngine();

        engine.Evaluate("""
            var s = Array.from({ length: 25000 }, function (_, i) { return String.fromCharCode(0x400 + (i % 200)); }).join('');
            var parts = s.split('');
            var ok = parts.length === 25000;
            for (var i = 0; ok && i < 25000; i++) { if (parts[i].charCodeAt(0) !== 0x400 + (i % 200)) ok = false; }
            ok && parts.join('') === s
            """).AsBoolean().Should().BeTrue();
    }

    // The fill checks the constraints every ConstraintCheckInterval code units, as the loop it replaces did, so a
    // host's timeout or memory limit is not inert while one built-in call builds a very long result.
    [Test]
    public void ALongSplitChecksTheConstraintsAsItFills()
    {
        const int Length = 55_000;
        var constraint = new CountingConstraint();
        var engine = new Engine(options => options.AddConstraint(constraint));
        engine.SetValue("small", "abc");
        engine.SetValue("big", new string('x', Length));

        constraint.Count = 0;
        engine.Execute("var r1 = small.split('');");
        var baseline = constraint.Count;

        constraint.Count = 0;
        engine.Execute("var r2 = big.split('');");

        (constraint.Count - baseline).Should().Be((Length - 1) / Engine.ConstraintCheckInterval,
            "one check at each multiple of the interval the fill passes, and none for the statements, which are the same");
    }

    [Test]
    public void AConstraintThatThrowsStopsTheFill()
    {
        var constraint = new ThrowPastConstraint();
        var engine = new Engine(options => options.AddConstraint(constraint));
        engine.SetValue("small", "abc");
        engine.SetValue("big", new string('x', 55_000));

        constraint.Count = 0;
        engine.Execute("var r1 = small.split('');");
        constraint.Limit = constraint.Count;
        constraint.Count = 0;

        // The same statements ran, so the first check past that many is one the fill made.
        var thrown = Caught.Exception(() => engine.Execute("var r2 = big.split('');"));

        thrown.Should().BeOfType<StatementsCountOverflowException>();
    }

    [TestCase("'abc'.split('')", 2u, false)]
    [TestCase("'abc'.split('')", 3u, true)]
    [TestCase("'abc'.split('', 2)", 2u, true)]
    [TestCase("'abc'.split('', 3)", 2u, false)]
    [TestCase("'abc'.split('', 100)", 2u, false)]
    [TestCase("''.split('')", 0u, true)]
    [TestCase("'abc'.split('', 0)", 0u, true)]
    [TestCase("'abc'.split(/(?:)/)", 2u, false)]
    [TestCase("'abc'.split(/(?:)/, 2)", 2u, true)]
    public void TheArrayLimitBoundsTheResult(string expression, uint maxArraySize, bool allowed)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = maxArraySize);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        if (allowed)
        {
            thrown.Should().BeNull();
        }
        else
        {
            thrown.Should().BeOfType<MemoryLimitExceededException>();
        }
    }

    [Test]
    public void AnArrayPastBothTheArrayLimitAndTheDenseLimitIsRefused()
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 10);
        engine.SetValue("big", new string('x', 12_000_000));

        // 12M code units are past the dense-array limit, the lane that keeps its sparse-backed loop.
        var thrown = Caught.Exception(() => engine.Evaluate("big.split('')"));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
    }

#if NET8_0_OR_GREATER
    [Test]
    public void AnArrayPastTheLimitIsRefusedBeforeItsStoreIsAllocated()
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 10);
        engine.SetValue("big", new string('x', 5_000_000));
        Caught.Exception(() => engine.Evaluate("big.split('')")).Should().BeOfType<MemoryLimitExceededException>();

        // 5M elements are under the dense limit, so this is the pre-sized lane, whose store would be 40 MB. Bytes are
        // counted on this thread, whatever else is running, and the refusal costs a message and a stack.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var thrown = Caught.Exception(() => engine.Evaluate("big.split('')"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        allocated.Should().BeLessThan(4_000_000);
    }
#endif

    private sealed class CountingConstraint : Constraint
    {
        public int Count;

        public override void Check() => Count++;

        public override void Reset()
        {
        }
    }

    private sealed class ThrowPastConstraint : Constraint
    {
        public int Count;
        public int Limit = int.MaxValue;

        public override void Check()
        {
            if (++Count > Limit)
            {
                throw new StatementsCountOverflowException();
            }
        }

        public override void Reset()
        {
        }
    }
}

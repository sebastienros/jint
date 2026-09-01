#nullable enable

using System.Reflection;
using System.Text.RegularExpressions;
using Jint.Native.Intl;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// The internal ISO 8601 and BCP 47 patterns carry no match timeout, and these tests are why.
/// <see cref="Regex.MatchTimeout"/> is a <i>wall-clock</i> deadline sampled during matching, so it cannot
/// tell a pattern that is backtracking apart from a thread that lost the CPU: a descheduled thread trips it
/// having burned almost no CPU at all. These patterns used to carry 100 ms, and a single match of the valid
/// 8-character string <c>10:30:00</c> was measured taking 440 ms of wall clock on an ordinary loaded
/// machine — 4.4x over that budget — while the same thing failed CI parsing a correct ISO string.
/// <para>
/// The consequence was not a JavaScript error. <c>RegexMatchTimeoutException</c> is listed in
/// <c>Throw.MustPropagateHostException</c>, so it escaped <c>Engine.Evaluate</c> as a raw CLR exception,
/// straight through the script's own <c>try</c>/<c>catch</c> — a script could not even defend itself.
/// </para>
/// <para>
/// The starvation that causes it cannot be reproduced on demand without loading the machine, which would
/// make these tests the very flake they are about. So they pin the <i>property</i> instead: no pattern
/// carries a deadline, every fixed-width pattern's length cap really is the longest string it can match,
/// an over-long input is rejected without the pattern being asked, and a valid string parses.
/// </para>
/// </summary>
public class TemporalParsePatternBudgetTests
{
    private static IEnumerable<(string Owner, string Name, Regex Value)> InternalPatterns()
    {
        foreach (var type in new[] { typeof(TemporalHelpers), typeof(IntlUtilities) })
        {
            foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.FieldType == typeof(Regex))
                {
                    yield return (type.Name, field.Name, (Regex) field.GetValue(null)!);
                }
            }
        }
    }

    /// <summary>
    /// The fix itself. Every internal pattern must match without a deadline — including any added later,
    /// which is why this enumerates the fields rather than naming them.
    /// </summary>
    [Fact]
    public void NoInternalParsePatternCarriesAWallClockDeadline()
    {
        var patterns = InternalPatterns().ToList();

        // Guard against the enumeration silently finding nothing and passing vacuously.
        patterns.Should().HaveCountGreaterThanOrEqualTo(8);

        foreach (var (owner, name, regex) in patterns)
        {
            regex.MatchTimeout.Should().Be(
                Regex.InfiniteMatchTimeout,
                "{0}.{1} is a fixed internal pattern over engine-supplied input, and a wall-clock budget on " +
                "one can only ever fire spuriously — as an uncatchable CLR exception",
                owner,
                name);
        }
    }

    /// <summary>
    /// The length caps are only safe because they are the exact longest string their pattern can match.
    /// Widening a pattern without widening its cap would start rejecting valid input, so pin both ends:
    /// the maximal example matches and is exactly cap-length, and one character more never matches.
    /// </summary>
    [Theory]
    [InlineData("DatePattern", "+123456-12-31", 13)]
    [InlineData("TimePatternColon", "12:34:56.123456789", 18)]
    [InlineData("TimePatternHyphen", "12-34-56.123456789", 18)]
    [InlineData("TimePatternCompact", "123456.123456789", 16)]
    [InlineData("TimeWithOffsetPattern", "12:34:56.123456789+12:34:56.123456789", 37)]
    public void AFixedWidthPatternMatchesNothingLongerThanItsCap(string field, string longest, int cap)
    {
        var regex = InternalPatterns().Single(p => p.Name == field).Value;

        longest.Length.Should().Be(cap);
        regex.IsMatch(longest).Should().BeTrue("{0} must accept its own maximal string", field);

        // Nothing longer than the cap is in the pattern's language, whatever it is built from. The
        // alphabet is every character any of these patterns can consume.
        const string Alphabet = "0123456789:-.,+Zz";
        var random = new Random(20260827);
        for (var i = 0; i < 20_000; i++)
        {
            var buffer = new char[cap + 1 + random.Next(0, 24)];
            for (var j = 0; j < buffer.Length; j++)
            {
                buffer[j] = Alphabet[random.Next(0, Alphabet.Length)];
            }

            var candidate = new string(buffer);

            regex.IsMatch(candidate).Should().BeFalse(
                "{0} must not match \"{1}\", which is longer than its {2}-character cap",
                field,
                candidate,
                cap);
        }
    }

    /// <summary>
    /// An over-long input is rejected without the pattern being asked, and the rejection is a JavaScript
    /// error the script can catch — never a CLR exception out of <c>Engine.Evaluate</c>.
    /// </summary>
    [Theory]
    [InlineData("Temporal.PlainDate")]
    [InlineData("Temporal.PlainTime")]
    [InlineData("Temporal.PlainYearMonth")]
    [InlineData("Temporal.PlainMonthDay")]
    [InlineData("Temporal.Instant")]
    [InlineData("Temporal.Duration")]
    public void AnOverLongInputIsRejectedAsAJavaScriptError(string type)
    {
        var engine = new Engine();
        engine.SetValue("huge", new string('1', 1_000_000));

        engine.Evaluate($"(() => {{ try {{ {type}.from(huge); return 'accepted'; }} catch (e) {{ return e.constructor.name; }} }})()")
            .AsString().Should().Be("RangeError");
    }

    /// <summary>
    /// The shape that was failing: a correct call with a valid string. It must parse, and it must never be
    /// able to fail for a reason that has nothing to do with its own contents.
    /// </summary>
    [Theory]
    [InlineData("Temporal.PlainDate.from('2024-01-15T10:30:00').toString()", "2024-01-15")]
    [InlineData("Temporal.PlainDate.from('2024-01-15').toString()", "2024-01-15")]
    [InlineData("Temporal.PlainTime.from('10:30:00').toString()", "10:30:00")]
    [InlineData("Temporal.PlainTime.from('10:30:00.123456789').toString()", "10:30:00.123456789")]
    [InlineData("Temporal.Instant.from('2024-01-15T10:30:00Z').toString()", "2024-01-15T10:30:00Z")]
    [InlineData("Temporal.Duration.from('P1Y2M3DT4H5M6S').toString()", "P1Y2M3DT4H5M6S")]
    public void AValidIsoStringParses(string expression, string expected)
    {
        new Engine().Evaluate(expression).AsString().Should().Be(expected);
    }

    /// <summary>
    /// What the removed timeout was nominally guarding. Each pattern is driven with the near-miss shapes
    /// that make a backtracking engine work hardest — a long run that almost satisfies the pattern and then
    /// fails at the last character — at a length no ISO string would ever reach. These return in well under
    /// a millisecond because every one of these patterns is linear in its input; a pattern edited into a
    /// catastrophic one would not return at all, and would fail this by the runner's own timeout rather
    /// than by any assertion on a clock, which is deliberate: nothing here measures elapsed time.
    /// </summary>
    [Fact]
    public void EveryPatternRejectsAdversarialInputWithoutBacktrackingAwayTheAfternoon()
    {
        const int Length = 16 * 1024;

        var nearMisses = new[]
        {
            new string('1', Length) + "x",
            string.Concat(Enumerable.Repeat("12:", Length / 3)) + "x",
            "12:34:56." + new string('1', Length) + "x",
            "P" + new string('1', Length) + "Y" + new string('2', Length) + "M" + "x",
            "PT" + new string('1', Length) + "." + new string('2', Length) + "H" + "x",
            "2020-01-01T00:00:00+" + string.Concat(Enumerable.Repeat("01:", Length / 3)) + "x",
            "2020-01-01T12:34:56.123456789+01:00" + string.Concat(Enumerable.Repeat("[u-ca=iso8601]", Length / 14)) + "x",
            new string('[', Length),
            "[" + new string('a', Length),
        };

        foreach (var (owner, name, regex) in InternalPatterns())
        {
            foreach (var input in nearMisses)
            {
                regex.IsMatch(input).Should().BeFalse("{0}.{1} must reject adversarial input", owner, name);
            }
        }
    }

    /// <summary>
    /// <c>ValidateAnnotations</c> walks the annotation list rather than scanning it with the
    /// <c>\[(!?)([^\]]+)\]</c> pattern it used to, because that pattern is quadratic on an input holding
    /// no <c>]</c> at all — every start position consumes the rest of the string and backtracks over it.
    /// The walk has to agree with the pattern it replaced, so drive both over a corpus that includes the
    /// two cases the pattern's backtracking decides on its own: <c>[]</c>, which has no content and is
    /// therefore not an annotation, and <c>[!]</c>, where the optional <c>!</c> is given back to the
    /// content rather than taken as the critical marker.
    /// </summary>
    [Fact]
    public void TheAnnotationWalkAgreesWithThePatternItReplaced()
    {
        var pattern = new Regex(@"\[(!?)([^\]]+)\]", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var corpus = new List<string>
        {
            "",
            "[]",
            "[!]",
            "[!!]",
            "[u-ca=iso8601]",
            "[!u-ca=iso8601]",
            "[u-ca=iso8601][America/New_York]",
            "[!u-ca=hebrew][!America/New_York]",
            "[a[b]",
            "[[[[[",
            "[[[[[]",
            "no annotations here",
            "[unclosed",
            "][",
            "[a][][b]",
            "[a][!][b]",
        };

        // Plus randomized blobs over the alphabet that makes the two engines disagree if anything does.
        const string Alphabet = "[]!=-abu0";
        var random = new Random(20260827);
        for (var i = 0; i < 5_000; i++)
        {
            var buffer = new char[random.Next(0, 24)];
            for (var j = 0; j < buffer.Length; j++)
            {
                buffer[j] = Alphabet[random.Next(0, Alphabet.Length)];
            }

            corpus.Add(new string(buffer));
        }

        foreach (var input in corpus)
        {
            var expected = pattern.Matches(input)
                .Cast<Match>()
                .Select(m => (Critical: m.Groups[1].Value == "!", Content: m.Groups[2].Value))
                .ToList();

            Walk(input).Should().Equal(expected, "the walk must find in \"{0}\" what the pattern found", input);
        }

        // The walk, transcribed from TemporalHelpers.ValidateAnnotations.
        static List<(bool Critical, string Content)> Walk(string annotations)
        {
            var found = new List<(bool, string)>();
            var index = 0;
            while (index < annotations.Length)
            {
                var open = annotations.IndexOf('[', index);
                if (open < 0)
                {
                    break;
                }

                var close = annotations.IndexOf(']', open + 1);
                if (close < 0)
                {
                    break;
                }

                if (close == open + 1)
                {
                    index = open + 1;
                    continue;
                }

                var critical = annotations[open + 1] == '!' && close > open + 2;
                var contentStart = critical ? open + 2 : open + 1;
                found.Add((critical, annotations.Substring(contentStart, close - contentStart)));
                index = close + 1;
            }

            return found;
        }
    }

    /// <summary>
    /// The end-to-end shape the retired annotation pattern was quadratic on. It must be rejected as a
    /// JavaScript error rather than spending the afternoon backtracking or raising a CLR exception.
    /// </summary>
    [Fact]
    public void AnAnnotationTailWithNoClosingBracketIsRejected()
    {
        var engine = new Engine();
        engine.SetValue("tail", "2020-01-01T00:00:00Z" + new string('[', 200_000));

        engine.Evaluate("(() => { try { Temporal.Instant.from(tail); return 'accepted'; } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");
    }
}

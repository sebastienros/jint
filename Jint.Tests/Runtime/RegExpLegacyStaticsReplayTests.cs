#nullable enable

using System.Text.RegularExpressions;
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// A lane that asked its matcher only for a range, or only for a yes, records the search that found the match and the
/// first read of a static runs it again. Running it can fail - the regex timeout, the backtracking ceiling - and when it
/// does the statics must not describe a match nobody found: the match reads as unavailable (a <c>TypeError</c>, as after an
/// invalidation) while <c>input</c>, which the replay does not need, stays readable, and the next successful match sets
/// them all again. The failure cannot be provoked through a lane (the lane's own search would fail the same way before it
/// recorded anything), so the pending search is recorded directly, as a lane would, for a match the search cannot find in
/// time.
/// </summary>
public class RegExpLegacyStaticsReplayTests
{
    [Test]
    public void AReplayThatTimesOutLeavesTheMatchUnreadableAndInputReadable()
    {
        var engine = new Engine(o => o.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(50));
        var regExp = (JsRegExp) engine.Evaluate("/^(?:a|a)*b/");
        var subject = new string('a', 30) + "!b";
        engine.Realm.Intrinsics.RegExp.SetLegacyPending(subject, regExp, regExp.Matcher!, 0, find: false);

        // input does not need the search ...
        engine.Evaluate("RegExp.input").AsString().Should().Be(subject);

        // ... the first read of anything else runs it, and it times out ...
        var act = () => engine.Evaluate("RegExp.lastMatch");
        act.Should().Throw<RegexMatchTimeoutException>();

        // ... after which the match is unavailable, not empty: nothing invented from a zeroed range.
        engine.Evaluate("""
            var out = [];
            for (var k of ['$1', 'lastMatch', 'lastParen', 'leftContext', 'rightContext']) {
              try { RegExp[k]; out.push('ok'); } catch (e) { out.push(e.constructor.name); }
            }
            out.join()
            """).AsString().Should().Be("TypeError,TypeError,TypeError,TypeError,TypeError");
        engine.Evaluate("RegExp.input").AsString().Should().Be(subject);

        // The next successful match sets them all again.
        engine.Evaluate("/(b)/.exec('xb'); JSON.stringify([RegExp.$1, RegExp.lastMatch, RegExp.leftContext, RegExp.input])")
            .AsString().Should().Be("""["b","b","x","xb"]""");
    }

    [Test]
    public void TheMatchAReplayFoundStaysWhileLaterMatchesFail()
    {
        var engine = new Engine();
        engine.Evaluate("/(b)(c)?/.test('abc')");
        engine.Evaluate("[RegExp.$1, RegExp.$2, RegExp.lastParen, RegExp.leftContext, RegExp.rightContext].join()")
            .AsString().Should().Be("b,c,c,a,");
        // The reads that follow see the same match, and a match that fails in between does not disturb it.
        engine.Evaluate("/(q)/.test('abc'); [RegExp.$1, RegExp.$2, RegExp.lastParen, RegExp.lastMatch].join()")
            .AsString().Should().Be("b,c,c,bc");
    }
}

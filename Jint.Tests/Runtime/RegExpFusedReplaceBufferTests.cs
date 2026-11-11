#nullable enable

using System.Reflection;
using Jint.Native.RegExp;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins what a fused <c>String.prototype.replace</c> over a regular expression (a replacer function or a
/// <c>$</c> template) must go on doing while it collects its match spans in a buffer kept between calls: a replacer
/// is script, so it can run a replace of its own, throw, or leave the legacy statics for the next caller.
/// https://tc39.es/ecma262/#sec-regexp.prototype-@@replace
/// </summary>
public class RegExpFusedReplaceBufferTests
{
    private static string Run(string body)
    {
        var engine = new Engine();
        return engine.Evaluate("(function() {" + body + "})()").ToString();
    }

    private static int[]? RetainedSpans(Engine engine)
    {
        var field = typeof(RegExpPrototype).GetField("_spansBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (int[]?) field!.GetValue(engine.Realm.Intrinsics.RegExp.PrototypeObject);
    }

    // Around the buffer's initial size, its doubling steps and the cap it is retained under. The capture count
    // includes group 0, so a match of /(a)b/ takes two (index, length) pairs, four ints: the 64-int buffer holds 16
    // matches and the 17th grows it, 2048 ints hold 512 and the 513th grows it, and 4096 ints hold 1024, so 1024 is
    // the last size kept and 1025 grows the buffer past the cap and drops it.
    [TestCase(1)]
    [TestCase(15)]
    [TestCase(16)]
    [TestCase(17)]
    [TestCase(511)]
    [TestCase(512)]
    [TestCase(513)]
    [TestCase(1023)]
    [TestCase(1024)]
    [TestCase(1025)]
    [TestCase(5000)]
    public void AReplaceOfAnySizeIsExact(int matches)
    {
        var engine = new Engine();
        engine.SetValue("n", matches);

        engine.Evaluate("'ab'.repeat(n).replace(/(a)b/g, 'b$1') === 'ba'.repeat(n)").AsBoolean().Should().BeTrue();
        engine.Evaluate("'ab'.repeat(n).replace(/(a)b/g, (m, a) => 'b' + a) === 'ba'.repeat(n)").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ReplacesOfAlternatingSizesStayExactWhileTheyShareOneBuffer()
    {
        var engine = new Engine();
        foreach (var matches in new[] { 1000, 3, 2000, 1, 1025, 1024, 5000, 2, 700, 700 })
        {
            engine.SetValue("n", matches);
            engine.Evaluate("'ab'.repeat(n).replace(/(a)b/g, 'b$1') === 'ba'.repeat(n)").AsBoolean().Should().BeTrue($"n = {matches}");
            engine.Evaluate("'ab'.repeat(n).replace(/(a)b/g, (m, a) => 'b' + a) === 'ba'.repeat(n)").AsBoolean().Should().BeTrue($"n = {matches}");
        }
    }

    [Test]
    public void ASmallReplaceLeavesItsBufferForTheNextOne()
    {
        var engine = new Engine();
        RetainedSpans(engine).Should().BeNull();

        engine.Evaluate("'xaby'.replace(/(a)b/g, 'b$1')").AsString().Should().Be("xbay");
        var kept = RetainedSpans(engine);
        kept.Should().NotBeNull();

        engine.Evaluate("'xaby'.replace(/(a)b/g, 'b$1')").AsString().Should().Be("xbay");
        RetainedSpans(engine).Should().BeSameAs(kept, "the next replace of the same size takes it and gives it back");
    }

    [Test]
    public void ARepeatedReplaceThatFindsNothingKeepsTheBuffer()
    {
        var engine = new Engine();
        engine.Evaluate("'xaby'.replace(/(a)b/g, 'b$1')");
        var kept = RetainedSpans(engine);
        kept.Should().NotBeNull();

        engine.Evaluate("'xyz'.replace(/(a)b/g, 'b$1')").AsString().Should().Be("xyz");
        RetainedSpans(engine).Should().BeSameAs(kept);
    }

    [Test]
    public void AHugeReplaceDoesNotPinItsStorage()
    {
        var engine = new Engine();
        engine.Evaluate("'ab'.repeat(5000).replace(/(a)b/g, 'b$1').length").AsNumber().Should().Be(10000);

        // 20000 ints on a fresh engine: past the cap, so nothing is kept for the life of the realm.
        RetainedSpans(engine).Should().BeNull();
    }

    [Test]
    public void AReplaceWithinAReplacerDoesNotShareTheOuterBuffer()
    {
        var engine = new Engine();
        // The outer replace holds its spans while its replacer runs; the inner one must not write into them.
        engine.Evaluate("""
            'abcabc'.replace(/(a)(b)(c)/g, function(m, a, b, c) { return 'xyzxyz'.replace(/(x)(y)(z)/g, '$3$2$1') + a + b + c; })
            """).AsString().Should().Be("zyxzyxabczyxzyxabc");
    }

    [Test]
    public void AReplacerThatThrowsLeavesTheNextReplaceWorking()
    {
        var engine = new Engine();
        engine.Evaluate("'aaa'.replace(/(a)/g, '[$1]')").AsString().Should().Be("[a][a][a]");

        engine.Evaluate("try { 'aaa'.replace(/(a)/g, function() { throw new RangeError('x'); }); 'no error' } catch (e) { e.name }")
            .AsString().Should().Be("RangeError");

        engine.Evaluate("'aaa'.replace(/(a)/g, '[$1]')").AsString().Should().Be("[a][a][a]");
        engine.Evaluate("'aaa'.replace(/(a)/g, function(m, g, i) { return g + i; })").AsString().Should().Be("a0a1a2");
    }

    [Test]
    public void NestedReplaceInTheReplacer()
    {
        Run("""
            var re = /(\d)(x)?/g;
            return '1x2'.replace(re, function(m, d, x) { return d + ':' + 'ab'.replace(/(a)(b)/g, '$2$1') + ':' + (x === undefined ? 'u' : x); });
            """).Should().Be("1:ba:x2:ba:u");
    }

    [Test]
    public void NestedReplaceUsingTheSameRegExp()
    {
        Run("""
            var re = /(a)(b)?/g;
            var out = 'ab a'.replace(re, function(m, a, b) { return '<' + 'xab'.replace(re, '[$1$2]') + '>'; });
            return out + '|' + re.lastIndex;
            """).Should().Be("<x[ab]> <x[ab]>|0");
    }

    [Test]
    public void RecursiveReplace()
    {
        Run("""
            function r(n, s) { return n === 0 ? s : s.replace(/(\w)(\w)/g, function(m, a, b) { return r(n - 1, b + a); }); }
            return r(5, 'abcdef');
            """).Should().Be("badcfe");
    }

    [Test]
    public void ReplacerThatThrows()
    {
        Run("""
            var out = [];
            try { 'aaa'.replace(/(a)/g, function(m, g) { if (out.length === 1) throw new Error('x'); out.push(g); return 'b'; }); } catch (e) { out.push(e.message); }
            out.push('aaa'.replace(/(a)/g, '[$1]'));
            return out.join('|');
            """).Should().Be("a|x|[a][a][a]");
    }

    [Test]
    public void LegacyStaticsAfterANestedReplace()
    {
        Run("""
            'xaby'.replace(/(a)(b)/g, function(m, a, b) { 'qzq'.replace(/(z)/g, '$1'); return m; });
            return RegExp.$1 + '|' + RegExp.lastMatch;
            """).Should().Be("z|z");
    }

    [Test]
    public void LegacyStaticsAfterAReplace()
    {
        Run("""
            'xaby'.replace(/(a)(b)/g, '$2$1');
            return RegExp.$1 + RegExp.$2 + '|' + RegExp.lastMatch + '|' + RegExp.leftContext + '|' + RegExp.rightContext;
            """).Should().Be("ab|ab|x|y");
    }

    [Test]
    public void EmptyMatchesAdvanceByCodePointUnderU()
    {
        Run("""
            return 'a\u{1F600}b'.replace(/(?:)/gu, function(m, i) { return '-' + i; });
            """).Should().Be("-0a-1\ud83d\ude00-3b-4");
    }

    [Test]
    public void EmptyMatchesAdvanceByCodeUnitWithoutU()
    {
        Run("""
            return 'a\u{1F600}b'.replace(/(?:)/g, function(m, i) { return '-'; }).length;
            """).Should().Be("9");
    }

    [Test]
    public void OptionalGroupsAreUndefined()
    {
        Run("""
            return JSON.stringify('abd'.replace(/(a)(x)?(b)(c)?/g, function(m, a, x, b, c, i) { return [x, c, i].join(',') + ';'; }));
            """).Should().Be("\",,0;d\"");
    }

    [Test]
    public void NamedGroups()
    {
        Run("""
            return 'k=v;k2=v2'.replace(/(?<k>\w+)=(?<v>\w+)/g, '$<v>=$<k>');
            """).Should().Be("v=k;v2=k2");
    }

    [Test]
    public void NamedGroupsInAReplacer()
    {
        Run("""
            return 'k=v;k2=v2'.replace(/(?<k>\w+)=(?<v>\w+)/g, function() { var g = arguments[arguments.length - 1]; return g.v + '=' + g.k; });
            """).Should().Be("v=k;v2=k2");
    }

    [Test]
    public void NonGlobalReplacesOnce()
    {
        Run("""
            return 'aaa'.replace(/(a)/, '[$1]') + '|' + 'aaa'.replace(/(a)/, function(m, a) { return '<' + a + '>'; });
            """).Should().Be("[a]aa|<a>aa");
    }

    [Test]
    public void NoMatchReturnsTheSubject()
    {
        Run("""
            var s = 'abc';
            return ('abc'.replace(/(z)/g, '[$1]') === s) + '|' + 'abc'.replace(/(z)/g, function() { return 'q'; });
            """).Should().Be("true|abc");
    }
}

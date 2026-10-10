#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>Generated patterns retain captures, backreferences, and finite repetition in Unicode mode.</summary>
public class RegExpCaptureTests
{
    [TestCase(255, "u")]
    [TestCase(300, "u")]
    [TestCase(255, "v")]
    [TestCase(300, "v")]
    public void UnicodePatternsSupportLargeCaptureSets(int captures, string flags)
    {
        using var engine = new Engine();
        engine.SetValue("pattern", string.Concat(System.Linq.Enumerable.Repeat("(a)", captures)));
        engine.SetValue("subject", new string('a', captures));
        engine.SetValue("flags", flags);

        engine.Evaluate("new RegExp(pattern, flags).exec(subject).length").Should().Be(captures + 1);
    }

    [TestCase("numeric")]
    [TestCase("forward")]
    [TestCase("named")]
    [TestCase("reset")]
    public void LargeCaptureSetsKeepTheirBackreferencesAndIterationState(string scenario)
    {
        var prefix = string.Concat(System.Linq.Enumerable.Repeat("(a)", 299));
        var subject = new string('a', 299);
        var (pattern, suffix) = scenario switch
        {
            "numeric" => (prefix + @"(b)\300", "bb"),
            "forward" => (@"\300" + prefix + "(b)", "b"),
            "named" => (prefix + @"(?<last>b)\k<last>", "bb"),
            _ => (prefix + "(?:(b)|(c))+", "bc"),
        };
        using var nonUnicodeEngine = new Engine();
        using var unicodeEngine = new Engine();
        foreach (var engine in new[] { nonUnicodeEngine, unicodeEngine })
        {
            engine.SetValue("pattern", pattern);
            engine.SetValue("subject", subject + suffix);
        }

        const string expression = "JSON.stringify((m => m && [Array.from(m), m.groups, m.indices, m.indices.groups])(new RegExp(pattern, 'd').exec(subject)))";
        var expected = nonUnicodeEngine.Evaluate(expression).ToString();
        expected.Should().NotBe("null");
        unicodeEngine.Evaluate(expression.Replace("'d'", "'du'")).ToString().Should().Be(expected);
    }

    [Test]
    public void ManyNamedAlternativesKeepTheirSharedBackreference()
    {
        var pattern = "(?:" + string.Join("|", System.Linq.Enumerable.Repeat("(?<part>a)", 299)) + "|(?<part>b)" + @")\k<part>";
        using var engine = new Engine();
        engine.SetValue("pattern", pattern);

        engine.Evaluate("JSON.stringify((m => [m[300], m.groups.part, m.indices[300], m.indices.groups.part])(new RegExp(pattern, 'du').exec('bb')))").ToString().Should().Be("[\"b\",\"b\",[0,1],[0,1]]");
        engine.Evaluate("new RegExp(pattern, 'u').test('ab')").Should().Be(false);
    }

    [Test]
    public void UnicodePatternsSupportDeeplyNestedFiniteQuantifiers()
    {
        var pattern = "a";
        for (var i = 0; i < 300; i++)
        {
            pattern = "(?:" + pattern + "){1}";
        }
        using var engine = new Engine();
        engine.SetValue("pattern", pattern);

        engine.Evaluate("new RegExp(pattern, 'u').test('a')").Should().Be(true);
    }

    [Test]
    public void ExcessiveGroupNestingProducesACatchableSyntaxError()
    {
        const int depth = 20000;
        var pattern = string.Concat(System.Linq.Enumerable.Repeat("(?:", depth)) + "a" + new string(')', depth);
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine();
            engine.SetValue("pattern", pattern);
            engine.Evaluate("(() => { try { new RegExp(pattern, 'v'); return 'compiled'; } catch (error) { return error.name; } })()")
                .AsString().Should().Be("SyntaxError");
            engine.Evaluate("6 * 7").Should().Be(42);
        }, joinTimeout: TestBudgets.WedgeCeiling, maxStackSize: 1024 * 1024);
    }
}

#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>Generated patterns retain captures, backreferences, and finite repetition in Unicode mode.</summary>
public class HostRegexMatcherCompatibilityTests
{
    [TestCase(255, "u")]
    [TestCase(300, "u")]
    [TestCase(255, "v")]
    [TestCase(300, "v")]
    public void TheCooperativeMatcherSupportsLargeCaptureSets(int captures, string flags)
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
        using var unbounded = new Engine();
        using var cooperative = new Engine();
        foreach (var engine in new[] { unbounded, cooperative })
        {
            engine.SetValue("pattern", pattern);
            engine.SetValue("subject", subject + suffix);
        }

        const string expression = "JSON.stringify(new RegExp(pattern, 'd').exec(subject))";
        var expected = unbounded.Evaluate(expression).ToString();
        expected.Should().NotBe("null");
        cooperative.Evaluate("JSON.stringify(new RegExp(pattern, 'du').exec(subject))").ToString().Should().Be(expected);
    }

    [Test]
    public void ManyNamedAlternativesKeepTheirSharedBackreference()
    {
        var pattern = "(?:" + string.Join("|", System.Linq.Enumerable.Repeat("(?<part>a)", 300)) + @")\k<part>";
        using var engine = new Engine();
        engine.SetValue("pattern", pattern);

        engine.Evaluate("new RegExp(pattern, 'du').exec('aa').groups.part").Should().Be("a");
        engine.Evaluate("new RegExp(pattern, 'u').test('ab')").Should().Be(false);
    }

    [Test]
    public void TheCooperativeMatcherSupportsDeeplyNestedFiniteQuantifiers()
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
}

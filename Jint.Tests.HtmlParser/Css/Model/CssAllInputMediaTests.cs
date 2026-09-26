#nullable enable

using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssAllInputMediaTests
{
    [TestCase(1, false, true, false, false)]
    [TestCase(2, true, false, true, false)]
    [TestCase(4, true, false, false, true)]
    [TestCase(6, true, false, true, true)]
    public void AllPointerQueriesUseTheUnionRatherThanThePrimaryDevice(int capabilities,
        bool boolean, bool none, bool coarse, bool fine)
    {
        var environment = new CssMediaEnvironment { Pointer = "none", AnyPointer = (CssPointerCapabilities) capabilities };
        Matches("(any-pointer)", environment).Should().Be(boolean);
        Matches("(any-pointer:none)", environment).Should().Be(none);
        Matches("(any-pointer:coarse)", environment).Should().Be(coarse);
        Matches("(any-pointer:fine)", environment).Should().Be(fine);
        Matches("(pointer:none)", environment).Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(7)]
    [TestCase(8)]
    public void InvalidPointerSnapshotsRemainUnknownEvenUnderNegation(int capabilities)
    {
        var environment = new CssMediaEnvironment { AnyPointer = (CssPointerCapabilities) capabilities };
        foreach (var query in new[] { "(any-pointer)", "(any-pointer:none)", "not (any-pointer:none)", "not (any-pointer)" })
            Matches(query, environment).Should().BeFalse();
    }

    [TestCase("none", false)]
    [TestCase("hover", true)]
    public void AnyHoverIsIndependentAndHasABooleanContext(string hover, bool expected)
    {
        var environment = new CssMediaEnvironment { Hover = "none", AnyHover = hover };
        Matches("(any-hover)", environment).Should().Be(expected);
        Matches("(any-hover:hover)", environment).Should().Be(expected);
        Matches("(any-hover:none)", environment).Should().Be(!expected);
        Matches("(hover:none)", environment).Should().BeTrue();
    }

    [TestCase("fullscreen")]
    [TestCase("standalone")]
    [TestCase("minimal-ui")]
    [TestCase("browser")]
    [TestCase("picture-in-picture")]
    public void DisplayModeUsesTheIndependentHostSnapshot(string mode)
    {
        var environment = new CssMediaEnvironment { DisplayMode = mode };
        Matches("(display-mode:" + mode + ")", environment).Should().BeTrue();
        Matches("(display-mode)", environment).Should().BeTrue();
        Matches("not (display-mode:" + mode + ")", environment).Should().BeFalse();
    }

    [Test]
    public void InvalidStringSnapshotsAreNotTurnedIntoDefaultCapabilities()
    {
        var environment = new CssMediaEnvironment { AnyHover = "unavailable", DisplayMode = "invalid" };
        foreach (var query in new[] { "(any-hover)", "not (any-hover:none)", "(display-mode)", "not (display-mode:browser)" })
            Matches(query, environment).Should().BeFalse();
    }

    [TestCase("(any-pointer:coarse fine)")]
    [TestCase("(any-pointer:invalid)")]
    [TestCase("(any-hover:coarse)")]
    [TestCase("(display-mode:none)")]
    [TestCase("(min-any-pointer:coarse)")]
    [TestCase("(max-any-hover:hover)")]
    [TestCase("(display-mode >= browser)")]
    [TestCase("(any-pointer = fine)")]
    [TestCase("(any-hover > none)")]
    public void IllegalDiscreteFormsCannotMatchOrBecomeTrueUnderNegation(string query)
    {
        Matches(query, new CssMediaEnvironment()).Should().BeFalse();
        Matches("not " + query, new CssMediaEnvironment()).Should().BeFalse();
    }

    [Test]
    public void DefaultsAndLongPostfixEvaluationRemainBounded()
    {
        var environment = new CssMediaEnvironment();
        Matches("(any-pointer:fine) and (any-hover:hover) and (display-mode:browser)", environment).Should().BeTrue();
        var list = CssMediaList.Parse(string.Join(" and ", Enumerable.Repeat("(any-pointer:fine)", 16384)));
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 2) cancellation.Cancel(); });
        Action read = () => list.Matches(environment, work);
        read.Should().Throw<OperationCanceledException>();
    }

    private static bool Matches(string query, CssMediaEnvironment environment) => CssMediaList.Parse(query).Matches(environment);
}

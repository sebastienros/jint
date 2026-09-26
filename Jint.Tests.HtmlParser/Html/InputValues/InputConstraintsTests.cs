#nullable enable
using Jint.HtmlParser;
using System.Globalization;

namespace Jint.Tests.HtmlParser.Html.InputValues;

public class InputConstraintsTests
{
    [TestCase(".3", ".1", false)]
    [TestCase(".30000000000000004", ".1", true)]
    [TestCase("-.3", ".1", false)]
    [TestCase("9007199254740991", "2", true)]
    [TestCase("9007199254740993", "2", false)]
    [TestCase("9007199254740992", "3", true)]
    [TestCase("9007199254740994", "3", true)]
    [TestCase("5e-324", "5e-324", false)]
    [TestCase("5e-324", "2e-324", true)]
    [TestCase("1", "2e308", false)]
    public void ExactRemainder(string value, string step, bool mismatch)
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, null, null, step, null);
        constraints.GetFacts(value).StepMismatch.Should().Be(mismatch);
    }

    [TestCase(".3", ".1", false, 1, "0.4")]
    [TestCase(".30000000000000004", ".1", true, 1, "0.3")]
    [TestCase("-.3", ".1", true, 1, "-0.4")]
    [TestCase("9007199254740992", "3", false, 1, "9007199254740992")]
    [TestCase("9007199254740992", "3", false, 0, "9007199254740992")]
    [TestCase("9007199254740992", "3", true, 1, "9007199254740990")]
    [TestCase("5e-324", "5e-324", false, 1, "1e-323")]
    [TestCase(".3", ".1", false, -1, null)]
    [TestCase("1", "1", true, int.MinValue, null)]
    [TestCase(".3", ".1", false, int.MinValue, null)]
    [TestCase(".35", ".1", false, 0, "0.4")]
    [TestCase(".35", ".1", true, -100, "0.3")]
    [TestCase("1e308", "1e308", false, 1, null)]
    public void OrderedStepping(string value, string step, bool down, int count, string? expected)
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, null, null, step, null);
        var result = constraints.GetStep(value, count, down);
        result.Status.Should().Be(expected is null ? HtmlInputStepStatus.Unchanged : HtmlInputStepStatus.Write);
        result.Value.Should().Be(expected);
    }

    [TestCase(null, null, null, null, "", "50")]
    [TestCase("0", "100", "20", null, "50", "60")]
    [TestCase("5.3", "12", null, null, "6.7", "6.3")]
    [TestCase("5.3", "12", ".5", null, "6.7", "6.8")]
    [TestCase("0", "1", ".1", null, ".15", "0.2")]
    [TestCase("-1e308", "1e308", "any", null, "", "0")]
    [TestCase("0", "5", "junk", null, "", "3")]
    [TestCase("-5", "0", "1", null, "", "-2")]
    [TestCase("5", "2", "any", null, "", "5")]
    [TestCase("5", "5", "1", null, "", "5")]
    [TestCase(null, "0.1", "1", "0.5", "", "0.05")]
    [TestCase("0", "100", "20", null, "00040", "00040")]
    [TestCase("0", "100", "20", null, "4E1", "4E1")]
    [TestCase(null, ".1", "1", ".5", ".050", ".050")]
    public void RangeSanitization(string? min, string? max, string? step, string? defaultValue, string value, string expected)
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Range, min, max, step, defaultValue);
        HtmlInputRangeValue.Sanitize(value, constraints).Should().Be(expected);
    }

    [Test]
    public void BoundsBaseAnyAndReversedTime()
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, ".1", "2", ".1", ".15");
        constraints.GetFacts(".3").StepMismatch.Should().BeFalse();
        constraints.GetFacts("").ValueParses.Should().BeFalse();
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Time, "23:00", "01:00", "any", null);
        foreach (var value in new[] { "23:00", "23:30", "00:00", "01:00" })
        {
            var facts = constraints.GetFacts(value);
            facts.HasReversedRange.Should().BeTrue();
            facts.Underflow.Should().BeFalse();
            facts.Overflow.Should().BeFalse();
        }
        constraints.GetFacts("12:00").Underflow.Should().BeTrue();
        constraints.GetFacts("12:00").Overflow.Should().BeTrue();
        constraints.GetStep("23:00", 1, false).Status.Should().Be(HtmlInputStepStatus.NoAllowedStep);
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Time, "23:00", "01:00", "1", null);
        constraints.GetStep("23:00", 1, false).Status.Should().Be(HtmlInputStepStatus.Unchanged);
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, "7", null, "1", null);
        // Named input-stepdown-02 WPT/current-HTML discrepancy: zero substitution then direction guard.
        constraints.GetStep("", 1, true).Status.Should().Be(HtmlInputStepStatus.Unchanged);
    }

    [TestCase(HtmlInputType.Date, "1970-01-01", "1970-01-02", "1.5", "1970-01-02")]
    [TestCase(HtmlInputType.Date, "1970-01-01", "1970-01-01", ".5", "1970-01-01")]
    [TestCase(HtmlInputType.Month, "1970-01", "1970-01", ".5", "1970-01")]
    [TestCase(HtmlInputType.Week, "1970-W01", "1970-W01", ".5", "1970-W01")]
    [TestCase(HtmlInputType.Time, "00:00", "00:00", ".0005", "00:00")]
    [TestCase(HtmlInputType.Time, "00:00", "00:00:00.001", ".0015", "00:00:00.001")]
    public void FractionalTemporalLatticeProjectsOnce(object kind, string min, string value, string step, string expected)
    {
        var constraints = HtmlInputNumericConstraints.Create((HtmlInputType) kind, min, null, step, null);
        constraints.GetStep(value, 1, false).Value.Should().Be(expected);
    }

    [Test]
    public void LargeScaledStepRemainsExactAndFiniteBoundCanRescueOverflow()
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Time, null, null, "1e308", null);
        constraints.GetFacts("00:01").StepMismatch.Should().BeTrue();
        constraints.GetStep("00:01", 1, false).Status.Should().Be(HtmlInputStepStatus.Unchanged);
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, null, "1e308", "1e308", null);
        constraints.GetStep("1e308", 1, false).Value.Should().Be("1e+308");
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, "0.1", "0.2", "1", "0.5");
        // A parsed min always wins as base, so the min itself is a legal lattice point.
        constraints.GetStep("0.15", 1, false).Status.Should().Be(HtmlInputStepStatus.Unchanged);
    }

    [Test]
    public void TemporalPublicationFloorsExactCandidateBeforeBinary64Rounding()
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Time, null, null,
            ".0009999999999999999", "23:59:59.999");
        // Exact candidate is just below midnight, although P(candidate) is 86400000.
        constraints.GetStep("23:59:59.999", 1, false).Value.Should().Be("23:59:59.999");
    }

    [Test]
    public void BoundsNoCandidateAndPublishedMismatchRemainObservable()
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Range, null, ".1", "1", ".5");
        constraints.GetStep(".05", 0, false).Status.Should().Be(HtmlInputStepStatus.Unchanged);
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, "2", "1", "1", null);
        var facts = constraints.GetFacts("1.5");
        facts.Underflow.Should().BeTrue();
        facts.Overflow.Should().BeTrue();
        constraints.GetStep("1.5", 0, false).Status.Should().Be(HtmlInputStepStatus.Unchanged);
        constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, null, null, "3", null);
        var published = constraints.GetStep("9007199254740992", 0, false);
        published.Status.Should().Be(HtmlInputStepStatus.Write);
        constraints.GetFacts(published.Value!).StepMismatch.Should().BeTrue();
    }

    [Test]
    public void BoundedArithmeticCancellationAndCountsAreDeterministic()
    {
        var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Number, null, null, "5e-324", null);
        var value = HtmlInputNumberFormatter.FormatFinite(double.MaxValue);
        using var cancellation = new CancellationTokenSource();
        long units = 0;
        Action action = () => constraints.GetStep(value, int.MaxValue, false,
            count => { units = count; if (count == 4) cancellation.Cancel(); }, cancellation.Token);
        action.Should().Throw<OperationCanceledException>();
        units.Should().Be(4);
        constraints.GetStep(value, int.MaxValue, false, count => units = count, default).Status.Should().Be(HtmlInputStepStatus.Write);
        units.Should().Be(7); // Counts arithmetic phases, never the enormous grid index or Int32 count.
    }

    [TestCase("fr-FR")]
    [TestCase("tr-TR")]
    [TestCase("ar-SA")]
    public void InputAlgorithmsAreCultureIndependent(string cultureName)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            HtmlInputNumberSyntax.TryParseValue(".3", out var number).Should().BeTrue();
            HtmlInputNumberFormatter.FormatFinite(number).Should().Be("0.3");
            HtmlInputNumberSyntax.TryParseValue("0,3", out _).Should().BeFalse();
            var constraints = HtmlInputNumericConstraints.Create(HtmlInputType.Range, "5.3", "12", ".5", null);
            HtmlInputRangeValue.Sanitize("6.7", constraints).Should().Be("6.8");
            HtmlInputTemporalSyntax.FormatNumber(HtmlInputType.DateTimeLocal, 8640000000000001d)
                .Should().Be("275760-09-13T00:00:00.001");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }
}

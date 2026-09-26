#nullable enable
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionCycleTests
{
    [Test]
    public void TheRootCustomContextParticipatesInItsOwnCycle()
    {
        var input = SubstitutionFixture.Input("var(--a,red)");
        var work = new CssValueWork(default);
        var result = CssSubstitutionExecutor.Resolve(input,
            CssSubstitutionSnapshot.Create([CssSubstitutionBinding.Specified("--a", input, false)], work),
            CssEnvironmentSnapshot.Create([], work),
            new CssSubstitutionContext("--a", CssReferenceUse.CustomPropertyValue, true), work);
        result.Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void ActiveParticipantsCannotRescueTheirOwnCycleButAnOutsideDependentCan()
    {
        var a = SubstitutionFixture.Specified("--a", "var(--b,red)");
        var b = SubstitutionFixture.Specified("--b", "var(--a,blue)");
        SubstitutionFixture.Resolve("var(--a)", a, b).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--a,green)", a, b)).Should().Be("green");
        var outside = SubstitutionFixture.Specified("--outside", "var(--a,yellow)");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--outside)", a, b, outside)).Should().Be("yellow");
    }

    [Test]
    public void AnUnusedCyclicFallbackDoesNotPoisonASelectedValue()
    {
        var value = SubstitutionFixture.Specified("--value", "blue");
        var self = SubstitutionFixture.Specified("--self", "var(--value,var(--self))");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--self)", value, self)).Should().Be("blue");
        var selected = SubstitutionFixture.Specified("--self", "var(--missing,var(--self,red))");
        SubstitutionFixture.Resolve("var(--self)", selected).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void InheritedComputedAliasIsNeverReevaluatedAgainstChildOverrides()
    {
        var parent = SubstitutionFixture.Resolve("var(--base)", SubstitutionFixture.Specified("--base", "blue"));
        var inherited = CssSubstitutionBinding.Computed("--alias", parent.Value, false);
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--alias)", inherited,
            SubstitutionFixture.Specified("--base", "red"))).Should().Be("blue");
        var empty = CssSubstitutionBinding.Computed("--empty", SubstitutionFixture.Resolve("").Value, false);
        SubstitutionFixture.Resolve("var(--empty,red)", empty).Value.TokenCount.Should().Be(0);
    }
}

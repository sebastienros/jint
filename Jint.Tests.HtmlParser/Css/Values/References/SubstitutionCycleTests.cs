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

    [Test]
    public void InheritedSpecifiedAliasesResolveInTheirDefiningScope()
    {
        var work = new CssValueWork(default);
        var parent = CssSubstitutionSnapshot.Create([
            SubstitutionFixture.Specified("--base", "blue"),
            SubstitutionFixture.Specified("--alias", "var(--base)")], work);
        var child = CssSubstitutionSnapshot.CreateLayer([
            SubstitutionFixture.Specified("--base", "red")], parent, work);
        SubstitutionFixture.Identifier(Resolve("var(--alias)", child, work)).Should().Be("blue");
        SubstitutionFixture.Identifier(Resolve("var(--base)", child, work)).Should().Be("red");
    }

    [Test]
    public void SameNameAcrossScopesIsNeitherACycleNorASharedMemoEntry()
    {
        var work = new CssValueWork(default);
        var parent = CssSubstitutionSnapshot.Create([
            SubstitutionFixture.Specified("--a", "blue"),
            SubstitutionFixture.Specified("--b", "var(--a)")], work);
        var child = CssSubstitutionSnapshot.CreateLayer([
            SubstitutionFixture.Specified("--a", "var(--b)")], parent, work);
        SubstitutionFixture.Identifier(Resolve("var(--a)", child, work)).Should().Be("blue");

        child = CssSubstitutionSnapshot.CreateLayer([
            SubstitutionFixture.Specified("--a", "red")], parent, work);
        var result = Resolve("var(--a) var(--b)", child, work);
        result.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        result.Value.Components[0].Token.Text.Should().Be("red");
        result.Value.Components[2].Token.Text.Should().Be("blue");
    }

    [Test]
    public void ParentCyclesStayInvalidAfterAChildOverridesOneParticipant()
    {
        var work = new CssValueWork(default);
        var parent = CssSubstitutionSnapshot.Create([
            SubstitutionFixture.Specified("--a", "var(--b)"),
            SubstitutionFixture.Specified("--b", "var(--a)")], work);
        var child = CssSubstitutionSnapshot.CreateLayer([
            SubstitutionFixture.Specified("--a", "red")], parent, work);
        SubstitutionFixture.Identifier(Resolve("var(--b,green)", child, work)).Should().Be("green");
    }

    private static CssSubstitutionResult Resolve(string source, CssSubstitutionSnapshot snapshot,
        CssValueWork work) => CssSubstitutionExecutor.Resolve(SubstitutionFixture.Input(source), snapshot,
            CssEnvironmentSnapshot.Create([], work),
            new CssSubstitutionContext("width", CssReferenceUse.PropertyValue, true), work);
}

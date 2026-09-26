#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionSpreadTests
{
    [Test]
    public void SpreadIntroducesTheFirstCommaBeforeArgumentsAreDivided()
    {
        var args = SubstitutionFixture.Specified("--args", "--missing,blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(...var(--args))", args)).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(./**/../**/var(--args))", args)).Should().Be("blue");
        SubstitutionFixture.Resolve("var(... var(--args))", args).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [TestCase("var(...var(--args),{red;blue})")]
    [TestCase("var(...var(--args),{!})")]
    public void RawFallbackWrapperBecomesOrdinaryContentWhenEarlyHeaderIntroducesComma(string source)
    {
        var result = SubstitutionFixture.Resolve(source, SubstitutionFixture.Specified("--args", "--missing,blue"));
        result.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        result.Value.Components[0].Token.Text.Should().Be("blue");
        result.Value.Components[1].Token.Kind.Should().Be(CssTokenKind.Comma);
        result.Value.Components[2].Kind.Should().Be(CssComponentKind.SimpleBlock);
        result.Value.Components[2].OpeningDelimiter.Should().Be('{');
    }

    [Test]
    public void ASelectedWrapperIsRemovedAndExplicitSpreadRunsBeforeSelection()
    {
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--missing,{blue})")).Should().Be("blue");
        var self = SubstitutionFixture.Specified("--self", "var(--value,...var(--self))");
        SubstitutionFixture.Resolve("var(--self)", self, SubstitutionFixture.Specified("--value", "blue"))
            .Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        var nested = SubstitutionFixture.Resolve("var(--missing,foo(...var(--parts)))",
            SubstitutionFixture.Specified("--parts", "red,blue"));
        nested.Value.Components[0].FunctionName.Should().Be("foo");
        nested.Value.Components[0].Values[1].Token.Kind.Should().Be(CssTokenKind.Comma);
    }

    [TestCase("var(...var(--missing),red)")]
    [TestCase("var(--present,...var(--missing))")]
    public void EarlySpreadFailureInvalidatesTheInvocation(string source) =>
        SubstitutionFixture.Resolve(source, SubstitutionFixture.Specified("--present", "blue"))
            .Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);

    [TestCase("var(...var(--args),{red;blue})")]
    [TestCase("var(...var(--args),{red!important})")]
    public void NewlyExposedWrapperContentMustPassDeclarationGrammar(string source) =>
        SubstitutionFixture.Resolve(source, SubstitutionFixture.Specified("--args", "--missing"))
            .Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);

    [TestCase("var(...var(--args),{red;blue})")]
    [TestCase("var(...var(--args),{red!important})")]
    public void MalformedPostEarlyFallbackInvalidatesEvenAnUnusedBranch(string source) =>
        SubstitutionFixture.Resolve(source, SubstitutionFixture.Specified("--args", "--present"),
            SubstitutionFixture.Specified("--present", "blue")).Kind
            .Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);

    [Test]
    public void EmptyPostEarlyHeaderIsInvalidArgumentGrammar() =>
        SubstitutionFixture.Resolve("var(...var(--args),red)", SubstitutionFixture.Specified("--args", ""))
            .Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);

    [Test]
    public void NormalHeaderSubstitutionCannotIntroduceAnArgumentWrapper()
    {
        var name = SubstitutionFixture.Specified("--name", "{--target}");
        var target = SubstitutionFixture.Specified("--target", "blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(var(--name),red)", name, target))
            .Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var({var(--name)},red)",
            SubstitutionFixture.Specified("--name", "--target"), target)).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(...var(--name),red)", name, target))
            .Should().Be("blue");
    }

    [Test]
    public void SpreadOutsideAnArbitraryArgumentContextRemainsLiteralPeriods()
    {
        var value = SubstitutionFixture.Resolve("...var(--x)", SubstitutionFixture.Specified("--x", "blue"));
        value.Value.Components.Count.Should().Be(4);
        value.Value.Components[0].Token.Delimiter.Should().Be('.');
    }
}

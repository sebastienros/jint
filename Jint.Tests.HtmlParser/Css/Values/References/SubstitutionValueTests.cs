#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionValueTests
{
    [Test]
    public void LiteralAndSelectedValuesRetainTokenBoundaries()
    {
        var result = SubstitutionFixture.Resolve("var(--n)px", SubstitutionFixture.Specified("--n", "12"));
        result.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        result.Value.Components.Count.Should().Be(2);
        result.Value.Components[0].Token.Kind.Should().Be(CssTokenKind.Number);
        result.Value.Components[1].Token.Kind.Should().Be(CssTokenKind.Ident);
        CssPrimitiveParser.ParseNumericAtom(result.Value.Components, new CssValueWork(default)).IsMatch
            .Should().BeFalse();
    }

    [Test]
    public void EmptyIsSuccessfulAndMissingOrInvalidCanSelectEmptyFallback()
    {
        SubstitutionFixture.Resolve("var(--empty,red)", SubstitutionFixture.Specified("--empty", ""))
            .Value.TokenCount.Should().Be(0);
        SubstitutionFixture.Resolve("var(--missing,)").Value.TokenCount.Should().Be(0);
        SubstitutionFixture.Resolve("var(--invalid,)", CssSubstitutionBinding.Invalid("--invalid", false))
            .Value.TokenCount.Should().Be(0);
        SubstitutionFixture.Resolve("var(--missing)").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        SubstitutionFixture.Resolve("var(--invalid)", CssSubstitutionBinding.Invalid("--invalid", false))
            .Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void PendingPropagatesWithoutSelectingFallbackAndUnreachedBindingIsHarmless()
    {
        var pending = CssSubstitutionBinding.Pending("--registered", "registered-property");
        SubstitutionFixture.Resolve("var(--registered,red)", pending).PendingFeature.Should().Be("registered-property");
        SubstitutionFixture.Resolve("red", pending).Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        SubstitutionFixture.Resolve("var(--x,attr(data-x))", SubstitutionFixture.Specified("--x", "blue"))
            .PendingFeature.Should().Be("attr");
    }

    [Test]
    public void DynamicNamesAndDecodedCaseSensitiveKeysAreUsed()
    {
        var result = SubstitutionFixture.Resolve("var(var(--name),red)",
            SubstitutionFixture.Specified("--name", "--Abc"), SubstitutionFixture.Specified("--Abc", "blue"));
        SubstitutionFixture.Identifier(result).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--abc,red)",
            SubstitutionFixture.Specified("--Abc", "blue"))).Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--\\41 bc)",
            SubstitutionFixture.Specified("--Abc", "blue"))).Should().Be("blue");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(foo,red)")).Should().Be("red");
    }

    [Test]
    public void TaintUsesConsumerEligibilityAndCustomEvaluationUsesItsOwnContext()
    {
        var tainted = SubstitutionFixture.Specified("--tainted", "blue", true);
        var alias = SubstitutionFixture.Specified("--alias", "var(--tainted)", true);
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("var(--alias,red)", [tainted, alias], false))
            .Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("var(--alias,red)", [tainted, alias], true))
            .Should().Be("blue");
        var suppliedFinalTaint = SubstitutionFixture.Specified("--x", "var(--missing,blue)", true);
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("var(--x,red)", [suppliedFinalTaint], false))
            .Should().Be("red");
        // The alias's metadata is supplied by the cascade caller, independently of the branch taken.
        var untaintedAlias = SubstitutionFixture.Specified("--alias", "var(--tainted)");
        SubstitutionFixture.Identifier(SubstitutionFixture.ResolveWith("var(--alias)", [tainted, untaintedAlias], false))
            .Should().Be("blue");
    }

    [Test]
    public void InvalidSourceAndDefaultPayloadsAreProgrammerErrors()
    {
        Assert.Throws<ArgumentException>(() => SubstitutionFixture.Resolve("var()"));
        Assert.Throws<ArgumentException>(() => SubstitutionFixture.Resolve("var(--bad)",
            SubstitutionFixture.Specified("--bad", "red;blue")));
        Assert.Throws<InvalidOperationException>(() => _ = default(CssSubstitutionResult).Value);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssSubstitutionBinding).Input);
        Assert.Throws<ArgumentException>(() => new CssSubstitutionContext("Width", CssReferenceUse.PropertyValue, true));
        Assert.Throws<ArgumentException>(() => new CssSubstitutionContext("--x", CssReferenceUse.CustomPropertyValue, false));
        Assert.Throws<ArgumentException>(() => new CssSubstitutionContext("font-family", CssReferenceUse.DescriptorValue, true));
    }
}

internal static class SubstitutionFixture
{
    internal static CssReferenceInput Input(string source) => CssReferenceInput.Parse(source, null, default);
    internal static CssSubstitutionBinding Specified(string name, string source, bool tainted = false) =>
        CssSubstitutionBinding.Specified(name, Input(source), tainted);
    internal static CssSubstitutionResult Resolve(string source, params CssSubstitutionBinding[] bindings) =>
        ResolveWith(source, bindings, true);
    internal static CssSubstitutionResult ResolveWith(string source, CssSubstitutionBinding[] bindings,
        bool animatable, CssEnvironmentBinding[]? environment = null, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        return CssSubstitutionExecutor.Resolve(Input(source), CssSubstitutionSnapshot.Create(bindings, work),
            CssEnvironmentSnapshot.Create(environment ?? [], work),
            new CssSubstitutionContext("width", CssReferenceUse.PropertyValue, animatable), work);
    }

    internal static string Identifier(CssSubstitutionResult result)
    {
        result.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        var parsed = CssPrimitiveParser.ParseIdentifier(result.Value.Components, new CssValueWork(default));
        parsed.IsMatch.Should().BeTrue();
        return parsed.Value.Text;
    }
}

#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

public sealed class CustomPropertySerializationTests
{
    [TestCase("10px")]
    [TestCase(@"10\70 x")]
    [TestCase("+1e03PX")]
    [TestCase("MiXeD /*Hi*/ +1e03PX")]
    [TestCase("/*leading*/10px/*trailing*/")]
    [TestCase("/*comments only*/")]
    public void AuthoredLexicalSpellingIsNotCanonicalized(string source)
    {
        var value = SubstitutionFixture.Resolve(source).Value;
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(source);
    }

    [Test]
    public void SelectedReplacementsRetainCommentsWithoutRestoringInvocationText()
    {
        var value = SubstitutionFixture.Resolve("/*foo*/var(--a)/*bar*/",
            SubstitutionFixture.Specified("--a", "/*baz*/10px")).Value;
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("/*foo*//*baz*/10px/*bar*/");
        var nested = SubstitutionFixture.Resolve("calc(var(--a) + var(--missing,/*before*/2px/*after*/))",
            SubstitutionFixture.Specified("--a", "var(--b)"), SubstitutionFixture.Specified("--b", "10px")).Value;
        nested.SerializeCustomProperty(new CssValueWork(default)).Should().Be("calc(10px + /*before*/2px/*after*/)");
    }

    [Test]
    public void JoinsSeparateTokensWhileTypedProjectionAndOriginsStayUnchanged()
    {
        var value = SubstitutionFixture.Resolve("var(--n)px", SubstitutionFixture.Specified("--n", "10")).Value;
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("10/**/px");
        value.Components.Count.Should().Be(2);
        value.Components[0].Token.Kind.Should().Be(CssTokenKind.Number);
        value.Components[1].Token.Kind.Should().Be(CssTokenKind.Ident);
        value.SpellingLength.Should().Be(4);
        var origins = value.OriginsFor(new CssSourceSpan(0, 4));
        origins.Count.Should().Be(2);
        origins[0].ProjectionSpan.Start.Should().Be(0);
        origins[1].ProjectionSpan.Start.Should().Be(2);
        value.AsReferenceInput(new CssValueWork(default)).Source.Should().Be("10px");
        SubstitutionFixture.Resolve("a var(--empty)b", SubstitutionFixture.Specified("--empty", ""))
            .Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("a b");
        SubstitutionFixture.Resolve("var(--a)var(--b)", SubstitutionFixture.Specified("--a", "foo"),
            SubstitutionFixture.Specified("--b", "bar")).Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("foo/**/bar");
    }

    [TestCase("f('x", "f('x')")]
    [TestCase("/*comment", "/*comment*/")]
    [TestCase("var(--missing,/*comment", "/*comment*/")]
    public void C1RecoverySurvivesOnlyInTheSelectedLexicalPieces(string source, string expected)
    {
        SubstitutionFixture.Resolve(source).Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void SuccessfulEmptyAndGuaranteedInvalidRemainDifferent()
    {
        var empty = SubstitutionFixture.Resolve("var(--empty)", SubstitutionFixture.Specified("--empty", ""));
        empty.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
        empty.Value.TokenCount.Should().Be(0);
        empty.Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(" ");
        SubstitutionFixture.Resolve("var(--missing)").Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
    }

    [Test]
    public void CommentSeparatedSpreadAndWrapperFallbackRetainSelectedComments()
    {
        SubstitutionFixture.Resolve("var(./**/../**/var(--args))",
            SubstitutionFixture.Specified("--args", "--missing,/*keep*/10px"))
            .Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("/*keep*/10px");
        SubstitutionFixture.Resolve("var(--missing,{/*keep*/10px/*tail*/})")
            .Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("/*keep*/10px/*tail*/");
    }

    [TestCase("<var(--x)--", "!", "<!/**/--")]
    [TestCase("<!var(--x)", "--", "<!/**/--")]
    [TestCase("var(--x)>", "--", "--/**/>")]
    [TestCase("var(--x)>", "a--", "a-->")]
    public void JoinsCannotCreateHtmlCommentTokens(string source, string replacement, string expected) =>
        SubstitutionFixture.Resolve(source, SubstitutionFixture.Specified("--x", replacement))
            .Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(expected);

    [TestCase(@"\61", " ", true)]
    [TestCase(@"\061", "\t", true)]
    [TestCase(@"\000061", " ", true)]
    [TestCase(@"\000061", "\r\n", true)]
    [TestCase(@"\61", "\r\n", true)]
    [TestCase(@"\\61", " ", false)]
    [TestCase(@"\61 ", " ", false)]
    public void TerminalHexEscapesCannotConsumeAuthoredWhitespaceAtSubstitutionJoins(
        string replacement, string whitespace, bool needsSeparator)
    {
        var value = SubstitutionFixture.Resolve("var(--a)" + whitespace + "b",
            SubstitutionFixture.Specified("--a", replacement)).Value;
        var serialized = value.SerializeCustomProperty(new CssValueWork(default));
        serialized.Should().Be(replacement + (needsSeparator ? "/**/" : "") + whitespace + "b");
        var reparsed = CssReferenceInput.Parse(serialized, options: null).Components;
        reparsed.Count.Should().Be(value.Components.Count);
        for (var i = 0; i < reparsed.Count; i++)
        {
            reparsed[i].Kind.Should().Be(value.Components[i].Kind);
            reparsed[i].Token.Kind.Should().Be(value.Components[i].Token.Kind);
            reparsed[i].Token.Text.Should().Be(value.Components[i].Token.Text);
        }
        reparsed.Count.Should().Be(3);
        reparsed[1].Token.Kind.Should().Be(CssTokenKind.Whitespace);
    }

    [Test]
    public void CommentOnlyExpansionAndSerializationAreBounded()
    {
        var comment = "/*" + new string('x', 20000) + "*/";
        var bindings = new List<CssSubstitutionBinding> { SubstitutionFixture.Specified("--v0", comment) };
        for (var i = 1; i <= 8; i++) bindings.Add(SubstitutionFixture.Specified("--v" + i,
            "var(--v" + (i - 1) + ")var(--v" + (i - 1) + ")"));
        SubstitutionFixture.Resolve("var(--v8)", bindings.ToArray()).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        var value = SubstitutionFixture.Resolve("var(--v0)", bindings[0]).Value;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => value.SerializeCustomProperty(
            new CssValueWork(cancellation.Token, cancellation.Cancel)));
    }
}

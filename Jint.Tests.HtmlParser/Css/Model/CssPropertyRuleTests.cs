using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssPropertyRuleTests
{
    [TestCase("@property --x {}")]
    [TestCase("@property --x {syntax:'*'}")]
    [TestCase("@property --x {inherits:true}")]
    [TestCase("@property x {syntax:'*';inherits:true}")]
    [TestCase("@property -- {syntax:'*';inherits:true}")]
    [TestCase("@property --x, --y {syntax:'*';inherits:true}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:red}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:1em}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:calc(1em - 1em)}")]
    [TestCase("@property --x {syntax:'<color>';inherits:true;initial-value:currentColor}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:var(--y)}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:initial}")]
    [TestCase("@property --x {syntax:'<number>';inherits:yes;initial-value:0}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true!important;initial-value:0}")]
    [TestCase("@property --x {syntax:'<length>';inherits:true;initial-value:0!important}")]
    public void InvalidRegistrationsRecoverAndStrictInsertionIsAtomic(string source)
    {
        CssStyleSheet.Parse(source).Rules.Should().BeEmpty();
        var sheet = CssStyleSheet.Parse("div {}");
        var stamp = sheet.Stamp;
        Action insert = () => sheet.InsertRule(source, 0);
        insert.Should().Throw<DomException>().Which.Name.Should().Be("SyntaxError");
        sheet.Stamp.Should().Be(stamp);
        sheet.Rules.Should().ContainSingle();
    }

    [TestCase("")]
    [TestCase("* | <length>")]
    [TestCase("<length> +")]
    [TestCase("<length> || <color>")]
    [TestCase("<Length>")]
    [TestCase("<transform-list>+")]
    [TestCase("initial")]
    [TestCase("default")]
    [TestCase("foo bar")]
    public void InvalidSyntaxDefinitionsAreNotArbitraryValues(string syntax)
    {
        CssRegisteredSyntax.Parse(syntax, new(default)).Should().BeNull();
    }

    [Test]
    public void DescriptorRecoverySerializationAndDetachedIdentities()
    {
        var sheet = CssStyleSheet.Parse("""
            @media all {
              @property --X {
                ignored: 1;
                syntax: "<number>";
                syntax: broken;
                inherits: false;
                inherits: broken;
                initial-value: 2;
                initial-value: 4!important;
              }
            }
            @property --empty {syntax:"*";inherits:true}
            """);
        var group = (CssMediaRule) sheet.Rules[0];
        var rule = (CssPropertyRule) group.Rules[0];
        rule.Name.Should().Be("--X");
        rule.Syntax.Text.Should().Be("<number>");
        rule.Inherits.Should().BeFalse();
        rule.InitialValue(new(default)).Should().Be("2");
        rule.Type.Should().Be((CssRuleType) 0);
        rule.ParentRule.Should().BeSameAs(group);
        rule.ParentStyleSheet.Should().BeSameAs(sheet);
        rule.CssText.Should().Be("@property --X { syntax: \"<number>\"; inherits: false; initial-value: 2; }");
        ((CssPropertyRule) sheet.Rules[1]).Initial.Should().BeNull();
        var snapshot = sheet.SerializeWithRanges();
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
        foreach (var (item, range) in snapshot.Ranges)
            snapshot.Text[range.Start..range.End].Should().Be(item.CssText);
        group.DeleteRule(0);
        rule.ParentRule.Should().BeNull();
        rule.ParentStyleSheet.Should().BeNull();
        rule.Name.Should().Be("--X");
    }

    [TestCase("<length>", "1in")]
    [TestCase("<length>", "10vw")]
    [TestCase("<length-percentage>", "calc(1px + 2%)")]
    [TestCase("<percentage>", "50%")]
    [TestCase("<color>", "rgb(1 2 3 / 50%)")]
    [TestCase("<length># | auto", "1px, 2px")]
    [TestCase("<number>+", "1 2 3")]
    [TestCase("red | <color>", "red")]
    [TestCase("<string>", "'word'")]
    [TestCase("<custom-ident>", "Word")]
    [TestCase("<custom-ident>", "currentColor")]
    [TestCase("currentColor", "currentColor")]
    [TestCase("<angle>", "1turn")]
    [TestCase("<time>", "1ms")]
    [TestCase("<resolution>", "96dpi")]
    public void SupportedInitialValuesHaveTypedGrammar(string syntax, string initial)
    {
        CssStyleSheet.Parse($"@property --x {{syntax:\"{syntax}\";inherits:false;initial-value:{initial}}}")
            .Rules.Should().ContainSingle().Which.Should().BeOfType<CssPropertyRule>();
    }

    [Test]
    public void CancellationDoesNotPublishARegistration()
    {
        var sheet = CssStyleSheet.Parse("");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action insert = () => sheet.InsertRule("@property --x {syntax:'*';inherits:false}", 0,
            cancellationToken: cancellation.Token);
        insert.Should().Throw<OperationCanceledException>();
        sheet.Rules.Should().BeEmpty();
    }

    [Test]
    public void RegistrationDescriptorsAreInvalidInAStyleRuleContext()
    {
        var sheet = CssStyleSheet.Parse("div {@property --x {syntax:'<image>';inherits:false;initial-value:url(x)} color:red}");
        sheet.Rules.Should().ContainSingle();
        var style = (CssStyleRule) sheet.Rules[0];
        style.Rules.Should().BeEmpty();
        style.Style.GetPropertyValue("color", new(default)).Should().Be("red");
    }

    [TestCase("<image>", "url(x)")]
    [TestCase("<url>", "url(x)")]
    [TestCase("<transform-list>", "translateX(1px) rotate(1deg)")]
    public void RemainingTypedDependenciesAreExplicitFailures(string syntax, string value)
    {
        Action parse = () => CssStyleSheet.Parse($"@property --x {{syntax:'{syntax}';inherits:false;initial-value:{value}}}");
        parse.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("R6:property:" + syntax);
    }
}

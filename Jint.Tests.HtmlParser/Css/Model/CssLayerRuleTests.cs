using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssLayerRuleTests
{
    [Test]
    public void NamedAnonymousAndNestedLayersKeepIdentityNamesAndSerialization()
    {
        var sheet = CssStyleSheet.Parse("@layer a.b, C; @layer a { @layer b { div {color:red} } } @layer {}");
        var statement = (CssLayerStatementRule) sheet.Rules[0];
        statement.Names.Select(x => x.Text).Should().Equal("a.b", "C");
        var layer = (CssLayerBlockRule) sheet.Rules[1];
        layer.Name.Should().Be("a");
        ((CssLayerBlockRule) layer.Rules[0]).Name.Should().Be("b");
        ((CssLayerBlockRule) sheet.Rules[2]).Name.Should().BeEmpty();
        layer.Rules[0].ParentRule.Should().BeSameAs(layer);
        layer.Rules[0].ParentStyleSheet.Should().BeSameAs(sheet);
        layer.Type.Should().Be((CssRuleType) 0);
        var snapshot = sheet.SerializeWithRanges();
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
        foreach (var (rule, range) in snapshot.Ranges)
            snapshot.Text[range.Start..range.End].Should().Be(rule.CssText);
    }

    [TestCase("@layer;")]
    [TestCase("@layer a,;")]
    [TestCase("@layer a,b {}")]
    [TestCase("@layer a. b {}")]
    [TestCase("@layer a .b {}")]
    [TestCase("@layer initial {}")]
    [TestCase("@layer a.inherit {}")]
    [TestCase("@layer 'name' {}")]
    [TestCase("@layer a..b {}")]
    public void InvalidNamesRecoverAndStrictMutationIsAtomic(string source)
    {
        CssStyleSheet.Parse(source).Rules.Should().BeEmpty();
        var sheet = CssStyleSheet.Parse("@layer kept {}");
        var stamp = sheet.Stamp;
        Action insert = () => sheet.InsertRule(source, 0);
        insert.Should().Throw<DomException>().Which.Name.Should().Be("SyntaxError");
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void EscapedNamesRemainDistinctFromNestedNames()
    {
        var sheet = CssStyleSheet.Parse(@"@layer a\.b; @layer a.b {}");
        var single = ((CssLayerStatementRule) sheet.Rules[0]).Names[0];
        single.Segments.Should().Equal("a.b");
        single.Text.Should().Be(@"a\.b");
        ((CssLayerBlockRule) sheet.Rules[1]).LayerName!.Segments.Should().Equal("a", "b");
    }

    [Test]
    public void LayerStatementsPrecedeButNeverSplitTheImportPrologue()
    {
        var sheet = CssStyleSheet.Parse("@layer a,b; @import 'one'; @layer c; @import 'ignored';");
        sheet.Rules.Should().HaveCount(3);
        var stamp = sheet.Stamp;
        Action late = () => sheet.InsertRule("@import 'two';", 3);
        late.Should().Throw<DomException>().Which.Name.Should().Be("HierarchyRequestError");
        sheet.Stamp.Should().Be(stamp);
        sheet.InsertRule("@import 'two';", 2);
        Action split = () => sheet.InsertRule("@layer split;", 2);
        split.Should().Throw<DomException>().Which.Name.Should().Be("HierarchyRequestError");
        sheet.InsertRule("@layer first;", 0);
    }

    [Test]
    public void LiveChildrenCancellationAndDetachmentPreserveOwnership()
    {
        var sheet = CssStyleSheet.Parse("@layer a {div {color:red}}");
        var layer = (CssLayerBlockRule) sheet.Rules[0];
        var children = layer.Rules;
        var stamp = sheet.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action cancelled = () => layer.InsertRule("span {}", 1, cancellationToken: cancellation.Token);
        cancelled.Should().Throw<OperationCanceledException>();
        sheet.Stamp.Should().Be(stamp);
        layer.InsertRule("@layer nested {}", 1);
        children.Should().HaveCount(2);
        sheet.Stamp.Should().NotBe(stamp);
        sheet.DeleteRule(0);
        stamp = sheet.Stamp;
        layer.DeleteRule(0);
        sheet.Stamp.Should().Be(stamp);
        layer.ParentStyleSheet.Should().BeNull();
    }
}

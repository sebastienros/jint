#nullable enable
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssBoxDeclarationTests
{
    [TestCase("margin", "1px", "1px", "1px", "1px", "1px", "1px")]
    [TestCase("padding", "1px 2px", "1px", "2px", "1px", "2px", "1px 2px")]
    [TestCase("margin", "1px 2px 3px", "1px", "2px", "3px", "2px", "1px 2px 3px")]
    [TestCase("padding", "1px 2px 3px 4px", "1px", "2px", "3px", "4px", "1px 2px 3px 4px")]
    [TestCase("margin", "1px 2px 1px 2px", "1px", "2px", "1px", "2px", "1px 2px")]
    public void ExpandsCompressesAndRemovesInPhysicalSideOrder(string name, string source,
        string top, string right, string bottom, string left, string compressed)
    {
        var block = CssDeclarationBlock.Parse(name + ":" + source + "!important");
        block.GetPropertyValue(name + "-top").Should().Be(top);
        block.GetPropertyValue(name + "-right").Should().Be(right);
        block.GetPropertyValue(name + "-bottom").Should().Be(bottom);
        block.GetPropertyValue(name + "-left").Should().Be(left);
        block.GetDeclaration(0).Value.Should().Be(top);
        block.GetPropertyValue(name).Should().Be(compressed);
        block.GetPropertyPriority(name).Should().Be("important");
        block.CssText.Should().Be(name + ": " + compressed + " !important;");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        block.RemoveProperty(name).Should().Be(compressed);
        block.Count.Should().Be(0);
    }


    [Test]
    public void ImportanceAndSpecifiedOrderStayAtTheSharedDeclarationBoundary()
    {
        var block = CssDeclarationBlock.Parse("padding:1px!important;opacity:.5;padding-top:2px;padding-right:3px!important");
        Enumerable.Range(0, block.Count).Select(i => block.GetDeclaration(i).Name)
            .Should().Equal("padding-top", "padding-bottom", "padding-left", "opacity", "padding-right");
        block.GetPropertyValue("padding-top").Should().Be("1px");
        block.GetPropertyValue("padding").Should().Be("1px 3px 1px 1px");
        block.SetProperty("padding-bottom", "4px");
        block.GetPropertyValue("padding").Should().BeEmpty();
        block.GetPropertyPriority("padding").Should().BeEmpty();
        block.RemoveProperty("padding").Should().BeEmpty();
        block.Count.Should().Be(1);
        block.GetDeclaration(0).Name.Should().Be("opacity");
    }

    [Test]
    public void CancellationAtEveryStagingCheckpointPreservesTheOldDeclaration()
    {
        const string source = "padding:1px 2px 3px 4px!important;opacity:.5";
        var probe = CssDeclarationBlock.Parse(source);
        var retained = probe.CssText;
        var checks = 0;
        probe.SetProperty("padding", "5px 6px 7px 8px", null, null, new CssValueWork(default, () => checks++));
        checks.Should().BeGreaterThan(0);
        for (var cancelAt = 1; cancelAt <= checks; cancelAt++)
        {
            var block = CssDeclarationBlock.Parse(source);
            block.CssText.Should().Be(retained);
            var stamp = block.Stamp;
            var original = block.GetDeclaration(0);
            using var cancellation = new CancellationTokenSource();
            var reached = 0;
            var work = new CssValueWork(cancellation.Token, () => { if (++reached == cancelAt) cancellation.Cancel(); });
            Assert.Throws<OperationCanceledException>(() => block.SetProperty("padding", "5px 6px 7px 8px", null, null, work));
            block.CssText.Should().Be(retained);
            block.Stamp.Should().Be(stamp);
            block.GetDeclaration(0).Should().BeSameAs(original);
        }
    }


}

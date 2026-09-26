using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssDeclarationResolutionTests
{
    private static CssDeclarationBlock Syntax(string text) => CssDeclarationBlock.ParseUnresolved(text,
        CssDeclarationContext.Style, null, new CssValueWork(default), default);

    [Test]
    public void TargetReadsAndSettersLeaveUnrelatedPendingSyntaxUnmaterialized()
    {
        var block = Syntax("background:red; display:inline");
        var work = new CssValueWork(default);
        var stamp = block.Stamp;
        block.ResolveProperty("display", work)!.Value.Text.Should().Be("inline");
        block.Stamp.Should().Be(stamp);
        block.SetProperty("display", "block");
        block.ResolveProperty("display", work)!.Value.Text.Should().Be("block");
        block.SerializeSource(work).Should().Contain("background: red;");
        Assert.Throws<CssIncompleteGrammarException>(() => block.ResolveAll(work))!.PropertyName.Should().Be("background");
        Assert.Throws<CssIncompleteGrammarException>(() => block.ResolveProperty("background-color", work))!.PropertyName.Should().Be("background");
    }

    [TestCase("text-wrap:balance", "text-wrap-mode", "text-wrap")]
    [TestCase("border:solid", "border-image-source", "border")]
    [TestCase("font:12px serif", "font-kerning", "font")]
    [TestCase("word-wrap:break-word", "overflow-wrap", "overflow-wrap")]
    [TestCase("all:initial", "display", "all")]
    public void CorrelationIncludesShorthandsResetEffectsAndAliases(string text, string property, string expectedFailure)
    {
        var block = Syntax(text);
        Assert.Throws<CssIncompleteGrammarException>(() => block.ResolveProperty(property, new CssValueWork(default)))!
            .PropertyName.Should().Be(expectedFailure);
    }

    [Test]
    public void AllDoesNotCorrelateDirectionUnicodeBidiOrCustomProperties()
    {
        var block = Syntax("all:initial;direction:rtl;--x:red");
        var work = new CssValueWork(default);
        block.ResolveProperty("direction", work)!.Value.Text.Should().Be("rtl");
        block.ResolveProperty("unicode-bidi", work).Should().BeNull();
        block.ResolveProperty("--x", work)!.Value.Text.Should().Be("red");
    }

    [Test]
    public void DuplicateFallbackImportanceAndSourceOrderSurviveLazyResolution()
    {
        var block = Syntax("display:block;display:invalid;opacity:.1 !important;opacity:.2;visibility:hidden;visibility:visible");
        var work = new CssValueWork(default);
        block.ResolveProperty("display", work)!.Value.Text.Should().Be("block");
        block.ResolveProperty("opacity", work)!.Value.Text.Should().Be("0.1");
        block.ResolveProperty("visibility", work)!.Value.Text.Should().Be("visible");
        var entries = block.ResolveAll(work);
        entries.Select(entry => entry.Name).Should().Equal("display", "opacity", "visibility");
        var stamp = block.Stamp;
        block.ResolveAll(work).Should().BeSameAs(entries);
        block.ResolveProperty("display", work);
        block.Stamp.Should().Be(stamp);
    }

    [Test]
    public void EditingOneShorthandLonghandPreservesTheOtherAndRepeatedRemovalIsANoop()
    {
        var block = Syntax("background:red; overflow:hidden scroll !important");
        block.SetProperty("overflow-x", "visible");
        block.GetPropertyValue("overflow-x").Should().Be("visible");
        block.GetPropertyPriority("overflow-x").Should().BeEmpty();
        block.GetPropertyValue("overflow-y").Should().Be("scroll");
        block.GetPropertyPriority("overflow-y").Should().Be("important");
        block.RemoveProperty("overflow-x").Should().Be("visible");
        var stamp = block.Stamp;
        block.RemoveProperty("overflow-x").Should().BeEmpty();
        block.Stamp.Should().Be(stamp);
        block.SerializeSource(new CssValueWork(default)).Should().Contain("background: red;");
    }

    [Test]
    public void SettersKeepTheWinnersExistingPositionsAcrossDuplicateAndShorthandSources()
    {
        var block = Syntax("display:inline; opacity:.5; display:block; overflow-y:scroll; visibility:hidden; overflow-x:hidden");
        block.SetProperty("display", "none");
        block.SetProperty("overflow", "auto");
        block.ResolveAll(new CssValueWork(default)).Select(entry => entry.Name)
            .Should().Equal("opacity", "display", "overflow-y", "visibility", "overflow-x");
    }

    [Test]
    public void GuardedMaterializationSharesTheInvocationPollingRemainder()
    {
        var checks = 0;
        var work = new CssValueWork(default, () => checks++);
        work.Charge(4000);
        var guarded = CssValueWork.Guard(work, work.CheckCancellation);
        guarded.Charge(96);
        checks.Should().Be(1);
        work.Charge(4096);
        checks.Should().Be(2);
    }

    [Test]
    public void CancellationAndHostMutationCannotPublishAnObsoleteResolutionCache()
    {
        var block = Syntax("display:block;" + string.Join(';', Enumerable.Range(0, 20).Select(i => "--" + new string('x', 2000) + i + ":red")));
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++calls == 5) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => block.ResolveProperty("display", work));
        block.Stamp.Should().Be(stamp);
        block.ResolveProperty("display", new CssValueWork(default))!.Value.Text.Should().Be("block");
        var mutated = false;
        var reentrant = new CssValueWork(default, () =>
        {
            if (mutated) return;
            mutated = true;
            block.SetProperty("display", "none");
        });
        Assert.Throws<InvalidOperationException>(() => block.ResolveProperty("display", reentrant));
        block.ResolveProperty("display", new CssValueWork(default))!.Value.Text.Should().Be("none");
    }
}

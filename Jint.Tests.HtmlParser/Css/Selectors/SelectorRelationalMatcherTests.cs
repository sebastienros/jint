#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorRelationalMatcherTests
{
    private static CompiledSelector Parse(string source, SelectorParseContext? context = null)
        => SelectorCompiler.Compile(source, context, default);

    [Test]
    public void LogicalFunctionsMatchListsAndKeepStaticFunctionSpecificity()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        var target = document.CreateElement("p");
        document.AppendChild(root);
        root.AppendChild(target);
        target.SetAttribute("class", "yes");

        SelectorMatcher.Matches(Parse("p:is(.yes, #absent)"), target).Should().BeTrue();
        SelectorMatcher.Matches(Parse("p:matches(.yes)"), target).Should().BeTrue();
        SelectorMatcher.Matches(Parse("p:where(.yes)"), target).Should().BeTrue();
        SelectorMatcher.Matches(Parse("p:not(.no, #absent)"), target).Should().BeTrue();
        SelectorMatcher.Matches(Parse("p:not(.yes, #absent)"), target).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":is(:bogus)"), target).Should().BeFalse();
        SelectorMatcher.Matches(Parse(":is(:where(.no), :not(.no), :bogus)"), target)
            .Should().BeTrue();
        SelectorMatcher.TryMatch(Parse("p:where(.yes), p:is(.yes, #absent)"), target,
            out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 1));

        // WPT dom/nodes/selectors.js, ":not(*)" expects no matches;
        // vendored under Jint.Tests/Wpt/Vendor/wpt-LICENSE.md (BSD 3-Clause).
        SelectorMatcher.QuerySelectorAll(Parse(":not(*)"), document).Should().BeEmpty();
    }

    [Test]
    public void LogicalSubjectImplicitUniversalSkipsOnlyItsOwnDefaultNamespace()
    {
        var document = Document.CreateXml();
        var parent = document.CreateElementNS("urn:b", "parent");
        var child = document.CreateElementNS("urn:b", "child");
        document.AppendChild(parent);
        parent.AppendChild(child);
        parent.SetAttribute("class", "a");
        child.SetAttribute("class", "b x");
        var context = new SelectorParseContext(new[]
        {
            new KeyValuePair<string, string>("", "urn:a")
        });

        SelectorMatcher.Matches(Parse("*|*:is(.x)", context), child).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|*:where(.x)", context), child).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|*:not(.missing)", context), child).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|*:is(*.x)", context), child).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|*:is(.a > .b)", context), child).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|*:is(*|*.a > .b)", context), child).Should().BeTrue();
    }

    [Test]
    public void RelativeHasUsesDescendantChildAndFollowingSiblingAnchors()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var anchor = document.CreateElement("article");
        var direct = document.CreateElement("section");
        var nested = document.CreateElement("b");
        var adjacent = document.CreateElement("aside");
        var adjacentChild = document.CreateElement("em");
        var following = document.CreateElement("footer");
        document.AppendChild(root);
        root.AppendChild(anchor);
        anchor.AppendChild(direct);
        direct.AppendChild(nested);
        root.AppendChild(document.CreateComment("between"));
        root.AppendChild(adjacent);
        adjacent.AppendChild(adjacentChild);
        root.AppendChild(following);

        SelectorMatcher.Matches(Parse("article:has(section)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse("article:has(> section > b)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse("article:has(> b)"), anchor).Should().BeFalse();
        SelectorMatcher.Matches(Parse("article:has(+ aside > em)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse("article:has(+ footer)"), anchor).Should().BeFalse();
        SelectorMatcher.Matches(Parse("article:has(~ footer)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse("aside:has(~ footer)"), adjacent).Should().BeTrue();
        SelectorMatcher.Matches(Parse("footer:has(~ article)"), following).Should().BeFalse();
        SelectorMatcher.Matches(Parse("article:has(:is(> b, section))"), anchor).Should().BeTrue();
        SelectorMatcher.QuerySelectorAll(Parse("article:has(> section > b)"), root)
            .Should().Equal(anchor);
        direct.RemoveChild(nested);
        SelectorMatcher.Matches(Parse("article:has(> section > b)"), anchor).Should().BeFalse();
        root.RemoveChild(following);
        SelectorMatcher.Matches(Parse("article:has(~ footer)"), anchor).Should().BeFalse();
    }

    [Test]
    public void RelativeHasBacktracksAfterALeftmostCompoundFailsItsAnchor()
    {
        var document = Document.CreateHtml();
        var anchor = document.CreateElement("anchor");
        var outer = document.CreateElement("a");
        var inner = document.CreateElement("a");
        var subject = document.CreateElement("b");
        document.AppendChild(anchor);
        anchor.AppendChild(outer);
        outer.AppendChild(inner);
        inner.AppendChild(subject);

        SelectorMatcher.Matches(Parse(":has(> a b)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":has(> a > b)"), anchor).Should().BeFalse();
    }

    [Test]
    public void LeadingAdjacentHasCanMatchALaterSiblingSubject()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var anchor = document.CreateElement("anchor");
        var aside = document.CreateElement("aside");
        var footer = document.CreateElement("footer");
        document.AppendChild(root);
        root.AppendChild(anchor);
        root.AppendChild(aside);
        root.AppendChild(footer);

        SelectorMatcher.Matches(Parse(":has(+ aside + footer)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":has(+ aside ~ footer)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":has(~ aside + footer)"), anchor).Should().BeTrue();
        SelectorMatcher.Matches(Parse(":has(+ footer)"), anchor).Should().BeFalse();
    }

    [Test]
    public void FilteredNthCountsOnlyMatchingSiblingsAndRequiresSubjectToMatch()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var items = new Element[5];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = document.CreateElement("item");
            items[index].SetAttribute("class", index % 2 == 0 ? "x" : "y");
            root.AppendChild(items[index]);
        }
        SelectorMatcher.Matches(Parse("item:nth-child(2 of .x)"), items[2]).Should().BeTrue();
        SelectorMatcher.Matches(Parse("item:nth-last-child(2 of .x)"), items[2]).Should().BeTrue();
        SelectorMatcher.Matches(Parse("item:nth-child(1 of .x)"), items[1]).Should().BeFalse();
        SelectorMatcher.Matches(Parse("item:nth-child(2 of .x, .y)"), items[1]).Should().BeTrue();
        SelectorMatcher.Matches(Parse("item:nth-child(2 of :is(.x, .y))"), items[1]).Should().BeTrue();
        items[0].SetAttribute("class", "y");
        SelectorMatcher.Matches(Parse("item:nth-child(2 of .x)"), items[4]).Should().BeTrue();
        SelectorMatcher.Matches(Parse("item:nth-child(2 of .x)"), items[2]).Should().BeFalse();
        SelectorMatcher.TryMatch(Parse("item:nth-child(2 of #absent, .x)"), items[4],
            out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 1, 1));
    }

    [Test]
    public void LogicalScopeCanAnchorAFeaturelessFragment()
    {
        var document = Document.CreateHtml();
        var fragment = document.CreateDocumentFragment();
        var child = document.CreateElement("child");
        fragment.AppendChild(child);
        SelectorMatcher.QuerySelectorAll(Parse(":is(:scope) > child"), fragment).Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":where(:scope) > child"), fragment).Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":not(.x) > child"), fragment).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse(":scope:has(> child) > child"), fragment)
            .Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":has(> child):scope > child"), fragment)
            .Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":is(:scope:has(> child)) > child"), fragment)
            .Should().Equal(child);
        SelectorMatcher.QuerySelectorAll(Parse(":has(> child) > child"), fragment).Should().BeEmpty();
        SelectorMatcher.QuerySelectorAll(Parse(":has(> child):has(> child) > child"), fragment)
            .Should().BeEmpty();
    }

    [Test]
    public void DeepNestedLogicalFunctionsUseEvaluationFrames()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var source = ".x";
        for (var index = 0; index < 512; index++) source = $":is({source})";
        var program = Parse(source);
        SelectorMatcher.Matches(program, target).Should().BeFalse();
        target.SetAttribute("class", "x");
        SelectorMatcher.Matches(program, target).Should().BeTrue();
    }

    [Test]
    public void UnsuccessfulRelativeScanSharesCancellationCounter()
    {
        var document = Document.CreateHtml();
        var anchor = document.CreateElement("root");
        document.AppendChild(anchor);
        for (var index = 0; index < 1024; index++) anchor.AppendChild(document.CreateElement("item"));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(":has(.absent)"), anchor, null, () =>
            {
                if (++checkpoints == 2) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(2);
    }

    [Test]
    public void SingleCompoundChildHasDoesNotScanGrandchildren()
    {
        var document = Document.CreateHtml();
        var anchor = document.CreateElement("root");
        var current = document.CreateElement("item");
        document.AppendChild(anchor);
        anchor.AppendChild(current);
        for (var index = 0; index < 1024; index++)
        {
            var child = document.CreateElement("item");
            current.AppendChild(child);
            current = child;
        }
        var checkpoints = 0;
        SelectorMatcher.Matches(Parse(":has(> .missing)"), anchor, null,
            () => checkpoints++, default).Should().BeFalse();
        checkpoints.Should().Be(0);
    }

    [Test]
    public void SingleCompoundSiblingHasDoesNotScanSiblingDescendants()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var anchor = document.CreateElement("anchor");
        var sibling = document.CreateElement("sibling");
        document.AppendChild(root);
        root.AppendChild(anchor);
        root.AppendChild(sibling);
        var current = sibling;
        for (var index = 0; index < 1024; index++)
        {
            var child = document.CreateElement("item");
            current.AppendChild(child);
            current = child;
        }
        var checkpoints = 0;
        SelectorMatcher.Matches(Parse(":has(+ .missing)"), anchor, null,
            () => checkpoints++, default).Should().BeFalse();
        checkpoints.Should().Be(0);
    }

    [Test]
    public void FilteredNthSiblingScanSharesCancellationCounter()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        for (var index = 0; index < 1024; index++) root.AppendChild(document.CreateElement("item"));
        var last = document.CreateElement("item");
        last.SetAttribute("class", "missing");
        root.AppendChild(last);
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(":nth-child(2 of .missing)"), last, null, () =>
            {
                if (++checkpoints == 2) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(2);
    }

    [Test]
    public void UnsuccessfulRelativeBacktrackingChecksCancellation()
    {
        var document = Document.CreateHtml();
        var anchor = document.CreateElement("anchor");
        document.AppendChild(anchor);
        var current = anchor;
        for (var index = 0; index < 24; index++)
        {
            var child = document.CreateElement("a");
            current.AppendChild(child);
            current = child;
        }
        current.AppendChild(document.CreateElement("b"));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(":has(> x a a b)"), anchor, null, () =>
            {
                if (++checkpoints == 1) source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(1);
    }
}

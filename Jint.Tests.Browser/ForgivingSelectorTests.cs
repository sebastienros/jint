using AngleSharp;
using AngleSharp.Css.Parser;
using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>https://drafts.csswg.org/selectors/#forgiving-selector: invalid branches are discarded.</summary>
public sealed class ForgivingSelectorTests
{
    [TestCase(":is(div:unknown, #target)")]
    [TestCase(":where(|123, #target)")]
    [TestCase(":is(ns|div, #target)")]
    [TestCase(":is([ns|attr], #target)")]
    [TestCase(":is(> div, #target)")]
    [TestCase(":is(:not(div:unknown), #target)")]
    [TestCase(":is(:has(div:unknown), #target)")]
    [TestCase(":is(:where(|123, #target), :unknown)")]
    [TestCase(":not(:is(div:unknown, #other))")]
    [TestCase(@":\69 s(div:unknown, #target)")]
    [TestCase(@":wh\65 re(ns|div, #target)")]
    [TestCase(":is(/* comma , */ :unknown, #target)")]
    [TestCase(":is([title=',)([]'], :unknown)")]
    [TestCase(@":is(.a\,b, :unknown)")]
    [TestCase(":is(::before, #target)")]
    [TestCase(":is(:unknown, #target")]
    [TestCase(":is(, #target,)")]
    [TestCase(":is(p], #target)")]
    [TestCase(":is({bad,bad}, #target)")]
    [TestCase(":is([title=\"bad\n], #target)")]
    [TestCase(":is([title=\"bad\r], #target)")]
    [TestCase(":is([title=\"bad\r\n], #target)")]
    [TestCase(":is([title=\"bad\f], #target)")]
    [TestCase(":is(#target), ::picker(select)")]
    [TestCase(":is(#target), :/**/:picker(select)")]
    [TestCase(":is(#target), :/**/:/**/picker(select)")]
    [TestCase(":is(::picker(select), #target)")]
    [TestCase(":/**/is(|123, #target)")]
    [TestCase(":is(:has(::before), #target)")]
    [TestCase(":is(:has(:has(p)), #target)")]
    [TestCase(":is(:nth-child(1 of ::before), #target)")]
    [TestCase(":is(:not(#other), :unknown)")]
    [TestCase(":is(:is(#target, :unknown), :where(ns|div))")]
    public void InvalidBranchesAreDiscardedAcrossDomEntryPoints(string selector)
    {
        using var fixture = DomTestFixture.Create("<div id='root'><div id='other'></div><p id='target' class='a,b' title=',)([]'></p></div>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("""
            (() => {
                const root = document.getElementById('root'), target = document.getElementById('target');
                return [root.querySelector(selector) === target,
                    Array.from(root.querySelectorAll(selector)).map(e => e.id).join(',') === 'target',
                    target.matches(selector), target.webkitMatchesSelector(selector), target.closest(selector) === target,
                    !document.getElementById('other').matches(selector)].join('/');
            })()
            """).Should().Be("true/true/true/true/true/true");
    }

    [TestCase(":is(:unknown)")]
    [TestCase(":where(ns|div, > div, |123)")]
    [TestCase(":is()")]
    [TestCase(":where(,,)")]
    [TestCase("::picker(select):is(p)")]
    public void AllInvalidAndEmptyListsMatchNothing(string selector)
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("[document.querySelectorAll(selector).length, document.body.matches(selector)].join('/')")
            .Should().Be("0/false");
    }

    [TestCase(":not(:is(p), :unknown)")]
    [TestCase(":has(:is(p), :unknown)")]
    [TestCase(":not(:is(p), ns|div)")]
    [TestCase(":is(p), ns|div")]
    [TestCase(":is(p), > div")]
    [TestCase(":is(p))")]
    [TestCase(":is(p)]")]
    [TestCase(":has(:is(p), ::before)")]
    [TestCase(":has(:is(p), :has(p))")]
    [TestCase(":nth-child(1 of :is(p), ::before)")]
    public void InvalidStrictOrEnclosingSyntaxStillThrows(string selector)
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("try { document.querySelector(selector); 'accepted'; } catch (e) { e.name; }")
            .Should().Be("SyntaxError");
    }

    [Test]
    public void ForgivingBranchesRemainForgivingBeyondSixtyFourLevels()
    {
        using var fixture = DomTestFixture.Create("<p id='target'></p>");
        var selector = "#target";
        for (var i = 0; i < 80; i++)
        {
            selector = ":is(|123," + selector + ")";
        }
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("document.querySelector(selector)?.id").Should().Be("target");
    }

    [TestCase(":has(> :is(|123, #target))")]
    [TestCase(":has(:is(:has(p), #target))")]
    public void StrictRelativeSelectorsRetainValidForgivingChildren(string selector)
    {
        using var fixture = DomTestFixture.Create("<div id='root'><p id='target'></p></div>");
        fixture.Engine.SetValue("selector", selector);
        fixture.Text("document.getElementById('root').matches(selector) ? 'yes' : 'no'").Should().Be("yes");
    }


    [TestCase(@":is(/*keep*/ |d\69 v, .a\,b)")]
    [TestCase(":is([title='a\\\nb'])")]
    public void NativeValidBranchSourceAndSpecificityArePreserved(string selector)
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        var normalized = DomForgivingSelectors.Normalize(fixture.Document, selector);
        normalized.Should().Be(selector);
        var parser = fixture.Document.Context.GetService<ICssSelectorParser>()!;
        parser.ParseSelector(normalized)!.Specificity.Should().Be(parser.ParseSelector(selector)!.Specificity);
        // This exercises branch filtering directly. DOM entry points normalize string continuations
        // in DomSelectorText first; SelectorStringContinuationTests covers that value correction.
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void EscapedNewlinesInStringsDoNotInventFunctionBoundaries(string newline)
    {
        using var fixture = DomTestFixture.Create("<p></p>");
        var selector = "[title='a\\" + newline + ":is(p),b']";
        DomForgivingSelectors.Normalize(fixture.Document, selector).Should().Be(selector);
    }

}

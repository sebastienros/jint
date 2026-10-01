#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectInsertionBatchTests
{
    // WPT html/semantics/forms/the-select-element/inserted-or-removed.html,
    // source commit 2e8c1f1e6181b3b5d5b7fd6e417bf60c921a5ff4; current HTML §4.10.7.
    [TestCase("optgroup")]
    [TestCase("div")]
    public void DetachedSubtreePreservesEachPreselectedEntrantUntilItsInsertion(string name)
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var container = document.CreateElement(name);
        var a = document.CreateElement("option"); var b = document.CreateElement("option");
        a.SetAttribute("selected", ""); b.SetAttribute("selected", "");
        container.AppendChild(a); container.AppendChild(b);
        select.AppendChild(container);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(1);
        a.GetHtmlState()!.Option!.Selected.Should().BeFalse();
        b.GetHtmlState()!.Option!.Selected.Should().BeTrue();
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
    }
    [Test]
    public void BatchPreservesDirtySelectednessRatherThanReconstructingFromAttributes()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var wrapper = document.CreateElement("div");
        var a = document.CreateElement("option"); var b = document.CreateElement("option");
        a.SetAttribute("selected", ""); a.GetHtmlState()!.Option!.SetSelected(false, default);
        b.GetHtmlState()!.Option!.SetSelected(true, default);
        wrapper.AppendChild(a); wrapper.AppendChild(b);
        select.AppendChild(wrapper);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(1);
        a.GetHtmlState()!.Option!.DefaultSelected.Should().BeTrue();
        a.GetHtmlState()!.Option!.Selected.Should().BeFalse();
        b.GetHtmlState()!.Option!.DefaultSelected.Should().BeFalse();
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
    }
    [Test]
    public void IndividuallyInsertedSelectedOptionWinsEvenBeforeExistingSelectedOption()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var old = document.CreateElement("option"); old.SetAttribute("selected", ""); select.AppendChild(old);
        var entrant = document.CreateElement("option"); entrant.SetAttribute("selected", ""); select.InsertBefore(entrant, old);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
        entrant.GetHtmlState()!.Option!.Selected.Should().BeTrue(); old.GetHtmlState()!.Option!.Selected.Should().BeFalse();
    }
    [Test]
    public void FragmentDrainingAndWrapperMovesPreserveLiveMembershipAtEachBoundary()
    {
        var document = Document.CreateHtml(); var a = document.CreateElement("select"); var b = document.CreateElement("select");
        var fragment = document.CreateDocumentFragment();
        for (var i = 0; i < 3; i++)
        {
            var option = document.CreateElement("option"); option.SetAttribute("selected", ""); fragment.AppendChild(option);
        }
        a.AppendChild(fragment); a.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(2);
        var wrapper = document.CreateElement("div"); wrapper.AppendChild(a.LastChild!); b.AppendChild(wrapper);
        a.GetHtmlState()!.Select!.Options.Count.Should().Be(2);
        a.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
        b.GetHtmlState()!.Select!.Options.Count.Should().Be(1);
        b.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
    }
    [Test]
    public void DisabledParserAppendWorkRemainsLinearAndDisabledEditsInvalidateFallback()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < 5000; i++)
        {
            var option = document.CreateElement("option"); option.SetAttribute("disabled", ""); select.AppendParsedChild(option);
        }
        probe.Units.Should().BeLessThan(40000);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(-1);
        ((Element) select.FirstChild!).RemoveAttribute("disabled");
        select.AppendParsedChild(document.CreateElement("option"));
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
    }
}

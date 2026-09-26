#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectLifetimeTests
{
    [Test]
    [NonParallelizable]
    public void WarmLiveSelectedViewDoesNotRetainRemovedOptionsWithoutAnotherRead()
    {
        var (document, select, options, selected, weak) = RemoveOption();
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        weak.TryGetTarget(out _).Should().BeFalse();
        GC.KeepAlive(document); GC.KeepAlive(select); GC.KeepAlive(options); GC.KeepAlive(selected);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Document Document, Element Select, HtmlSelectOptions Options, HtmlSelectOptions Selected,
        WeakReference<Element> Weak) RemoveOption()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); document.AppendChild(select);
        var option = document.CreateElement("option"); select.AppendChild(option);
        var state = select.GetHtmlState()!.Select!; var options = state.Options; var selected = state.SelectedOptions;
        selected.Count.Should().Be(1);
        select.RemoveChild(option);
        return (document, select, options, selected, new WeakReference<Element>(option));
    }
    [Test]
    public void TemplateAndShadowTreesKeepOrdinarySelectMembershipAndFormOwnership()
    {
        var document = Document.CreateHtml(); var html = document.CreateElement("html"); document.AppendChild(html);
        var form = document.CreateElement("form"); html.AppendChild(form);
        var select = document.CreateElement("select"); form.AppendChild(select);
        var lightOption = document.CreateElement("option"); select.AppendChild(lightOption);
        var template = document.CreateElement("template"); select.AppendChild(template);
        var inertOption = template.TemplateContent!.OwnerDocument!.CreateElement("option"); template.TemplateContent.AppendChild(inertOption);
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(1);
        lightOption.GetHtmlState()!.Option!.GetForm(default).Should().BeSameAs(form);
        inertOption.GetHtmlState()!.Option!.GetForm(default).Should().BeNull();
        var host = document.CreateElement("div"); select.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var shadowSelect = document.CreateElement("select"); shadow.AppendChild(shadowSelect);
        var shadowOption = document.CreateElement("option"); shadowSelect.AppendChild(shadowOption);
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(1);
        shadowSelect.GetHtmlState()!.Select!.Options.Count.Should().Be(1);
        shadowOption.GetHtmlState()!.Option!.GetForm(default).Should().BeNull();
    }
}

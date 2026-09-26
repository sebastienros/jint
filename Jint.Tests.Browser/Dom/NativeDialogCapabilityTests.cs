#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Runtime;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeDialogCapabilityTests
{
    [Test]
    public void DialogTransitionsEmitRealToggleAndDeferredCloseEvents()
    {
        using var fixture = DomTestFixture.Create("<dialog></dialog>");
        var realm = DomRealm.Of(fixture.Engine);
        var dialog = fixture.Document.CreateElement("dialog");
        var state = BrowserDialogState.Of(realm, dialog);
        fixture.Engine.SetValue("dialog", realm.WrapNode(dialog));
        fixture.Execute("var seen=[]; for(const name of ['beforetoggle','toggle','close']) dialog.addEventListener(name,e=>seen.push([e.type,e.oldState,e.newState,e.bubbles,e.cancelable,e.isTrusted]));");
        state.Show(realm);
        state.Open.Should().BeTrue();
        state.Close(realm, "accepted");
        state.Open.Should().BeFalse();
        state.ReturnValue.Should().Be("accepted");
        fixture.Number("seen.length").Should().Be(2);
        fixture.Engine.Tasks.ProcessTasks();
        fixture.Text("JSON.stringify(seen)").Should().Be("[[\"beforetoggle\",\"closed\",\"open\",false,true,true],[\"beforetoggle\",\"open\",\"closed\",false,false,true],[\"toggle\",\"closed\",\"closed\",false,false,true],[\"close\",null,null,false,false,true]]");
    }

    [Test]
    public void DialogOpeningHonorsCancellationAndReentrantAttributeMutation()
    {
        using var fixture = DomTestFixture.Create("<dialog></dialog>");
        var realm = DomRealm.Of(fixture.Engine);
        var dialog = fixture.Document.CreateElement("dialog");
        var state = BrowserDialogState.Of(realm, dialog);
        fixture.Engine.SetValue("dialog", realm.WrapNode(dialog));
        fixture.Execute("dialog.addEventListener('beforetoggle',e=>e.preventDefault(),{once:true});");
        state.Show(realm);
        state.Open.Should().BeFalse();
        fixture.Execute("dialog.addEventListener('beforetoggle',()=>dialog.setAttribute('open','authored'),{once:true});");
        state.Show(realm);
        dialog.GetAttributeNS(null, "open").Should().Be("authored");
        state.ReturnValue = "retained";
        dialog.RemoveAttributeNS(null, "open");
        state.Close(realm, "ignored");
        state.ReturnValue.Should().Be("retained");
    }

    [Test]
    public void RawOpenAttributesStayAuthoritativeAndUnsupportedModalDoesNotOpen()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var dialog = document.CreateElement("dialog");
        dialog.SetAttributeNS("urn:test", "open", "");
        var state = BrowserDialogState.Of(realm, dialog);
        state.Open.Should().BeFalse();
        var error = Caught.Exception(() => state.ShowModal(realm)).Should().BeOfType<JavaScriptException>().Subject;
        error.Error.AsObject().Get("name").AsString().Should().Be("NotSupportedError");
        state.Open.Should().BeFalse();
        state.IsModal.Should().BeFalse();
        state.ReturnValue = "original";
        document.CreateElement("div").AppendChild(dialog);
        var other = Document.CreateHtml();
        other.AdoptNode(dialog);
        BrowserDialogState.Of(realm, dialog).Should().BeSameAs(state);
        BrowserDialogState.Of(realm, (Element) dialog.CloneNode()).ReturnValue.Should().BeEmpty();
        Caught.Exception(() => BrowserDialogState.Of(realm, document.CreateElementNS("urn:test", "dialog")))
            .Should().BeOfType<JavaScriptException>();
    }

    [Test]
    public async Task OrdinaryDialogFocusReturnsOnlyWhenFocusRemainsInside()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id=outside><dialog id=dialog><input id=first><input id=preferred autofocus></dialog><input id=other>");
        (await page.EvaluateAsync<string>("outside.focus(); dialog.show(); document.activeElement.id"))
            .Should().Be("preferred");
        (await page.EvaluateAsync<string>("dialog.close(); document.activeElement.id"))
            .Should().Be("outside");
        (await page.EvaluateAsync<string>("dialog.show(); other.focus(); dialog.close(); document.activeElement.id"))
            .Should().Be("other");
        page.Errors.Should().BeEmpty();
    }
}

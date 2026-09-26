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
        state.IsOpen(realm).Should().BeTrue();
        state.Close(realm, "accepted");
        state.IsOpen(realm).Should().BeFalse();
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
        state.IsOpen(realm).Should().BeFalse();
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
        state.IsOpen(realm).Should().BeFalse();
        var error = Caught.Exception(() => state.ShowModal(realm)).Should().BeOfType<JavaScriptException>().Subject;
        error.Error.AsObject().Get("name").AsString().Should().Be("NotSupportedError");
        state.IsOpen(realm).Should().BeFalse();
        BrowserDialogState.IsModal.Should().BeFalse();
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
        await page.EvaluateAsync("var outside=document.getElementById('outside'), dialog=document.getElementById('dialog'), other=document.getElementById('other');");
        (await page.EvaluateAsync<string>("outside.focus(); dialog.show(); document.activeElement.id"))
            .Should().Be("preferred");
        (await page.EvaluateAsync<string>("dialog.close(); document.activeElement.id"))
            .Should().Be("outside");
        (await page.EvaluateAsync<string>("dialog.show(); other.focus(); dialog.close(); document.activeElement.id"))
            .Should().Be("other");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public void NestedBeforeToggleCloseWinsAndEmitsOneClose()
    {
        using var fixture = DomTestFixture.Create("");
        var realm = DomRealm.Of(fixture.Engine);
        var dialog = fixture.Document.CreateElement("dialog");
        var state = BrowserDialogState.Of(realm, dialog);
        fixture.Engine.SetValue("dialog", realm.WrapNode(dialog));
        state.Show(realm);
        fixture.Engine.SetValue("nestedClose", new Action(() => state.Close(realm, "inner")));
        fixture.Execute("var closes=0; dialog.addEventListener('close',()=>closes++); dialog.addEventListener('beforetoggle',nestedClose,{once:true});");
        state.Close(realm, "outer");
        fixture.Engine.Tasks.ProcessTasks();
        state.IsOpen(realm).Should().BeFalse();
        state.ReturnValue.Should().Be("inner");
        fixture.Number("closes").Should().Be(1);
    }

    [Test]
    public void CoalescedToggleRunsAfterAnInterveningTask()
    {
        using var fixture = DomTestFixture.Create("");
        var realm = DomRealm.Of(fixture.Engine);
        var dialog = fixture.Document.CreateElement("dialog");
        var state = BrowserDialogState.Of(realm, dialog);
        fixture.Engine.SetValue("dialog", realm.WrapNode(dialog));
        fixture.Execute("var order=[]; dialog.addEventListener('toggle',e=>order.push(e.oldState+':'+e.newState));");
        state.Show(realm);
        fixture.Engine.Tasks.Post(() => fixture.Execute("order.push('middle');"));
        state.Close(realm);
        fixture.Engine.Tasks.ProcessTasks();
        fixture.Text("JSON.stringify(order)").Should().Be("[\"middle\",\"closed:closed\"]");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DialogAttributeAndFocusCandidateScansCancelBeforeOpening(bool candidate)
    {
        using var fixture = DomTestFixture.Create("");
        var realm = DomRealm.Of(fixture.Engine);
        var dialog = fixture.Document.CreateElement("dialog");
        var target = candidate ? fixture.Document.CreateElement("input") : dialog;
        if (candidate) dialog.AppendChild(target);
        for (var i = 0; i < 1024; i++) target.SetAttributeNS(null, "data-" + i, "x");
        var charged = 0;
        var batches = new List<int>();
        var work = new DomReadWork(units =>
        {
            batches.Add(units);
            charged += units;
            if (charged >= 256) throw new OperationCanceledException();
        }, default);
        var state = BrowserDialogState.Of(realm, dialog);
        Caught.Exception(() => state.Show(realm, work)).Should().BeOfType<OperationCanceledException>();
        state.IsOpen(realm).Should().BeFalse();
        // A focus candidate first charges its descendant link, then IsFocusable flushes that one
        // unit before the attribute scan reaches its next 256-unit checkpoint.
        charged.Should().Be(candidate ? 257 : 256);
        batches.Should().OnlyContain(units => units <= 256);
    }
}

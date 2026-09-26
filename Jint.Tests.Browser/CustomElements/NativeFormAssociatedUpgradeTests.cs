using Jint.Browser;
using Jint.Browser.CustomElements;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.CustomElements;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeFormAssociatedUpgradeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UpgradePublishesNativeOwnerOnlyAfterConstructionAndClearsFailedCategory(bool fail)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f'><x-field id='x'></x-field></form>");
        var ownerDuringConstruction = false;
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "x")!;
            engine.SetValue("observeNativeOwner", () =>
            {
                HtmlFormState.IsFormAssociated(element).Should().BeTrue();
                ownerDuringConstruction = HtmlFormState.GetOwner(element) is not null;
            });
            engine.Execute("class Field extends HTMLElement { static formAssociated = true; constructor() { super(); observeNativeOwner(); "
                + (fail ? "throw new Error('failed');" : "") + " } } customElements.define('x-field', Field);");
            ownerDuringConstruction.Should().BeFalse();
            var record = CustomElementRegistry.Of(engine)!.TryGetRecord(element)!;
            record.State.Should().Be(fail ? CustomElementState.Failed : CustomElementState.Custom);
            record.FormAssociated.Should().Be(!fail);
            HtmlFormState.IsFormAssociated(element).Should().Be(!fail);
            if (fail)
            {
                HtmlFormState.GetOwner(element).Should().BeNull();
                record.Definition.Should().BeNull();
                record.Reactions.Should().BeEmpty();
            }
            else HtmlFormState.GetOwner(element).Should().BeSameAs(element.ParentNode);
            return true;
        });
    }

    [Test]
    public async Task FreshAutonomousCategoryAssociatesOnInsertionButCustomizedBuiltinKeepsItsCategory()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f'></form>");
        await page.EvaluateAsync("""
            class Fresh extends HTMLElement { static formAssociated = true; }
            class Builtin extends HTMLButtonElement { static formAssociated = true; }
            customElements.define('x-fresh', Fresh);
            customElements.define('x-button', Builtin, { extends: 'button' });
            const fresh = new Fresh(); fresh.id = 'fresh';
            const button = document.createElement('button', { is: 'x-button' }); button.id = 'button';
            document.getElementById('f').append(fresh, button);
            """);
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            foreach (var id in new[] { "fresh", "button" })
            {
                var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, id)!;
                HtmlFormState.GetOwner(element).Should().BeSameAs(element.ParentNode);
                CustomElementRegistry.Of(engine)!.TryGetRecord(element)!.FormAssociated.Should().Be(id == "fresh");
            }
            return true;
        });
    }
}

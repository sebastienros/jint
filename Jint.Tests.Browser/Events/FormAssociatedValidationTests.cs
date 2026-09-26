using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Events;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

public sealed class FormAssociatedValidationTests
{
    [TestCase("<form id=f><x-field></x-field></form>")]
    [TestCase("<form id=f></form><x-field form=f></x-field>")]
    public async Task OwnedCustomElementCannotSilentlyPassUnavailableValidationOrReset(string markup)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(markup);
        await page.EvaluateAsync("customElements.define('x-field', class extends HTMLElement { static formAssociated = true; });");

        Func<Task> submit = async () => await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            FormSubmission.Submit(realm, ContentDom.ElementById(realm.Document!, "f"), null);
            return true;
        });
        (await submit.Should().ThrowAsync<NotSupportedException>()).Which.Message.Should().Contain("ElementInternals");

        Func<Task> reset = async () => await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            FormSubmission.Reset(realm, ContentDom.ElementById(realm.Document!, "f"));
            return true;
        });
        (await reset.Should().ThrowAsync<NotSupportedException>()).Which.Message.Should().Contain("reaction callback");
    }

    [Test]
    public async Task CustomElementAssociatedElsewhereDoesNotBlockValidationOrReset()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id=f><textarea id=t>default</textarea><x-field form=other></x-field></form><form id=other></form>");
        await page.EvaluateAsync(
            """
            customElements.define('x-field', class extends HTMLElement { static formAssociated = true; });
            window.submissions = 0;
            document.getElementById('f').addEventListener('submit', event => { submissions++; event.preventDefault(); });
            document.getElementById('t').value = 'changed';
            """);
        await page.RunOnLoopAsync(engine =>
        {
            var realm = DomRealm.Of(engine);
            var form = ContentDom.ElementById(realm.Document!, "f")!;
            FormSubmission.Submit(realm, form, null);
            FormSubmission.Reset(realm, form);
            return true;
        });
        (await page.EvaluateAsync<int>("submissions")).Should().Be(1);
        (await page.EvaluateAsync<string>("document.getElementById('t').value")).Should().Be("default");
    }
}

using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Customized built-in elements: <c>define(name, ctor, { extends })</c>, the <c>is</c> content attribute and
/// the <c>is</c> option on <c>createElement</c>.
/// </summary>
public sealed class CustomizedBuiltInTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task CreateElementWithAnIsOptionRunsTheConstructorAndKeepsTheBuiltInInterface()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class FancyButton extends HTMLButtonElement {
                constructor() { super(); window.log.push('ctor:' + this.localName); }
              }
              customElements.define('fancy-button', FancyButton, { extends: 'button' });
              const el = document.createElement('button', { is: 'fancy-button' });
              window.log.push(el instanceof FancyButton, el instanceof HTMLButtonElement, el.localName, String(el.getAttribute('is')));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("ctor:button|true|true|button|null");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheIsContentAttributeInMarkupUpgradesAtTheDefinition()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <button is="fancy-button" id="one"></button>
            <button id="two"></button>
            <script>
              window.log = [];
              class FancyButton extends HTMLButtonElement {
                constructor() { super(); window.log.push('ctor:' + this.id); }
                connectedCallback() { window.log.push('connected:' + this.id); }
              }
              customElements.define('fancy-button', FancyButton, { extends: 'button' });
              window.log.push(
                document.getElementById('one') instanceof FancyButton,
                document.getElementById('two') instanceof FancyButton);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("ctor:one|connected:one|true|false");
    }

    [Test]
    public async Task NewOfACustomizedBuiltInCreatesTheBuiltInsElement()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              class FancyButton extends HTMLButtonElement {}
              customElements.define('fancy-button', FancyButton, { extends: 'button' });
              const el = new FancyButton();
              window.result = [el.localName, el instanceof HTMLButtonElement, el.type].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.result")).Should().Be("button|true|submit");
    }

    [Test]
    public async Task ExtendingANameThatIsNotABuiltInIsANotSupportedError()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              try {
                customElements.define('x-a', class extends HTMLElement {}, { extends: 'x-b' });
              } catch (e) { window.log.push(e.name); }
              try {
                customElements.define('x-c', class extends HTMLElement {}, { extends: 'bogus' });
              } catch (e) { window.log.push(e.name); }
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("NotSupportedError|NotSupportedError");
    }

    [Test]
    public async Task AnAutonomousConstructorExtendingTheWrongInterfaceIsATypeError()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Wrong extends HTMLButtonElement {}
              customElements.define('x-wrong', Wrong);
              try { new Wrong(); } catch (e) { window.log.push(e.constructor.name); }
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("TypeError");
    }

    [Test]
    public async Task FormAssociatedIsReadAtDefineTime()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Field extends HTMLElement {
                static formAssociated = true;
                static get observedAttributes() { return ['value']; }
                attributeChangedCallback() { window.log.push('attr'); }
              }
              customElements.define('x-field', Field);
              const el = document.createElement('x-field');
              el.setAttribute('value', 'v');
              window.log.push('ok');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("attr|ok");
        page.Errors.Should().BeEmpty();
    }
}

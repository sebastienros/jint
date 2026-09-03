using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Upgrades: the parser-created element that becomes custom at a definition, at a parse boundary or on
/// <c>customElements.upgrade</c>, and the markup paths that create one after the parse.
/// </summary>
public sealed class CustomElementUpgradeTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task DefineUpgradesEveryMatchingElementAlreadyInTheDocumentInTreeOrder()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-thing id="a"></x-thing>
            <div><x-thing id="b"></x-thing></div>
            <x-thing id="c"></x-thing>
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push(this.id); } }
              customElements.define('x-thing', Thing);
              window.log.push('all:' + [...document.querySelectorAll('x-thing')].every(e => e instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("a|b|c|all:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnElementParsedAfterTheDefinitionIsCustomByTheNextScript()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor:' + this.id); } }
              customElements.define('x-thing', Thing);
            </script>
            <x-thing id="later"></x-thing>
            <script>
              window.log.push('second:' + (document.getElementById('later') instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor:later|second:true");
    }

    [Test]
    public async Task AnElementParsedAfterTheLastScriptIsCustomByDomContentLoaded()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              document.addEventListener('DOMContentLoaded', () => {
                window.log.push('dcl:' + (document.querySelector('x-thing') instanceof Thing));
              });
            </script>
            <x-thing></x-thing>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|dcl:true");
    }

    [Test]
    public async Task ADetachedElementIsNotUpgradedByDefineButIsWhenItEntersTheDocument()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <p></p>
            <script>
              window.log = [];
              const el = document.createElement('x-thing');
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              window.log.push('afterDefine:' + (el instanceof Thing));
              document.body.appendChild(el);
              window.log.push('afterInsert:' + (el instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("afterDefine:false|ctor|afterInsert:true");
    }

    [Test]
    public async Task UpgradeCustomizesADetachedSubtreeInTreeOrder()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push(this.id || 'root'); } }
              customElements.define('x-thing', Thing);
              const holder = document.createElement('div');
              holder.innerHTML = '<x-thing id="one"></x-thing><span><x-thing id="two"></x-thing></span>';
              window.log.push('|');
              customElements.upgrade(holder);
              window.log.push('done:' + (holder.firstChild instanceof Thing));
            </script>
            """);

        // The markup path already upgraded both, so upgrade() finds nothing left to do; what it must not do
        // is run a constructor twice.
        (await page.EvaluateAsync<string>("window.log.join(',')")).Should().Be("one,two,|,done:true");
    }

    [Test]
    public async Task InnerHtmlOnADetachedElementUpgradesWhatItParsed()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              const holder = document.createElement('div');
              holder.innerHTML = '<x-thing></x-thing>';
              window.log.push('sync:' + (holder.firstChild instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|sync:true");
    }

    [Test]
    public async Task InsertAdjacentHtmlUpgradesWhatItParsed()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              class Thing extends HTMLElement { constructor() { super(); window.log.push('ctor'); } }
              customElements.define('x-thing', Thing);
              document.getElementById('host').insertAdjacentHTML('beforeend', '<x-thing></x-thing>');
              window.log.push('sync:' + (document.querySelector('x-thing') instanceof Thing));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|sync:true");
    }

    [Test]
    public async Task AFailedUpgradeIsNotRetried()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-bad></x-bad>
            <script>
              window.log = [];
              class Bad extends HTMLElement { constructor() { super(); window.log.push('ctor'); throw new Error('nope'); } }
              customElements.define('x-bad', Bad);
              customElements.upgrade(document.body);
              window.log.push('count:' + window.log.filter(x => x === 'ctor').length);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|count:1");
        page.Errors.Should().NotBeEmpty();
    }
}

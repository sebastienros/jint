using Jint.Browser;

namespace Jint.Tests.Browser.CustomElements;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The lifecycle reactions — connected, disconnected and attribute-changed — and when each of them runs.
/// </summary>
public sealed class CustomElementReactionTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    private const string Definition =
        """
        window.log = [];
        class Thing extends HTMLElement {
          static get observedAttributes() { return ['a', 'b']; }
          connectedCallback() { window.log.push('connected:' + this.id); }
          disconnectedCallback() { window.log.push('disconnected:' + this.id); }
          attributeChangedCallback(name, oldValue, newValue, ns) {
            window.log.push('attr:' + name + ':' + oldValue + ':' + newValue + ':' + ns + ':' + this.getAttribute(name));
          }
        }
        customElements.define('x-thing', Thing);
        """;

    [Test]
    public async Task ConnectedFiresOnInsertionAndDisconnectedOnRemovalBeforeTheOperationReturns()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            "<p></p><script>" + Definition + """
              const el = document.createElement('x-thing');
              el.id = 'one';
              document.body.appendChild(el);
              window.log.push('afterAppend');
              el.remove();
              window.log.push('afterRemove');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("connected:one|afterAppend|disconnected:one|afterRemove");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnObservedAttributeReportsItsOldAndNewValueAndFiresBeforeSetAttributeReturns()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            "<script>" + Definition + """
              const el = document.createElement('x-thing');
              el.setAttribute('a', '1');
              window.log.push('after1');
              el.setAttribute('a', '2');
              el.removeAttribute('a');
              el.setAttribute('ignored', 'x');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("attr:a:null:1:null:1|after1|attr:a:1:2:null:2|attr:a:2:null:null:null");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task OneSetAttributeIsOneCallback()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            "<script>" + Definition + """
              const el = document.createElement('x-thing');
              el.setAttribute('a', '1');
              window.result = window.log.length;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.result")).Should().Be(1);
    }

    [Test]
    public async Task AnUpgradeReportsEveryAttributeAlreadyThereAndThenConnected()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-thing id="one" a="1" b="2" c="3"></x-thing>
            <script>
            """ + Definition + """
              window.log.push('afterDefine');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("attr:a:null:1:null:1|attr:b:null:2:null:2|connected:one|afterDefine");
    }

    [Test]
    public async Task TheConstructorRunsBeforeTheAttributeAndConnectedReactions()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <x-thing id="one" a="1"></x-thing>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                static get observedAttributes() { return ['a']; }
                constructor() { super(); window.log.push('ctor'); }
                connectedCallback() { window.log.push('connected'); }
                attributeChangedCallback() { window.log.push('attr'); }
              }
              customElements.define('x-thing', Thing);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("ctor|attr|connected");
    }

    [Test]
    public async Task ReplacingTheDocumentWithInnerHtmlConnectsWhatArrivedAndDisconnectsWhatLeft()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"><x-thing id="old"></x-thing></div>
            <script>
            """ + Definition + """
              document.getElementById('host').innerHTML = '<x-thing id="new"></x-thing>';
              window.log.push('after');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("connected:old|disconnected:old|connected:new|after");
    }

    [Test]
    public async Task MovingAnElementDisconnectsAndReconnectsIt()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="a"><x-thing id="one"></x-thing></div><div id="b"></div>
            <script>
            """ + Definition + """
              document.getElementById('b').appendChild(document.getElementById('one'));
              window.log.push('after');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("connected:one|disconnected:one|connected:one|after");
    }

    [Test]
    public async Task ACallbackThatThrowsIsReportedAndThePageSurvives()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <p></p>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                connectedCallback() { throw new Error('boom'); }
              }
              customElements.define('x-thing', Thing);
              document.body.appendChild(document.createElement('x-thing'));
              window.log.push('survived');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("survived");
        string.Join("\n", page.Errors.Select(e => e.Message)).Should().Contain("boom");
    }

    /// <summary>
    /// The one attribute write neither channel reports, which is AngleSharp's gap rather than a deferral:
    /// <c>TokenList</c> writes the content attribute without notifying its <c>IAttributeObserver</c> and
    /// without queueing a mutation record, so nothing arrives at all — not now and not at the checkpoint.
    /// </summary>
    [Test]
    public async Task AClassListWriteReachesNeitherNotificationChannel()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <p></p>
            <script>
              window.log = [];
              class Thing extends HTMLElement {
                static get observedAttributes() { return ['class']; }
                attributeChangedCallback(name, o, n) { window.log.push('attr:' + n); }
              }
              customElements.define('x-thing', Thing);
              const el = document.createElement('x-thing');
              document.body.appendChild(el);
              el.classList.add('one');
              window.log.push('sync:' + window.log.length);
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        // Pumped to the checkpoint first, so a failure here would mean a deferral rather than a gap.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("sync:0");
        (await page.EvaluateAsync<string>("document.querySelector('x-thing').className")).Should().Be("one");
    }
}

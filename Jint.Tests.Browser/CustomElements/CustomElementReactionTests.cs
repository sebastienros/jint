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
    /// A <c>classList</c> write is an ordinary attribute change, and reaches the reaction like one.
    /// </summary>
    /// <remarks>
    /// It did not use to. AngleSharp's <c>TokenList</c> writes the content attribute through its own
    /// <c>Changed</c> event, which notifies no <c>IAttributeObserver</c> and queues no mutation record, so
    /// nothing arrived at all — not then and not at the checkpoint. DOM §7.1's <i>update steps</i> are
    /// <c>Dom/Collections/DomTokenListMembers</c>' now and they are a plain <i>set an attribute value</i>,
    /// which is the same door <c>setAttribute</c> comes through; the notification is what that buys.
    /// </remarks>
    [Test]
    public async Task AClassListWriteIsAnOrdinaryAttributeChange()
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

        // Synchronously, at the write, which is where DOM's attribute change steps enqueue the reaction.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("attr:one|sync:1");
        (await page.EvaluateAsync<string>("document.querySelector('x-thing').className")).Should().Be("one");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/custom-elements.html#custom-element-reactions-stack: every
    /// <c>[CEReactions]</c> operation pushes an element queue of its own, so a reaction a callback causes
    /// runs <b>inside</b> that callback and not after it.
    /// </summary>
    [Test]
    public async Task AReactionCausedInsideACallbackRunsBeforeThatCallbackReturns()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.order = [];
              class Thing extends HTMLElement {
                static get observedAttributes() { return ['data-title', 'title']; }
                attributeChangedCallback() { this.handler(); }
                handler() { }
              }
              customElements.define('x-thing', Thing);

              const first = document.createElement('x-thing');
              const second = document.createElement('x-thing');
              first.id = 'first';
              second.id = 'second';
              first.handler = function () {
                window.order.push(this.id + ':begin');
                second.setAttribute('data-title', 'x');
                window.order.push(this.id + ':end');
              };
              second.handler = function () {
                window.order.push(this.id + ':begin');
                window.order.push(this.id + ':end');
              };
              first.setAttribute('title', 'x');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.order.join('|')"))
            .Should().Be("first:begin|second:begin|second:end|first:end");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The same stack seen through a constructor: a clone made inside a custom element constructor is
    /// upgraded before the outer constructor returns.
    /// </summary>
    [Test]
    public async Task ACloneMadeInsideAConstructorIsUpgradedBeforeThatConstructorReturns()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <script>
              window.log = [];
              let cloneNext = false;
              let other;
              class SelfCloning extends HTMLElement {
                constructor() {
                  super();
                  const mark = window.log.length;
                  window.log.push(mark + ':begin');
                  if (cloneNext) { cloneNext = false; other.cloneNode(false); }
                  window.log.push(mark + ':end');
                }
              }
              customElements.define('x-self-cloning', SelfCloning);
              const one = document.createElement('x-self-cloning');
              other = document.createElement('x-self-cloning');
              window.log = [];
              cloneNext = true;
              one.cloneNode(false);
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')"))
            .Should().Be("0:begin|1:begin|1:end|0:end");
        page.Errors.Should().BeEmpty();
    }
}

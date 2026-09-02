namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>&lt;template&gt;</c>, shadow roots and slots: the wrappers the generated binding already had, and what
/// the engine's tree dispatch makes of a shadow boundary.
/// </summary>
public sealed class ShadowDomTests
{
    [Test]
    public async Task TemplateContentIsAnInertDocumentFragment()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="anchor"></div>
            <template id="t"><p class="row">inside</p></template>
            <script>
              const template = document.getElementById('t');
              const content = template.content;
              window.log = [
                content instanceof DocumentFragment,
                content.nodeType,
                content.childElementCount,
                content.querySelector('.row').textContent,
                document.querySelector('.row') === null,
              ].join('|');

              const clone = content.cloneNode(true);
              document.body.appendChild(clone);
              window.afterClone = document.querySelectorAll('.row').length;
            </script>
            """);

        // 11 is DOCUMENT_FRAGMENT_NODE, and a template's content is outside the document until it is cloned in.
        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|11|1|inside|true");
        (await page.EvaluateAsync<int>("window.afterClone")).Should().Be(1);
    }

    [Test]
    public async Task AttachShadowGivesAHostAShadowRoot()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><span slot="body">light</span></div>
            <script>
              const host = document.getElementById('host');
              const root = host.attachShadow({ mode: 'open' });
              root.innerHTML = '<slot name="body"></slot><em>shadow</em>';

              window.log = [
                root instanceof ShadowRoot,
                root instanceof DocumentFragment,
                root.mode,
                root.host === host,
                host.shadowRoot === root,
                root.querySelector('em').textContent,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("true|true|open|true|true|shadow");
    }

    [Test]
    public async Task ASlotReportsTheNodesAssignedToIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"><span slot="body">light</span><b>unslotted</b></div>
            <script>
              const host = document.getElementById('host');
              const root = host.attachShadow({ mode: 'open' });
              root.innerHTML = '<slot name="body"></slot>';
              const slot = root.querySelector('slot');

              window.log = [
                slot.name,
                slot.assignedNodes().length,
                slot.assignedNodes()[0].textContent,
                slot.assignedElements().length,
                slot.assignedElements()[0].tagName,
              ].join('|');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log")).Should().Be("body|1|light|1|SPAN");
    }

    [Test]
    public async Task AnEventFromInsideAnOpenShadowRootCrossesTheBoundaryWhenItIsComposed()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"></div>
            <script>
              const host = document.getElementById('host');
              const root = host.attachShadow({ mode: 'open' });
              root.innerHTML = '<em id="inner">x</em>';
              const inner = root.getElementById ? root.querySelector('#inner') : root.querySelector('em');

              window.path = [];
              window.outside = [];
              document.body.addEventListener('composed', e => {
                window.outside.push(e.target === host);
                window.path = e.composedPath().map(t => t === inner ? 'inner' : t === root ? 'root' : t === host ? 'host' : t === document.body ? 'body' : t === document ? 'document' : t === window ? 'window' : t.nodeName);
              });

              inner.dispatchEvent(new Event('composed', { bubbles: true, composed: true }));
            </script>
            """);

        // The path is the whole composed tree, and retargeting makes the listener outside the shadow root see
        // the host rather than the element inside it.
        (await page.EvaluateAsync<string>("window.outside.join('|')")).Should().Be("true");
        (await page.EvaluateAsync<string>("window.path.join(',')")).Should().Be("inner,root,host,body,HTML,document,window");
    }

    [Test]
    public async Task AnUncomposedEventStopsAtTheShadowRoot()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"></div>
            <script>
              const host = document.getElementById('host');
              const root = host.attachShadow({ mode: 'open' });
              root.innerHTML = '<em>x</em>';
              const inner = root.querySelector('em');

              window.reachedOutside = false;
              window.reachedRoot = false;
              document.body.addEventListener('contained', () => { window.reachedOutside = true });
              root.addEventListener('contained', () => { window.reachedRoot = true });

              inner.dispatchEvent(new Event('contained', { bubbles: true }));
            </script>
            """);

        (await page.EvaluateAsync<bool>("window.reachedRoot")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.reachedOutside")).Should().BeFalse();
    }

    [Test]
    public async Task AClosedShadowRootHidesItsContentsFromComposedPath()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id="host"></div>
            <script>
              const host = document.getElementById('host');
              const root = host.attachShadow({ mode: 'closed' });
              root.innerHTML = '<em>x</em>';
              const inner = root.querySelector('em');

              window.path = [];
              document.body.addEventListener('composed', e => {
                window.path = e.composedPath().map(t => t === host ? 'host' : t === document.body ? 'body' : t === document ? 'document' : t === window ? 'window' : t.nodeName);
              });

              inner.dispatchEvent(new Event('composed', { bubbles: true, composed: true }));
              window.mode = root.mode;
            </script>
            """);

        (await page.EvaluateAsync<string>("window.mode")).Should().Be("closed");
        (await page.EvaluateAsync<string>("window.path.join(',')")).Should().Be("host,body,HTML,document,window");
    }
}

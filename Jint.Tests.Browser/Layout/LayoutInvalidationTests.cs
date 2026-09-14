using Jint.Browser;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Layout;

public sealed class LayoutInvalidationTests
{
    private const string Markup = """
        <style>
          .unmatched { display: none }
          .gone, #target[hidden], #target[data-hide], #target[aria-hidden=true],
          body[data-hide] #target, input:checked ~ #target, input:indeterminate ~ #target,
          input:invalid ~ #target, #target:empty { display: none }
        </style>
        <input id="flag" type="checkbox"><button id="target" class="target">Save</button>
        <div id="hiddenParent" hidden></div>
        """;

    [Test]
    public async Task UnchangedReadsShareMeasurementsAndCompleteLayouts()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);

        (await page.EvaluateAsync<string>("""
            (() => {
              const target = document.getElementById('target');
              const before = target.getBoundingClientRect();
              const first = queryStamp();
              for (let i = 0; i < 10; i++) {
                target.getClientRects(); target.clientWidth; target.offsetHeight;
                document.querySelector('#target').getBoundingClientRect();
                getComputedStyle(target).width;
              }
              const second = queryStamp();
              document.elementFromPoint(1, before.top + 1);
              const full = layoutStamp();
              document.elementFromPoint(1, before.top + 1);
              return [first, second, queryStamp(), full, layoutStamp()].join(',');
            })()
            """)).Should().Be("1,1,1,1,1");
    }

    [TestCase("target.hidden = true")]
    [TestCase("target.classList.add('gone')")]
    [TestCase("target.classList.toggle('gone')")]
    [TestCase("target.className = 'gone'")]
    [TestCase("target.setAttribute('hidden', '')")]
    [TestCase("target.setAttributeNS(null, 'hidden', '')")]
    [TestCase("target.attributes.getNamedItem('class').value = 'gone'")]
    [TestCase("const a = document.createAttribute('hidden'); target.attributes.setNamedItem(a)")]
    [TestCase("target.dataset.hide = 'yes'")]
    [TestCase("target.ariaHidden = 'true'")]
    [TestCase("target.style.display = 'none'")]
    [TestCase("target.style.setProperty('display', 'none', 'important')")]
    [TestCase("target.style.cssText = 'display: none'")]
    [TestCase("document.styleSheets[0].cssRules[0].selectorText = '.target'")]
    [TestCase("document.styleSheets[0].insertRule('.target { display: none }', 0)")]
    [TestCase("document.styleSheets[0].cssRules[1].style.setProperty('display', 'none'); target.className = 'gone'")]
    [TestCase("document.querySelector('style').textContent = '.target { display: none }'")]
    [TestCase("target.firstChild.replaceData(0, 99999, '')")]
    [TestCase("document.getElementById('flag').checked = true")]
    [TestCase("document.getElementById('flag').indeterminate = true")]
    [TestCase("document.getElementById('flag').setCustomValidity('invalid')")]
    [TestCase("document.getElementById('hiddenParent').appendChild(target)")]
    [TestCase("target.remove()")]
    [TestCase("document.body.removeChild(target)")]
    [TestCase("target.replaceWith(document.createElement('span'))")]
    [TestCase("target.outerHTML = '<span>replacement</span>'")]
    [TestCase("target.innerHTML = ''")]
    [TestCase("target.textContent = ''")]
    [TestCase("target.firstChild.data = ''")]
    [TestCase("target.replaceChildren()")]
    [TestCase("const range = document.createRange(); range.selectNodeContents(target); range.deleteContents()")]
    [TestCase("const range = document.createRange(); range.selectNodeContents(target); const s = getSelection(); s.addRange(range); s.deleteFromDocument()")]
    public async Task EveryMutationRefreshesWarmGeometryWithinTheSameScript(string mutation)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);

        (await page.EvaluateAsync<string>($$"""
            (() => {
              const target = document.getElementById('target');
              const before = target.getBoundingClientRect().height;
              const first = queryStamp();
              {{mutation}};
              const after = target.getBoundingClientRect().height;
              return [before, after, queryStamp() > first, queryStamp() === queryStamp()].join(',');
            })()
            """)).Should().Be("16,0,true,true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NamedDeletionAndCssomChangesRefreshTheCache()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>#target:not([data-show]) {display:none}</style>
            <button id="target" data-show="">Save</button>
            """);
        (await page.EvaluateAsync<string>("""
            (() => {
              const target = document.getElementById('target');
              const height = () => target.getBoundingClientRect().height;
              const result = [height()];
              delete target.dataset.show; result.push(height());
              const sheet = document.styleSheets[0];
              sheet.disabled = true; result.push(height());
              sheet.disabled = false; result.push(height());
              sheet.media.mediaText = 'not all'; result.push(height());
              sheet.media.mediaText = 'all'; result.push(height());
              sheet.cssRules[0].style.removeProperty('display'); result.push(height());
              sheet.cssRules[0].style.cssText = 'display:none'; result.push(height());
              sheet.deleteRule(0); result.push(height());
              return result.join(',');
            })()
            """)).Should().Be("16,0,16,0,16,0,16,0,16");
    }

    [Test]
    public async Task ArgumentReentryAndFailedWritesDoNotLeaveAReusableIntermediateResult()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            (() => {
              const target = document.getElementById('target');
              const heights = [target.getBoundingClientRect().height];
              target.setAttribute('class', {toString() {
                heights.push(target.getBoundingClientRect().height);
                target.hidden = true;
                heights.push(target.getBoundingClientRect().height);
                target.hidden = false;
                heights.push(target.getBoundingClientRect().height);
                return 'gone';
              }});
              heights.push(target.getBoundingClientRect().height);
              try { target.appendChild(target); } catch (_) {}
              heights.push(target.getBoundingClientRect().height);
              return heights.join(',') + '|' + (queryStamp() === queryStamp());
            })()
            """)).Should().Be("16,16,0,16,0,0|true");
    }

    [Test]
    public async Task NativeActivationAndRollbackAreVisibleInsideAndAfterListeners()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        (await page.EvaluateAsync<string>("""
            (() => {
              const target = document.getElementById('target');
              const flag = document.getElementById('flag');
              const heights = [target.getBoundingClientRect().height];
              flag.addEventListener('click', e => {
                heights.push(target.getBoundingClientRect().height);
                e.preventDefault();
              });
              flag.dispatchEvent(new MouseEvent('click', {bubbles:true, cancelable:true}));
              heights.push(target.getBoundingClientRect().height);
              return heights.join(',');
            })()
            """)).Should().Be("16,0,16");
    }

    [Test]
    public async Task ParserCallbacksDoNotRetainConstructionResults()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <script>var heights = [document.body ? document.body.getBoundingClientRect().height : 0];</script>
            <div>one</div>
            <script>heights.push(document.body.getBoundingClientRect().height);</script>
            <div>two</div>
            <script>heights.push(document.body.getBoundingClientRect().height);</script>
            """);
        (await page.EvaluateAsync<string>("heights.join(',') + ',' + document.body.getBoundingClientRect().height"))
            .Should().Be("0,32,48,48");
    }

    [Test]
    public async Task HostConfigurationAndUnknownNativeCallbacksKeepQueriesFresh()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions().ConfigureEngine(_ => { }));
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<bool>("queryStamp() !== queryStamp()")).Should().BeTrue();

        (await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var target = runtime.Document!.GetElementById("target")!;
            var before = runtime.Layout.ClientBoxOf(target)!.Value.Height;
            target.SetAttribute("hidden", "");
            return before > 0 && runtime.Layout.ClientBoxOf(target) is null;
        })).Should().BeTrue();
    }

    [Test]
    public async Task SeparatePagesAndNavigationOwnTheirCacheLifetimes()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var first = await browser.NewPageAsync();
        var second = await browser.NewPageAsync();
        foreach (var page in new[] { first, second })
        {
            await page.SetContentAsync(Markup);
            await InstallDiagnostics(page);
            (await page.EvaluateAsync<int>("queryStamp()")).Should().Be(1);
        }
        await first.EvaluateAsync("document.getElementById('target').hidden = true");
        (await second.EvaluateAsync<int>("queryStamp()")).Should().Be(1);
        (await first.EvaluateAsync<int>("queryStamp()")).Should().Be(2);
        await first.SetContentAsync("<div>replacement</div>");
        (await first.EvaluateAsync<double>("document.body.getBoundingClientRect().height")).Should().Be(32);
        (await second.EvaluateAsync<double>("document.getElementById('target').getBoundingClientRect().height"))
            .Should().Be(16);
    }

    [Test]
    public async Task CustomElementReactionsCannotRetainIntermediateGeometry()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<x-box id='target'>Save</x-box>");
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            (() => {
              let during, height;
              customElements.define('x-box', class extends HTMLElement {
                static get observedAttributes() { return ['data-x']; }
                attributeChangedCallback() {
                  during = queryStamp() === queryStamp();
                  this.hidden = true;
                  height = this.getBoundingClientRect().height;
                }
              });
              const target = document.getElementById('target');
              const before = target.getBoundingClientRect().height;
              target.setAttribute('data-x', 'value');
              return [before, during, height, target.getBoundingClientRect().height,
                queryStamp() === queryStamp()].join(',');
            })()
            """)).Should().Be("16,false,0,0,true");
    }

    [TestCase("<style>@import url('data:text/css,button%7Bdisplay:block%7D');</style>")]
    [TestCase("<link rel='stylesheet' href='data:text/css,button%7Bdisplay:block%7D'>")]
    public async Task NativeResourceAttachmentUsesTheUncachedFallback(string styles)
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(styles + "<button id='target'>Save</button>");
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<bool>("queryStamp() !== queryStamp()")).Should().BeTrue();
    }

    [Test]
    public async Task CoverageStartedAfterWarmReadsObservesTheDocumentAndThenAllowsReuseAgain()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>button{display:block}</style><button id='target'>Save</button>");
        await InstallDiagnostics(page);
        var tracker = new global::Jint.Browser.Dom.Views.CssRuleUsageTracker();
        await page.RunOnLoopAsync(engine =>
        {
            tracker.Rebind(PageRuntime.Find(engine)!.Document);
            return 0;
        });
        (await page.EvaluateAsync<bool>("queryStamp() === queryStamp()")).Should().BeTrue();
        global::Jint.Browser.Dom.Views.CssRuleUsage.Arm(tracker);
        try
        {
            (await page.EvaluateAsync<bool>("queryStamp() !== queryStamp()")).Should().BeTrue();
            await page.EvaluateAsync("document.getElementById('target').getBoundingClientRect()");
            (await page.RunOnLoopAsync(_ => tracker.TakeDelta().Length)).Should().Be(1);
        }
        finally
        {
            global::Jint.Browser.Dom.Views.CssRuleUsage.Disarm(tracker);
        }
        (await page.EvaluateAsync<bool>("queryStamp() === queryStamp()")).Should().BeTrue();
    }

    [Test]
    public async Task InsertingAnImportStopsReuseOfAPreviouslyCachedSheet()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(Markup);
        await InstallDiagnostics(page);
        (await page.EvaluateAsync<string>("""
            (() => {
              const before = queryStamp() === queryStamp();
              document.styleSheets[0].insertRule("@import url('data:text/css,button%7Bdisplay:block%7D');", 0);
              return before + ',' + (queryStamp() === queryStamp());
            })()
            """)).Should().Be("true,false");
    }

    // Read-only diagnostic callbacks installed by the test on the page loop. No Browser option or native
    // mutator is exposed: the public evaluation still exercises exactly the default cache eligibility.
    private static Task<int> InstallDiagnostics(Page page) => page.RunOnLoopAsync(engine =>
    {
        var layout = PageRuntime.Find(engine)!.Layout;
        FlatLayout.SizeQuery? previous = null;
        FlatLayout? full = null;
        var queries = 0;
        var layouts = 0;
        engine.SetValue("queryStamp", () =>
        {
            var current = layout.MeasureSizes();
            if (!ReferenceEquals(previous, current))
            {
                previous = current;
                queries++;
            }
            return queries;
        });
        engine.SetValue("layoutStamp", () =>
        {
            var current = layout.Current();
            if (!ReferenceEquals(full, current))
            {
                full = current;
                layouts++;
            }
            return layouts;
        });
        return 0;
    });
}

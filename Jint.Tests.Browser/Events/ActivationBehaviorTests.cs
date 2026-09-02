using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// HTML's activation behaviours, reached the way a browser reaches them: a <c>MouseEvent</c> named
/// <c>click</c> dispatched through the tree, with the behaviour running after the listeners unless one of them
/// canceled the event.
/// </summary>
public sealed class ActivationBehaviorTests
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-click — <c>click()</c> fires a synthetic
    /// pointer event <b>with the not trusted flag set</b>, and its activation behaviour still runs. Trust
    /// decides what a page can tell apart, not whether the default action happens.
    /// </summary>
    [Test]
    public async Task ClickTogglesACheckboxAndTheSyntheticEventIsNotTrusted()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='c' type='checkbox'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const c = document.getElementById('c');
              const seen = [];
              c.addEventListener('click', e => seen.push('click:' + e.isTrusted + ':' + c.checked + ':' + e.type));
              c.click();
              const first = c.checked;
              c.click();
              return [first, c.checked, seen.join('|')].join(',');
            })()
            """))
            // The listener sees the checkbox already toggled: that is what the legacy pre-activation behaviour
            // is for, and it is why `onclick` can read `this.checked` and get the new value.
            .Should().Be("true,false,click:false:true:click|click:false:false:click");
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-canceled-activation-behavior — <c>preventDefault()</c>
    /// puts the checkedness back, which is the whole reason the specification has a rollback step at all.
    /// </summary>
    [Test]
    public async Task PreventDefaultRestoresTheCheckboxTheLegacyPreActivationBehaviourToggled()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='c' type='checkbox'><input id='d' type='checkbox' checked>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const c = document.getElementById('c');
              const d = document.getElementById('d');
              for (const el of [c, d]) {
                el.addEventListener('click', e => { seen.push(el.id + ':' + el.checked); e.preventDefault(); });
                el.addEventListener('input', () => seen.push(el.id + ':input'));
                el.addEventListener('change', () => seen.push(el.id + ':change'));
                el.click();
              }
              return [c.checked, d.checked, seen.join('|')].join(',');
            })()
            """))
            // Inside the listener the toggle has happened; afterwards it is undone, and neither `input` nor
            // `change` fires because the activation behaviour never ran.
            .Should().Be("false,true,c:true|d:false");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#checkbox-state-(type=checkbox): the activation
    /// behaviour fires <c>input</c> and then <c>change</c>, both bubbling.
    /// </summary>
    [Test]
    public async Task AClickedCheckboxFiresInputThenChangeAndBothBubble()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f'><input id='c' type='checkbox'></form>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              for (const type of ['click', 'input', 'change']) {
                document.getElementById('f').addEventListener(type, e =>
                  seen.push(type + ':' + e.target.id + ':' + e.bubbles + ':' + e.composed + ':' + e.cancelable));
              }
              document.getElementById('c').click();
              return seen.join('|');
            })()
            """)).Should().Be("click:c:true:true:true|input:c:true:true:false|change:c:true:false:false");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#radio-button-state-(type=radio) — the group is
    /// exclusive, and a canceled activation restores the member that was checked before rather than simply
    /// unchecking the one that was clicked.
    /// </summary>
    [Test]
    public async Task ARadioGroupIsExclusiveAndACanceledClickRestoresThePreviousMember()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f'>
              <input id='a' type='radio' name='g' value='a' checked>
              <input id='b' type='radio' name='g' value='b'>
              <input id='c' type='radio' name='g' value='c'>
              <input id='other' type='radio' name='h'>
            </form>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const state = () => ['a', 'b', 'c', 'other'].map(id => document.getElementById(id).checked ? 1 : 0).join('');
              const seen = [];
              document.getElementById('f').addEventListener('change', e => seen.push('change:' + e.target.id));

              document.getElementById('b').click();
              const afterB = state();

              const c = document.getElementById('c');
              c.addEventListener('click', e => e.preventDefault(), { once: true });
              c.click();
              const afterCancelled = state();

              return [afterB, afterCancelled, seen.join('|')].join(',');
            })()
            """)).Should().Be("0100,0100,change:b");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#the-label-element — the label forwards the click to
    /// its labeled control, and HTML's own "do nothing for a target inside interactive content" clause is what
    /// keeps the forwarded click from looping back through the label.
    /// </summary>
    [Test]
    public async Task ALabelForwardsItsClickToTheLabeledControlExactlyOnce()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <label id='l' for='c'>Agree</label>
            <input id='c' type='checkbox'>
            <label id='wrap'><input id='inner' type='checkbox'> Wrapped</label>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const c = document.getElementById('c');
              c.addEventListener('click', e => seen.push('c:' + e.isTrusted));
              document.getElementById('l').click();

              const inner = document.getElementById('inner');
              inner.addEventListener('click', () => seen.push('inner'));
              inner.click();

              return [c.checked, inner.checked, seen.join('|')].join(',');
            })()
            """))
            // One forwarded click at the control, carrying the original's (un)trust; clicking the control
            // inside a wrapping label does not forward again.
            .Should().Be("true,true,c:false|inner");
    }

    [Test]
    public async Task ALabelWithNoControlDoesNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<label id='l'>Nothing</label>");

        (await page.EvaluateAsync<bool>("document.getElementById('l').click(), true")).Should().BeTrue();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interactive-elements.html#the-summary-element — the summary
    /// toggles its details' <c>open</c> attribute, and the <c>toggle</c> event is a queued task rather than a
    /// synchronous fire, which is what the specification's details notification task steps say.
    /// </summary>
    [Test]
    public async Task ASummaryTogglesItsDetailsAndTheToggleEventArrivesAsATask()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <details id='d'><summary id='s'>More</summary><p>body</p></details>
            <script>
              window.log = [];
              document.getElementById('d').addEventListener('toggle', e => window.log.push('toggle:' + e.target.open));
            </script>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const d = document.getElementById('d');
              document.getElementById('s').click();
              return [d.open, window.log.length].join(',');
            })()
            """))
            // Open immediately, and the event has not been delivered yet: it is a task.
            .Should().Be("true,0");

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("toggle:true");

        await page.EvaluateAsync("document.getElementById('s').click()");
        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("[document.getElementById('d').open, window.log.join('|')].join(',')"))
            .Should().Be("false,toggle:true|toggle:false");
    }

    [Test]
    public async Task ASummaryThatIsNotTheFirstOneOfItsDetailsTogglesNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<details id='d'><summary id='first'>A</summary><summary id='second'>B</summary></details>");

        (await page.EvaluateAsync<string>(
            "document.getElementById('second').click(), String(document.getElementById('d').open)"))
            .Should().Be("false");
    }

    /// <summary>
    /// A submit button reaches its form with itself as the submitter, and the <c>submit</c> event is
    /// cancelable — https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm.
    /// </summary>
    [Test]
    public async Task ASubmitButtonFiresACancelableSubmitEventCarryingItselfAsTheSubmitter()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f' action='/go'>
              <button id='b' type='submit'>Go</button>
              <button id='plain' type='button'>Nothing</button>
              <input id='i' type='submit' value='Send'>
            </form>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const f = document.getElementById('f');
              f.addEventListener('submit', e => {
                seen.push('submit:' + (e.submitter === null ? 'null' : e.submitter.id) + ':' + e.cancelable + ':' + e.bubbles);
                e.preventDefault();
              });
              document.getElementById('b').click();
              document.getElementById('i').click();
              document.getElementById('plain').click();
              f.requestSubmit();
              f.requestSubmit(document.getElementById('b'));
              return seen.join('|');
            })()
            """)).Should().Be("submit:b:true:true|submit:i:true:true|submit:null:true:true|submit:b:true:true");
    }

    [Test]
    public async Task RequestSubmitRefusesAnElementThatIsNotASubmitButtonOfThisForm()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f'><button id='b' type='submit'>Go</button><span id='s'></span></form>
            <form id='g'><button id='c' type='submit'>Go</button></form>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const f = document.getElementById('f');
              const results = [];
              for (const id of ['s', 'c']) {
                try { f.requestSubmit(document.getElementById(id)); results.push('no error'); }
                catch (e) { results.push(e.constructor.name); }
              }
              return results.join(',');
            })()
            """))
            // The two refusals are different on purpose: an element that is not a submit button at all is a
            // TypeError, and one belonging to another form is a NotFoundError DOMException.
            .Should().Be("TypeError,DOMException");
    }

    /// <summary>
    /// <c>form.submit()</c> submits without firing <c>submit</c> at all, which is the one thing that
    /// distinguishes it from <c>requestSubmit()</c>.
    /// </summary>
    [Test]
    public async Task FormSubmitSkipsTheSubmitEvent()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<form id='f' action='/go'></form>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let fired = 0;
              const f = document.getElementById('f');
              f.addEventListener('submit', () => { fired++; });
              f.submit();
              return String(fired);
            })()
            """)).Should().Be("0");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-reset — the
    /// <c>reset</c> event is cancelable, and its default action puts every control back to its default.
    /// </summary>
    [Test]
    public async Task AResetButtonFiresACancelableResetEventWhoseDefaultActionRestoresTheDefaults()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f'>
              <input id='t' type='text' value='default'>
              <input id='c' type='checkbox' checked>
              <button id='r' type='reset'>Reset</button>
            </form>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const f = document.getElementById('f');
              const t = document.getElementById('t');
              const c = document.getElementById('c');
              const seen = [];
              const dirty = () => { t.value = 'edited'; c.checked = false; };

              f.addEventListener('reset', e => { seen.push('reset:' + e.cancelable + ':' + e.bubbles); }, { once: true });
              dirty();
              document.getElementById('r').click();
              const afterReset = t.value + ':' + c.checked;

              f.addEventListener('reset', e => e.preventDefault(), { once: true });
              dirty();
              document.getElementById('r').click();
              const afterCancelled = t.value + ':' + c.checked;

              return [seen.join('|'), afterReset, afterCancelled].join(',');
            })()
            """)).Should().Be("reset:true:true,default:true,edited:false");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks-2 — the link's default action is
    /// a navigation the page runs, and a canceled click never gets there.
    /// </summary>
    [Test]
    public async Task AnAnchorFollowsItsHyperlinkUnlessTheClickWasCanceled()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/start.html", "<title>start</title><a id='go' href='/next.html'>Next</a><a id='bare'>Not a link</a>")
            .MapHtml("/next.html", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/start.html"));

        // An <a> with no href has no activation behaviour at all, and a canceled click never reaches the
        // navigation — neither moves the page.
        await fixture.Page.EvaluateAsync(
            "document.getElementById('bare').click();"
            + " document.getElementById('go').addEventListener('click', e => e.preventDefault(), { once: true });"
            + " document.getElementById('go').click();");

        (await fixture.Page.TitleAsync()).Should().Be("start");

        await fixture.NavigateByScriptAsync("document.getElementById('go').click()");
        (await fixture.Page.TitleAsync()).Should().Be("next");
        (await fixture.Page.EvaluateAsync<string>("location.href")).Should().Be(fixture.Url("/next.html"));
    }

    /// <summary>
    /// A link whose <c>target</c> names anything but <c>_self</c> still loads here, because this version opens
    /// no second page — and the page is told rather than left to wonder.
    /// </summary>
    [Test]
    public async Task ATargetedLinkLoadsInTheSamePageAndSaysSo()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/start.html", "<title>start</title><a id='go' href='/next.html' target='_blank'>Next</a>")
            .MapHtml("/next.html", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/start.html"));
        await fixture.NavigateByScriptAsync("document.getElementById('go').click()");

        (await fixture.Page.TitleAsync()).Should().Be("next");
        fixture.Page.Errors.Should().ContainSingle(error => error.Message.Contains("_blank"));
    }

    [Test]
    public async Task ADisabledControlHasNoActivationBehaviour()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <form id='f' action='/go'>
              <input id='c' type='checkbox' disabled>
              <button id='b' type='submit' disabled>Go</button>
            </form>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let submits = 0;
              document.getElementById('f').addEventListener('submit', e => { submits++; e.preventDefault(); });
              document.getElementById('c').click();
              document.getElementById('b').click();
              return [document.getElementById('c').checked, submits].join(',');
            })()
            """))
            // The click still dispatches — a disabled control is not inert to events here — but nothing acts
            // on it. AngleSharp's own IsDisabled is what decides, and the checkbox's pre-activation toggle is
            // rolled back by the same disabled test.
            .Should().Be("false,0");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#the-option-element — selecting an option
    /// changes the select's value and fires <c>input</c> then <c>change</c> at the select.
    /// </summary>
    [Test]
    public async Task ClickingAnOptionSelectsItAndFiresInputThenChangeAtTheSelect()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <select id='s'>
              <option id='one' value='1'>One</option>
              <option id='two' value='2'>Two</option>
            </select>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const s = document.getElementById('s');
              const seen = [];
              s.addEventListener('input', e => seen.push('input:' + e.target.id));
              s.addEventListener('change', e => seen.push('change:' + e.target.id));
              document.getElementById('two').click();
              const after = s.value + ':' + s.selectedIndex;
              document.getElementById('two').click();
              return [after, seen.join('|'), document.getElementById('one').selected].join(',');
            })()
            """))
            // Clicking the already-selected option changes nothing and fires nothing.
            .Should().Be("2:1,input:s|change:s,false");
    }

    /// <summary>
    /// A file input's picker is a host decision, so it becomes a seam a protocol client can answer — CDP's
    /// <c>Page.setInterceptFileChooserDialog</c> (campaign item C5). Until one does, the page is told that it
    /// asked for something nobody answered. A colour or date input's picker has nothing to pick with and is
    /// honestly nothing.
    /// </summary>
    [Test]
    public async Task AFileInputReportsAChooserNobodyAnsweredAndAColourInputDoesNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<input id='f' type='file' name='upload'><input id='c' type='color'><input id='d' type='date'>");

        await page.EvaluateAsync("document.getElementById('c').click(); document.getElementById('d').click()");
        page.Errors.Should().BeEmpty();

        await page.EvaluateAsync("document.getElementById('f').click()");
        page.Errors.Should().ContainSingle(error => error.Message.Contains("file chooser"));
    }

    /// <summary>
    /// With no page behind it the seam still answers: the events bridge records what it was asked for, which
    /// is what a binding-only engine gets and what keeps a submission from silently doing nothing.
    /// </summary>
    [Test]
    public void WithNoPageTheActivationSeamRecordsInsteadOfActing()
    {
        using var fixture = DomTestFixture.Create(
            "<a id='go' href='https://example.com/next'>Next</a>"
            + "<form id='f' action='/post'><button id='b' type='submit'>Go</button></form>");

        fixture.Engine.Evaluate("document.getElementById('go').click(); document.getElementById('b').click()");

        BrowserTestAccess.PendingActivations(fixture.Engine).Should().Equal(
            "Navigation https://example.com/next",
            "FormSubmission /post (b)");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#checkbox-state-(type=checkbox) — step 1 of the
    /// checkbox and radio activation behaviours is "if the element is not connected, then return", so a
    /// detached control toggles silently.
    /// </summary>
    /// <remarks>
    /// <b>Connected is the shadow-including root being a document</b>, not the node having a parent, which is
    /// the half a page cannot get wrong and an implementation can: a control inside a shadow tree of a
    /// connected host is connected. AngleSharp has no member that answers the question — <c>INode.Owner</c>
    /// is the node document whether or not the node is in it — so the walk is the package's own and this is
    /// what holds it to the definition.
    /// </remarks>
    [Test]
    public async Task OnlyAConnectedCheckboxAnnouncesItsToggle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];

              function watch(name, input) {
                input.addEventListener('input', () => seen.push(name + ':input'));
                input.addEventListener('change', () => seen.push(name + ':change'));
                input.click();
                seen.push(name + ':checked=' + input.checked);
              }

              const detached = document.createElement('input');
              detached.type = 'checkbox';
              watch('detached', detached);

              // A detached *subtree* is still detached, however deep the control sits in it.
              const orphanTree = document.createElement('div');
              const inTree = document.createElement('input');
              inTree.type = 'checkbox';
              orphanTree.appendChild(inTree);
              watch('inOrphanTree', inTree);

              const connected = document.createElement('input');
              connected.type = 'checkbox';
              document.body.appendChild(connected);
              watch('connected', connected);

              // Inside a shadow tree of a connected host: the shadow-including root is the document.
              const shadow = document.getElementById('host').attachShadow({ mode: 'closed' });
              const inShadow = document.createElement('input');
              inShadow.type = 'checkbox';
              shadow.appendChild(inShadow);
              watch('inShadow', inShadow);

              return seen.join(',');
            })()
            """))
            .Should().Be(
                "detached:checked=true,"
                + "inOrphanTree:checked=true,"
                + "connected:input,connected:change,connected:checked=true,"
                + "inShadow:input,inShadow:change,inShadow:checked=true");
    }
}

using Jint.Browser;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Focus without a layout — https://html.spec.whatwg.org/multipage/interaction.html#focus.
/// </summary>
public sealed class FocusTests
{
    [TestCase("+00020")]
    [TestCase("-2147483648")]
    [TestCase("2147483648")]
    [TestCase("-2147483649")]
    [TestCase(" \t+12\u00a0")]
    [TestCase("12\0\0")]
    [TestCase("12x")]
    public void BoundedFocusabilityPreservesTheExistingTabIndexClassification(string value)
    {
        var realm = DomRealm.Of(new Engine());
        var element = global::Jint.Browser.Accessibility.ContentDom.ElementById(
            global::Jint.Browser.Accessibility.ContentDom.Parse("<div id=target>"), "target")!;
        element.SetAttribute("tabindex", value);
        var expected = FocusController.IsFocusable(realm, element);
        var work = new DomReadWork(realm.NativeReadCheckpoint, CancellationToken.None);
        FocusController.IsFocusable(realm, element, work).Should().Be(expected);
    }

    [Test]
    public void BoundedFocusabilityPollsDuringTheActualAttributeScan()
    {
        var realm = DomRealm.Of(new Engine());
        var element = global::Jint.Browser.Accessibility.ContentDom.ElementById(
            global::Jint.Browser.Accessibility.ContentDom.Parse("<div id=target>"), "target")!;
        for (var i = 0; i < 1024; i++) element.SetAttribute("data-" + i, "value");
        var checks = new List<int>();
        var work = new DomReadWork(units =>
        {
            checks.Add(units);
            if (checks.Count == 2) throw new OperationCanceledException();
        }, CancellationToken.None);
        Action read = () => FocusController.IsFocusable(realm, element, work);
        read.Should().ThrowExactly<OperationCanceledException>();
        checks.Should().Equal(0, 256, "the second check must occur in a bounded attribute batch rather than after the scan");
    }

    [Test]
    public async Task ShadowActiveElementChecksCancellationDuringInitialConnectivityWalk()
    {
        var options = new BrowserOptions().ConfigureEngine(o => o.AddConstraint(static () => new CancelFocusRead()));
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host></div>");
        await page.EvaluateAsync(
            """
            window.savedRoot = document.getElementById('host').attachShadow({mode: 'closed'});
            let parent = savedRoot;
            for (let i = 0; i < 1024; i++) {
              const child = document.createElement('div');
              parent.appendChild(child);
              parent = child;
            }
            window.savedInput = document.createElement('input');
            parent.appendChild(savedInput);
            savedInput.focus();
            """);

        await page.RunOnLoopAsync(engine =>
        {
            var root = (ShadowRoot) ((IDomWrapper) engine.GetValue("savedRoot")).DomTarget;
            var input = ((IDomWrapper) engine.GetValue("savedInput")).DomTarget;
            var realm = BrowserEventRealm.Of(engine);
            root.Host.ParentNode!.RemoveChild(root.Host);
            realm.FocusedElement.Should().BeSameAs(input);
            var constraint = engine.Constraints.Find<CancelFocusRead>()!;
            constraint.Armed = true;
            try
            {
                Action read = () => FocusController.ActiveElement(realm, root);
                read.Should().ThrowExactly<OperationCanceledException>();
                constraint.Checks.Should().Be(2, "the initial check and the first bounded ancestor batch must both poll");
                realm.FocusedElement.Should().BeSameAs(input, "cancellation must interrupt connectivity before stale focus is cleared");
            }
            finally
            {
                constraint.Armed = false;
            }
            FocusController.ActiveElement(realm, root).Should().BeNull();
            realm.FocusedElement.Should().BeNull();
            return true;
        });
    }

    [Test]
    public async Task ShallowDetachedFocusIsNotClearedWhenTheFinalReadCheckCancels()
    {
        var options = new BrowserOptions().ConfigureEngine(o => o.AddConstraint(static () => new CancelFocusRead()));
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=host></div>");
        await page.EvaluateAsync("window.savedRoot = host.attachShadow({mode: 'closed'}); window.savedInput = document.createElement('input'); savedRoot.appendChild(savedInput); savedInput.focus();");
        await page.RunOnLoopAsync(engine =>
        {
            var root = (ShadowRoot) ((IDomWrapper) engine.GetValue("savedRoot")).DomTarget;
            var input = ((IDomWrapper) engine.GetValue("savedInput")).DomTarget;
            var realm = BrowserEventRealm.Of(engine);
            root.Host.ParentNode!.RemoveChild(root.Host);
            var constraint = engine.Constraints.Find<CancelFocusRead>()!;
            constraint.Armed = true;
            try
            {
                Action read = () => FocusController.ActiveElement(realm, root);
                read.Should().ThrowExactly<OperationCanceledException>();
                constraint.Checks.Should().Be(2);
                realm.FocusedElement.Should().BeSameAs(input);
            }
            finally { constraint.Armed = false; }
            FocusController.ActiveElement(realm, root).Should().BeNull();
            realm.FocusedElement.Should().BeNull();
            return true;
        });
    }

    [Test]
    public async Task ActiveElementPollsDocumentCommentPrefixWhileFindingItsFallbackBody()
    {
        var options = new BrowserOptions().ConfigureEngine(o => o.AddConstraint(static () => new CancelFocusRead()));
        await using var browser = new Browser(options);
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p>content</p>");
        await page.RunOnLoopAsync(engine =>
        {
            var document = DomRealm.Of(engine).Document!;
            var html = document.DocumentElement!;
            for (var i = 0; i < 1024; i++) document.InsertBefore(document.CreateComment("prefix"), html);
            var constraint = engine.Constraints.Find<CancelFocusRead>()!;
            constraint.CancelAt = int.MaxValue;
            constraint.Armed = true;
            try
            {
                FocusController.ActiveElement(BrowserEventRealm.Of(engine), document).Should().BeSameAs(DomDocumentElements.Body(document));
                constraint.Checks.Should().BeGreaterThanOrEqualTo(6, "the 1024 document-prefix links require four bounded checks between entry and publication");
            }
            finally { constraint.Armed = false; }
            return true;
        });
    }

    private sealed class CancelFocusRead : Constraint
    {
        internal bool Armed;
        internal int Checks;
        internal int CancelAt = 2;
        public override void Check()
        {
            if (Armed && ++Checks == CancelAt) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }

    [TestCase("open")]
    [TestCase("closed")]
    public async Task ShadowActiveElementRetargetsNestedFocusAndClearsAfterHostRemoval(string mode)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=outer></div><div id=unrelated></div>");
        (await page.EvaluateAsync<string>(
            $$"""
            (() => {
              const outer = document.getElementById('outer');
              const first = outer.attachShadow({ mode: '{{mode}}' });
              const nested = document.createElement('div');
              first.appendChild(nested);
              const second = nested.attachShadow({ mode: '{{mode}}' });
              const input = document.createElement('input');
              second.appendChild(input);
              const unrelated = document.getElementById('unrelated').attachShadow({ mode: '{{mode}}' });
              input.focus();
              const result = [document.activeElement === outer, first.activeElement === nested,
                              second.activeElement === input, unrelated.activeElement === null];
              outer.remove();
              result.push(first.activeElement === null, second.activeElement === null);
              return result.join('|');
            })()
            """)).Should().Be("true|true|true|true|true|true");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("open")]
    [TestCase("closed")]
    public async Task DocumentActiveElementRetargetsShadowFocusToTheHost(string mode)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");
        (await page.EvaluateAsync<string>(
            $$"""
            (() => {
              const host = document.getElementById('host');
              const shadow = host.attachShadow({ mode: '{{mode}}' });
              const input = document.createElement('input');
              shadow.appendChild(input);
              input.focus();
              const focused = document.activeElement.id;
              input.blur();
              return focused + ':' + document.activeElement.tagName;
            })()
            """)).Should().Be("host:BODY");
    }

    [TestCase("open")]
    [TestCase("closed")]
    public async Task KeyboardInputReachesTheFocusedShadowControl(string mode)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='host'></div>");
        await page.EvaluateAsync(
            $$"""
            const shadow = document.getElementById('host').attachShadow({ mode: '{{mode}}' });
            window.shadowInput = document.createElement('input');
            shadow.appendChild(shadowInput);
            shadowInput.focus();
            """);
        await BrowserTestAccess.DispatchKeyAsync(page, "x");
        (await page.EvaluateAsync<string>("document.activeElement.id + ':' + shadowInput.value"))
            .Should().Be("host:x");
    }

    [Test]
    public async Task DetachedInputCannotTakeFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const a = document.getElementById('a');
              a.focus();
              const detached = document.createElement('input');
              let seen = 0;
              detached.addEventListener('focus', () => seen++);
              detached.focus();
              return document.activeElement.id + ':' + seen;
            })()
            """)).Should().Be("a:0");
    }

    [Test]
    public async Task FocusListenerRedirectsFocusWithoutFinishingTheSupersededTransition()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'><input id='b'><input id='c'>");
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const a = document.getElementById('a');
              const b = document.getElementById('b');
              const c = document.getElementById('c');
              const seen = [];
              for (const type of ['blur', 'focusout', 'focus', 'focusin']) {
                document.addEventListener(type, e => seen.push(type + ':' + e.target.id), true);
              }
              a.focus();
              seen.length = 0;
              b.addEventListener('focus', () => c.focus());
              b.focus();
              return document.activeElement.id + '|' + seen.join(',');
            })()
            """)).Should().Be("c|blur:a,focusout:a,focus:b,blur:b,focusout:b,focus:c,focusin:c");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#focus-update-steps — the old chain first, so
    /// <c>blur</c> then <c>focusout</c>, then the new chain's <c>focus</c> then <c>focusin</c>. The first two
    /// of each pair do not bubble and the second two do.
    /// </summary>
    [Test]
    public async Task MovingFocusFiresBlurFocusoutFocusAndFocusinInThatOrder()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='box'><input id='a'><input id='b'></div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const seen = [];
              const box = document.getElementById('box');
              const a = document.getElementById('a');
              const b = document.getElementById('b');

              // Capturing on the container catches the non-bubbling pair too, which is the only way to see
              // `focus` and `blur` from an ancestor.
              for (const type of ['focus', 'blur', 'focusin', 'focusout']) {
                box.addEventListener(type, e =>
                  seen.push(type + ':' + e.target.id + ':' + (e.relatedTarget ? e.relatedTarget.id : 'null') + ':' + e.bubbles), true);
              }

              a.focus();
              const first = document.activeElement.id;
              b.focus();
              const second = document.activeElement.id;
              b.blur();
              return [first, second, document.activeElement.tagName, seen.join('|')].join(',');
            })()
            """)).Should().Be(
            "a,b,BODY," +
            "focus:a:null:false|focusin:a:null:true|" +
            "blur:a:b:false|focusout:a:b:true|focus:b:a:false|focusin:b:a:true|" +
            "blur:b:null:false|focusout:b:null:true");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#dom-document-activeelement — the body when
    /// nothing is focused, which AngleSharp's own <c>ActiveElement</c> never answers because it never assigns
    /// one.
    /// </summary>
    [Test]
    public async Task ActiveElementIsTheBodyUntilSomethingTakesFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            [
              document.activeElement.tagName,
              document.hasFocus(),
              (document.getElementById('a').focus(), document.activeElement.id),
              (document.getElementById('a').blur(), document.activeElement.tagName)
            ].join(',')
            """)).Should().Be("BODY,true,a,BODY");
    }

    /// <summary>
    /// A document made inside a page's realm has no browsing context of its own. Its focus and visibility
    /// queries therefore cannot observe or replace the state of the document the page is displaying.
    /// </summary>
    [Test]
    public async Task InertDocumentsDoNotReadOrChangeTheDisplayedDocumentsFocusAndVisibility()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='page'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const pageInput = document.getElementById('page');
              pageInput.focus();

              const parsed = new DOMParser().parseFromString('<input id="other">', 'text/html');
              const constructed = document.implementation.createHTMLDocument('other');
              constructed.body.innerHTML = '<input id="other">';

              const answers = [];
              for (const other of [parsed, constructed]) {
                const input = other.getElementById('other');
                const seen = [];
                input.addEventListener('focus', () => seen.push('focus'));
                input.addEventListener('blur', () => seen.push('blur'));

                answers.push([
                  other.activeElement.tagName,
                  other.hasFocus(),
                  other.visibilityState,
                  other.hidden,
                ].join(':'));

                input.focus();
                input.blur();
                answers.push(other.activeElement.tagName + ':' + seen.join(','));
              }

              answers.push([
                document.activeElement.id,
                document.hasFocus(),
                document.visibilityState,
                document.hidden,
              ].join(':'));
              return answers.join('|');
            })()
            """)).Should().Be(
            "BODY:false:hidden:true|BODY:|BODY:false:hidden:true|BODY:|page:true:visible:false");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// Focusability without a rendering: the element's own kind, or a <c>tabindex</c> content attribute.
    /// AngleSharp's <c>TabIndex</c> cannot decide it — it answers 0 for every element, including a bare
    /// <c>&lt;div&gt;</c>, where HTML says −1.
    /// </summary>
    [Test]
    public async Task OnlyAFocusableElementTakesFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <div id='plain'>text</div>
            <div id='indexed' tabindex='0'>text</div>
            <div id='negative' tabindex='-1'>text</div>
            <a id='link' href='#x'>link</a>
            <a id='anchor'>no href</a>
            <button id='button'>go</button>
            <button id='disabled' disabled>no</button>
            <input id='hiddenInput' type='hidden'>
            <select id='select'></select>
            <textarea id='textarea'></textarea>
            <details><summary id='summary'>s</summary></details>
            <span id='hidden' tabindex='0' hidden>x</span>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const ids = ['plain', 'indexed', 'negative', 'link', 'anchor', 'button', 'disabled',
                           'hiddenInput', 'select', 'textarea', 'summary', 'hidden'];
              return ids.map(id => {
                document.body.focus();
                document.activeElement && document.activeElement.blur && document.activeElement.blur();
                document.getElementById(id).focus();
                return document.activeElement.id === id ? id : '-';
              }).join(',');
            })()
            """)).Should().Be("-,indexed,negative,link,-,button,-,-,select,textarea,summary,-");
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interaction.html#the-autofocus-attribute — the first focusable
    /// element asking for it takes focus once the document has parsed.
    /// </summary>
    [Test]
    public async Task AutofocusTakesEffectOnLoad()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <div id='before' autofocus>not focusable</div>
            <input id='first' autofocus>
            <input id='second' autofocus>
            <script>
              window.log = [];
              document.addEventListener('focusin', e => window.log.push('focusin:' + e.target.id));
            </script>
            """);

        (await page.EvaluateAsync<string>("[document.activeElement.id, window.log.join('|')].join(',')"))
            .Should().Be("first,focusin:first");
    }

    [Test]
    public async Task AutofocusDoesNothingWhenTheDocumentAlreadyMovedFocus()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <input id='a'>
            <input id='b' autofocus>
            <script>document.getElementById('a').focus();</script>
            """);

        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");
    }

    /// <summary>
    /// A focused element taken out of the tree stops being the active element, which is what HTML's "no longer
    /// being rendered" clause amounts to with no rendering.
    /// </summary>
    [Test]
    public async Task RemovingTheFocusedElementReturnsFocusToTheBody()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const a = document.getElementById('a');
              a.focus();
              const before = document.activeElement.id;
              a.remove();
              return [before, document.activeElement.tagName].join(',');
            })()
            """)).Should().Be("a,BODY");
    }

    [Test]
    public async Task FocusingTheAlreadyFocusedElementFiresNothing()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id='a'>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              let count = 0;
              const a = document.getElementById('a');
              a.addEventListener('focus', () => count++);
              a.focus();
              a.focus();
              a.focus();
              return String(count);
            })()
            """)).Should().Be("1");
    }

    /// <summary>
    /// A click focuses what was clicked, and a click on a non-focusable descendant focuses the nearest
    /// focusable ancestor — which is why clicking the text inside a button focuses the button.
    /// </summary>
    [Test]
    public async Task AClickThroughTheInputDispatcherFocusesTheNearestFocusableAncestor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<button id='b'><span id='label'>Go</span></button><p id='p'>text</p>");

        await BrowserTestAccess.DispatchClickAsync(page, "label");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");

        // Clicking something with no focusable ancestor leaves focus where it was, which is a browser's
        // behaviour for a click on inert text.
        await BrowserTestAccess.DispatchClickAsync(page, "p");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");
    }

    /// <summary>
    /// A click from the input dispatcher is trusted, because a protocol client driving a page stands in for a
    /// user; <c>element.click()</c> is not.
    /// </summary>
    [Test]
    public async Task AClickFromTheInputDispatcherIsTrustedAndActivates()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <input id='c' type='checkbox'>
            <script>
              window.log = [];
              document.getElementById('c').addEventListener('click', e => window.log.push(e.isTrusted + ':' + e.detail));
            </script>
            """);

        await BrowserTestAccess.DispatchClickAsync(page, "c");

        (await page.EvaluateAsync<string>("[document.getElementById('c').checked, document.activeElement.id, window.log.join('|')].join(',')"))
            .Should().Be("true,c,true:1");
    }
}

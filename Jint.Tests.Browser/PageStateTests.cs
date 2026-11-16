using System.Text.RegularExpressions;
using Jint.Tests.Browser.Navigation;
using NativeBrowser = Jint.Browser.Browser;

namespace Jint.Tests.Browser;

/// <summary>
/// The native URL, attribute, focus, value and checkedness readers and waits (#4626): no JavaScript
/// predicate, and the state a test asserts about rather than the markup it started from.
/// </summary>
public sealed class PageStateTests
{
    // A wedge ceiling, never an assertion: a wait expected to fail is given TimeSpan.Zero instead, which answers
    // after exactly one look.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Test]
    public async Task TheIssueReproNeedsNoJavaScriptPredicate()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><input id=c type=checkbox><button id=b>Next</button>
            <script>setTimeout(() => { history.pushState(null, '', '?page=2'); c.checked = true; b.setAttribute('aria-current', 'page'); b.focus(); }, 200)</script>
            """, "http://127.0.0.1/");

        var url = page.WaitForUrlAsync("?page=2", Patience);
        var urlPattern = page.WaitForUrlAsync(new Regex(@"\?page=2$"), Patience);
        var urlPredicate = page.WaitForUrlAsync(uri => uri.Query == "?page=2", Patience);
        var isChecked = page.WaitForCheckedAsync("#c", Patience);
        var attribute = page.WaitForAttributeAsync("#b", "aria-current", "page", Patience);
        var focus = page.WaitForFocusAsync("#b", Patience);

        (await url).Should().BeTrue();
        (await urlPattern).Should().BeTrue();
        (await urlPredicate).Should().BeTrue();
        (await isChecked).Should().BeTrue();
        (await attribute).Should().BeTrue();
        (await focus).Should().BeTrue();

        page.Url.Should().Be("http://127.0.0.1/?page=2");
        (await page.IsCheckedAsync("#c")).Should().BeTrue();
        (await page.GetAttributeAsync("#b", "aria-current")).Should().Be("page");
        (await page.IsFocusedAsync("#b")).Should().BeTrue();
        (await page.IsFocusedAsync("#c")).Should().BeFalse();
    }

    [Test]
    public async Task UrlWaitsFollowReplaceStateFragmentsAndRealNavigations()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<a id=next href='/next?x=1'>next</a>")
            .MapHtml("/next", "<p>next</p>"));
        var page = fixture.Page;
        var fromBlank = page.WaitForUrlAsync("/", Patience);
        await page.NavigateAsync(fixture.Url("/"));
        (await fromBlank).Should().BeTrue("a relative URL started on about:blank still matches once the page has an origin");

        (await page.WaitForUrlAsync(fixture.Url("/"), TimeSpan.Zero)).Should().BeTrue();
        (await page.WaitForUrlAsync("/", TimeSpan.Zero)).Should().BeTrue("a relative URL resolves against the URL it is compared with");
        (await page.WaitForUrlAsync("/elsewhere", TimeSpan.Zero)).Should().BeFalse();

        var replaced = page.WaitForUrlAsync("/replaced", Patience);
        await page.EvaluateAsync("history.replaceState(null, '', '/replaced')");
        (await replaced).Should().BeTrue();

        var fragment = page.WaitForUrlAsync(new Regex("#part$"), Patience);
        await page.EvaluateAsync("location.hash = 'part'");
        (await fragment).Should().BeTrue();

        var navigated = page.WaitForUrlAsync(uri => uri.AbsolutePath == "/next" && uri.Query == "?x=1", Patience);
        await page.ClickAsync("#next");
        (await navigated).Should().BeTrue();
        page.Url.Should().Be(fixture.Url("/next?x=1"));
    }

    [Test]
    public async Task AttributeWaitsDistinguishAbsentFromEmptyAndMatchPatterns()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id=d data-State=open aria-busy=''></div>");

        (await page.GetAttributeAsync("#d", "DATA-STATE")).Should().Be("open", "an HTML element's attribute names are lowercased, as getAttribute's are");
        (await page.GetAttributeAsync("#d", "aria-busy")).Should().BeEmpty();
        (await page.GetAttributeAsync("#d", "title")).Should().BeNull();
        (await page.GetAttributeAsync("#missing", "title")).Should().BeNull();

        (await page.WaitForAttributeAsync("#d", "title", value: null, TimeSpan.Zero)).Should().BeTrue();
        (await page.WaitForAttributeAsync("#d", "aria-busy", value: null, TimeSpan.Zero)).Should().BeFalse("an empty attribute is present");
        (await page.WaitForAttributeAsync("#d", "aria-busy", "", TimeSpan.Zero)).Should().BeTrue();
        (await page.WaitForAttributeAsync("#missing", "title", value: null, TimeSpan.Zero)).Should().BeFalse("a missing element has no attribute to be absent");
        (await page.WaitForAttributeAsync("#d", "data-state", new Regex("^op"), TimeSpan.Zero)).Should().BeTrue();
        (await page.WaitForAttributeAsync("#d", "title", new Regex(".*"), TimeSpan.Zero)).Should().BeFalse("a pattern needs the attribute to be present");

        var removed = page.WaitForAttributeAsync("#d", "data-state", value: null, Patience);
        var changed = page.WaitForAttributeAsync("#d", "aria-busy", new Regex("^true$"), Patience);
        await page.EvaluateAsync("setTimeout(() => { d.removeAttribute('data-state'); d.setAttribute('aria-busy', 'true'); }, 50)");
        (await removed).Should().BeTrue();
        (await changed).Should().BeTrue();
    }

    [Test]
    public async Task ValueReadersSeeIdlOnlyChangesOfEveryControl()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <input id=i value=initial><textarea id=t>text</textarea>
            <select id=s><option value=a>A</option><option value=b selected>B</option></select>
            <div id=d>not a control</div>
            """);

        (await page.InputValueAsync("#i")).Should().Be("initial");
        (await page.InputValueAsync("#t")).Should().Be("text");
        (await page.InputValueAsync("#s")).Should().Be("b");
        (await page.InputValueAsync("#d")).Should().BeNull();
        (await page.InputValueAsync("#missing")).Should().BeNull();
        (await page.WaitForValueAsync("#d", "", TimeSpan.Zero)).Should().BeFalse();

        var input = page.WaitForValueAsync("#i", "scripted", Patience);
        var textarea = page.WaitForValueAsync("#t", new Regex("^new"), Patience);
        var select = page.WaitForValueAsync("#s", "a", Patience);
        await page.EvaluateAsync("setTimeout(() => { i.value = 'scripted'; t.value = 'new text'; s.value = 'a'; }, 50)");
        (await input).Should().BeTrue();
        (await textarea).Should().BeTrue();
        (await select).Should().BeTrue();
        (await page.GetAttributeAsync("#i", "value")).Should().Be("initial", "the IDL value moved, not the content attribute");

        var typed = page.WaitForValueAsync("#i", "typed", Patience);
        (await page.FillAsync("#i", "typed")).Should().BeTrue();
        (await typed).Should().BeTrue();
    }

    [Test]
    public async Task CheckedReadersSeeIdlOnlyChangesRadiosAndAriaRoles()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <input id=c type=checkbox><input id=r1 type=radio name=g checked><input id=r2 type=radio name=g>
            <div id=sw role="switch other" aria-checked=true></div><div id=plain aria-checked=true></div>
            <div id=fallback role="x-toggle checkbox" aria-checked=TRUE></div><div id=mixed role=checkbox aria-checked=mixed></div>
            <input id=text><input id=tri type=checkbox checked>
            """);
        await page.EvaluateAsync("tri.indeterminate = true");

        (await page.IsCheckedAsync("#c")).Should().BeFalse();
        (await page.IsCheckedAsync("#r1")).Should().BeTrue();
        (await page.IsCheckedAsync("#sw")).Should().BeTrue();
        (await page.IsCheckedAsync("#plain")).Should().BeNull("aria-checked means nothing without a role that takes it");
        (await page.IsCheckedAsync("#fallback")).Should().BeTrue("the first role token ARIA recognizes is the explicit role");
        (await page.IsCheckedAsync("#mixed")).Should().BeNull("mixed is neither checked nor unchecked");
        (await page.WaitForCheckedAsync("#mixed", TimeSpan.Zero, isChecked: false)).Should().BeFalse();
        (await page.IsCheckedAsync("#tri")).Should().BeTrue("indeterminate does not change a native control's checkedness");
        (await page.IsCheckedAsync("#text")).Should().BeNull();
        (await page.IsCheckedAsync("#missing")).Should().BeNull();
        (await page.WaitForCheckedAsync("#missing", TimeSpan.Zero, isChecked: false)).Should().BeFalse("a missing element is not unchecked");
        (await page.WaitForCheckedAsync("#text", TimeSpan.Zero, isChecked: false)).Should().BeFalse();

        var box = page.WaitForCheckedAsync("#c", Patience);
        var radioOff = page.WaitForCheckedAsync("#r1", Patience, isChecked: false);
        var switchOff = page.WaitForCheckedAsync("#sw", Patience, isChecked: false);
        await page.EvaluateAsync("setTimeout(() => { c.checked = true; r2.checked = true; sw.setAttribute('aria-checked', 'false'); }, 50)");
        (await box).Should().BeTrue();
        (await radioOff).Should().BeTrue("checking a radio button unchecks the rest of its group");
        (await switchOff).Should().BeTrue();
        (await page.GetAttributeAsync("#c", "checked")).Should().BeNull("checked = true changes no content attribute");

        var cleared = page.WaitForCheckedAsync("#c", Patience, isChecked: false);
        (await page.ClickAsync("#c")).Should().BeTrue();
        (await cleared).Should().BeTrue();
    }

    [Test]
    public async Task FocusWaitsFollowFocusBlurAndRemoval()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id=a><input id=b><div id=host></div>");
        await page.EvaluateAsync("host.attachShadow({mode:'open'}).innerHTML = '<input id=inner>'");

        (await page.IsFocusedAsync("#a")).Should().BeFalse();
        (await page.IsFocusedAsync("#missing")).Should().BeFalse();
        (await page.WaitForFocusAsync("#missing", TimeSpan.Zero, focused: false)).Should().BeTrue();

        var focused = page.WaitForFocusAsync("#a", Patience);
        await page.EvaluateAsync("setTimeout(() => a.focus(), 50)");
        (await focused).Should().BeTrue();

        var left = page.WaitForFocusAsync("#a", Patience, focused: false);
        var arrived = page.WaitForFocusAsync("#b", Patience);
        await page.EvaluateAsync("setTimeout(() => b.focus(), 50)");
        (await left).Should().BeTrue();
        (await arrived).Should().BeTrue();

        var removed = page.WaitForFocusAsync("#b", Patience, focused: false);
        await page.EvaluateAsync("setTimeout(() => b.remove(), 50)");
        (await removed).Should().BeTrue();

        await page.EvaluateAsync("host.shadowRoot.querySelector('input').focus()");
        (await page.IsFocusedAsync("#host")).Should().BeFalse("the host is document.activeElement, but the inner control holds focus");
    }

    [Test]
    public async Task ElementWaitsObserveNavigation()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<input id=c type=checkbox><input id=v><p id=p>")
            .MapHtml("/next", "<input id=c type=checkbox checked><input id=v value=next><p id=p data-x=next><input id=f autofocus>"));
        var page = fixture.Page;
        await page.NavigateAsync(fixture.Url("/"));

        var isChecked = page.WaitForCheckedAsync("#c", Patience);
        var value = page.WaitForValueAsync("#v", "next", Patience);
        var attribute = page.WaitForAttributeAsync("#p", "data-x", "next", Patience);
        await page.NavigateAsync(fixture.Url("/next"));
        (await isChecked).Should().BeTrue();
        (await value).Should().BeTrue();
        (await attribute).Should().BeTrue();
    }

    [Test]
    public async Task CallerPredicatesSeeADetachedUriAndTheirFailuresFaultTheWait()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<p id=p data-x=1>", "http://127.0.0.1/");

        Uri? seen = null;
        (await page.WaitForUrlAsync(uri => { seen = uri; return true; }, TimeSpan.Zero)).Should().BeTrue();
        seen.Should().Be(new Uri("http://127.0.0.1/"));

        var failing = () => page.WaitForUrlAsync(_ => throw new InvalidOperationException("caller"), Patience);
        (await failing.Should().ThrowAsync<InvalidOperationException>()).WithMessage("caller");
        (await page.EvaluateAsync<int>("1 + 1")).Should().Be(2, "a caller's failure leaves the page running");
    }

    [Test]
    public async Task WaitsOnAClosedPageAnswerFalse()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<input id=c type=checkbox>");
        var waiting = page.WaitForCheckedAsync("#c", Patience);
        await page.CloseAsync();
        (await waiting).Should().BeFalse();
        var reading = () => page.IsCheckedAsync("#c");
        await reading.Should().ThrowAsync<ObjectDisposedException>();
    }
}

using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <see cref="Page.FocusAsync"/>, <see cref="Page.BlurAsync"/> and the targeted
/// <see cref="Page.PressAsync(string, string, KeyModifiers, int, NavigationOptions, TimeSpan?)"/>.
/// </summary>
/// <remarks>
/// What these hold is that the members run the focusing steps <c>element.focus()</c> runs — the same four
/// events, the same focusability, the same <c>inert</c> — that they wait for a focusable match rather than
/// answering for one that is not there yet, and that a targeted key reaches the target rather than the body.
/// </remarks>
public sealed class PageFocusTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Test]
    public async Task TheIssueReproFocusesTheTargetAndDeliversTheKeyToIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id=a><input id=b onkeydown=\"if(event.key==='Escape')document.title='esc'\">", "http://127.0.0.1/");

        (await page.FocusAsync("#b")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");

        (await page.PressAsync("#b", "Escape")).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("esc");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("b");
    }

    [Test]
    public async Task FocusRunsTheFocusUpdateStepsWithTrustedEventsInHtmlsOrder()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><input id=a><input id=b>
            <script>
            window.log=[];
            for(const type of ['focus','blur','focusin','focusout'])
                document.addEventListener(type,e=>log.push(e.type+':'+e.target.id+':'+(e.relatedTarget?.id??'')+':'+e.isTrusted),true);
            </script>
            """);

        (await page.FocusAsync("#a")).Should().BeTrue();
        (await page.FocusAsync("#b")).Should().BeTrue();
        (await page.FocusAsync("#b")).Should().BeTrue("focusing the focused element is not a failure");

        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be(
            "focus:a::true focusin:a::true "
            + "blur:a:b:true focusout:a:b:true focus:b:a:true focusin:b:a:true");
    }

    [Test]
    public async Task TabindexMakesAnElementFocusableAndItsAbsenceDoesNot()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id=plain>text</div><div id=minus tabindex=-1>minus</div><div id=zero tabindex=0>zero</div>
            <div id=junk tabindex=x>junk</div><a id=nohref>no href</a><a id=link href=#>link</a>
            """);

        (await page.FocusAsync("#plain", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.FocusAsync("#junk", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.FocusAsync("#nohref", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");

        (await page.FocusAsync("#minus")).Should().BeTrue("a negative tabindex leaves the element focusable by other means");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("minus");
        (await page.FocusAsync("#zero")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("zero");
        (await page.FocusAsync("#link")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("link");
    }

    [Test]
    public async Task DisabledHiddenAndInertMatchesAreNotFocused()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=disabled disabled>d</button><button id=hidden hidden>h</button>
            <button id=inert inert>i</button>
            <div inert><section><button id=nested>n</button><div id=nestedTab tabindex=0>t</div></section></div>
            <fieldset disabled><input id=fieldset></fieldset>
            <script>window.focused=0;document.addEventListener('focusin',()=>focused++);</script>
            """);

        foreach (var target in new[] { "#disabled", "#hidden", "#inert", "#nested", "#nestedTab", "#fieldset" })
        {
            (await page.FocusAsync(target, timeout: TimeSpan.Zero)).Should().BeFalse(target);
        }

        (await page.EvaluateAsync<int>("focused")).Should().Be(0);
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");
    }

    [Test]
    public async Task ScriptFocusAndTabOrderHonourAnInertAncestor()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=first>1</button>
            <div id=backdrop inert><button id=behind>2</button><span><input id=deep></span></div>
            <button id=last>3</button>
            """);

        // element.focus() and Page.FocusAsync are the same focusing steps, so both refuse an inert subtree.
        await page.EvaluateAsync("behind.focus(); deep.focus()");
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");

        await page.PressAsync("Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("first");
        await page.PressAsync("Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("last", "Tab skips the inert subtree");

        await page.EvaluateAsync("backdrop.removeAttribute('inert')");
        (await page.FocusAsync("#deep")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("deep");
    }

    [Test]
    public async Task AnInertShadowHostMakesItsShadowTreeUnfocusable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><div id=host inert></div>
            <script>
            const button=host.attachShadow({mode:'open'}).appendChild(document.createElement('button'));
            button.focus();
            window.focusedInside=host.shadowRoot.activeElement!==null;
            </script>
            """);

        (await page.EvaluateAsync<bool>("focusedInside")).Should().BeFalse();
    }

    [Test]
    public async Task FocusWaitsForATargetThatIsNotThereYet()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <script>setTimeout(()=>{const i=document.createElement('input');i.id='late';document.body.append(i);},100)</script>
            """);

        (await page.FocusAsync("#late", timeout: Patience)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("late");
    }

    [Test]
    public async Task FocusWaitsForAMatchToBecomeFocusable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=save disabled>Save</button><div id=backdrop inert><input id=field></div>
            <script>setTimeout(()=>{save.disabled=false;backdrop.removeAttribute('inert');},100)</script>
            """);

        (await page.FocusAsync("#save", timeout: Patience)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("save");
        (await page.FocusAsync("#field", timeout: Patience)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("field");
    }

    [Test]
    public async Task NothingMatchingAnswersFalseOnceTheTimeoutWins()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id=a><div id=plain>plain</div>");

        (await page.FocusAsync("#missing", timeout: TimeSpan.FromMilliseconds(100))).Should().BeFalse();
        (await page.FocusAsync("#plain", timeout: TimeSpan.FromMilliseconds(100))).Should().BeFalse();
        (await page.FocusAsync("not a [selector", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.BlurAsync("#missing", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.PressAsync("#missing", "a", timeout: TimeSpan.Zero)).Should().BeFalse();
    }

    [Test]
    public async Task ANegativeTimeoutIsRefused()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await FluentActions.Awaiting(() => page.FocusAsync("#a", timeout: TimeSpan.FromSeconds(-1)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => page.BlurAsync("#a", timeout: TimeSpan.FromSeconds(-1)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => page.PressAsync("#a", "a", timeout: TimeSpan.FromSeconds(-1)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => page.PressAsync("#a", "a", (KeyModifiers) 64))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task IndexAndSnapshotReferencesNameTheSameElements()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><button id=one>One</button><button id=two>Two</button><button id=three>Three</button>");

        (await page.FocusAsync("button", 1)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("two");
        (await page.FocusAsync("button", -1)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("three");

        var snapshot = await page.AccessibilitySnapshotAsync(includeReferences: true);
        var line = snapshot.Split('\n').Single(l => l.Contains("\"One\"", StringComparison.Ordinal));
        var reference = line[line.IndexOf("[ref=", StringComparison.Ordinal)..].Trim('[', ']', ' ', '\r');
        (await page.FocusAsync(reference)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("one");
    }

    [Test]
    public async Task BlurTakesFocusAwayAndLeavesAnUnfocusedElementAlone()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><input id=a><input id=b>
            <script>
            window.log=[];
            for(const type of ['blur','focusout'])
                document.addEventListener(type,e=>log.push(e.type+':'+e.target.id+':'+(e.relatedTarget?.id??'')),true);
            </script>
            """);

        (await page.FocusAsync("#a")).Should().BeTrue();
        (await page.BlurAsync("#b")).Should().BeTrue("an element without focus is left alone");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().BeEmpty();

        (await page.BlurAsync("#a")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");
        (await page.EvaluateAsync<string>("log.join(' ')")).Should().Be("blur:a: focusout:a:");
    }

    [Test]
    public async Task ATargetedKeyReachesTheTargetWithItsModifiersAndNotTheElementFocusedBefore()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <input id=other>
            <details id=menu><summary id=toggle tabindex=0>Menu</summary><a href=#x id=item>Item</a></details>
            <script>
            window.keys=[];
            document.addEventListener('keydown',e=>keys.push(e.type+':'+e.target.id+':'+e.key+':'+e.shiftKey+':'+e.isTrusted));
            document.addEventListener('keyup',e=>keys.push(e.type+':'+e.target.id+':'+e.key+':'+e.shiftKey+':'+e.isTrusted));
            toggle.addEventListener('keydown',e=>{ if(e.key==='ArrowDown'){menu.open=true;item.focus();} });
            item.addEventListener('keydown',e=>{ if(e.key==='Escape'){menu.open=false;toggle.focus();} });
            </script>
            """);

        (await page.FocusAsync("#other")).Should().BeTrue();
        (await page.PressAsync("#toggle", "ArrowDown", KeyModifiers.Shift)).Should().BeTrue();
        (await page.EvaluateAsync<bool>("menu.open")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("item");

        await page.PressAsync("Escape");
        (await page.EvaluateAsync<bool>("menu.open")).Should().BeFalse();
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("toggle");

        (await page.EvaluateAsync<string>("keys.join(' ')")).Should().Be(
            "keydown:toggle:ArrowDown:true:true keyup:item:ArrowDown:true:true "
            + "keydown:item:Escape:false:true keyup:toggle:Escape:false:true");
    }

    [Test]
    public async Task ATargetedKeyEditsTheTargetControl()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id=a><input id=b>");

        (await page.FocusAsync("#a")).Should().BeTrue();
        (await page.PressAsync("#b", "x")).Should().BeTrue();
        (await page.EvaluateAsync<string>("a.value + '|' + b.value")).Should().Be("|x");
    }

    [Test]
    public async Task ATargetThatIsNotFocusableReceivesNoKey()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><div id=plain>plain</div><button id=off disabled>off</button>
            <script>window.keys=0;document.addEventListener('keydown',()=>keys++);</script>
            """);

        (await page.PressAsync("#plain", "Escape", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.PressAsync("#off", "Escape", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.EvaluateAsync<int>("keys")).Should().Be(0);
    }

    [Test]
    public async Task ATargetedEnterAwaitsTheNavigationItsImplicitSubmissionStarts()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <form action="/results"><input name=q id=q value=alpha><button>Go</button></form>
                """)
            .MapHtml("/results", "<title>Results</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.PressAsync("#q", "Enter")).Should().BeTrue();

        fixture.Page.Url.Should().Be(fixture.Url("/results?q=alpha"));
        (await fixture.Page.TitleAsync()).Should().Be("Results");
    }

    [Test]
    public async Task AHiddenAncestorMakesItsDescendantsUnfocusableAndTabSkipsThem()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=first>1</button>
            <div hidden><button id=behind>2</button><span tabindex=0 id=span>s</span></div>
            <button id=last>3</button>
            """);

        (await page.FocusAsync("#behind", timeout: TimeSpan.Zero)).Should().BeFalse();
        (await page.FocusAsync("#span", timeout: TimeSpan.Zero)).Should().BeFalse();
        await page.EvaluateAsync("behind.focus()");
        (await page.EvaluateAsync<string>("document.activeElement.localName")).Should().Be("body");

        await page.PressAsync("Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("first");
        await page.PressAsync("Tab");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("last");
        await page.PressAsync("Tab", KeyModifiers.Shift);
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("first");
    }

    [Test]
    public async Task AnInertFrameMakesItsDocumentUnfocusable()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/child.html", "<!doctype html><input id=inner>")
            .MapHtml("/", """
                <!doctype html><iframe id=f src="/child.html" inert></iframe><iframe id=g src="/child.html"></iframe>
                """));
        await loopback.Page.NavigateAsync(loopback.Url("/"));

        (await loopback.Page.EvaluateAsync<string>("""
            (() => {
                const log = [];
                for (const frame of [f, g]) {
                    const input = frame.contentDocument.getElementById('inner');
                    input.addEventListener('focus', () => log.push(frame.id));
                    input.focus();
                }
                return log.join(',');
            })()
            """)).Should().Be("g", "only the input outside the inert frame takes focus");
        loopback.Page.Errors.Should().BeEmpty();
    }

    [TestCase("<div inert><input id=t></div>")]
    [TestCase("<div hidden><input id=t></div>")]
    public async Task FillTypeAndSelectRefuseATargetNoUserCouldReach(string html)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id=a value=keep>" + html
            + html.Replace("<input id=t>", "<select id=s><option>x</option><option>y</option></select>", StringComparison.Ordinal));

        (await page.FocusAsync("#a")).Should().BeTrue();

        (await page.FillAsync("#t", "filled")).Should().BeFalse();
        (await page.TypeAsync("#t", "typed")).Should().BeFalse();
        (await page.SelectAsync("#s", "y")).Should().BeFalse();

        (await page.EvaluateAsync<string>("a.value + '|' + t.value + '|' + s.value")).Should().Be("keep||x",
            "neither the unreachable target nor the element that had focus is edited");
        (await page.EvaluateAsync<string>("document.activeElement.id")).Should().Be("a");
    }
}

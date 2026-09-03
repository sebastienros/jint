using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Events;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The page-level input members: a click, a hover, a fill, a type, a select, a key and a scroll.
/// </summary>
/// <remarks>
/// The dispatcher has its own suite; what these hold is that the page members reach it — that a click is a
/// real hit test with a real activation behaviour, that a navigation one causes is awaited rather than left
/// running, and that a snapshot reference and a selector reach the same element.
/// </remarks>
public sealed class PageInputTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Test]
    public async Task AClickRunsTheActivationBehaviour()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <input type="checkbox" id="agree">
            <p id="log"></p>
            <script>
              document.getElementById('agree').addEventListener('click', e => {
                document.getElementById('log').textContent = e.isTrusted ? 'trusted' : 'synthetic';
              });
            </script>
            """);

        (await page.ClickAsync("#agree")).Should().BeTrue();

        (await page.EvaluateAsync<bool>("document.getElementById('agree').checked")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('log').textContent")).Should().Be("trusted");
    }

    [Test]
    public async Task AClickOnALinkNavigatesAndTheTaskWaitsForIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><a id='go' href='/next'>next</a>")
            .MapHtml("/next", "<!doctype html><title>Next</title><p>arrived</p>"));

        await fixture.Page.NavigateAsync(fixture.Url("/"));

        (await fixture.Page.ClickAsync("#go")).Should().BeTrue();

        fixture.Page.Url.Should().EndWith("/next", "the navigation the click caused is awaited before the click answers");
        (await fixture.Page.TitleAsync()).Should().Be("Next");
    }

    [Test]
    public async Task ATargetThatMatchesNothingIsFalseRatherThanAThrow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><p>nothing to click</p>");

        (await page.ClickAsync("#absent")).Should().BeFalse();
        (await page.HoverAsync("#absent")).Should().BeFalse();
        (await page.FillAsync("#absent", "x")).Should().BeFalse();
        (await page.TypeAsync("#absent", "x")).Should().BeFalse();
        (await page.SelectAsync("#absent", "x")).Should().BeFalse();
        (await page.ClickAsync("ref=9999")).Should().BeFalse("a reference no snapshot printed names nothing");
        (await page.ClickAsync("!!not a selector!!")).Should().BeFalse("a selector that will not parse is an answer of no");
    }

    [Test]
    public async Task AnAccessibilityReferenceReachesTheSameElementASelectorDoes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id="save">Save</button>
            <p id="log"></p>
            <script>document.getElementById('save').onclick = () => document.getElementById('log').textContent = 'saved';</script>
            """);

        var snapshot = await page.AccessibilitySnapshotAsync(includeReferences: true);
        snapshot.Should().Contain("button \"Save\" [ref=");

        var reference = snapshot[(snapshot.IndexOf("[ref=", StringComparison.Ordinal) + 5)..];
        reference = reference[..reference.IndexOf(']', StringComparison.Ordinal)];

        (await page.ClickAsync("ref=" + reference)).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('log').textContent")).Should().Be("saved");
    }

    [Test]
    public async Task ASnapshotWithoutReferencesCarriesNone()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><button>Save</button>");

        (await page.AccessibilitySnapshotAsync()).Should().NotContain("[ref=");
    }

    [Test]
    public async Task FillReplacesTheValueAndTypeAddsToIt()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id='q' value='old'>");

        (await page.FillAsync("#q", "new")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('q').value")).Should().Be("new");

        (await page.TypeAsync("#q", "er")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('q').value")).Should().Be("newer");
    }

    [Test]
    public async Task TypeFiresOneInputEventPerCharacter()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <input id="q">
            <script>window.inputs = 0; document.getElementById('q').addEventListener('input', () => window.inputs++);</script>
            """);

        await page.TypeAsync("#q", "abc");

        (await page.EvaluateAsync<double>("window.inputs")).Should().Be(3);
    }

    [Test]
    public async Task EnterInASingleLineControlSubmitsTheFormAndTheKeyWaitsForIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<!doctype html><form action='/results'><input name='q' id='q'></form>")
            .MapHtml("/results", "<!doctype html><title>Results</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.FillAsync("#q", "jint");
        await fixture.Page.PressAsync("Enter");

        fixture.Page.Url.Should().Contain("/results?q=jint");
        (await fixture.Page.TitleAsync()).Should().Be("Results");
    }

    [Test]
    public async Task SelectChoosesAnOptionByValueOrByLabelAndFiresChange()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <select id="s"><option value="a">Apples</option><option value="b">Bananas</option></select>
            <script>window.changes = 0; document.getElementById('s').addEventListener('change', () => window.changes++);</script>
            """);

        (await page.SelectAsync("#s", "b")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('s').value")).Should().Be("b");

        (await page.SelectAsync("#s", "Apples")).Should().BeTrue("an option is named by its value or by its text");
        (await page.EvaluateAsync<string>("document.getElementById('s').value")).Should().Be("a");

        (await page.SelectAsync("#s", "Cherries")).Should().BeFalse();
        (await page.EvaluateAsync<double>("window.changes")).Should().Be(2);
    }

    [Test]
    public async Task HoverFiresAMouseMoveAtTheElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div id="target">hover me</div>
            <script>document.getElementById('target').addEventListener('mousemove', () => window.moved = true);</script>
            """);

        (await page.HoverAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("window.moved === true")).Should().BeTrue();
    }

    [Test]
    public async Task ScrollingMovesThePagesOwnOffsetAndFiresScroll()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <script>window.scrolls = 0; addEventListener('scroll', () => window.scrolls++);</script>
            <div id="tall"></div>
            <script>
              for (var i = 0; i < 400; i++) { document.getElementById('tall').appendChild(document.createElement('p')); }
            </script>
            """);

        await page.ScrollToAsync(500);
        await page.WaitForIdleAsync(Patience);

        (await page.EvaluateAsync<double>("window.scrollY")).Should().Be(500);
        (await page.EvaluateAsync<double>("window.scrolls")).Should().BeGreaterThan(0);
    }

    [Test]
    public async Task WaitForSelectorSeesWhatATimerAdds()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <script>
              setTimeout(() => {
                const p = document.createElement('p');
                p.id = 'late';
                p.textContent = 'here at last';
                document.body.appendChild(p);
              }, 120);
            </script>
            """);

        (await page.WaitForSelectorAsync("#late", Patience)).Should().BeTrue();
        (await page.WaitForTextAsync("here at last", Patience)).Should().BeTrue();
    }

    [Test]
    public async Task AWaitGivesUpRatherThanHangs()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><p>nothing is coming</p>");

        (await page.WaitForSelectorAsync("#never", TimeSpan.FromMilliseconds(200))).Should().BeFalse();
        (await page.WaitForTextAsync("never", TimeSpan.FromMilliseconds(200))).Should().BeFalse();
    }
}

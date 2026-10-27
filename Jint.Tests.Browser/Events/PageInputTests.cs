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

    [TestCase("textarea", "Orchard \U0001F34E")]
    [TestCase("textarea", "\U0001F34E")]
    [TestCase("input", "\U0001F34E")]
    [TestCase("div contenteditable", "\U0001F34E")]
    public async Task TypingSupplementaryCharactersPreservesTextAndTrustedInput(string tag, string text)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<" + tag + " id='text'></" + tag.Split(' ')[0] + ">");
        await page.EvaluateAsync("""
            window.events=[];
            for(const type of ['keydown','keypress','beforeinput','input','keyup'])
                document.getElementById('text').addEventListener(type,e=>
                    events.push({type:e.type,key:e.key,data:e.data,trusted:e.isTrusted}));
            """);

        (await page.TypeAsync("#text", text)).Should().BeTrue();
        (await page.EvaluateAsync<string>("text.value ?? text.textContent")).Should().Be(text);
        (await page.EvaluateAsync<bool>("""
            (() => {
                const eventsForScalar=events.slice(-5);
                return eventsForScalar.map(e=>e.type).join(',') === 'keydown,keypress,beforeinput,input,keyup' &&
                    eventsForScalar.every(e=>e.trusted) &&
                    eventsForScalar.filter(e=>e.key!==undefined).every(e=>e.key==='\uD83C\uDF4E') &&
                    eventsForScalar.filter(e=>e.data!==undefined).every(e=>e.data==='\uD83C\uDF4E');
            })()
            """)).Should().BeTrue();
        (await page.EvaluateAsync<int>("text.selectionStart ?? getSelection().focusOffset")).Should().Be(text.Length);
    }

    [TestCase("keydown")]
    [TestCase("keypress")]
    [TestCase("beforeinput")]
    public async Task SupplementaryCharacterTypingRespectsCancellation(string canceledEvent)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<textarea id='text'></textarea>");
        await page.EvaluateAsync("text.addEventListener('" + canceledEvent + "',e=>e.preventDefault());"
            + "window.insertions=0;text.addEventListener('input',()=>insertions++);");
        (await page.TypeAsync("#text", "\U0001F34E")).Should().BeTrue();
        (await page.EvaluateAsync<string>("text.value")).Should().BeEmpty();
        (await page.EvaluateAsync<int>("insertions")).Should().Be(0);
    }

    [Test]
    public async Task AClickCoveredByASiblingDoesNotDispatchOrReportSuccess()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div style="display:flex;direction:rtl">
                <div id=target style="flex:1"><span>A</span><span>B</span><span>C</span></div>
                <div style="flex:0 0 64px;display:flex;direction:ltr"><button id=cover style="flex:0 0 1280px"><span>cover</span></button></div>
            </div>
            <script>
            window.inputEvents=[];
            for(const type of ['pointermove','mousemove','pointerdown','mousedown','mouseup','click'])
                document.addEventListener(type, e=>inputEvents.push(e.target.id));
            </script>
            """);
        (await page.EvaluateAsync<string>("""
            (() => {
                const box=target.getBoundingClientRect();
                const hit=document.elementFromPoint(box.x+box.width/2,box.y+box.height/2);
                return hit.closest('button')?.id ?? JSON.stringify({
                    hit:hit.id,target:box,cover:cover.getBoundingClientRect()
                });
            })()
            """)).Should().Be("cover");
        (await page.ClickAsync("#target")).Should().BeFalse();
        (await page.EvaluateAsync<int>("inputEvents.length")).Should().Be(0);
    }

    [TestCase("mousedown")]
    [TestCase("blur")]
    public async Task AClickWhoseTargetMovesDuringPressReportsTheActualClick(string moveEvent)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <input id="previous"><button id="target">Save</button>
            <script>
            window.pointerEvents=[];window.targetClicks=0;window.actualClicks=[];
            for(const type of ['pointerdown','mousedown','pointerup','mouseup'])
                document.addEventListener(type,e=>pointerEvents.push(type));
            target.addEventListener('click',()=>targetClicks++);
            document.addEventListener('click',e=>actualClicks.push({id:e.target.id,trusted:e.isTrusted}),true);
            </script>
            """);
        await page.EvaluateAsync("""
            previous.focus();
            window.moveTarget=()=>{
                const spacer=document.createElement('div');
                spacer.innerHTML='<p>one</p><p>two</p><p>three</p>';
                target.before(spacer);
            };
            """ + (moveEvent == "blur" ? "previous" : "target")
            + ".addEventListener('" + moveEvent + "',moveTarget,{once:true});");

        (await page.ClickAsync("#target")).Should().BeFalse();
        (await page.EvaluateAsync<string>("pointerEvents.join(',')"))
            .Should().Be("pointerdown,mousedown,pointerup,mouseup");
        (await page.EvaluateAsync<bool>("actualClicks.length===1 && actualClicks[0].id!=='target' && actualClicks[0].trusted"))
            .Should().BeTrue();
        (await page.EvaluateAsync<int>("targetClicks")).Should().Be(0);
    }

    [Test]
    public async Task AClickThatRemovesItsOwnTargetStillReportsTheDeliveredClick()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><button id="target"><span>Save</span></button>
            <script>window.clicks=0;target.onclick=()=>{clicks++;target.remove();};</script>
            """);
        (await page.ClickAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<int>("clicks")).Should().Be(1);
    }

    [Test]
    public async Task AClickCanHitADescendantOfTheRequestedElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><button id=target><span id=child>child</span></button>
            <script>window.clicked=null;target.onclick=e=>clicked={id:e.target.id,trusted:e.isTrusted};</script>
            """);
        (await page.ClickAsync("#target")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("clicked.id === 'child' && clicked.trusted")).Should().BeTrue();
    }

    [Test]
    public async Task ElementScrollMethodsUseTheExistingVirtualScrollRange()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><div id='other'></div>" + string.Concat(Enumerable.Repeat("<p>row</p>", 200)));

        await page.EvaluateAsync("document.scrollingElement.scrollTo({top: 100, behavior: 'smooth'})");
        (await page.EvaluateAsync<double>("scrollY")).Should().Be(100);
        await page.EvaluateAsync("document.scrollingElement.scrollBy(0, 20)");
        (await page.EvaluateAsync<double>("scrollY")).Should().Be(120);
        await page.EvaluateAsync("document.scrollingElement.scroll({top: 40})");
        (await page.EvaluateAsync<double>("scrollY")).Should().Be(40);
        await page.EvaluateAsync("document.querySelector('#other').scrollTo({top: 100})");
        (await page.EvaluateAsync<double>("document.querySelector('#other').scrollTop")).Should().Be(0);
        (await page.EvaluateAsync<double>("scrollY")).Should().Be(40);
    }

    [TestCase("javascript:;")]
    [TestCase("javascript:void(0)")]
    [TestCase("javascript:window.activated = true; void(0)")]
    public async Task JavaScriptLinksExecuteWithoutNavigatingForNonStringResults(string href)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><a id='go'>go</a><script>window.marker = 42;</script>");
        await page.EvaluateAsync("document.querySelector('#go').href = " + System.Text.Json.JsonSerializer.Serialize(href));
        var url = page.Url;

        (await page.ClickAsync("#go")).Should().BeTrue();

        page.Url.Should().Be(url);
        (await page.EvaluateAsync<double>("window.marker")).Should().Be(42);
        if (href.Contains("activated", StringComparison.Ordinal))
            (await page.EvaluateAsync<bool>("window.activated")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CancelingAJavaScriptLinkPreventsExecution()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><a id="go" href="javascript:window.activated=true">go</a>
            <script>document.querySelector('#go').onclick = () => false;</script>
            """);

        await page.ClickAsync("#go");

        (await page.EvaluateAsync<bool>("window.activated === undefined")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task JavaScriptUrlExceptionsAreReportedWithoutReplacingTheDocument()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><title>Original</title>
            <a id="go" href="javascript:throw new Error('URL script failed')">go</a>
            """);

        (await page.ClickAsync("#go")).Should().BeTrue();

        (await page.TitleAsync()).Should().Be("Original");
        page.Errors.Should().Contain(error => error.Kind == PageErrorKind.ScriptError && error.Message.Contains("URL script failed"));
    }

    [Test]
    public async Task AJavaScriptStringResultReplacesTheDocumentWithoutFetchingOrChangingItsUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """<!doctype html><a id="go" href="javascript:'%3Ctitle%3EReplaced%3C/title%3E'">go</a>"""));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        var requests = fixture.Page.Requests.Count;
        var historyLength = await fixture.Page.EvaluateAsync<double>("history.length");

        await fixture.Page.ClickAsync("#go");

        fixture.Page.Url.Should().Be(fixture.Url("/"));
        (await fixture.Page.TitleAsync()).Should().Be("Replaced");
        fixture.Page.Requests.Count.Should().Be(requests);
        (await fixture.Page.EvaluateAsync<double>("history.length")).Should().Be(historyLength);
    }

    [TestCase("date", "2026-10-26")]
    [TestCase("month", "2026-10")]
    [TestCase("week", "2026-W44")]
    [TestCase("time", "12:34")]
    [TestCase("datetime-local", "2026-10-26T12:34")]
    public async Task FillCommitsPickerControlValuesAndTrustedEvents(string type, string value)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"""
            <!doctype html><form><input id="field" name="value" type="{type}"></form>
            <script>
              window.events = [];
              for (const type of ['input', 'change'])
                document.querySelector('#field').addEventListener(type, e => events.push(e.type + ':' + e.isTrusted));
            </script>
            """);

        (await page.FillAsync("#field", value)).Should().BeTrue();

        (await page.EvaluateAsync<string>("document.querySelector('#field').value")).Should().Be(value);
        (await page.EvaluateAsync<string>("new FormData(document.querySelector('form')).get('value')")).Should().Be(value);
        (await page.EvaluateAsync<string>("events.join(',')")).Should().Be("input:true,change:true");
    }

    [Test]
    public async Task InvalidPickerValuesAreRejectedWithoutChangingTheControl()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><input id='field' type='date' value='2026-10-26'>");

        var fill = async () => await page.FillAsync("#field", "not-a-date");
        await fill.Should().ThrowAsync<ArgumentException>();

        (await page.EvaluateAsync<string>("document.querySelector('#field').value")).Should().Be("2026-10-26");
    }

    [TestCase("date", "disabled")]
    [TestCase("date", "readonly")]
    [TestCase("text", "readonly")]
    [TestCase("checkbox", "")]
    public async Task FillDoesNotClaimSuccessForControlsItCannotEdit(string type, string attribute)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<!doctype html><input id='field' type='{type}' {attribute}>");

        (await page.FillAsync("#field", "2026-10-26")).Should().BeFalse();
    }

    [TestCase(KeyModifiers.Control, "true:false")]
    [TestCase(KeyModifiers.Meta, "false:true")]
    public async Task PressCarriesModifiersWithoutInsertingShortcutText(KeyModifiers modifiers, string flags)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html><input id="field">
            <script>
              window.keys = [];
              for (const type of ['keydown', 'keyup'])
                document.addEventListener(type, e => keys.push(e.type + ':' + e.ctrlKey + ':' + e.metaKey));
            </script>
            """);
        await page.FillAsync("#field", "");
        await page.EvaluateAsync("keys.length = 0");

        await page.PressAsync("k", modifiers);

        (await page.EvaluateAsync<string>("keys.join(',')")).Should().Be("keydown:" + flags + ",keyup:" + flags);
        (await page.EvaluateAsync<string>("document.querySelector('#field').value")).Should().BeEmpty();
    }

    [TestCase(false, "value=\"Filter\"", "Filter")]
    [TestCase(true, "value=\"Filter\"", "Filter")]
    [TestCase(false, "", "Submit")]
    [TestCase(true, "", "Submit")]
    [TestCase(true, "value=\"\"", "")]
    public async Task EnterUsesTheFirstSubmitInputEvenWhenItHasNoVisibleBox(bool clickFromCanceledKeydown,
        string valueAttribute, string submittedValue)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", $$"""
                <!doctype html><form action="/results">
                  <input type="search" id="search-box" name="Options.Search">
                  <input type="submit" name="submit.Filter" id="submitFilter" {{valueAttribute}} style="display:none">
                  <input type="submit" name="submit.BulkAction" value="Apply">
                </form>
                """)
            .MapHtml("/results", "<!doctype html><title>Results</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        if (clickFromCanceledKeydown)
            await fixture.Page.EvaluateAsync("""
                document.querySelector('#search-box').addEventListener('keydown', event => {
                    if (event.key === 'Enter') {
                        event.preventDefault();
                        document.querySelector('#submitFilter').click();
                    }
                });
                """);
        await fixture.Page.FillAsync("#search-box", "Alpha");

        await fixture.Page.PressAsync("Enter");

        fixture.Page.Url.Should().Contain("Options.Search=Alpha").And.Contain("submit.Filter=" + submittedValue)
            .And.NotContain("submit.BulkAction");
    }

    [Test]
    public async Task WindowSubmitListenersSynchronizeHiddenEditorsBeforeThePostEntryList()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <!doctype html>
                <form action="/results" method="post">
                    <textarea hidden id="editor" name="Schema">old</textarea>
                    <input type="hidden" name="Bubbled" id="bubbled" value="false">
                    <button id="save">Save</button>
                </form>
                <script>
                window.addEventListener('submit', event => {
                    editor.value='updated \uD83C\uDF4E';
                    bubbled.value=String(event.bubbles && event.target===document.forms[0] &&
                        event.currentTarget===window && event.submitter===save);
                });
                </script>
                """)
            .MapHtml("/results", "<title>Results</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.ClickAsync("#save")).Should().BeTrue();
        fixture.Server.Received.Should().Contain(request =>
            request.Method == "POST" && request.Path == "/results"
            && request.Body == "Schema=updated+%F0%9F%8D%8E&Bubbled=true");
    }

    [Test]
    public async Task SubmitInputsUseTheDefaultLabelOnlyForAnAbsentValueAttribute()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", """
                <form action="/results" method="post">
                <input type="search" id="search-box" name="Options.Search">
                <input type="submit" id="filter" name="submit.Filter" style="display:none">
                <input type="submit" name="submit.BulkAction">
                </form>
                <script>
                document.querySelector('#search-box').onkeydown=event=>{
                    if(event.key==='Enter'){event.preventDefault();document.querySelector('#filter').click();}
                };
                </script>
                """)
            .MapHtml("/results", "<title>Results</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAsync<string>("new FormData(document.forms[0], document.querySelector('#filter')).get('submit.Filter')"))
            .Should().Be("Submit");
        (await fixture.Page.EvaluateAsync<string>("document.querySelector('#filter').value")).Should().BeEmpty();
        await fixture.Page.EvaluateAsync("document.querySelector('#filter').value=''");
        (await fixture.Page.EvaluateAsync<string>("new FormData(document.forms[0], document.querySelector('#filter')).get('submit.Filter')"))
            .Should().BeEmpty();
        await fixture.Page.EvaluateAsync("document.querySelector('#filter').removeAttribute('value')");
        await fixture.Page.FillAsync("#search-box", "Alpha");
        await fixture.Page.PressAsync("Enter");
        fixture.Server.Received.Should().Contain(request =>
            request.Method == "POST" && request.Path == "/results"
            && request.Body == "Options.Search=Alpha&submit.Filter=Submit");
    }

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
    public async Task AClickPropagatesANavigationFailure()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><a id='go' href='ftp://example.test/unsupported'>go</a>");

        var act = async () => await page.ClickAsync("#go");

        (await act.Should().ThrowAsync<NavigationFailedException>()).WithMessage("*cannot load*");
        page.Errors.Should().BeEmpty("an awaited navigation reports its failure to its caller");
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
    public async Task IndexedTargetsSelectOneMatchAndAcceptOffsetsFromTheEnd()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <!doctype html>
            <button class="click">first</button><button class="click">second</button>
            <div class="hover">first</div><div class="hover">second</div>
            <input class="field" value="first"><input class="field" value="second">
            <select class="pick"><option value="a">A</option><option value="b">B</option></select>
            <select class="pick"><option value="a">A</option><option value="b">B</option></select>
            <span class="late">first</span>
            <script>
              document.querySelectorAll('.click')[1].onclick = () => document.body.dataset.clicked = 'second';
              document.querySelectorAll('.hover')[1].onmousemove = () => document.body.dataset.hovered = 'second';
              setTimeout(() => {
                const span = document.createElement('span');
                span.className = 'late';
                span.textContent = 'second';
                document.body.appendChild(span);
              }, 20);
            </script>
            """);

        (await page.ClickAsync(".click", -1)).Should().BeTrue();
        (await page.HoverAsync(".hover", 1)).Should().BeTrue();
        (await page.FillAsync(".field", -1, "changed")).Should().BeTrue();
        (await page.TypeAsync(".field", 0, "+typed")).Should().BeTrue();
        (await page.SelectAsync(".pick", -1, "b")).Should().BeTrue();
        (await page.WaitForSelectorAsync(".late", 1, Patience)).Should().BeTrue();

        (await page.EvaluateAsync<string>("document.body.dataset.clicked")).Should().Be("second");
        (await page.EvaluateAsync<string>("document.body.dataset.hovered")).Should().Be("second");
        (await page.EvaluateAsync<string>("document.querySelectorAll('.field')[0].value")).Should().Be("+typedfirst");
        (await page.EvaluateAsync<string>("document.querySelectorAll('.field')[1].value")).Should().Be("changed");
        (await page.EvaluateAsync<string>("document.querySelectorAll('.pick')[1].value")).Should().Be("b");
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

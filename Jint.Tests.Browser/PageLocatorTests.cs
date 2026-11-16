using Jint.Browser.Runtime;
using Jint.HtmlParser;
using NativeBrowser = Jint.Browser.Browser;

namespace Jint.Tests.Browser;

/// <summary>
/// Role, label and text targets (#4624): <c>role=button[name="Next"]</c> and its relatives, accepted wherever a
/// selector or a <c>ref=</c> is, and resolved by the accessibility snapshot's own role and name computation.
/// </summary>
public sealed class PageLocatorTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [TestCase("role=button[name=\"Next\"]")]
    [TestCase("internal:role=button[name=\"Next\"i]")]
    [TestCase("text=›")]
    [TestCase("aria/Next")]
    [TestCase("aria/Next[role=\"button\"]")]
    public async Task TheIssueReproClicksTheButtonForEveryForm(string target)
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><button onclick=\"document.title='clicked'\" aria-label=\"Next\">›</button>", "http://127.0.0.1/");

        (await page.AccessibilitySnapshotAsync(includeReferences: true)).Should().Contain("button \"Next\"");
        (await page.ClickAsync(target)).Should().BeTrue();
        (await page.TitleAsync()).Should().Be("clicked");
    }

    [Test]
    public async Task TheNameIsTheAccessibleNameNotAnAttributeOrTextContent()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <span id=lbl>Search the docs</span>
            <input id=labelledby aria-labelledby=lbl>
            <label for=email>Email address</label><input id=email type=email>
            <label>Subscribe <input id=wrapped type=checkbox></label>
            <button id=titled title="Close dialog"><svg></svg></button>
            <img id=logo alt="Company logo" src="data:,">
            <a id=link href="/x">Read <b>more</b> about it</a>
            """);

        (await Ids(page, "role=textbox[name=\"Search the docs\"]")).Should().Equal("labelledby");
        (await Ids(page, "role=textbox[name=\"email address\"]")).Should().Equal("email");
        (await Ids(page, "role=checkbox[name=\"Subscribe\"]")).Should().Equal("wrapped");
        (await Ids(page, "role=button[name=\"Close dialog\"]")).Should().Equal("titled");
        (await Ids(page, "role=img[name=\"logo\"]")).Should().Equal("logo");
        (await Ids(page, "role=image[name=\"logo\"]")).Should().Equal("logo");
        (await Ids(page, "role=link[name=\"read more about it\"]")).Should().Equal("link");
        (await page.CountAsync("role=textbox")).Should().Be(2);
    }

    [Test]
    public async Task NameMatchingIsASubstringByDefaultAndExactWithTheSFlag()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=next>Next</button>
            <button id=page>Next page</button>
            <button id=lower>next</button>
            <button id=spaced>  Next
                step </button>
            """);

        (await Ids(page, "role=button[name=\"Next\"]")).Should().Equal("next", "page", "lower", "spaced");
        (await Ids(page, "role=button[name=\"NEXT\"i]")).Should().Equal("next", "page", "lower", "spaced");
        (await Ids(page, "role=button[name=\"Next\"s]")).Should().Equal("next");
        (await Ids(page, "role=button[name='next'S]")).Should().Equal("lower");
        (await Ids(page, "role=button[name=\"Next   step\"s]")).Should().Equal("spaced");
        (await Ids(page, "role=button[name=\"Nex\"s]")).Should().BeEmpty();
        (await Ids(page, "aria/Next page")).Should().Equal("page");
        (await page.IsVisibleAsync("role=button[name=\"Next\"]", -1)).Should().BeTrue();
        (await page.InnerTextAsync("role=button[name=\"Next\"s]")).Should().Be("Next");
    }

    [Test]
    public async Task StatesNarrowARoleTheWayTheSnapshotPublishesThem()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <h1 id=h1>Title</h1><h2 id=h2>Section</h2><div id=h4 role=heading aria-level=4>Deep</div>
            <input id=on type=checkbox checked><input id=off type=checkbox><input id=mixed type=checkbox>
            <div id=ariaon role=checkbox aria-checked=true>A</div><div id=ariaoff role=checkbox>B</div>
            <button id=pressed aria-pressed=true>Bold</button><button id=unpressed>Italic</button>
            <button id=open aria-expanded=true>Menu</button><button id=closed aria-expanded=false>More</button>
            <details id=details open><summary id=summary>Details</summary>body</details>
            <button id=disabled disabled>Gone</button><button id=ariadisabled aria-disabled=true>Off</button>
            <select id=list size=3><option id=first>One</option><option id=second selected>Two</option></select>
            <script>document.getElementById('mixed').indeterminate = true;</script>
            """);

        (await Ids(page, "role=heading[level=1]")).Should().Equal("h1");
        (await Ids(page, "role=heading[level=2]")).Should().Equal("h2");
        (await Ids(page, "role=heading[level=4][name=\"deep\"]")).Should().Equal("h4");
        (await Ids(page, "role=checkbox[checked]")).Should().Equal("on", "ariaon");
        (await Ids(page, "role=checkbox[checked=true]")).Should().Equal("on", "ariaon");
        (await Ids(page, "role=checkbox[checked=false]")).Should().Equal("off", "ariaoff");
        (await Ids(page, "role=checkbox[checked=\"mixed\"]")).Should().Equal("mixed");
        (await Ids(page, "role=button[pressed]")).Should().Equal("pressed");
        (await Ids(page, "role=button[pressed=false]")).Should().Contain("unpressed").And.NotContain("pressed");
        // HTML-AAM maps a <summary> to a button, expanded with its <details>.
        (await Ids(page, "role=button[expanded]")).Should().Equal("open", "summary");
        (await Ids(page, "role=button[expanded=false]")).Should().Equal("closed");
        (await Ids(page, "role=group[expanded=true]")).Should().Equal("details");
        (await Ids(page, "role=button[disabled]")).Should().Equal("disabled", "ariadisabled");
        (await Ids(page, "role=option[selected]")).Should().Equal("second");
        (await Ids(page, "role=option[selected=false]")).Should().Equal("first");
        (await Ids(page, "role=link[checked]")).Should().BeEmpty();
    }

    [Test]
    public async Task HiddenElementsAreFoundOnlyWithIncludeHidden()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <style>.gone{display:none}.ghost{visibility:hidden}.ghost .back{visibility:visible}</style>
            <button id=shown>Save</button>
            <button id=attr hidden>Save</button>
            <div class=gone><button id=display>Save</button></div>
            <div aria-hidden=true><button id=aria>Save</button></div>
            <div class=ghost><button id=ghost>Save</button><button id=back class=back>Save</button></div>
            """);

        (await Ids(page, "role=button[name=\"Save\"]")).Should().Equal("shown", "back");
        (await Ids(page, "role=button[name=\"Save\"][include-hidden]")).Should().Equal("shown", "attr", "display", "aria", "ghost", "back");
        (await Ids(page, "role=button[include-hidden=false]")).Should().Equal("shown", "back");

        var snapshot = await page.AccessibilitySnapshotAsync();
        snapshot.Split('\n').Count(line => line.Contains("button \"Save\"", StringComparison.Ordinal)).Should().Be(2);
    }

    [Test]
    public async Task LabelTargetsFindTheControlALabelNames()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <label for=email>Email</label><input id=email>
            <label>Password <input id=password type=password></label>
            <span id=described>Search terms</span><input id=search aria-labelledby=described>
            <textarea id=notes aria-label="Release notes"></textarea>
            <input id=hiddeninput type=hidden aria-label=Email>
            <label for=missing>Email</label>
            """);

        (await Ids(page, "label=Email")).Should().Equal("email", "hiddeninput");
        (await Ids(page, "label=\"Email\"")).Should().Equal("email", "hiddeninput");
        (await Ids(page, "label=password")).Should().Equal("password");
        (await Ids(page, "label=\"password\"")).Should().BeEmpty();
        (await Ids(page, "label=\"password\"i")).Should().Equal("password");
        (await Ids(page, "internal:label=\"Search terms\"s")).Should().Equal("search");
        (await Ids(page, "label=notes")).Should().Equal("notes");

        (await page.FillAsync("label=Email", "someone@example.com")).Should().BeTrue();
        (await page.EvaluateAsync<string>("email.value")).Should().Be("someone@example.com");
    }

    [Test]
    public async Task TextTargetsNameTheDeepestElementContainingTheText()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <head><title>Save the title</title></head>
            <div id=card><p id=para>Click <b id=bold>Save   now</b> please</p><span id=other>save</span></div>
            <input id=submit type=submit value="Save form">
            <script>var text = 'Save in script';</script>
            <style>/* Save in style */</style>
            """);

        (await Ids(page, "text=save")).Should().Equal("bold", "other", "submit");
        (await Ids(page, "text=\"Save now\"")).Should().Equal("bold");
        (await Ids(page, "text=\"Save\"")).Should().BeEmpty();
        (await Ids(page, "text=\"save\"")).Should().Equal("other");
        (await Ids(page, "text=\"save\"s")).Should().Equal("other");
        (await Ids(page, "text=\"SAVE\"i")).Should().Equal("bold", "other", "submit");
        (await Ids(page, "text=click save now")).Should().Equal("para");
        (await page.CountAsync("internal:text=\"Save form\"s")).Should().Be(1);
        (await page.TextsAsync("text=\"Save now\"")).Should().Equal("Save   now");
    }

    [Test]
    public async Task PartsChainInsideACssScope()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <fieldset data-filter-group=type>
                <label><input id=type-integration type=checkbox> Integration</label>
                <label><input id=type-hosting type=checkbox> Hosting</label>
            </fieldset>
            <fieldset data-filter-group=language>
                <label><input id=lang-integration type=checkbox> Integration</label>
            </fieldset>
            <ul><li id=a><button id=a-edit>Edit</button></li><li id=b><button id=b-edit>Edit</button></li></ul>
            """);

        const string scoped = "[data-filter-group=type] >> role=checkbox[name=\"Integration\"]";
        (await Ids(page, "role=checkbox[name=\"Integration\"]")).Should().Equal("type-integration", "lang-integration");
        (await Ids(page, scoped)).Should().Equal("type-integration");
        (await Ids(page, "css=fieldset >> label=Hosting")).Should().Equal("type-hosting");
        (await Ids(page, "fieldset >> text=integration >> input")).Should().Equal("type-integration", "lang-integration");
        (await Ids(page, "li >> role=button")).Should().Equal("a-edit", "b-edit");
        (await Ids(page, "ul >> li >> role=button[name=\"Edit\"]")).Should().Equal("a-edit", "b-edit");
        (await Ids(page, "#b >> role=button")).Should().Equal("b-edit");
        (await Ids(page, "#b >> role=listitem")).Should().BeEmpty();
        (await Ids(page, "[data-filter-group=\"a>>b\"] >> role=checkbox")).Should().BeEmpty();

        (await page.ClickAsync(scoped)).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.getElementById('type-integration').checked")).Should().BeTrue();
        (await page.EvaluateAsync<bool>("document.getElementById('lang-integration').checked")).Should().BeFalse();
        (await Ids(page, "[data-filter-group=type] >> role=checkbox[checked]")).Should().Equal("type-integration");
        (await Ids(page, "[data-filter-group=type] >> role=checkbox[checked=false]")).Should().Equal("type-hosting");
        (await page.ClickAsync("li >> role=button", -1)).Should().BeTrue();
    }

    [Test]
    public async Task EveryMatchIsAScopeForTheNextPartNestedOnesIncluded()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <div class=item id=outer><span class=t id=a>A</span>
                <div class=item id=inner><span class=t id=b>B</span><button id=deep>Go</button></div>
            </div>
            <div class="it's"><span id=apostrophe>quoted</span></div>
            """);

        (await Ids(page, ".item >> :scope > .t")).Should().Equal("a", "b");
        (await Ids(page, ".item >> role=button")).Should().Equal("deep");
        (await Ids(page, ".item >> text=b")).Should().Equal("b");
        (await Ids(page, ".it\\'s >> span")).Should().Equal("apostrophe");
        (await Ids(page, "text=\"it's\" >> span")).Should().BeEmpty();
        (await page.InnerTextAsync(".item >> :scope > .t", -1)).Should().Be("B");
    }

    [Test]
    public async Task AStateTheRoleDoesNotSupportNeverMatches()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <!doctype html>
            <button id=selectedbutton aria-selected=true>A</button>
            <div id=leveledlink role=link aria-level=3 tabindex=0>B</div>
            <div id=expandednote role=note aria-expanded=true>C</div>
            <ul><li id=item aria-level=2>D</li></ul>
            <div role=tablist><div id=tab role=tab>E</div></div>
            """);

        (await Ids(page, "role=button[selected]")).Should().BeEmpty();
        (await Ids(page, "role=link[level=3]")).Should().BeEmpty();
        (await Ids(page, "role=note[expanded]")).Should().BeEmpty();
        (await Ids(page, "role=listitem[level=2]")).Should().Equal("item");
        (await Ids(page, "role=tab[selected=false]")).Should().Equal("tab");
    }

    [Test]
    public async Task AReferenceChainsWithTheOtherParts()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><nav aria-label=Primary><a id=home href=/>Home</a></nav><a id=other href=/x>Home</a>");

        var snapshot = await page.AccessibilitySnapshotAsync(includeReferences: true);
        var line = snapshot.Split('\n').Single(l => l.Contains("navigation \"Primary\"", StringComparison.Ordinal));
        var reference = line[(line.IndexOf("[ref=", StringComparison.Ordinal) + 1)..line.LastIndexOf(']')];

        (await Ids(page, reference + " >> role=link[name=\"Home\"]")).Should().Equal("home");
        (await Ids(page, "role=navigation >> " + reference)).Should().BeEmpty();
        (await page.CountAsync(reference)).Should().Be(1);
    }

    [Test]
    public async Task MalformedLocatorsMatchNothingInInputsAndThrowInQueries()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><button>Next</button>");

        foreach (var target in new[]
                 {
                     "role=", "role=button[name]", "role=button[name=\"Next]", "role=button[unknown]", "role=button[level=x]",
                     "role=button[checked=maybe]", "role=button[disabled=\"yes\"]", "role=button name", "text=", "label=\"\"",
                     "text=\"Next\" trailing", "aria/", "ref=abc", "button >>", ">> button", "role=button[expanded=true]i",
                 })
        {
            (await page.ClickAsync(target)).Should().BeFalse(target);
            (await page.IsVisibleAsync(target)).Should().BeFalse(target);
            var thrown = await Caught.ExceptionAsync(() => page.CountAsync(target));
            thrown.Should().BeOfType<DomException>(target).Which.Name.Should().Be("SyntaxError");
        }

        (await page.CountAsync("role=nonexistent")).Should().Be(0);
    }

    [Test]
    public async Task WaitsAndQueriesFollowTheDocumentAcrossChanges()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><main></main>");

        var appeared = page.WaitForSelectorAsync("role=button[name=\"Later\"]", Patience);
        var counted = page.WaitForCountAsync("main >> role=button", 2, Patience);
        var shown = page.WaitForVisibleAsync("role=button[name=\"Later\"s]", Patience);
        var text = page.WaitForTextAsync("role=main", "Later", Patience);
        await page.EvaluateAsync("""
            setTimeout(() => {
                document.querySelector('main').innerHTML = '<button>Later</button><button aria-label="Later too">x</button>';
            }, 50);
            """);

        (await appeared).Should().BeTrue();
        (await counted).Should().BeTrue();
        (await shown).Should().BeTrue();
        (await text).Should().BeTrue();
        (await page.TextsAsync("role=button[name=\"later\"]", visibleOnly: true)).Should().Equal("Later", "x");

        await page.SetContentAsync("<!doctype html><button>Other</button>");
        (await page.CountAsync("role=button[name=\"Later\"]")).Should().Be(0);
        (await page.WaitForCountAsync("role=button", 1, Patience)).Should().BeTrue();
    }

    [Test]
    public async Task APlainSelectorIsUnchanged()
    {
        await using var browser = new NativeBrowser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><button id=a>One</button><button id=b title=\"role=x\">Two</button>");

        (await Ids(page, "  button  ")).Should().Equal("a", "b");
        (await Ids(page, "[title=\"role=x\"]")).Should().Equal("b");
        (await Ids(page, "[title='a>>b'], #a")).Should().Equal("a");
        (await page.InnerTextAsync("button", 1)).Should().Be("Two");
        var thrown = await Caught.ExceptionAsync(() => page.CountAsync("button["));
        thrown.Should().BeOfType<DomException>();
    }

    /// <summary>The ids of every element the target names, in document order, cross-checked against the count.</summary>
    private static async Task<string[]> Ids(Jint.Browser.Page page, string target)
    {
        var ids = await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            return ElementLocator.Parse(target).FindAll(runtime.Dom, runtime.Document)
                .Select(element => element.GetAttribute("id") ?? "")
                .ToArray();
        });
        (await page.CountAsync(target)).Should().Be(ids.Length, target);
        return ids;
    }
}

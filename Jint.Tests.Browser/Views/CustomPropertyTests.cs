namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class CustomPropertyTests
{
    [TestCase("--a:var(--a)")]
    [TestCase("--a:var(--a, red)")]
    [TestCase("--a:var(--b);--b:var(--a)")]
    [TestCase("--a:var(--b, red);--b:var(--a, blue)")]
    [TestCase("--a:var(--b);--b:var(--c);--c:var(--a)")]
    [TestCase("--a:var(--present, var(--a));--present:red")]
    [TestCase("--a:var(--present, calc(var(--a)));--present:red")]
    [TestCase("--a:var(--b);--b:var(--present, calc(var(--a)));--present:red")]
    [TestCase("--a:var(--b, var(--c));--b:var(--a);--c:var(--b, red)")]
    public async Task EveryMemberOfACycleIsInvalidEvenWithAFallback(string declarations)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <style>
              #t { {{declarations}}; color:var(--a); width:var(--a); opacity:.25 }
            </style>
            <span id="t">text</span>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const s = getComputedStyle(document.getElementById('t'));
              return [s.getPropertyValue('--a'), s.getPropertyValue('--b'),
                      s.getPropertyValue('--c'), s.color, s.opacity, s.width].join('|');
            })()
            """)).Should().Be("||||0.25|1280px");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("var(--a, red)", "rgba(255, 0, 0, 1)")]
    [TestCase("var(--a, var(--missing, blue))", "rgba(0, 0, 255, 1)")]
    [TestCase("var(--recovered)", "rgba(0, 128, 0, 1)")]
    [TestCase("var(--missing, var(--a))", "")]
    [TestCase("var(--a,)", "")]
    public async Task AConsumerCanRecoverFromAnInvalidVariable(string value, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <style>
              :root { --a:var(--b); --b:var(--a); --recovered:var(--a, green) }
              span { color:{{value}} }
            </style>
            <span>text</span>
            """);

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('span')).color")).Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InheritanceUsesResolvedCustomPropertiesAndChildCyclesDoNotPoisonSiblings()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>
              #parent { --a:red; --b:var(--a); --bad:var(--bad); color:green }
              #child { --a:blue; color:var(--b) }
              #cycle { --a:var(--a); color:var(--a) }
              #sibling { color:var(--a) }
              #invalid { --bad:inherit; color:var(--bad, blue) }
            </style>
            <div id="parent">
              <span id="child"></span><span id="cycle"></span>
              <span id="sibling"></span><span id="invalid"></span>
            </div>
            """);

        (await page.EvaluateAsync<string>(
            """
            ['child','cycle','sibling','invalid'].map(id =>
              getComputedStyle(document.getElementById(id)).color).join('|')
            """)).Should().Be(
                "rgba(255, 0, 0, 1)|rgba(0, 128, 0, 1)|rgba(255, 0, 0, 1)|rgba(0, 0, 255, 1)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AChildCannotRepairAnInheritedCycleByOverridingOneOfItsMembers()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>
              :root { --a:var(--b); --b:var(--a) }
              span { --a:red; color:var(--b, green) }
            </style>
            <span>text</span>
            """);

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('span')).color")).Should().Be("rgba(0, 128, 0, 1)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task InitialAndUnsetCustomPropertiesHaveTheirCssWideMeaning()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>
              div { --a:red }
              span { color:var(--a, blue) }
              #initial { --a:initial }
              #unset { --a:unset }
            </style>
            <div><span id="initial"></span><span id="unset"></span></div>
            """);

        (await page.EvaluateAsync<string>(
            "['initial','unset'].map(id => getComputedStyle(document.getElementById(id)).color).join('|')"))
            .Should().Be("rgba(0, 0, 255, 1)|rgba(255, 0, 0, 1)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SharedDependenciesAreNotCyclesAndCustomPropertyNamesAreCaseSensitive()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <span style="--a:red;--A:blue;--b:var(--a);--c:var(--b, var(--a));color:var(--c);background-color:var(--A)">text</span>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const s = getComputedStyle(document.querySelector('span'));
              return s.color + '|' + s.backgroundColor;
            })()
            """)).Should().Be("rgba(255, 0, 0, 1)|rgba(0, 0, 255, 1)");
        page.Errors.Should().BeEmpty();
    }

    [TestCase("--a:hidden", "hidden", "hidden")]
    [TestCase("--a:var(--b)", "", "hidden")]
    public async Task ARuleMatchingParentAndChildDeclaresVariablesLocally(string child, string variable, string visibility)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <style>
              div { --b:var(--a); visibility:var(--b, hidden) }
              #parent { --a:visible }
              #child { {{child}} }
            </style>
            <div id="parent"><div id="child">text</div></div>
            """);

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = document.getElementById('child');
              const s = getComputedStyle(e);
              return [s.getPropertyValue('--b'), s.visibility, e.getBoundingClientRect().height].join('|');
            })()
            """)).Should().Be($"{variable}|{visibility}|0");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task NestedRawFallbacksDoNotUseTheClrCallStack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span>text</span>");
        (await page.EvaluateAsync<string>(
            """
            (() => {
              const e = document.querySelector('span');
              e.style.setProperty('--a', 'var(--missing,calc(red))');
              e.style.color = 'var(--a,blue)';
              const shallow = getComputedStyle(e).color;
              e.style.setProperty('--a', 'var(--missing,calc('.repeat(8192) + 'red' + '))'.repeat(8192));
              return shallow + '|' + getComputedStyle(e).color;
            })()
            """)).Should().Be("rgba(0, 0, 255, 1)|rgba(0, 0, 255, 1)");
        page.Errors.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LongDependencyGraphsDoNotUseTheClrCallStack(bool cyclic)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        var declarations = string.Concat(Enumerable.Range(0, 4096).Select(i => $"--v{i}:var(--v{i + 1});"));
        await page.SetContentAsync(
            $"<span style='{declarations}--v4096:{(cyclic ? "var(--v0)" : "red")};color:var(--v0,blue)'>text</span>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('span')).color")).Should()
            .Be(cyclic ? "rgba(0, 0, 255, 1)" : "rgba(255, 0, 0, 1)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ANewQuerySeesCyclesAddedAndRemovedWithoutMutatingTheCssom()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<span id='t' style='--a:red;color:var(--a,blue)'>text</span>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const t = document.getElementById('t');
              const read = () => getComputedStyle(t).color;
              const values = [read()];
              t.style.setProperty('--a', 'var(--a)');
              values.push(read(), t.style.getPropertyValue('--a'));
              t.style.setProperty('--a', 'green');
              values.push(read());
              return values.join('|');
            })()
            """)).Should().Be("rgba(255, 0, 0, 1)|rgba(0, 0, 255, 1)|var(--a)|rgba(0, 128, 0, 1)");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CyclesDoNotDiscardVisibilityLayoutOrActionability()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <style>
              :root { --a:var(--b); --b:var(--a) }
              button { color:var(--a); display:var(--a, block) }
              #hidden { display:none }
            </style>
            <button id="save" onclick="this.textContent='Saved'">Save</button>
            <button id="hidden">Hidden</button>
            """);

        (await page.EvaluateAsync<string>(
            """
            ['save','hidden'].map(id => {
              const e = document.getElementById(id);
              return getComputedStyle(e).display + ',' + e.getBoundingClientRect().height;
            }).join('|')
            """)).Should().Be("block,16|none,0");
        (await page.AccessibilitySnapshotAsync()).Should().Contain("Save").And.NotContain("Hidden");
        (await page.ClickAsync("#save")).Should().BeTrue();
        (await page.EvaluateAsync<string>("document.getElementById('save').textContent")).Should().Be("Saved");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AnUnrelatedComputationFailureStillUsesTheExistingCascadeFailurePolicy()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<span style='--a:var(--a);color:var(--a,red);width:20ch;visibility:hidden'>text</span>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('span')).visibility")).Should().Be("visible");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Issue3851DirectBrowserSampleDoesNotOverflow()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <!doctype html>
            <style>
              :root {
                --a: var(--b);
                --b: var(--a);
              }

              button {
                color: var(--a);
              }
            </style>
            <button>Save</button>
            """);

        // Color has no synthetic initial value in the existing resolved-style policy.
        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.querySelector('button')).color")).Should().BeEmpty();
        page.Errors.Should().BeEmpty();
    }
}

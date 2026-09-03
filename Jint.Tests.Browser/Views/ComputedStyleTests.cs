namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>getComputedStyle</c>: AngleSharp.Css's cascade, with ten resolved values over it, read-only.
/// </summary>
/// <remarks>
/// <para>
/// <b>The standing decision, and the exception to it.</b>
/// <a href="https://drafts.csswg.org/cssom/#resolved-values">CSSOM</a> says a computed style answers a
/// resolved value for every supported longhand — the initial value where nothing is declared, and the used
/// value for the box properties. AngleSharp.Css reports only what the cascade <i>declared</i>, so everything
/// else is the empty string, and the decision here has been to record that as a divergence rather than to
/// keep an initial-value table of some three hundred properties in a package whose whole point is that it
/// does not re-implement a CSSOM.
/// </para>
/// <para>
/// That decision still stands for the table. What it cost was a client: Playwright's actionability check
/// ends in <c>style.visibility !== "visible"</c>, so an empty <c>visibility</c> made it read <i>every</i>
/// element of <i>every</i> page as hidden — an unforced <c>ClickAsync</c> waited out its timeout and
/// <c>GetByRole</c> dropped the page. So exactly ten properties resolve, and they are the ones a client
/// reads to decide that an element can be interacted with: <c>visibility</c>, <c>display</c>,
/// <c>opacity</c>, <c>pointer-events</c>, <c>overflow</c> with its two longhands, <c>position</c>, and
/// <c>width</c>/<c>height</c> from the flat box model. <c>Jint.Browser/Dom/Views/ResolvedStyle</c> is the
/// table and argues each one.
/// </para>
/// <para>
/// <b>Everything else is still the declared cascade</b>, and the empty string where nothing declared it —
/// which is what <see cref="APropertyOutsideTheExceptionIsStillTheDeclaredCascade"/> pins, so the exception
/// cannot quietly grow into the table it was written instead of.
/// </para>
/// </remarks>
public sealed class ComputedStyleTests
{
    /// <summary>The ten, with the value CSS's initial value gives each.</summary>
    private static readonly (string Property, string Initial)[] _resolved =
    [
        ("visibility", "visible"),
        ("display", "inline"),
        ("opacity", "1"),
        ("pointer-events", "auto"),
        ("overflow", "visible"),
        ("overflow-x", "visible"),
        ("overflow-y", "visible"),
        ("position", "static"),
    ];

    [Test]
    public async Task AStyleElementRuleAndAnInlineStyleBothReachTheComputedStyle()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              .tinted { color: rgb(0, 128, 0) }
              #by-id { font-weight: bold }
            </style>
            <p id="by-id" class="tinted" style="text-align: center">text</p>
            """);

        var computed = "getComputedStyle(document.getElementById('by-id'))";

        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('font-weight')")).Should().Be("bold");
        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('text-align')")).Should().Be("center");
        (await page.EvaluateAsync<string>(computed + ".color")).Should().Contain("0, 128, 0");
        (await page.EvaluateAsync<bool>(computed + " instanceof CSSStyleDeclaration")).Should().BeTrue();
    }

    /// <summary>Each of the eight non-geometric ones, where the cascade declares nothing at all.</summary>
    [Test]
    public async Task EveryResolvedPropertyAnswersItsInitialValueWhereNothingIsDeclared()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<span id='plain'>b</span>");

        foreach (var (property, initial) in _resolved)
        {
            (await page.EvaluateAsync<string>(
                "getComputedStyle(document.getElementById('plain')).getPropertyValue('" + property + "')"))
                .Should().Be(initial, "nothing declares {0}, so it resolves to CSS's initial value", property);
        }

        // And through the IDL attribute, which is the spelling every client actually uses.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('plain')).visibility")).Should().Be("visible");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('plain')).display")).Should().Be("inline");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('plain')).pointerEvents")).Should().Be("auto");
    }

    /// <summary>A declaration always wins, which is what keeps the resolved value a fallback.</summary>
    [Test]
    public async Task ADeclaredValueBeatsTheResolvedOne()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              #declared {
                visibility: hidden; display: block; opacity: 0.25; pointer-events: none;
                overflow: hidden; overflow-x: scroll; overflow-y: auto; position: absolute;
                width: 40px; height: 12px;
              }
            </style>
            <span id="declared">c</span>
            """);

        var expected = new (string Property, string Value)[]
        {
            ("visibility", "hidden"),
            ("display", "block"),
            ("opacity", "0.25"),
            ("pointer-events", "none"),
            ("overflow-x", "scroll"),
            ("overflow-y", "auto"),
            ("position", "absolute"),
            ("width", "40px"),
            ("height", "12px"),
        };

        foreach (var (property, value) in expected)
        {
            (await page.EvaluateAsync<string>(
                "getComputedStyle(document.getElementById('declared')).getPropertyValue('" + property + "')"))
                .Should().Be(value, "{0} is declared, so the cascade answers rather than the resolved value", property);
        }
    }

    /// <summary>
    /// <c>display: none</c> on an ancestor: the descendant is still <c>visible</c> and has no box.
    /// </summary>
    /// <remarks>
    /// CSS does not inherit <c>display</c>, so a browser answers the descendant's own computed
    /// <c>display</c> — <c>inline</c> for a <c>&lt;span&gt;</c> — and <c>visibility: visible</c>, because
    /// being out of the layout is not the same thing as being invisible. What it has none of is a box, and
    /// the resolved <c>width</c> of an element with no box is its computed value: <c>auto</c>. Playwright
    /// reads exactly this pair — <c>visibility</c> from the element and <c>display: none</c> from the
    /// ancestor walk — so answering a fabricated <c>1280px</c> here would make a hidden subtree clickable.
    /// </remarks>
    [Test]
    public async Task ADisplayNoneAncestorLeavesTheDescendantVisibleAndWithoutABox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<style>#gone { display: none }</style><div id='gone'><span id='inside'>d</span></div>");

        var inside = "getComputedStyle(document.getElementById('inside'))";

        (await page.EvaluateAsync<string>(inside + ".visibility")).Should().Be("visible");
        (await page.EvaluateAsync<string>(inside + ".display")).Should().Be("inline", "display is not inherited");
        (await page.EvaluateAsync<string>(inside + ".width")).Should().Be("auto", "an element with no box has no used width");
        (await page.EvaluateAsync<string>(inside + ".height")).Should().Be("auto");

        // The ancestor itself keeps its declaration, which is what a client's own walk reads.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('gone')).display")).Should().Be("none");
    }

    /// <summary>
    /// <c>visibility</c> is inherited, so a hidden ancestor hides its subtree — and a descendant can escape.
    /// </summary>
    [Test]
    public async Task VisibilityIsInheritedAndADescendantCanDeclareItsWayBack()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              #veiled { visibility: hidden }
              #shown { visibility: visible }
            </style>
            <div id="veiled"><span id="child">e</span><span id="shown">f</span></div>
            """);

        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('veiled')).visibility")).Should().Be("hidden");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('child')).visibility"))
            .Should().Be("hidden", "CSS inherits visibility, and the cascade answers before the resolved value does");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('shown')).visibility"))
            .Should().Be("visible", "a descendant that declares visible comes back");
    }

    /// <summary>The geometry is the flat box model's, and it agrees with the box the same page reports.</summary>
    [Test]
    public async Task WidthAndHeightAreTheFlatBoxModelAndAgreeWithGetBoundingClientRect()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='block'>a</div><span id='leaf'>b</span>");

        var leaf = "getComputedStyle(document.getElementById('leaf'))";

        (await page.EvaluateAsync<string>(leaf + ".width")).Should().Be("1280px", "every box is the viewport's width");
        (await page.EvaluateAsync<string>(leaf + ".height")).Should().Be("16px", "a leaf owns exactly one row");

        (await page.EvaluateAsync<bool>(
            """
            (() => {
              const el = document.getElementById('leaf');
              const rect = el.getBoundingClientRect();
              const style = getComputedStyle(el);
              return style.width === rect.width + 'px' && style.height === rect.height + 'px';
            })()
            """))
            .Should().BeTrue("a client that compares the two is told one story");

        // A container's box spans its subtree, so it is taller than one row.
        (await page.EvaluateAsync<string>("getComputedStyle(document.body).height"))
            .Should().Be("48px", "the body owns its own row and the two elements under it");

        // And the cascade still wins where AngleSharp's user-agent sheet declares something: a <div> is
        // block because that sheet says so, where the <span> above took the initial value.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('block')).display")).Should().Be("block");
    }

    /// <summary>What is deliberately not resolved, which is what keeps the exception an exception.</summary>
    [Test]
    public async Task APropertyOutsideTheExceptionIsStillTheDeclaredCascade()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<span id='plain'>b</span>");

        var plain = "getComputedStyle(document.getElementById('plain'))";

        foreach (var property in new[] { "color", "font-size", "margin-top", "z-index", "background-color", "cursor" })
        {
            (await page.EvaluateAsync<string>(plain + ".getPropertyValue('" + property + "')"))
                .Should().BeEmpty("{0} is outside the ten, so it stays the declared cascade", property);
        }

        // And the enumeration stays the declared set rather than growing the ten into a list.
        (await page.EvaluateAsync<int>(plain + ".length")).Should().Be(0);
    }

    /// <summary>
    /// A percentage anywhere in the matching cascade must not become a CLR exception in script.
    /// </summary>
    /// <remarks>
    /// AngleSharp.Css 1.0.2 resolves every length against an <c>IRenderDevice</c> whose default viewport is
    /// 0 × 0, and raises <c>ArgumentException</c> rather than skipping the declaration — so
    /// <c>width: 50%</c>, <c>height: 100vh</c> and <c>calc(100% - 10px)</c> each made <c>getComputedStyle</c>
    /// throw out of AngleSharp and into the page. It is the member every automation client calls on every
    /// element it touches, so the guard is what makes the resolved values reachable on a real page at all.
    /// Recorded as an AngleSharp divergence in <c>Jint.Browser/AGENTS.md</c>.
    /// </remarks>
    [Test]
    public async Task ARelativeLengthInTheCascadeDoesNotThrowIntoThePage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            "<style>#sized { width: 50%; height: 100vh }</style><div id='sized'>g</div>");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              try { return getComputedStyle(document.getElementById('sized')).visibility }
              catch (e) { return 'threw: ' + e }
            })()
            """))
            .Should().Be("visible", "a cascade AngleSharp cannot compute leaves the resolved values answering");

        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TheComputedStyleRefusesEveryWrite()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p' style='color: rgb(1, 2, 3)'>text</p>");

        var refusal =
            """
            (member => {
              const style = getComputedStyle(document.getElementById('p'));
              try {
                if (member === 'setProperty') { style.setProperty('color', 'blue') }
                else if (member === 'removeProperty') { style.removeProperty('color') }
                else if (member === 'cssText') { style.cssText = 'color: blue' }
                else { style.color = 'blue' }
                return 'no throw';
              } catch (e) { return e.name }
            })
            """;

        foreach (var member in new[] { "setProperty", "removeProperty", "cssText", "color" })
        {
            (await page.EvaluateAsync<string>("(" + refusal + ")('" + member + "')"))
                .Should().Be("NoModificationAllowedError", "writing {0} on a computed style is refused", member);
        }

        // And the element's own style is untouched, which is the point of refusing rather than accepting a
        // write into a detached copy.
        (await page.EvaluateAsync<string>("document.getElementById('p').style.color")).Should().Contain("1, 2, 3");
    }

    [Test]
    public async Task TheInlineStyleIsStillWritable()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<p id='p'>text</p>");

        await page.EvaluateAsync("document.getElementById('p').style.setProperty('font-weight', 'bold')");

        (await page.EvaluateAsync<string>("document.getElementById('p').getAttribute('style')")).Should().Contain("font-weight");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).getPropertyValue('font-weight')")).Should().Be("bold");
    }

    [Test]
    public async Task ThePseudoElementArgumentIsAcceptedAndIgnored()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<style>#p::before { content: 'x' }</style><p id='p' style='color: rgb(9, 9, 9)'>text</p>");

        // Documented divergence: the pseudo-element selector is ignored, so the answer is the element's own
        // computed style rather than the pseudo-element's.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p'), '::before').color"))
            .Should().Contain("9, 9, 9");
    }

    [Test]
    public async Task GetComputedStyleNeedsAnElement()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>(
            "(() => { try { getComputedStyle({}); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }
}

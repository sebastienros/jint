using Jint.Browser.Runtime;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>CSSOM resolved values over the native computed query, with the synthetic flat box policy.</summary>
public sealed class ComputedStyleTests
{
    [TestCase("@media all")]
    [TestCase("@layer theme")]
    [TestCase("@supports (display:block)")]
    public async Task UnsupportedNestedAtRulesDoNotBreakPageWideStyleReads(string prelude)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<style>.card { color:green; " + prelude +
            " { color:red; & { display:none; } } display:block; } " +
            "#other { color:blue; } .hidden { display:none; }</style>" +
            "<p id=card class=card>card</p><p id=other>other</p>" +
            "<p id=plain>plain</p><p id=hidden class=hidden>hidden</p>");
        (await page.EvaluateAsync<string>("getComputedStyle(card).color")).Should().Be("rgb(0, 128, 0)");
        (await page.EvaluateAsync<string>("getComputedStyle(card).display")).Should().Be("block");
        (await page.EvaluateAsync<string>("getComputedStyle(other).color")).Should().Be("rgb(0, 0, 255)");
        (await page.EvaluateAsync<string>("getComputedStyle(plain).display")).Should().Be("block");
        (await page.EvaluateAsync<string>("card.innerText")).Should().Be("card");
        (await page.EvaluateAsync<string>("other.innerText")).Should().Be("other");
        (await page.EvaluateAsync<string>("plain.innerText")).Should().Be("plain");
        (await page.EvaluateAsync<string>("getComputedStyle(hidden).display")).Should().Be("none");
        (await page.EvaluateAsync<string>("document.body.innerText")).Should().NotContain("hidden");
        (await page.EvaluateAsync<string>("document.styleSheets[0].cssRules[0].cssRules[0].cssText"))
            .Should().Be(prelude + " { color:red; & { display:none; } }");
        (await page.EvaluateAsync<bool>("(() => { const rule = document.styleSheets[0].cssRules[0]; " +
            "return rule.cssRules === rule.rules && rule.cssRules === rule.cssRules && " +
            "Object.getPrototypeOf(rule.cssRules[0]) === CSSRule.prototype && " +
            "rule.cssRules[0].parentRule === rule; })()")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }

    /// <summary>Initial values for the interaction properties.</summary>
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

    /// <summary>
    /// CSSOM §9: a color property's resolved value is its used color, serialized per CSS Color 4 §15.2 as
    /// legacy <c>rgb()</c>, or <c>rgba()</c> below full opacity; <c>currentcolor</c> is the element's color.
    /// </summary>
    [TestCase("color: red", "color", "rgb(255, 0, 0)")]
    [TestCase("color: #0f08", "color", "rgba(0, 255, 0, 0.533)")]
    [TestCase("color: #00ff0080", "color", "rgba(0, 255, 0, 0.5)")]
    [TestCase("color: hsl(240 100% 50% / 25%)", "color", "rgba(0, 0, 255, 0.25)")]
    [TestCase("color: RebeccaPurple", "color", "rgb(102, 51, 153)")]
    [TestCase("background-color: transparent", "background-color", "rgba(0, 0, 0, 0)")]
    [TestCase("color: lime; border-top-color: currentcolor", "border-top-color", "rgb(0, 255, 0)")]
    [TestCase("color: currentcolor", "color", "rgb(0, 0, 255)")]
    [TestCase("", "color", "rgb(0, 0, 255)")]
    [TestCase("color: CanvasText", "color", "CanvasText")]
    [TestCase("fill: url(#paint)", "fill", "url(#paint)")]
    public async Task ColorPropertiesResolveToSrgb(string declarations, string property, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<div style='color: blue'><p id=t style='{declarations}'>x</p></div>");

        (await page.EvaluateAsync<string>($"getComputedStyle(t).getPropertyValue('{property}')")).Should().Be(expected);
        page.Errors.Should().BeEmpty();
    }

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
        (await page.EvaluateAsync<string>("document.styleSheets[0].cssRules[1].style.fontWeight")).Should().Be("bold");
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

        await page.SetContentAsync("<div id='block'>a</div><div id='leaf'>b</div>");

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

        // The native user-agent sheet supplies HTML block defaults.
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('block')).display")).Should().Be("block");
    }

    /// <summary>Supported longhands expose native initial values and enumeration.</summary>
    [Test]
    public async Task SupportedLonghandsAnswerInitialValuesAndAreEnumerated()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<span id='plain'>b</span>");

        var plain = "getComputedStyle(document.getElementById('plain'))";

        foreach (var (property, expected) in new[]
        {
            ("color", "rgb(0, 0, 0)"), ("font-size", "16px"), ("margin-top", "0px"),
            ("z-index", "auto"), ("background-color", "rgba(0, 0, 0, 0)"), ("cursor", "auto")
        })
        {
            (await page.EvaluateAsync<string>(plain + ".getPropertyValue('" + property + "')")).Should().Be(expected);
            (await page.EvaluateAsync<bool>("Array.from(" + plain + ").includes('" + property + "')")).Should().BeTrue();
        }
        (await page.EvaluateAsync<int>(plain + ".length")).Should().BeGreaterThan(6);

    }

    [TestCase("100%", "100%")]
    [TestCase("50%", "50%")]
    [TestCase("calc(100% - 10px)", "calc(100% - 10px)")]
    [TestCase("2em", "2em")]
    [TestCase("2rem", "2rem")]
    [TestCase("10vw", "10vw")]
    [TestCase("10vh", "10vh")]
    public async Task MinimumWidthKeepsComputedPercentages(string value, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($"<div id='t' style='min-width:{value}'>g</div>");
        (await Read(page, "min-width")).Should().Be(expected);
    }

    [Test]
    public async Task InvalidKeywordsUseInitialValuesAndUnusedFontMetricsDoNotDiscardVisibility()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        foreach (var (property, expected) in new[]
        {
            ("width", "1280px"), ("height", "16px"), ("margin-left", "auto"),
            ("min-width", "auto"), ("padding-left", "auto"), ("font-size", "auto")
        })
        {
            await page.SetContentAsync($"<div id='t' style='{property}:auto'>g</div>");
            (await Read(page, property)).Should().Be(expected);
        }
        await page.SetContentAsync("<div id='t' style='width:20ch;visibility:hidden'>g</div>");
        (await Read(page, "visibility")).Should().Be("hidden");
        (await page.EvaluateAsync<int>("document.getElementById('t').getClientRects().length")).Should().Be(0);
        // A missing box retains declaration text without requiring a font metric.
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var element = DomDocumentReads.ById(runtime.Dom, runtime.Document!, "t")!;
            var style = CssCascade.Traversal.For(runtime.Document)!.Of(element);
            ResolvedStyle.ValueOf("width", style, element, runtime).Should().Be("20ch");
            return true;
        });
    }

    [Test]
    public async Task UnresolvedInheritanceDoesNotReplaceNativeReadsAndStillResolvesOnDemand()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("""
            <style>
              :root { --extent: 10px; text-decoration: underline solid red }
              .outer { width: 10px }
              .inner { width: auto; --extent: 20px }
              #t { width: inherit; text-decoration: inherit; visibility: visible; color: blue }
            </style>
            <span class="outer"><span class="inner"><span id="t">target</span><span id="direct">direct</span></span></span>
            <span id="root-child" style="text-decoration:inherit">root child</span>
            """);
        await page.EvaluateAsync("document.documentElement.appendChild(document.getElementById('root-child'))");

        (await page.EvaluateAsync<string>(
            """
            (() => {
              const style = getComputedStyle(document.getElementById('t'));
              const color = style.color;
              return [
                style.visibility, style.width, style.textDecorationLine,
                style.getPropertyValue('text-decoration').includes('underline'),
                Array.from(style).includes('width'), style.cssText.includes('20px'),
                style.color === color
              ].join('|');
            })()
            """)).Should().Be("visible|1280px||false|true|false|true");
        (await page.EvaluateAsync<string>("getComputedStyle(document.querySelector('.outer')).width")).Should().Be("1280px");
        (await page.EvaluateAsync<string>("getComputedStyle(document.querySelector('.inner')).width")).Should().Be("1280px");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('t')).cssText")).Should().BeEmpty();
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('root-child')).getPropertyValue('text-decoration')"))
            .Should().Be("underline solid red");
        await page.EvaluateAsync("document.getElementById('direct').style.minWidth = 'var(--extent)'");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('direct')).minWidth")).Should().Be("20px");
        await page.EvaluateAsync("document.querySelector('.inner').style.setProperty('--extent', '30px')");
        (await page.EvaluateAsync<string>("getComputedStyle(document.getElementById('direct')).minWidth")).Should().Be("30px");
        page.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// An <c>@media</c> rule in the cascade answers the same question <c>matchMedia</c> does.
    /// </summary>
    /// <remarks>
    /// Half of <see href="https://github.com/sebastienros/jint/issues/3707">#3707</see>: the cascade is
    /// evaluated against the page's own render device, so a dimension query in a style sheet and the same
    /// query through <c>matchMedia</c> read one viewport. Supported preference features use the same
    /// environment through <c>IRenderDevicePreferences</c>.
    /// </remarks>
    [TestCase(1280, "absolute")]
    [TestCase(400, "relative")]
    public async Task ADimensionMediaRuleInTheCascadeAgreesWithMatchMedia(int width, string expected)
    {
        await using var browser = new Browser(new global::Jint.Browser.BrowserOptions { Viewport = new global::Jint.Browser.Viewport(width, 720) });
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              #t { position: relative }
              @media (min-width: 600px) { #t { position: absolute } }
            </style>
            <div id="t">g</div>
            """);

        (await Read(page, "position")).Should().Be(expected);

        (await page.EvaluateAsync<bool>("matchMedia('(min-width: 600px)').matches"))
            .Should().Be(expected == "absolute", "the page's own matchMedia reads the same viewport");

        page.Errors.Should().BeEmpty();
    }

    [TestCase("prefers-color-scheme", "dark", false)]
    [TestCase("prefers-color-scheme", "light", true)]
    [TestCase("prefers-reduced-motion", "reduce", false)]
    [TestCase("prefers-reduced-transparency", "reduce", false)]
    [TestCase("prefers-reduced-data", "reduce", false)]
    [TestCase("prefers-contrast", "more", false)]
    [TestCase("forced-colors", "active", false)]
    [TestCase("display-mode", "standalone", false)]
    [TestCase("hover", "none", false)]
    [TestCase("any-hover", "none", false)]
    [TestCase("pointer", "coarse", false)]
    [TestCase("any-pointer", "coarse", false)]
    public async Task PreferenceRulesReadThePagesDefaultsAndLiveEmulatedValues(string feature, string value, bool defaultMatch)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            $$"""
            <style>
              #t { position: relative }
              @media ({{feature}}: {{value}}) { #t { position: absolute } }
            </style>
            <div id="t">text</div>
            """);
        var matches = $"matchMedia('({feature}: {value})').matches";

        (await Read(page, "position")).Should().Be(defaultMatch ? "absolute" : "relative");
        (await page.EvaluateAsync<bool>(matches)).Should().Be(defaultMatch);

        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Features = new Dictionary<string, string> { [feature] = value } });
            return 0;
        });

        (await Read(page, "position")).Should().Be("absolute");
        (await page.EvaluateAsync<bool>(matches)).Should().BeTrue();

        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Features = PageMediaEnvironment.Default.Features });
            return 0;
        });

        (await Read(page, "position")).Should().Be(defaultMatch ? "absolute" : "relative");
        (await page.EvaluateAsync<bool>(matches)).Should().Be(defaultMatch);
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task PreferenceCascadesStayLocalToTheirBrowsingContextAndObserveStylesheetWrites()
    {
        await using var browser = new Browser();
        await using var firstContext = await browser.NewContextAsync();
        await using var secondContext = await browser.NewContextAsync();
        var first = await firstContext.NewPageAsync();
        var second = await secondContext.NewPageAsync();
        const string content =
            """
            <style>
              #t { position: relative }
              @media (prefers-color-scheme: dark) { #t { position: absolute } }
            </style>
            <span id="t">text</span>
            """;
        await first.SetContentAsync(content);
        await second.SetContentAsync(content);
        await first.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with
            {
                Features = new Dictionary<string, string> { ["prefers-color-scheme"] = "dark" }
            });
            return 0;
        });

        (await Read(first, "position")).Should().Be("absolute");
        (await Read(second, "position")).Should().Be("relative");

        await first.EvaluateAsync("document.styleSheets[0].cssRules[1].cssRules[0].style.position = 'fixed'");
        (await Read(first, "position")).Should().Be("fixed");
        (await Read(second, "position")).Should().Be("relative");
        first.Errors.Should().BeEmpty();
        second.Errors.Should().BeEmpty();
    }

    private static async Task<string> Read(global::Jint.Browser.Page page, string property)
        => await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('t')).getPropertyValue('" + property + "')")
           ?? "";

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
        (await page.EvaluateAsync<string>("document.getElementById('p').style.fontWeight")).Should().Be("bold");
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

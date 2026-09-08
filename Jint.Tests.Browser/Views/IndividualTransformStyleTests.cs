namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The individual transform properties —
/// <a href="https://drafts.csswg.org/css-transforms-2/#individual-transforms">CSS Transforms Level 2 §3</a>'s
/// <c>translate</c>, <c>rotate</c> and <c>scale</c> — reach a computed style and a box without taking the
/// process with them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a suite of its own.</b> Every one of these declarations used to be fatal rather than
/// wrong. AngleSharp.Css shapes those three properties as an <c>Or</c> of <c>none</c> with an <i>any</i>
/// arm, and <c>CssAnyValue.Compute</c> only avoided reparsing when the compute context's converter was
/// <i>directly</i> that any-converter; through the composite it reparsed the unchanged text with the same
/// composite converter, whose any arm handed back another unresolved value, forever
/// (<a href="https://github.com/AngleSharp/AngleSharp.Css/issues/243">AngleSharp/AngleSharp.Css#243</a>,
/// fixed by <a href="https://github.com/AngleSharp/AngleSharp.Css/pull/244">#244</a> and released in
/// 1.1.1-beta.308). A stack overflow is not an exception in .NET: it cannot be caught, so
/// <c>Dom/Views/CssCascade</c>'s guard — the door every <c>ComputeCurrentStyle()</c> caller comes through —
/// could not have converted it into the <see langword="null"/> cascade that every other CSS failure becomes.
/// <c>&lt;div style="translate: 1px"&gt;</c> took the whole host down with it, and a page is not something a
/// host can sandbox out of that.
/// </para>
/// <para>
/// <b>Both doors, because they are two code paths.</b> <c>getComputedStyle</c> reads the native
/// per-element cascade through <c>CssCascade.Of</c>, while a geometry query builds the flat box model over
/// <c>CssCascade.Traversal</c>, which computes each element's declarations against its own context. The
/// first is the exact shape the upstream issue reports; the second was measured to be just as fatal on
/// 1.1.0, running alone, so both are pinned here.
/// </para>
/// <para>
/// <b>What this does not claim.</b> The upstream fix is a computation-boundary fix, so the value that comes
/// back is the token sequence AngleSharp.Css parsed, not a browser's normalized <c>translate</c>. Nothing
/// here transforms a box: the flat layout has no transform stage, and the geometry assertions are about a
/// box still being answered rather than about where it moved to.
/// </para>
/// </remarks>
public sealed class IndividualTransformStyleTests
{
    /// <summary>
    /// The declarations the upstream issue confirms as fatal in 1.1.0, plus the multi-component and
    /// percentage forms its reproduction lists.
    /// </summary>
    [TestCase("translate", "1px")]
    [TestCase("translate", "0 -50%")]
    [TestCase("translate", "10px 20px 30px")]
    [TestCase("rotate", "45deg")]
    [TestCase("scale", "1")]
    public async Task AnIndividualTransformComputesRatherThanReenteringItsConverter(string property, string value)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='moved' style='" + property + ": " + value + "'>a</div>");

        var computed = "getComputedStyle(document.getElementById('moved'))";

        // The token sequence AngleSharp.Css parsed, unchanged: the upstream fix is about the computation
        // terminating, not about a browser's normalized transform grammar.
        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('" + property + "')"))
            .Should().Be(value, "the declared {0} is in the cascade, so the computed style answers it", property);

        // And the rest of the cascade survives the same call: ComputeCurrentStyle() computes every matched
        // declaration in one pass, so a property that could not compute used to take the whole style with it.
        (await page.EvaluateAsync<string>(computed + ".visibility")).Should().Be("visible");
    }

    /// <summary>
    /// The keyword arm of the same converter, which terminated even in 1.1.0 — so a green run here is not
    /// evidence for the cases above.
    /// </summary>
    [TestCase("translate")]
    [TestCase("rotate")]
    [TestCase("scale")]
    public async Task TheNoneKeywordStillComputes(string property)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='still' style='" + property + ": none'>a</div>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('still')).getPropertyValue('" + property + "')"))
            .Should().Be("none");
    }

    /// <summary>
    /// The other door: a geometry query over a document whose style sheet puts an individual transform on
    /// an element and on one of its ancestors, so the cascade the flat layout walks carries it too.
    /// </summary>
    [Test]
    public async Task AGeometryQueryOverATranslatedSubtreeStillAnswersABox()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync(
            """
            <style>
              #outer { translate: 0 -50%; rotate: 45deg }
              #inner { translate: 10px 20px; scale: 2 }
            </style>
            <div id="outer"><span id="inner">a</span></div>
            """);

        (await page.EvaluateAsync<bool>(
            """
            (() => {
              const rect = document.getElementById('inner').getBoundingClientRect();
              return rect.width > 0 && rect.height > 0;
            })()
            """))
            .Should().BeTrue("the flat box model computes the cascade for every rendered element");

        // The ancestor that declares its own individual transforms has a box as well, so the walk reached
        // both elements rather than stopping at the first one it could compute.
        (await page.EvaluateAsync<double>("document.getElementById('outer').getBoundingClientRect().height"))
            .Should().BeGreaterThan(0);
    }
}

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The <c>transform</c> property's translate functions —
/// <a href="https://drafts.csswg.org/css-transforms-1/#funcdef-transform-translate">CSS Transforms Level 1
/// §12.1</a>'s <c>translate()</c>, <c>translateX()</c>, <c>translateY()</c> and
/// <a href="https://drafts.csswg.org/css-transforms-2/#funcdef-three-d-transform-translatez">Level 2
/// §11.2</a>'s <c>translateZ()</c> — reach a computed style with the components the page omitted still
/// omitted, rather than being dropped from the cascade.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a suite of its own.</b> <c>CssTranslateValue</c> holds one <c>ICssValue</c> per axis and
/// leaves the axes the function did not name <see langword="null"/> — that is how it tells
/// <c>translateX(10px)</c> from <c>translate3d(10px, 0, 0)</c> when it serializes itself. Its
/// <c>ICssValue.Compute</c> dereferenced all three unconditionally, so computing any translate that named
/// fewer than three axes raised a <c>NullReferenceException</c> inside the computed declaration
/// (AngleSharp.Css <c>8c7ec8b9</c>, released in 1.1.1-beta.309). That is a failure
/// <c>Dom/Views/CssCascade</c> does catch, so nothing crashed and nothing was reported: the property simply
/// answered the empty string, and a page reading <c>getComputedStyle(el).transform</c> was told the element
/// has no transform at all. Every ordinary 2D spelling is in that set — <c>translate(10px)</c>,
/// <c>translateX()</c>, <c>translateY()</c> and the two-argument <c>translate()</c> — and only the fully
/// three-dimensional <c>translate3d()</c> escaped it, which is why the suite beside this one
/// (<c>IndividualTransformStyleTests</c>) stayed green through the whole of it.
/// </para>
/// <para>
/// <b>What this does not claim.</b> The computed value is the token sequence AngleSharp.Css resolved, not a
/// browser's <c>matrix()</c> normalization: a browser answers <c>matrix(1, 0, 0, 1, 10, 0)</c> where this
/// answers <c>translateX(10px)</c>. Nothing here transforms a box either — the flat layout has no transform
/// stage — so these are assertions about a property being answered, not about where the element moved to.
/// </para>
/// </remarks>
public sealed class TransformFunctionStyleTests
{
    /// <summary>
    /// One case per translate spelling that leaves an axis unnamed, with the serialization the computed
    /// declaration answers for it. Each of these answered <c>""</c> on 1.1.1-beta.308.
    /// </summary>
    [TestCase("translate(10px)", "translateX(10px)")]
    [TestCase("translateX(10px)", "translateX(10px)")]
    [TestCase("translateY(20px)", "translateY(20px)")]
    [TestCase("translateZ(30px)", "translateZ(30px)")]
    [TestCase("translate(10px, 20px)", "translate(10px, 20px)")]
    public async Task ATranslateThatNamesFewerThanThreeAxesComputes(string declared, string computedValue)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='moved' style='transform: " + declared + "'>a</div>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().Be(computedValue, "computing {0} must not dereference the axes it does not name", declared);
    }

    /// <summary>
    /// The same defect through a transform list and through a percentage, whose serializations depend on the
    /// rest of the list and on the page's render device — so these assert that the property is answered at
    /// all, which is exactly what was lost.
    /// </summary>
    [TestCase("translate(0, -50%)", "translate(0, -")]
    [TestCase("translate(10px) rotate(45deg)", "translateX(10px) rotate(")]
    public async Task ATranslateInAListOrOverAPercentageComputesToo(string declared, string prefix)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='moved' style='transform: " + declared + "'>a</div>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().StartWith(prefix, "the whole of {0} is in the computed cascade", declared);
    }

    /// <summary>
    /// The transform functions that named every component they have, which computed even on
    /// 1.1.1-beta.308 — so a green run here is not evidence for the cases above.
    /// </summary>
    [TestCase("translate3d(1px, 2px, 3px)", "translate3d(1px, 2px, 3px)")]
    [TestCase("scale(2)", "scale(2)")]
    [TestCase("scale(2, 3)", "scale(2, 3)")]
    [TestCase("perspective(100px)", "perspective(100px)")]
    [TestCase("matrix(2, 3, 4, 5, 6, 7)", "matrix(2, 3, 4, 5, 6, 7)")]
    public async Task AFullyArgumentedTransformFunctionStillComputes(string declared, string computedValue)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='moved' style='transform: " + declared + "'>a</div>");

        (await page.EvaluateAsync<string>(
            "getComputedStyle(document.getElementById('moved')).getPropertyValue('transform')"))
            .Should().Be(computedValue);
    }
}

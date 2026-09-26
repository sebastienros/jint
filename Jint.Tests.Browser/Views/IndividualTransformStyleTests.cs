#nullable enable

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The individual transform properties —
/// <a href="https://drafts.csswg.org/css-transforms-2/#individual-transforms">CSS Transforms Level 2 §5</a>'s
/// <c>translate</c>, <c>rotate</c> and <c>scale</c> — reach a computed style and a box without taking the
/// process with them.
/// </summary>
/// <remarks>
/// The historical AngleSharp.Css converter recursion could make either a computed-style read or a
/// geometry query fatal (AngleSharp/AngleSharp.Css#243). Native typed values now provide canonical
/// individual-transform computation. These geometry assertions continue to pin survival; the flat
/// layout has no visual transform stage.
/// </remarks>
public sealed class IndividualTransformStyleTests
{
    /// <summary>
    /// The declarations the upstream issue confirms as fatal in 1.1.0, plus the multi-component and
    /// percentage forms its reproduction lists.
    /// </summary>
    [TestCase("translate", "1px", "1px")]
    [TestCase("translate", "0 -50%", "0px -50%")]
    [TestCase("translate", "10px 20px 30px", "10px 20px 30px")]
    [TestCase("rotate", "45deg", "45deg")]
    [TestCase("scale", "1", "1")]
    public async Task AnIndividualTransformComputesRatherThanReenteringItsConverter(string property, string value, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetContentAsync("<div id='moved' style='" + property + ": " + value + "'>a</div>");

        var computed = "getComputedStyle(document.getElementById('moved'))";

        (await page.EvaluateAsync<string>(computed + ".getPropertyValue('" + property + "')"))
            .Should().Be(expected, "the declared {0} is in the cascade, so the computed style answers it", property);

        // A later unrelated read still succeeds after the transform computation.
        (await page.EvaluateAsync<string>(computed + ".visibility")).Should().Be("visible");
        page.Errors.Should().BeEmpty();
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
        page.Errors.Should().BeEmpty();
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
        page.Errors.Should().BeEmpty();
    }
}

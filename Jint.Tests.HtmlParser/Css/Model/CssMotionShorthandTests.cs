using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.HtmlParser.Css.Model;

/// <summary>The animation and transition shorthands set their longhands (#4617).</summary>
/// <remarks>https://drafts.csswg.org/css-animations-1/#animation and https://drafts.csswg.org/css-transitions-1/#transition-shorthand-property</remarks>
[TestFixture]
public sealed class CssMotionShorthandTests
{
    [Test]
    public void AnimationComponentsAreOrderFreeAndOmittedOnesReset()
    {
        var block = CssDeclarationBlock.Parse("animation: spin 2s LINEAR infinite");
        block.GetPropertyValue("animation-name").Should().Be("spin");
        block.GetPropertyValue("animation-duration").Should().Be("2s");
        block.GetPropertyValue("animation-timing-function").Should().Be("linear");
        block.GetPropertyValue("animation-delay").Should().Be("0s");
        block.GetPropertyValue("animation-iteration-count").Should().Be("infinite");
        block.GetPropertyValue("animation-direction").Should().Be("normal");
        block.GetPropertyValue("animation-fill-mode").Should().Be("none");
        block.GetPropertyValue("animation-play-state").Should().Be("running");
        // Chromium's component order, shortest form.
        block.GetPropertyValue("animation").Should().Be("2s linear infinite spin");
        block.CssText.Should().Be("animation: 2s linear infinite spin;");
    }

    [Test]
    public void AnimationTimesKeywordsAndLayersFollowTheGrammar()
    {
        // The second <time> is the delay; a keyword goes to another component before the name.
        var block = CssDeclarationBlock.Parse("animation: 1s 500ms reverse both paused none, fade 3 alternate");
        block.GetPropertyValue("animation-duration").Should().Be("1s, 0s");
        block.GetPropertyValue("animation-delay").Should().Be("500ms, 0s");
        block.GetPropertyValue("animation-direction").Should().Be("reverse, alternate");
        block.GetPropertyValue("animation-fill-mode").Should().Be("both, none");
        block.GetPropertyValue("animation-play-state").Should().Be("paused, running");
        block.GetPropertyValue("animation-iteration-count").Should().Be("1, 3");
        block.GetPropertyValue("animation-name").Should().Be("none, fade");
        block.GetPropertyValue("animation").Should().Be("1s 500ms reverse both paused, 3 alternate fade");

        CssDeclarationBlock.Parse("animation: none").GetPropertyValue("animation").Should().Be("none");
        // The first none is the fill mode, the second the name.
        CssDeclarationBlock.Parse("animation: none none").GetPropertyValue("animation-fill-mode").Should().Be("none");
        CssDeclarationBlock.Parse("animation: 2s cubic-bezier(0.1, 0.7, 1, 0.1) x").GetPropertyValue("animation-timing-function")
            .Should().Be("cubic-bezier(0.1, 0.7, 1, 0.1)");
        // A delay forces the duration to be written.
        CssDeclarationBlock.Parse("animation: x 0s 1s").GetPropertyValue("animation").Should().Be("0s 1s x");
        // A name another component would claim is written after every other component, so it reparses as a name.
        var claimed = CssDeclarationBlock.Parse("animation: 0s ease 0s 1 normal none running reverse");
        claimed.GetPropertyValue("animation-name").Should().Be("reverse");
        claimed.GetPropertyValue("animation").Should().Be("0s ease 0s 1 normal none running reverse");
    }

    [TestCase("animation: 1s 2s 3s x")]
    [TestCase("animation: x y")]
    [TestCase("animation: both none none")]
    [TestCase("animation: linear ease x")]
    [TestCase("animation: x, , y")]
    [TestCase("animation: initial x")]
    [TestCase("animation: calc(1px) spin")]
    [TestCase("animation: calc(1px + 1s) spin")]
    [TestCase("animation: -1s -2s spin")]
    [TestCase("animation: -1 spin")]
    [TestCase("transition: opacity transform")]
    [TestCase("transition: none, opacity")]
    [TestCase("transition: default 1s")]
    public void InvalidShorthandsAreDropped(string source) =>
        CssDeclarationBlock.Parse(source).Count.Should().Be(0);

    [Test]
    public void TransitionComponentsAreOrderFreeAndSerializeLikeChromium()
    {
        var block = CssDeclarationBlock.Parse("transition: OPACITY 300ms ease-in-out, --Custom 1s 2s allow-discrete");
        block.GetPropertyValue("transition-property").Should().Be("opacity, --Custom");
        block.GetPropertyValue("transition-duration").Should().Be("300ms, 1s");
        block.GetPropertyValue("transition-timing-function").Should().Be("ease-in-out, ease");
        block.GetPropertyValue("transition-delay").Should().Be("0s, 2s");
        block.GetPropertyValue("transition-behavior").Should().Be("normal, allow-discrete");
        block.GetPropertyValue("transition").Should().Be("opacity 300ms ease-in-out, --Custom 1s 2s allow-discrete");

        CssDeclarationBlock.Parse("transition: all").GetPropertyValue("transition").Should().Be("all");
        CssDeclarationBlock.Parse("transition: none").GetPropertyValue("transition-property").Should().Be("none");
        CssDeclarationBlock.Parse("transition: 0s 1s").GetPropertyValue("transition").Should().Be("0s 1s");
    }

    [Test]
    public void ANegativeTimeIsTheDelayAndMathKeepsItsType()
    {
        // The duration is a <time [0s,∞]>, so a negative first time is the delay.
        var block = CssDeclarationBlock.Parse("animation: -1s spin");
        block.GetPropertyValue("animation-duration").Should().Be("0s");
        block.GetPropertyValue("animation-delay").Should().Be("-1s");
        block.GetPropertyValue("animation").Should().Be("0s -1s spin");
        var transition = CssDeclarationBlock.Parse("transition: opacity -1s 1s");
        transition.GetPropertyValue("transition-duration").Should().Be("1s");
        transition.GetPropertyValue("transition-delay").Should().Be("-1s");

        var math = CssDeclarationBlock.Parse("animation: calc(1s * 2) calc(2 + 1) spin");
        math.GetPropertyValue("animation-duration").Should().Be("calc(1s * 2)");
        math.GetPropertyValue("animation-iteration-count").Should().Be("calc(2 + 1)");
    }

    [TestCase("transition-property: ease", "0s ease 0s normal ease")]
    [TestCase("transition-property: allow-discrete", "0s ease 0s normal allow-discrete")]
    [TestCase("transition-property: ALL", "all")]
    [TestCase("animation-name: None", "none")]
    [TestCase("animation-name: forwards", "0s ease 0s 1 normal none running forwards")]
    public void KeywordNamesReparseAsNames(string longhand, string expected)
    {
        var shorthand = longhand.StartsWith("animation", StringComparison.Ordinal) ? "animation" : "transition";
        var block = CssDeclarationBlock.Parse(shorthand + ": none");
        block.SetProperty(longhand[..longhand.IndexOf(':')], longhand[(longhand.IndexOf(':') + 1)..].Trim());
        if (shorthand == "transition") block.SetProperty("transition-behavior", "normal");
        var serialized = block.GetPropertyValue(shorthand);
        serialized.Should().Be(expected);
        var reparsed = CssDeclarationBlock.Parse(shorthand + ": " + serialized);
        var name = shorthand == "animation" ? "animation-name" : "transition-property";
        reparsed.GetPropertyValue(name).ToLowerInvariant().Should().Be(block.GetPropertyValue(name).ToLowerInvariant());
    }

    [Test]
    public void SpecifiedListsOfDifferentLengthsHaveNoShorthand()
    {
        var block = CssDeclarationBlock.Parse("transition: opacity 1s, color 2s; transition-duration: 3s");
        block.GetPropertyValue("transition-duration").Should().Be("3s");
        block.GetPropertyValue("transition").Should().BeEmpty();
        block.CssText.Should().Contain("transition-property: opacity, color;");
    }

    [Test]
    public void WideKeywordsAndVarDeferredValuesExpandWhole()
    {
        var block = CssDeclarationBlock.Parse("animation: inherit");
        block.GetPropertyValue("animation-name").Should().Be("inherit");
        block.GetPropertyValue("animation").Should().Be("inherit");
        var pending = CssDeclarationBlock.Parse("transition: var(--t)");
        pending.GetPropertyValue("transition").Should().Be("var(--t)");
        pending.GetPropertyValue("transition-duration").Should().BeEmpty();
    }
}

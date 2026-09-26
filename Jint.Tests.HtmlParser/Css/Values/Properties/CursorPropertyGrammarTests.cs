#nullable enable

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class CursorPropertyGrammarTests
{
    [TestCase("auto")]
    [TestCase("default")]
    [TestCase("none")]
    [TestCase("context-menu")]
    [TestCase("help")]
    [TestCase("pointer")]
    [TestCase("progress")]
    [TestCase("wait")]
    [TestCase("cell")]
    [TestCase("crosshair")]
    [TestCase("text")]
    [TestCase("vertical-text")]
    [TestCase("alias")]
    [TestCase("copy")]
    [TestCase("move")]
    [TestCase("no-drop")]
    [TestCase("not-allowed")]
    [TestCase("grab")]
    [TestCase("grabbing")]
    [TestCase("e-resize")]
    [TestCase("n-resize")]
    [TestCase("ne-resize")]
    [TestCase("nw-resize")]
    [TestCase("s-resize")]
    [TestCase("se-resize")]
    [TestCase("sw-resize")]
    [TestCase("w-resize")]
    [TestCase("ew-resize")]
    [TestCase("ns-resize")]
    [TestCase("nesw-resize")]
    [TestCase("nwse-resize")]
    [TestCase("col-resize")]
    [TestCase("row-resize")]
    [TestCase("all-scroll")]
    [TestCase("zoom-in")]
    [TestCase("zoom-out")]
    public void EveryPredefinedCursorIsAnUnchangedComputedKeyword(string keyword)
    {
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var result = CssPropertyParser.Parse("cursor", keyword.ToUpperInvariant(), context);
            result.Status.Should().Be(CssPropertyStatus.Valid);
            result.Value.Kind.Should().Be(CssPropertyValueKind.Keyword);
            result.Value.Serialize().Should().Be(keyword);
        }
    }

    [Test]
    public void EscapesCommentsWideValuesAndReferencesUseSharedPaths()
    {
        CssPropertyParser.Parse("cursor", @" /*before*/ p\6f inter /*after*/ ").Value.Text.Should().Be("pointer");
        foreach (var wide in new[] { "initial", "inherit", "unset", "revert", "revert-layer", "revert-rule" })
            CssPropertyParser.Parse("cursor", wide).Value.Text.Should().Be(wide);
        CssPropertyParser.Parse("cursor", "var(--cursor)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("cursor", "env(cursor-kind)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyRegistry.Completed["cursor"].Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["cursor"].InitialValue.Should().Be("auto");
    }

    [TestCase("bogus")]
    [TestCase("pointer text")]
    [TestCase("pointer, text")]
    [TestCase("pointer,")]
    [TestCase("1 2 pointer")]
    [TestCase("1")]
    [TestCase("pointer 1 2")]
    [TestCase("\"pointer\"")]
    [TestCase("cursor(pointer)")]
    public void OrdinaryInvalidSequencesStayInvalid(string source) =>
        CssPropertyParser.Parse("cursor", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("url(cursor.cur), pointer")]
    [TestCase("url('cursor.cur') 1 2, pointer")]
    [TestCase("URL(cursor.cur) 1 2, url(other.cur), auto")]
    [TestCase("image-set(url(cursor.cur) 1x), pointer")]
    [TestCase("-webkit-image-set(url(cursor.cur) 1x), pointer")]
    public void ImageFamiliesStayNamedPending(string source)
    {
        var result = CssPropertyParser.Parse("cursor", source);
        result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        result.Blocker.Should().Be("V6:cursor-images");
    }

    [Test]
    public void AnAlreadyTokenizedInvalidSequencePollsAndCancels()
    {
        var input = CssReferenceInput.Parse(string.Join(" ", Enumerable.Repeat("pointer", 16384)), null, default);
        var parts = CssPropertyParser.Significant(input.Components, new CssValueWork(default));
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 2) cancellation.Cancel(); });
        Action parse = () => CssCursorPropertyParser.Parse(parts, work);
        parse.Should().Throw<OperationCanceledException>();
        polls.Should().Be(2);
    }
}

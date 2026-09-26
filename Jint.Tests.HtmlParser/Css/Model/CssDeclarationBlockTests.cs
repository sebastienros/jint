using System.Reflection;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssDeclarationBlockTests
{
    [Test]
    public void ParseKeepsCascadeWinnersAtTheirSpecifiedPositions()
    {
        var block = CssDeclarationBlock.Parse("opacity:.1; DISPLAY:block; opacity:.2 !important; visibility:hidden; opacity:.3; display:flex");
        Names(block).Should().Equal("opacity", "visibility", "display");
        block.GetPropertyValue("OPACITY").Should().Be("0.2");
        block.GetPropertyPriority("opacity").Should().Be("important");
        block.CssText.Should().Be("opacity: 0.2 !important; visibility: hidden; display: flex;");
        block.SetProperty("opacity", ".4");
        Names(block).Should().Equal("opacity", "visibility", "display");
        block.GetPropertyPriority("opacity").Should().BeEmpty();
    }

    [Test]
    public void PendingGrammarAbortsWholeReplacementAndReportsOriginalProvenance()
    {
        var block = CssDeclarationBlock.Parse("opacity:.5");
        var text = block.CssText;
        var stamp = block.Stamp;
        var entry = block.GetDeclaration(0);
        const string replacement = "display:block;min-inline-size:1px;opacity:1";
        var exception = Assert.Throws<CssIncompleteGrammarException>(() => block.ReplaceText(replacement))!;
        exception.PropertyName.Should().Be("min-inline-size");
        exception.Blocker.Should().Be("V2:min-inline-size");
        exception.Span.Start.Should().Be(replacement.IndexOf("min-inline-size", StringComparison.Ordinal));
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
        block.GetDeclaration(0).Should().BeSameAs(entry);
        Assert.Throws<CssIncompleteGrammarException>(() => CssDeclarationBlock.Parse(replacement));
        Assert.Throws<CssIncompleteGrammarException>(() => block.SetProperty("min-inline-size", "1px"));
        block.Stamp.Should().Be(stamp);
    }

    [TestCase("all", "V0:all-reset")]
    [TestCase("margin-block", "V2:margin-block")]
    [TestCase("min-inline-size", "V2:min-inline-size")]
    public void PendingRemovalMetadataAbortsBeforeMutationAndBeforeInvalidPriority(string name, string blocker)
    {
        var block = CssDeclarationBlock.Parse("opacity:.5;overflow:hidden");
        var before = block.CssText;
        var stamp = block.Stamp;
        var entry = block.GetDeclaration(0);
        var remove = Assert.Throws<CssIncompleteGrammarException>(() => block.RemoveProperty(name))!;
        remove.PropertyName.Should().Be(name);
        remove.Blocker.Should().Be(blocker);
        var setter = Assert.Throws<CssIncompleteGrammarException>(() => block.SetProperty(name, "", "bad"))!;
        setter.Blocker.Should().Be(blocker);
        block.RemoveProperty("made-up").Should().BeEmpty();
        block.SetProperty("made-up", "", "bad");
        block.CssText.Should().Be(before);
        block.Stamp.Should().Be(stamp);
        block.GetDeclaration(0).Should().BeSameAs(entry);
    }

    [Test]
    public void PriorityOrderingAndValueImportanceRemainDistinct()
    {
        var block = CssDeclarationBlock.Parse("opacity:.5!important; overflow:hidden");
        var stamp = block.Stamp;
        block.SetProperty("min-inline-size", "1px", "bad"); // rejected before the pending logical sizing grammar
        block.SetProperty("opacity", "1", " important");
        block.SetProperty("opacity", "1!important");
        block.SetProperty("--x", "red !important");
        block.Stamp.Should().Be(stamp);
        block.SetProperty("overflow", "", "bad");
        block.GetPropertyValue("overflow").Should().BeEmpty();
        block.Count.Should().Be(1);
        block.SetProperty("opacity", "1", "ImPoRtAnT");
        block.GetPropertyPriority("opacity").Should().Be("important");
        block.GetPropertyValue("opacity").Should().Be("1");
    }

    [Test]
    public void InvalidAndUnknownRecoverWhileValidSiblingsSurvive()
    {
        var block = CssDeclarationBlock.Parse("made-up:tokens; opacity:1px; broken; visibility:hidden; src:url(a)");
        block.Count.Should().Be(1);
        block.CssText.Should().Be("visibility: hidden;");
        var stamp = block.Stamp;
        block.SetProperty("made-up", "tokens");
        block.SetProperty("visibility", "none");
        block.RemoveProperty("absent").Should().BeEmpty();
        block.Stamp.Should().Be(stamp);
        block.ReplaceText("nonsense; opacity:wrong");
        block.Count.Should().Be(0);
        block.CssText.Should().BeEmpty();
        block.Stamp.Value.Should().BeGreaterThan(stamp.Value);
    }

    [TestCase("overflow:hidden scroll", "hidden", "scroll", "hidden scroll", "overflow: hidden scroll;")]
    [TestCase("overflow:overlay", "auto", "auto", "auto", "overflow: auto;")]
    [TestCase("overflow:inherit", "inherit", "inherit", "inherit", "overflow: inherit;")]
    [TestCase("overflow-x:initial; overflow-y:hidden", "initial", "hidden", "", "overflow-x: initial; overflow-y: hidden;")]
    [TestCase("overflow-y:scroll; opacity:.5; overflow-x:hidden", "hidden", "scroll", "hidden scroll", "overflow: hidden scroll; opacity: 0.5;")]
    public void OverflowExpandsAndSerializesOnlyRepresentableLonghands(string source, string x, string y,
        string shorthand, string text)
    {
        var block = CssDeclarationBlock.Parse(source);
        block.GetPropertyValue("overflow-x").Should().Be(x);
        block.GetPropertyValue("overflow-y").Should().Be(y);
        block.GetPropertyValue("overflow").Should().Be(shorthand);
        block.CssText.Should().Be(text);
        var reparsed = CssDeclarationBlock.Parse(text);
        reparsed.GetPropertyValue("overflow-x").Should().Be(x);
        reparsed.GetPropertyValue("overflow-y").Should().Be(y);
    }

    [Test]
    public void OverflowDuplicatesPropagateImportanceAndOrderPerLonghand()
    {
        var block = CssDeclarationBlock.Parse("overflow-x:clip!important;opacity:.5;overflow:hidden scroll;visibility:visible");
        Names(block).Should().Equal("overflow-x", "opacity", "overflow-y", "visibility");
        block.GetPropertyValue("overflow-x").Should().Be("clip");
        block.GetPropertyValue("overflow-y").Should().Be("scroll");
        block.GetPropertyValue("overflow").Should().BeEmpty();
        block.GetPropertyPriority("overflow").Should().BeEmpty();
        block.SetProperty("overflow", "hidden", "important");
        block.GetPropertyPriority("overflow").Should().Be("important");
        block.RemoveProperty("overflow").Should().Be("hidden");
        Names(block).Should().Equal("opacity", "visibility");
        block.SetProperty("overflow-x", "clip");
        Names(block).Should().Equal("opacity", "visibility", "overflow-x");
        block.RemoveProperty("overflow").Should().BeEmpty();
        Names(block).Should().Equal("opacity", "visibility");
    }

    [Test]
    public void DeferredOverflowRetainsSharedShorthandIdentityUntilOverridden()
    {
        var block = CssDeclarationBlock.Parse("opacity:.5;overflow:var(--X, env(foo, hidden))!important");
        var x = block.GetDeclaration(1);
        var y = block.GetDeclaration(2);
        x.PendingShorthand.Should().BeSameAs(y.PendingShorthand);
        x.PendingShorthand!.Name.Should().Be("overflow");
        x.Value.References.Count.Should().Be(2);
        x.Value.References.Input.Source.Length.Should().BeLessThan(50);
        block.GetPropertyValue("overflow").Should().Be("var(--X, env(foo, hidden))");
        block.GetPropertyValue("overflow-x").Should().BeEmpty();
        block.CssText.Should().Be("opacity: 0.5; overflow: var(--X, env(foo, hidden)) !important;");
        block.SetProperty("overflow-x", "auto", "important");
        block.GetPropertyValue("overflow").Should().BeEmpty();
        block.GetDeclaration(2).Should().BeSameAs(y);
        block.CssText.Should().Be("opacity: 0.5; overflow-x: auto !important;");
        block.RemoveProperty("overflow-y").Should().BeEmpty();
        block.SetProperty("overflow-y", "var(--Y)", "important");
        block.GetPropertyValue("overflow-y").Should().Be("var(--Y)");
        block.GetPropertyValue("overflow").Should().BeEmpty();
    }

    [TestCase("/* only */", "/* only */")]
    [TestCase(" /* first */ a /* last */ ", "/* first */ a /* last */")]
    [TestCase("a\\ ", "a\\ ")]
    [TestCase("a\\20 ", "a\\20 ")]
    [TestCase("\u00a0", "\u00a0")]
    [TestCase("", " ")]
    [TestCase(" \t ", " ")]
    [TestCase("/* note   ", "/* note   */")]
    [TestCase("f(a   ", "f(a   )")]
    public void CustomValuesPreserveLexicalMeaningAndEmptyPresence(string source, string expected)
    {
        var suffix = source.Contains("/* note", StringComparison.Ordinal) || source.StartsWith("f(", StringComparison.Ordinal) ? "" : ";";
        var block = CssDeclarationBlock.Parse("--X:" + source + suffix);
        block.Count.Should().Be(1);
        block.GetPropertyValue("--X").Should().Be(expected);
        block.GetPropertyValue("--x").Should().BeEmpty();
        if (source.Length != 0)
        {
            var assigned = CssDeclarationBlock.Parse("");
            assigned.SetProperty("--X", source);
            assigned.GetPropertyValue("--X").Should().Be(expected);
        }
        block.RemoveProperty("--X").Should().Be(expected);
        block.Count.Should().Be(0);
    }

    [Test]
    public void CustomNamesEscapeAndKeepCaseWithCanonicalWideKeywords()
    {
        var block = CssDeclarationBlock.Parse("--X:/* c */ 12345678-12e3; --x: InHerit; --a\\ b:green!important; --empty:;");
        block.GetPropertyValue("--X").Should().Be("/* c */ 12345678-12e3");
        block.GetPropertyValue("--x").Should().Be("inherit");
        block.CssText.Should().Be("--X: /* c */ 12345678-12e3; --x: inherit; --a\\ b: green !important; --empty: ;");
        var reparsed = CssDeclarationBlock.Parse(block.CssText);
        Names(reparsed).Should().Equal(Names(block));
        reparsed.GetPropertyValue("--a b").Should().Be("green");
        reparsed.GetPropertyValue("--empty").Should().Be(" ");
    }

    [TestCase("f(a   ", "f(a   )")]
    [TestCase("/* note   ", "/* note   */")]
    [TestCase("\"hello", "\"hello\"")]
    [TestCase("\"hello\\", "\"hello\\\n\"")]
    [TestCase("foo\\", "foo\\\ufffd")]
    [TestCase("#foo\\", "#foo\\\ufffd")]
    [TestCase("1foo\\", "1foo\\\ufffd")]
    [TestCase("url(foo", "url(foo)")]
    [TestCase("url(foo   ", "url(foo   )")]
    [TestCase("url(foo\\", "url(foo\\\ufffd)")]
    [TestCase("f([\"x\\", "f([\"x\\\n\"])")]
    public void EofRecoveryTerminatesCustomValuesBeforeEmbeddingOtherDeclarations(string source, string expected)
    {
        var block = CssDeclarationBlock.Parse("--x:" + source);
        block.GetPropertyValue("--x").Should().Be(expected);
        block.GetDeclaration(0).Value.References.Input.Source.Should().Be(source);
        block.SetProperty("opacity", ".5");
        var reparsed = CssDeclarationBlock.Parse(block.CssText);
        reparsed.Count.Should().Be(2);
        reparsed.GetPropertyValue("opacity").Should().Be("0.5");
        reparsed.GetPropertyValue("--x").Should().Be(expected);
        var assigned = CssDeclarationBlock.Parse("");
        assigned.SetProperty("--x", source);
        assigned.SetProperty("opacity", ".5");
        CssDeclarationBlock.Parse(assigned.CssText).Count.Should().Be(2);
    }

    [TestCase("opacity", "var(--x", "var(--x)")]
    [TestCase("overflow", "var(--x", "var(--x)")]
    [TestCase("opacity", "var(--x)/* note   ", "var(--x)/* note   */")]
    [TestCase("overflow", "var(--x)/* note   ", "var(--x)/* note   */")]
    public void EofRecoveryAlsoTerminatesDeferredOrdinaryAndShorthandValues(string name, string source, string expected)
    {
        var block = CssDeclarationBlock.Parse(name + ":" + source);
        block.GetPropertyValue(name).Should().Be(expected);
        block.SetProperty("display", "block");
        var reparsed = CssDeclarationBlock.Parse(block.CssText);
        reparsed.GetPropertyValue(name).Should().Be(expected);
        reparsed.GetPropertyValue("display").Should().Be("block");
        var assigned = CssDeclarationBlock.Parse("");
        assigned.SetProperty(name, source);
        assigned.GetPropertyValue(name).Should().Be(expected);
        assigned.SetProperty("display", "block");
        var assignedRoundTrip = CssDeclarationBlock.Parse(assigned.CssText);
        assignedRoundTrip.GetPropertyValue("display").Should().Be("block");
        assignedRoundTrip.GetPropertyValue(name).Should().Be(expected);
    }

    [Test]
    public void EofCommentAfterRemovedPriorityDoesNotReturnInTheValue()
    {
        var block = CssDeclarationBlock.Parse("--x:red!important/* note   ");
        block.GetPropertyValue("--x").Should().Be("red");
        block.CssText.Should().Be("--x: red !important;");
        block.GetDeclaration(0).Termination.Should().BeEmpty();
    }

    [Test]
    public void ComponentsAreConsumedOnceAndSmallValuesOwnNoSheetText()
    {
        var source = new string(' ', 100_000) + "--x:/* c */ var(--y);opacity:.5;";
        var syntax = new CssSyntaxParser(source, null, default).ParseDeclarationList();
        var work = new CssValueWork(default);
        var block = CssDeclarationBlock.FromDeclarations(source, syntax, CssDeclarationContext.Style, 0, work);
        var value = block.GetDeclaration(0).Value;
        value.References.Input.Components.Should().BeSameAs(syntax[0].Value);
        value.References.Input.Source.Should().Be("/* c */ var(--y)");
        value.References[0].Span.Start.Should().Be(source.IndexOf("var", StringComparison.Ordinal));
        block.GetPropertyValue("--x").Should().Be("/* c */ var(--y)");
    }

    [Test]
    public void KeyframeImportanceDropsAtTheContextBoundary()
    {
        var block = CssDeclarationBlock.Parse("opacity:.5!important;display:block;min-width:1px!important", CssDeclarationContext.Keyframe);
        block.CssText.Should().Be("display: block;");
        var stamp = block.Stamp;
        block.SetProperty("opacity", "1", "important");
        block.Stamp.Should().Be(stamp);
        Assert.Throws<CssIncompleteGrammarException>(() => CssDeclarationBlock.Parse("display:block", CssDeclarationContext.FontFace));
    }

    [Test]
    public void LimitsAndCancellationAbortBeforeCommitAndReadsNeverAdvanceStamp()
    {
        var block = CssDeclarationBlock.Parse("overflow:hidden;opacity:.5");
        var text = block.CssText;
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => block.ReplaceText("display:block", cancellationToken: cancellation.Token));
        Assert.Throws<ParseLimitException>(() => block.ReplaceText("display:block", new CssParseOptions
        { Limits = new ParseLimits { MaxInputCharacters = 2 } }));
        var calls = 0;
        using var stagedCancellation = new CancellationTokenSource();
        var work = new CssValueWork(stagedCancellation.Token, () => { if (++calls == 8) stagedCancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => block.SetProperty("overflow", "auto scroll", null, null, work));
        block.CssText.Should().Be(text);
        block.GetPropertyValue("overflow").Should().Be("hidden");
        block.Stamp.Should().Be(stamp);
        typeof(CssDeclarationBlock).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(block, ulong.MaxValue - 1);
        block.SetProperty("opacity", "1");
        block.Stamp.CanReuse.Should().BeFalse();
        block.RemoveProperty("opacity");
        block.Stamp.Value.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void SerializationAndOrdinaryNameNormalizationPollInsideLargeNames()
    {
        var block = CssDeclarationBlock.Parse("");
        var name = "--" + new string('a', 20_000);
        block.SetProperty(name, "red");
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++calls == 3) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => block.Serialize(work));
        block.GetPropertyValue(name).Should().Be("red");
        using var normalizeCancellation = new CancellationTokenSource();
        var normalizeCalls = 0;
        var normalizationWork = new CssValueWork(normalizeCancellation.Token,
            () => { if (++normalizeCalls == 2) normalizeCancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => CssPropertyRegistry.NormalizeName(new string('A', 20_000), normalizationWork));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LongCommonPrefixLookupPollsDuringOrdinalComparison(bool priority)
    {
        var prefix = "--" + new string('a', 20_000);
        var source = string.Join(';', Enumerable.Range(0, 100).Select(i => prefix + i.ToString("D3") + ":red"));
        var block = CssDeclarationBlock.Parse(source);
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++calls == 3) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() =>
        {
            if (priority) block.GetPropertyPriority(prefix + "999", work);
            else block.GetPropertyValue(prefix + "999", work);
        });
        block.Count.Should().Be(100);
        block.Stamp.Should().Be(stamp);
    }

    [TestCase("all:initial", "V0:all-reset")]
    [TestCase("opacity:attr(foo)", "attr")]
    [TestCase("--x:attr(foo)", "attr")]
    public void KnownUnfinishedFormsNeverBecomeInvalidRecovery(string source, string blocker)
    {
        var exception = Assert.Throws<CssIncompleteGrammarException>(() => CssDeclarationBlock.Parse(source))!;
        exception.Blocker.Should().Be(blocker);
    }

    private static string[] Names(CssDeclarationBlock block) =>
        Enumerable.Range(0, block.Count).Select(block.GetPropertyName).ToArray();
}

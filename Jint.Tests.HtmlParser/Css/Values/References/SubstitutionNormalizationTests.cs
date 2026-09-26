#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class SubstitutionNormalizationTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    [TestCase(5)]
    public void ReplacementContainersCopyEveryChildWithoutSourceAlignment(int count)
    {
        var work = new CssValueWork(default);
        var original = SubstitutionFixture.Input("foo(red blue)");
        var greenInput = SubstitutionFixture.Input("green");
        var green = CssSegment.FromInput(greenInput, work);
        var supplied = Enumerable.Repeat(green, count).ToArray();
        var container = CssSegment.Container(original, original.Components[0], supplied, work);
        var rebuilt = CssSegment.Rebuild(container, supplied, work);
        Array.Fill(supplied, CssSegment.FromInput(SubstitutionFixture.Input("blue"), work));
        foreach (var segment in new[] { container, rebuilt })
        {
            segment.Children.Length.Should().Be(count);
            segment.TokenCount.Should().Be(count + 2);
            segment.SpellingLength.Should().Be(5 + 5 * count);
            segment.LexicalLength.Should().Be(5 + 5 * count);
            var value = CssSubstitutedValue.Create(segment, 0, work);
            value.Components[0].Values.Count.Should().Be(count);
            for (var i = 0; i < count; i++)
                value.Components[0].Values[i].Token.Text.Should().Be("green");
            var origins = value.OriginsFor(new CssSourceSpan(0, value.SpellingLength));
            origins.Count.Should().Be(count + 2);
            origins[0].Source.Should().BeSameAs(original);
            for (var i = 0; i < count; i++) origins[i + 1].Source.Should().BeSameAs(greenInput);
            origins[count + 1].Source.Should().BeSameAs(original);
        }
    }

    [TestCase("f(/*head*/g(/*inner*/red/*tail*/))")]
    [TestCase("f(/*head*/g(/*inner*/red/*tail*/")]
    [TestCase("f(/*head*/g('red")]
    public void SourceAlignedContainersRetainCommentsAndNestedEofRecovery(string source)
    {
        var work = new CssValueWork(default);
        var input = SubstitutionFixture.Input(source);
        var segment = CssSegment.FromInput(input, work);
        var value = CssSubstitutedValue.Create(segment, 0, work);
        value.SerializeCustomProperty(work).Should().Be(source + input.ValueTermination);
        value.Components[0].Values[0].Kind.Should().Be(CssComponentKind.Function);
        var reparsed = SubstitutionFixture.Resolve(value.SerializeCustomProperty(work)).Value;
        reparsed.TokenCount.Should().Be(value.TokenCount);
    }

    [Test]
    public void NormalizationKeepsTokenBearingSegmentsWithEmptyLexicalSpans()
    {
        var work = new CssValueWork(default);
        var original = SubstitutionFixture.Input("x");
        var input = CssReferenceInput.FromComponents("x", original.Components, 0,
            new CssSourceSpan(0, 1), work, serializationSpan: new CssSourceSpan(0, 0));
        var token = CssSegment.Token(input, input.Components[0]);
        token.LexicalLength.Should().Be(0);
        token.IsEmpty.Should().BeFalse();
        var empty = CssSegment.Concat([], work);
        CssSegment.Concat([empty, token, empty], work).Should().BeSameAs(token);
        CssSubstitutedValue.Create(token, 0, work).TokenCount.Should().Be(1);
    }

    [Test]
    public void BoundaryNormalizationIsIdempotentAndInspectsOnlyImmediateChildren()
    {
        var work = new CssValueWork(default);
        var empty = CssSegment.Concat([], work);
        var boundary = CssSegment.Substitution(empty, work);
        boundary.BoundaryOnly.Should().BeTrue();
        boundary.IsEmpty.Should().BeFalse();
        CssSegment.Concat([empty, empty], work).Should().BeSameAs(empty);
        CssSegment.Concat([boundary, empty, boundary], work).Should().BeSameAs(boundary);
        CssSegment.Substitution(boundary, work).Should().BeSameAs(boundary);
        var red = CssSegment.FromInput(SubstitutionFixture.Input("red"), work);
        var bounded = CssSegment.Substitution(red, work);
        bounded.StartsBoundary.Should().BeTrue();
        bounded.EndsBoundary.Should().BeTrue();
        for (var i = 0; i < 5000; i++)
        {
            CssSegment.Substitution(bounded, work).Should().BeSameAs(bounded);
            CssSegment.Concat([boundary, bounded, boundary], work).Should().BeSameAs(bounded);
        }
        var shared = CssSegment.Concat([bounded, bounded], work);
        shared.Children.Length.Should().Be(2);
        shared.Children[0].Should().BeSameAs(bounded);
        shared.Children[1].Should().BeSameAs(bounded);
        Inventory(shared).Should().Be((4, 5));
    }

    [Test]
    public void ThousandsOfAliasesDoNotRetainGrowingBoundaryWrappers()
    {
        var bindings = Chain(4000, doubled: false);
        var value = SubstitutionFixture.Resolve("var(--v4000)", bindings).Value;
        value.TokenCount.Should().Be(1);
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be("x");
        Inventory(value.Root).Should().Be((3, 3));
        var origins = value.OriginsFor(new CssSourceSpan(0, 1));
        origins.Count.Should().Be(1);
        origins[0].Source.Source.Should().Be("x");
    }

    [Test]
    public void DoubledEmptyBindingsCollapseInLinearSourceAndBindingWork()
    {
        static int Checks(int depth)
        {
            var checks = 0;
            var result = SubstitutionFixture.ResolveWith("var(--v" + depth + ")", Chain(depth, doubled: true, empty: true),
                true, work: new CssValueWork(default, () => checks++));
            result.Kind.Should().Be(CssSubstitutionResultKind.Tokens);
            result.Value.TokenCount.Should().Be(0);
            result.Value.SpellingLength.Should().Be(0);
            result.Value.Root.BoundaryOnly.Should().BeTrue();
            Inventory(result.Value.Root).Should().Be((1, 0));
            result.Value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(" ");
            return checks;
        }
        var smaller = Checks(1500);
        smaller.Should().BeGreaterThan(0);
        Checks(3000).Should().BeLessThan(smaller * 3);
    }

    [Test]
    public void NormalizedAliasEdgesPreserveWhitespaceHtmlCommentAndHexEscapeSeams()
    {
        var empty = Chain(64, doubled: false, empty: true);
        SubstitutionFixture.Resolve("a var(--v64)b", empty).Value
            .SerializeCustomProperty(new CssValueWork(default)).Should().Be("a b");
        SubstitutionFixture.Resolve("f(<var(--v64)!--)", empty).Value
            .SerializeCustomProperty(new CssValueWork(default)).Should().Be("f(<!/**/--)");
        var escaped = Chain(64, doubled: false);
        escaped[0] = SubstitutionFixture.Specified("--v0", @"\61");
        var value = SubstitutionFixture.Resolve("var(--v64) b", escaped).Value;
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(@"\61/**/ b");
        value.Components.Count.Should().Be(3);
        value.Components[1].Token.Kind.Should().Be(CssTokenKind.Whitespace);
    }

    [Test]
    public void ExactTokenFanOutSerializesWithinTheExistingSpellingAndJoinBounds()
    {
        var bindings = Chain(17, doubled: true);
        var value = SubstitutionFixture.Resolve("var(--v16)", bindings).Value;
        value.TokenCount.Should().Be(65_536);
        value.SpellingLength.Should().Be(65_536);
        value.Root.LexicalLength.Should().Be(65_536);
        var lexical = value.SerializeCustomProperty(new CssValueWork(default));
        lexical.Length.Should().Be(65_536 + 4 * 65_535);
        lexical.Should().StartWith("x/**/x");
        lexical.Should().EndWith("x/**/x");
        Inventory(value.Root).Nodes.Should().BeLessThan(40);
        var origins = value.OriginsFor(new CssSourceSpan(0, value.SpellingLength));
        origins.Count.Should().Be(65_536);
        origins[0].Source.Should().BeSameAs(origins[65_535].Source);
        origins[65_535].ProjectionSpan.Start.Should().Be(65_535);
        SubstitutionFixture.Resolve("var(--v17)", bindings).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--v17,red)", bindings)).Should().Be("red");
    }

    [Test]
    public void CommentOnlyAuthoredLexicalCeilingIsInclusiveAndIndependentOfTokens()
    {
        var exact = "/*" + new string('x', 1_048_572) + "*/";
        var value = SubstitutionFixture.Resolve("var(--x)", SubstitutionFixture.Specified("--x", exact)).Value;
        value.TokenCount.Should().Be(0);
        value.SpellingLength.Should().Be(0);
        value.Root.LexicalLength.Should().Be(1_048_576);
        value.SerializeCustomProperty(new CssValueWork(default)).Should().Be(exact);
        var oversized = "/*" + new string('x', 1_048_573) + "*/";
        SubstitutionFixture.Resolve(oversized).Kind.Should().Be(CssSubstitutionResultKind.GuaranteedInvalid);
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--x,red)",
            SubstitutionFixture.Specified("--x", oversized))).Should().Be("red");
        SubstitutionFixture.Identifier(SubstitutionFixture.Resolve("var(--present," + oversized + ")",
            SubstitutionFixture.Specified("--present", "blue"))).Should().Be("blue");
    }

    [Test]
    public void NormalizationAndFinalSerializationPropagateMidOperationCancellation()
    {
        var work = new CssValueWork(default);
        var boundary = CssSegment.Substitution(CssSegment.Concat([], work), work);
        var supplied = Enumerable.Repeat(boundary, 20_000).ToArray();
        using var normalization = new CancellationTokenSource();
        var checks = 0;
        Assert.Throws<OperationCanceledException>(() => CssSegment.Concat(supplied,
            new CssValueWork(normalization.Token, () => { if (++checks == 3) normalization.Cancel(); })));
        supplied[0].Should().BeSameAs(boundary);
        var value = SubstitutionFixture.Resolve("var(--v12)", Chain(12, doubled: true)).Value;
        var total = 0;
        var expected = value.SerializeCustomProperty(new CssValueWork(default, () => total++));
        foreach (var target in new[] { total / 2, total - 1 })
        {
            using var cancellation = new CancellationTokenSource();
            checks = 0;
            Assert.Throws<OperationCanceledException>(() => value.SerializeCustomProperty(
                new CssValueWork(cancellation.Token, () => { if (++checks == target) cancellation.Cancel(); })));
        }
        value.TokenCount.Should().Be(4096);
        value.SerializeCustomProperty(work).Should().Be(expected);
    }

    private static CssSubstitutionBinding[] Chain(int depth, bool doubled, bool empty = false)
    {
        var bindings = new CssSubstitutionBinding[depth + 1];
        bindings[0] = SubstitutionFixture.Specified("--v0", empty ? "" : "x");
        for (var i = 1; i <= depth; i++)
        {
            var reference = "var(--v" + (i - 1) + ")";
            bindings[i] = SubstitutionFixture.Specified("--v" + i, doubled ? reference + reference : reference);
        }
        return bindings;
    }

    private static (int Nodes, int Edges) Inventory(CssSegment root)
    {
        var seen = new HashSet<CssSegment>();
        var pending = new Stack<CssSegment>();
        pending.Push(root);
        var edges = 0;
        while (pending.TryPop(out var node))
        {
            if (!seen.Add(node)) continue;
            edges += node.Children.Length;
            for (var i = 0; i < node.Children.Length; i++) pending.Push(node.Children[i]);
        }
        return (seen.Count, edges);
    }
}

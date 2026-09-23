#nullable enable
using System.Numerics;
using System.Runtime.ExceptionServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;
using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorCompilerTests
{
    private static CompiledSelector Parse(string source, SelectorParseContext? context = null,
        CancellationToken cancellationToken = default) => SelectorCompiler.Compile(source, context, cancellationToken);

    [TestCase("html body > main + div ~ p || col")]
    [TestCase("*|svg, |span, ns|rect, .class#id[attr][attr=x][attr~=x][attr|=x][attr^=x][attr$=x][attr*=x][attr='A' i][attr='B' s]")]
    [TestCase(":is(.a, .b), :matches(.a), :where(.x), :not(.x), :has(> .x, + .y, ~ .z)")]
    [TestCase(":scope:root:empty:first-child:last-child:only-child:first-of-type:last-of-type:only-of-type")]
    [TestCase(":nth-child(2n+1 of .a, #b):nth-last-child(odd):nth-of-type(-n+3):nth-last-of-type(even)")]
    [TestCase(":nth-col(2n):nth-last-col(4)")]
    [TestCase(":lang(en, 'fr'):dir(ltr):host:host(.a):host-context(.b)")]
    [TestCase(":any-link:link:visited:checked:unchecked:indeterminate:default:enabled:disabled:required:optional:valid:invalid:in-range:out-of-range:read-only:read-write:placeholder-shown:open:closed")]
    [TestCase(":hover:active:focus:focus-within:focus-visible:target:autofill:-webkit-autofill")]
    [TestCase("::before, ::after, ::selection, ::footnote-call, ::footnote-marker, ::first-line, ::first-letter, ::content, ::checkmark, ::picker-icon, ::picker(select), ::slotted(.x)")]
    [TestCase("::\\2d webkit-foo, ::-WeBkIt-Bar")]
    public void RequiredCataloguesCompile(string source)
    {
        var context = new SelectorParseContext(new[] { Pair("ns", "urn:test") });
        Parse(source, context).Branches.Should().NotBeEmpty();
    }

    [Test]
    public void NamespaceSnapshotAndExplicitUniversalAreDistinct()
    {
        var bindings = new Dictionary<string, string> { [""] = "urn:a", ["p"] = "urn:p" };
        var context = new SelectorParseContext(bindings);
        bindings[""] = "changed";
        bindings.Remove("p");
        var program = Parse(".a, *|*:is(.x), *|*:is(*.x), p|a, |b, [p|id], [id]", context);
        context.NamespaceBindings[""].Should().Be("urn:a");
        program.Branches[0].Compounds[0].NamespaceMode.Should().Be(NamespaceMode.Exact);
        program.Branches[1].Compounds[0].NamespaceMode.Should().Be(NamespaceMode.Any);
        program.Branches[1].Compounds[0].Predicates[0].Arguments!.Branches[0].Compounds[0].HasExplicitType.Should().BeFalse();
        program.Branches[2].Compounds[0].Predicates[0].Arguments!.Branches[0].Compounds[0].HasExplicitType.Should().BeTrue();
        program.Branches[3].Compounds[0].NamespaceUri.Should().Be("urn:p");
        program.Branches[4].Compounds[0].NamespaceMode.Should().Be(NamespaceMode.None);
        program.Branches[5].Compounds[0].Predicates[0].NamespaceUri.Should().Be("urn:p");
        program.Branches[6].Compounds[0].Predicates[0].NamespaceMode.Should().Be(NamespaceMode.None);
        var empty = new SelectorParseContext(new[] { Pair("", ""), Pair("p", "") });
        var emptyProgram = Parse("a, p|a, [p|id]", empty);
        emptyProgram.Branches[0].Compounds[0].NamespaceMode.Should().Be(NamespaceMode.None);
        emptyProgram.Branches[1].Compounds[0].NamespaceMode.Should().Be(NamespaceMode.None);
        emptyProgram.Branches[2].Compounds[0].Predicates[0].NamespaceMode.Should().Be(NamespaceMode.None);
    }

    [Test]
    public void SpecificityUsesSurvivingStaticMaximum()
    {
        var program = Parse(":is(.a, #id, :unknown):where(#id):not(.b):has(> div, #x):nth-child(2n of .c, #y)");
        program.MaximumSpecificity.Should().Be(new SelectorSpecificity(3, 2, 0));
        Parse(":where(#id)").MaximumSpecificity.Should().Be(default(SelectorSpecificity));
        Parse(":is(:unknown)").Branches[0].Compounds[0].Predicates[0].Arguments!.Branches.Should().BeEmpty();
    }

    [TestCase(":shadow", "selector/unsupported-construct", 1)]
    [TestCase(":contains(x)", "selector/unsupported-construct", 1)]
    [TestCase("::-webkit-foo(x)", "selector/unsupported-construct", 2)]
    [TestCase(":unknown", "selector/unsupported-construct", 1)]
    [TestCase("x,", "selector/invalid-syntax", 2)]
    [TestCase("   ", "selector/invalid-syntax", 3)]
    [TestCase(" > .x", "selector/invalid-syntax", 1)]
    [TestCase("p|x", "selector/undeclared-prefix", 0)]
    [TestCase(":not(.ok,:unknown)", "selector/unsupported-construct", 10)]
    [TestCase(":has(:has(.x))", "selector/invalid-syntax", 6)]
    [TestCase(":not(::before)", "selector/invalid-syntax", 5)]
    [TestCase(":not()", "selector/invalid-syntax", 5)]
    [TestCase(":not(", "selector/invalid-syntax", 5)]
    [TestCase("[a=]", "selector/invalid-syntax", 3)]
    [TestCase("[a=", "selector/invalid-syntax", 3)]
    public void InvalidConstructsHaveStableCodeAndOffset(string source, string code, int offset)
    {
        var exception = NUnit.Framework.Assert.Throws<SelectorParseException>(() => Parse(source));
        exception!.Code.Should().Be(code);
        exception.Offset.Should().Be(offset);
    }

    [Test]
    public void ForgivingRecoveryDoesNotSuppressStrictNestedFailures()
    {
        Parse(":is(.x, :unknown, #id)").Branches[0].Compounds[0].Predicates[0]
            .Arguments!.Branches.Should().HaveCount(2);
        Parse(":is(> .x)").Branches[0].Compounds[0].Predicates[0]
            .Arguments!.Branches.Should().BeEmpty();
        Parse(":is(:not(:unknown), .x)").Branches[0].Compounds[0].Predicates[0]
            .Arguments!.Branches.Should().ContainSingle();
        Parse(":where(:has(:has(.x)), .ok)").Branches[0].Compounds[0].Predicates[0]
            .Arguments!.Branches.Should().ContainSingle();
        var outerHas = Parse(":has(:is(:has(.x)))").Branches[0].Compounds[0].Predicates[0];
        outerHas.Arguments!.Branches[0].Compounds[0].Predicates[0].Arguments!
            .Branches.Should().BeEmpty();
    }

    [TestCase(":nth-child(2n-1)", 2, -1)]
    [TestCase(":nth-child(-2n+3)", -2, 3)]
    [TestCase(":nth-child(-n-3)", -1, -3)]
    [TestCase(":nth-child(2n- 3)", 2, -3)]
    public void NthOperandsAreExactForCommonForms(string source, int a, int b)
    {
        var predicate = Parse(source).Branches[0].Compounds[0].Predicates[0];
        predicate.A.Should().Be(new BigInteger(a));
        predicate.B.Should().Be(new BigInteger(b));
    }

    [Test]
    public void LargeNthOperandIsNotRounded()
    {
        const string operand = "9999999999999999999999999999999999999999999999999";
        var predicate = Parse(":nth-child(" + operand + "n-" + operand + ")")
            .Branches[0].Compounds[0].Predicates[0];
        predicate.A.Should().Be(BigInteger.Parse(operand));
        predicate.B.Should().Be(-BigInteger.Parse(operand));
    }

    [Test]
    public void BalancedDecimalConversionPreservesLongNonPowerOfTwoChunkCount()
    {
        var digits = "000" + string.Concat(Enumerable.Repeat("1234567890", 100));
        var predicate = Parse(":nth-child(" + digits + ")")
            .Branches[0].Compounds[0].Predicates[0];
        predicate.B.Should().Be(BigInteger.Parse(digits));
    }

    [TestCase(":nth-child(n-)")]
    [TestCase(":nth-child(2n-)")]
    [TestCase(":nth-child(2.5n)")]
    [TestCase(":nth-child(n + +3)")]
    [TestCase(":nth-child(2n-1+2)")]
    [TestCase(":nth-child(2n-1 - 2)")]
    [TestCase(":nth-child(+ -n)")]
    [TestCase(":nth-child(+ n)")]
    [TestCase(":nth-child(- n)")]
    [TestCase(":nth-child(-/**/n)")]
    [TestCase(":nth-of-type(2n of .x)")]
    [TestCase(":nth-child(2n of)")]
    [TestCase(":dir('ltr')")]
    [TestCase(":lang(en, )")]
    [TestCase("::picker(div)")]
    [TestCase("::before:root")]
    [TestCase("::before:not(:root)")]
    [TestCase("::before > .x")]
    [TestCase("::before .x")]
    [TestCase(":not(::-webkit-unknown)")]
    [TestCase("::-Kebkit-unknown")]
    [TestCase(":host(.a, .b)")]
    [TestCase(":host-context(.a, .b)")]
    [TestCase("::slotted(.a, .b)")]
    public void InvalidFunctionalAndPlacementGrammarIsRejected(string source)
    {
        NUnit.Framework.Assert.Throws<SelectorParseException>(() => Parse(source));
    }

    [Test]
    public void OriginalSpansAndDecodedTextSurviveCommentsEscapesAndContinuations()
    {
        var program = Parse("div/**/.\\,x[attr='a\\\r\nb']");
        var compound = program.Branches[0].Compounds[0];
        compound.TypeName.Should().Be("div");
        compound.Predicates[0].Name.Should().Be(",x");
        compound.Predicates[1].Value.Should().Be("ab");
        compound.Predicates[0].Span.Start.Should().Be(7);
        NUnit.Framework.Assert.Throws<SelectorParseException>(() => Parse("a/**/b"));
        Parse("a/**/ b").Branches[0].Compounds.Should().HaveCount(2);
        var failure = NUnit.Framework.Assert.Throws<SelectorParseException>(() => Parse("\r\n/**/:\\73 hadow"));
        failure!.Code.Should().Be("selector/unsupported-construct");
        failure.Offset.Should().Be(7);
        var escapedPrefix = new SelectorParseContext(new[] { Pair("p", "urn:p") });
        Parse("\\70 |a", escapedPrefix).Branches[0].Compounds[0].NamespaceUri.Should().Be("urn:p");
    }

    [Test]
    public void ComponentEofRecoveryKeepsValidSelectors()
    {
        Parse("[x").Branches.Should().ContainSingle();
        Parse(":is(.x").Branches.Should().ContainSingle();
        Parse("a/*").Branches.Should().ContainSingle();
    }

    [Test]
    public void CompatibilityPredicatesAndAliasHaveExplicitKinds()
    {
        var program = Parse("#target, ::-WeBkIt-Unknown, :-webkit-autofill, :autofill");
        program.Branches.Should().HaveCount(4);
        program.Branches[1].Compounds[0].Predicates[0].Kind.Should().Be(PredicateKind.WebkitUnknownPseudoElement);
        program.Branches[2].Compounds[0].Predicates[0].Kind.Should().Be(PredicateKind.Autofill);
        program.Branches[3].Compounds[0].Predicates[0].Kind.Should().Be(PredicateKind.Autofill);
        Parse(":is(::-webkit-unknown, .ok)").Branches[0].Compounds[0].Predicates[0]
            .Arguments!.Branches.Should().ContainSingle();
        Parse(":dir(sideways)").Branches[0].Compounds[0].Predicates[0]
            .TextArguments![0].Should().Be("sideways");
        Parse("::before:hover:focus").Branches[0].Compounds[0].Predicates.Should().HaveCount(3);
        Parse("::slotted(.x)::before").Branches[0].Compounds[0].Predicates.Should().HaveCount(2);
    }

    [Test]
    public void LogicalPseudoClassesAfterPseudoElementsInheritTheirPosition()
    {
        foreach (var selector in new[]
                 {
                     "::before:is(:hover)", "::before:where(:hover)",
                     "::before:not(:hover)", "::before:is(:where(:hover))"
                 })
        {
            Parse(selector).Branches[0].Compounds[0].Predicates.Should().HaveCount(2);
        }
        var surviving = Parse("::before:is(.x, :root, :hover)")
            .Branches[0].Compounds[0].Predicates[1].Arguments!;
        surviving.Branches.Should().ContainSingle();
        surviving.Branches[0].Compounds[0].Predicates[0].Kind.Should().Be(PredicateKind.Hover);
        Parse("::before:where(:root)").Branches[0].Compounds[0].Predicates[1]
            .Arguments!.Branches.Should().BeEmpty();
    }

    [TestCase(":not(.)", 6)]
    [TestCase(":not(p|)", 7)]
    [TestCase(":not(:)", 6)]
    [TestCase(":not(.", 6)]
    [TestCase("\r\n/**/:not(.)", 12)]
    [TestCase(":not(\\70 |)", 10)]
    [TestCase(":not(p|/**/)", 11)]
    public void NestedMissingTokenOffsetsUseClosingDelimiterOrActualEof(string source, int offset)
    {
        var failure = NUnit.Framework.Assert.Throws<SelectorParseException>(() => Parse(source));
        failure!.Code.Should().Be("selector/invalid-syntax");
        failure.Offset.Should().Be(offset);
    }

    [Test]
    public void InvalidNthBranchesFollowForgivingAndStrictBoundaries()
    {
        var forgiving = Parse(":is(:nth-child(2n-1+2), .ok)")
            .Branches[0].Compounds[0].Predicates[0].Arguments!;
        forgiving.Branches.Should().ContainSingle();
        forgiving.Branches[0].Compounds[0].Predicates[0].Kind.Should().Be(PredicateKind.Class);
        NUnit.Framework.Assert.Throws<SelectorParseException>(() =>
            Parse(":not(:nth-child(2n-1+2))"));
        NUnit.Framework.Assert.Throws<SelectorParseException>(() =>
            Parse(":nth-child(2n of .ok, :nth-child(2n-1+2))"));
    }

    [Test]
    public void SelectorSpecificityIsLexicographicAndValidatesEachCount()
    {
        new SelectorSpecificity(1, 0, 0).CompareTo(new SelectorSpecificity(0, int.MaxValue, int.MaxValue))
            .Should().BePositive();
        new SelectorSpecificity(1, 2, 3).Should().Be(new SelectorSpecificity(1, 2, 3));
        SelectorSpecificity.Add(new SelectorSpecificity(int.MaxValue, 0, 4),
            new SelectorSpecificity(1, 1, int.MaxValue))
            .Should().Be(new SelectorSpecificity(int.MaxValue, 1, int.MaxValue));
        NUnit.Framework.Assert.Throws<ArgumentOutOfRangeException>(() => new SelectorSpecificity(-1, 0, 0))!
            .ParamName.Should().Be("idCount");
        NUnit.Framework.Assert.Throws<ArgumentOutOfRangeException>(() => new SelectorSpecificity(0, -1, 0))!
            .ParamName.Should().Be("classCount");
        NUnit.Framework.Assert.Throws<ArgumentOutOfRangeException>(() => new SelectorSpecificity(0, 0, -1))!
            .ParamName.Should().Be("typeCount");
    }

    [Test]
    public void DeepNestedFunctionsCompileWithoutRecursiveDescent()
    {
        const int depth = 3000;
        var source = string.Concat(Enumerable.Repeat(":is(", depth)) + ".x" +
                     new string(')', depth);
        Parse(source).MaximumSpecificity.Should().Be(new SelectorSpecificity(0, 1, 0));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() => Parse(source, cancellationToken: canceled.Token));
    }

    [Test]
    public void CancellationDuringNumericConversionIsPolled()
    {
        using var cancellation = new CancellationTokenSource();
        var worker = new SelectorCompiler.Worker(string.Empty, new SelectorParseContext(),
            cancellation.Token);
        var digits = new string('9', 1024);
        var checkpointCount = 0;
        worker.TryUnsigned("123x".AsSpan(), out _, () => checkpointCount++).Should().BeFalse();
        checkpointCount.Should().Be(0);
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            worker.TryUnsigned(digits.AsSpan(), out _, () =>
            {
                checkpointCount++;
                cancellation.Cancel();
            }));
        checkpointCount.Should().Be(1);
    }

    [Test]
    public void CancellationAtDeepSyntaxFailureEscapesRecoveryWalk()
    {
        const int depth = 1024;
        var source = string.Concat(Enumerable.Repeat(":not(", depth)) +
                     ":unknown" + new string(')', depth);
        using var cancellation = new CancellationTokenSource();
        var testThread = Environment.CurrentManagedThreadId;
        void CancelOnFailure(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (Environment.CurrentManagedThreadId == testThread &&
                args.Exception is SelectorParseException { Code: "selector/unsupported-construct" })
                cancellation.Cancel();
        }
        AppDomain.CurrentDomain.FirstChanceException += CancelOnFailure;
        try
        {
            NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
                Parse(source, cancellationToken: cancellation.Token));
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= CancelOnFailure;
        }
    }

    [Test]
    public void InputTokenAndNestingLimitsAreInclusive()
    {
        Parse("ab", new SelectorParseContext(limits: new ParseLimits { MaxInputCharacters = 2 }));
        Parse("ab", new SelectorParseContext(limits: new ParseLimits { MaxTokenCharacters = 2 }));
        Parse(":is(:not(.x))", new SelectorParseContext(limits: new ParseLimits { MaxNestingDepth = 2 }));
        NUnit.Framework.Assert.Throws<ParseLimitException>(() =>
            Parse("abc", new SelectorParseContext(limits: new ParseLimits { MaxInputCharacters = 2 })))!
            .Kind.Should().Be(ParseLimitKind.InputCharacters);
        NUnit.Framework.Assert.Throws<ParseLimitException>(() =>
            Parse("abc", new SelectorParseContext(limits: new ParseLimits { MaxTokenCharacters = 2 })))!
            .Kind.Should().Be(ParseLimitKind.TokenCharacters);
    }

    [Test]
    public void SharedLimitsAndCancellationEscape()
    {
        var limit = new SelectorParseContext(limits: new ParseLimits { MaxNestingDepth = 2 });
        NUnit.Framework.Assert.Throws<ParseLimitException>(() => Parse(":is(:not(:has(.x)))", limit));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() => Parse(".x", cancellationToken: cts.Token));
    }

    [Test]
    public void ContextRejectsDuplicateAndNullBindings()
    {
        NUnit.Framework.Assert.Throws<ArgumentException>(() => new SelectorParseContext(new[] { Pair("a", "x"), Pair("a", "y") }));
        NUnit.Framework.Assert.Throws<ArgumentException>(() => new SelectorParseContext(new[] { Pair(null!, "x") }));
        NUnit.Framework.Assert.Throws<ArgumentException>(() => new SelectorParseContext(new[] { Pair("x", null!) }));
    }

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
}

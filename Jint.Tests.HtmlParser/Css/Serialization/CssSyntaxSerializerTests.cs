#nullable enable
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css.Serialization;

[TestFixture]
public sealed class CssSyntaxSerializerTests
{
    [Test]
    public void AdjacentTokensEscapesHashesNumbersAndUrlsRoundTrip()
    {
        const string source = "@unk\\6e own/**/x{a:#1/**/#abc/**/1/**/2/**/1e\\33/**/url(a\\)b)/**/url(\"quoted\")/**/f(1/**/2)}";
        var original = new CssSyntaxParser(source, null, default).ParseStyleSheet().Single();
        var text = CssSyntaxSerializer.SerializeRule(original);
        var reparsed = new CssSyntaxParser(text, null, default).ParseRule();
        reparsed.Kind.Should().Be(original.Kind);
        reparsed.Name.Should().Be(original.Name);
        AssertValuesEqual(original.Prelude, reparsed.Prelude);
        AssertValuesEqual(original.Block!.Value.Values, reparsed.Block!.Value.Values);
        text.Should().Contain("/**/");
    }

    [Test]
    public void DeclarationsPreserveDecodedNamesDuplicatesImportantAndComponentKinds()
    {
        const string source = "co\\6cor:red; color:bogus; --X: \"a\\\"b\" url(a\\ b) #1 #abc 1e\\33 !important";
        var block = CssSyntaxDeclarationBlock.Parse(source);
        var text = block.Serialize();
        var reparsed = CssSyntaxDeclarationBlock.Parse(text);
        reparsed.Declarations.Count.Should().Be(3);
        for (var index = 0; index < block.Declarations.Count; index++)
        {
            reparsed.Declarations[index].Name.Should().Be(block.Declarations[index].Name);
            reparsed.Declarations[index].IsImportant.Should().Be(block.Declarations[index].IsImportant);
            AssertValuesEqual(block.Declarations[index].Value, reparsed.Declarations[index].Value);
        }
    }

    [Test]
    public void RecoveredEofAndUnknownRulesHaveCanonicalSerialization()
    {
        var sheet = CssSyntaxStyleSheet.Parse("@unknown x { f(1");
        var text = sheet.Serialize();
        text.Should().Contain("@unknown");
        var second = CssSyntaxStyleSheet.Parse(text);
        second.Rules.Count.Should().Be(sheet.Rules.Count);
        second.Rules[0].Syntax.Kind.Should().Be(sheet.Rules[0].Syntax.Kind);
        second.Rules[0].Syntax.Block.Should().NotBeNull();
        second.Serialize().Should().Be(text);

        var bad = new CssSyntaxParser("a{ x:url(a b); y:\"bad\nz:ok }", null, default).ParseRule();
        var recoveredText = CssSyntaxSerializer.SerializeRule(bad);
        var recovered = new CssSyntaxParser(recoveredText, null, default).ParseRule();
        recovered.Block.Should().NotBeNull();
        recoveredText.Should().Contain("url(a b)");
        recovered.Block!.Value.Values.Any(value => value.Kind == CssComponentKind.Token &&
            value.Token.Kind == CssTokenKind.BadUrl).Should().BeTrue();
        recovered.Block!.Value.Values.Any(value => value.Kind == CssComponentKind.Token &&
            value.Token.Kind == CssTokenKind.BadString).Should().BeTrue();
    }

    [Test]
    public void DeepContainersAndLongSiblingListsSerializeIteratively()
    {
        const int depth = 3000;
        var source = "a{" + new string('(', depth) + "x" + new string(')', depth) + "}";
        var sheet = CssSyntaxStyleSheet.Parse(source);
        CssSyntaxStyleSheet.Parse(sheet.Serialize()).Rules.Count.Should().Be(1);

        var siblings = CssSyntaxStyleSheet.Parse(string.Concat(Enumerable.Repeat("a{}", 1000)));
        CssSyntaxStyleSheet.Parse(siblings.Serialize()).Rules.Count.Should().Be(1000);
    }

    private static void AssertValuesEqual(CssComponentValueList expected, CssComponentValueList actual)
    {
        var pending = new Stack<(CssComponentValueList Expected, CssComponentValueList Actual)>();
        pending.Push((expected, actual));
        while (pending.Count != 0)
        {
            var pair = pending.Pop();
            pair.Actual.Count.Should().Be(pair.Expected.Count);
            for (var index = 0; index < pair.Expected.Count; index++)
            {
                var left = pair.Expected[index];
                var right = pair.Actual[index];
                right.Kind.Should().Be(left.Kind);
                if (left.Kind == CssComponentKind.Token)
                {
                    right.Token.Kind.Should().Be(left.Token.Kind);
                    if (left.Token.Kind != CssTokenKind.Whitespace)
                    {
                        right.Token.Text.Should().Be(left.Token.Text);
                        right.Token.NumberText.Should().Be(left.Token.NumberText);
                        right.Token.Unit.Should().Be(left.Token.Unit);
                        right.Token.IsIdHash.Should().Be(left.Token.IsIdHash);
                    }
                }
                else
                {
                    if (left.Kind == CssComponentKind.Function)
                        right.FunctionName.Should().Be(left.FunctionName);
                    else right.OpeningDelimiter.Should().Be(left.OpeningDelimiter);
                    pending.Push((left.Values, right.Values));
                }
            }
        }
    }
}

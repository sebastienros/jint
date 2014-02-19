using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterLsU2028AsLineTerminatorsWhenParsingStatements()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterPsU2029AsANonescapecharacter()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-10.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5SpecifiesThatAMultilineCommentThatContainsALineTerminatorCharacterLsU2028MustBeTreatedAsASingleLineTerminatorForThePurposesOfSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-11.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5SpecifiesThatAMultilineCommentThatContainsALineTerminatorCharacterPsU2029MustBeTreatedAsASingleLineTerminatorForThePurposesOfSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-12.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5SpecifiesThatAMultilineCommentThatContainsALineTerminatorCharacterCrU000DMustBeTreatedAsASingleLineTerminatorForThePurposesOfSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-13.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5SpecifiesThatAMultilineCommentThatContainsALineTerminatorCharacterLfU000AMustBeTreatedAsASingleLineTerminatorForThePurposesOfSemicolonInsertion()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-14.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizeBomUffffAsAWhitespaceCharacter()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-15.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterPsU2029AsLineTerminatorsWhenParsingStatements()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterLsU2028AsTerminatingSinglelinecomments()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-3.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterPsU2029AsTerminatingSinglelinecomments()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-4.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterLsU2028AsTerminatingStringLiteral()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-5.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterPsU2029AsTerminatingStringLiteral()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-6.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterLsU2028AsTerminatingRegularExpressionLiterals()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-7.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterPsU2029AsTerminatingRegularExpressionLiterals()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-8.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void Es5RecognizesTheCharacterLsU2028AsANonescapecharacter()
        {
			RunTest(@"TestCases/ch07/7.3/7.3-9.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineFeedU000AMayOccurBetweenAnyTwoTokens()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineFeedU000AMayOccurBetweenAnyTwoTokens2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void CarriageReturnU000DMayOccurBetweenAnyTwoTokens()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void CarriageReturnU000DMayOccurBetweenAnyTwoTokens2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineSeparatorU2028MayOccurBetweenAnyTwoTokens()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.3.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void ParagraphSeparatorU2029MayOccurBetweenAnyTwoTokens()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A1.4.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineFeedU000AWithinStringsIsNotAllowed()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineFeedU000AWithinStringsIsNotAllowed2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void CarriageReturnU000DWithinStringsIsNotAllowed()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void CarriageReturnU000DWithinStringsIsNotAllowed2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineSeparatorU2028WithinStringsIsNotAllowed()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.3.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void ParagraphSeparatorU2029WithinStringsIsNotAllowed()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A2.4.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainLineFeedU000AInside()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainLineFeedU000AInside2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainLineFeedU000AInside3()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainCarriageReturnU000DInside()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainCarriageReturnU000DInside2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainCarriageReturnU000DInside3()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainLineSeparatorU2028Inside()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainLineSeparatorU2028Inside2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.3_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainParagraphSeparatorU2029Inside()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.4_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanNotContainParagraphSeparatorU2029Inside2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A3.4_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanContainLineTerminatorAtTheEndOfLine()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanContainLineTerminatorAtTheEndOfLine2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanContainLineTerminatorAtTheEndOfLine3()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void SingleLineCommentsCanContainLineTerminatorAtTheEndOfLine4()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainLineFeedU000A()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainLineFeedU000A2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainCarriageReturnU000D()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainCarriageReturnU000D2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainLineSeparatorU2028()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void MultiLineCommentCanContainLineSeparatorU2029()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A6_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A6_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits3()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A6_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits4()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A6_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed2()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed3()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed4()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed5()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed6()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed7()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.3")]
        public void LineTerminatorsBetweenOperatorsAreAllowed8()
        {
			RunTest(@"TestCases/ch07/7.3/S7.3_A7_T8.js", false);
        }


    }
}

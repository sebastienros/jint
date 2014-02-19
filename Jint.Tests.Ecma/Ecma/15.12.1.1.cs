using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_12_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarTreatsWhitespaceAsATokenSeperator()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void VtIsNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void FfIsNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void NbspIsNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void ZwsppIsNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void BomIsNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void OtherCategoryZSpacesAreNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void U2028AndU2029AreNotValidJsonWhitespaceAsSpecifiedByTheProductionJsonwhitespace()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void WhitespaceCharactersCanAppearBeforeAfterAnyJsontoken()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-0-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarTreatsTabAsAWhitespaceCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarTreatsCrAsAWhitespaceCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarTreatsLfAsAWhitespaceCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarTreatsSpAsAWhitespaceCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void JsonstringsCanBeWrittenUsingDoubleQuotes()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringMayNotBeDelimitedBySingleQuotes()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringMayNotBeDelimitedByUncodeEscapedQuotes()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringMustBothBeginAndEndWithDoubleQuotes()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringsCanContainNoJsonstringcharactersEmptyJsonstrings()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarDoesNotAllowAJsonstringcharacterToBeAnyOfTheUnicodeCharactersU0000ThruU0007()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarDoesNotAllowAJsonstringcharacterToBeAnyOfTheUnicodeCharactersU0008ThruU000F()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarDoesNotAllowAJsonstringcharacterToBeAnyOfTheUnicodeCharactersU0010ThruU0017()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarDoesNotAllowAJsonstringcharacterToBeAnyOfTheUnicodeCharactersU0018ThruU001F()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammarAllowsUnicodeEscapeSequencesInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringcharacterUnicodeescapeMayNotHaveFewerThan4HexCharacters()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void AJsonstringcharacterUnicodeescapeMayNotIncludeAnyNonHexCharacters()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g5-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsAsAJsonescapecharacterAfterInAJsonstring2()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsBAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsFAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsNAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsRAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.1.1")]
        public void TheJsonLexicalGrammerAllowsTAsAJsonescapecharacterAfterInAJsonstring()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.1/15.12.1.1/15.12.1.1-g6-7.js", false);
        }


    }
}

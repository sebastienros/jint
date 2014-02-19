using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.7")]
        public void CorrectInterpretationOfAllPunctuators()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits2()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T10.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits3()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits4()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits5()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits6()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T5.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits7()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits8()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T7.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits9()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T8.js", true);
        }

        [Fact]
        [Trait("Category", "7.7")]
        public void PunctuatorCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits10()
        {
			RunTest(@"TestCases/ch07/7.7/S7.7_A2_T9.js", true);
        }


    }
}

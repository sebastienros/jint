using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_8_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.8.4")]
        public void ADirectivePreceedingAnUseStrictDirectiveMayNotContainAnOctalescapesequence()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode9()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode10()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StrictModeOctalescapesequence0110IsForbiddenInStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode11()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode12()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode13()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode14()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode15()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode16()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode17()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode18()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode19()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode20()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode21()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode22()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode23()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode24()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void TwoOctalescapesequencesInAStringAreNotAllowedInAStringUnderStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void ThreeOctalescapesequencesInAStringAreNotAllowedInAStringUnderStrictMode()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode25()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode26()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode27()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode28()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode29()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void AnOctalescapesequenceIsNotAllowedInAStringUnderStrictMode30()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/7.8.4-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralDoublestringcharactersOpt()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A1.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralDoublestringcharactersOpt2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A1.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralSinglestringcharactersOpt()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A1.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralSinglestringcharactersOpt2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A1.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CorrectInterpretationOfEnglishAlphabet()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CorrectInterpretationOfEnglishAlphabet2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CorrectInterpretationOfRussianAlphabet()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CorrectInterpretationOfRussianAlphabet2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CorrectInterpretationOfDigits()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralOrIsNotCorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A3.1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralOrIsNotCorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A3.1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralOrIsNotCorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A3.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void StringliteralOrIsNotCorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A3.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceSingleescapesequence()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceSingleescapesequence2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void CharacterescapesequnceNonescapesequence8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void NonescapesequenceIsNotEscapecharacter()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void NonescapesequenceIsNotEscapecharacter2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.3_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void NonescapesequenceIsNotEscapecharacter3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A4.3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void Escapesequence0()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A5.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void Escapesequence02()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A5.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void Escapesequence03()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A5.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceHexescapesequenceXHexdigitHexdigit()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A6.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceHexescapesequenceXHexdigitHexdigit2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A6.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceHexescapesequenceXHexdigitHexdigit3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A6.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void XHexdigitHexdigitSinglestringcharacter()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A6.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void EscapesequenceUnicodeescapesequenceUHexdigitHexdigitHexdigitHexdigit4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.1_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T5.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UnicodeescapesequenceUHexdigitOneTwoOrThreeTimeIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.2_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.4")]
        public void UHexdigitHexdigitHexdigitHexdigitDoublestringcharacter()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.4/S7.8.4_A7.3_T1.js", false);
        }


    }
}

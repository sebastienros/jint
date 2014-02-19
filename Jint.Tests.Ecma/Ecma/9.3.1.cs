using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStringnumericliteralEmptyIs0()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsIsTheMvOfDecimaldigitsTimes10SupSmallNSmallSupWhereNIsTheNumberOfCharactersInDecimaldigits()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A10.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsExponentpartIsTheMvOfDecimaldigitsTimes10SupSmallENSmallSupWhereNIsTheNumberOfCharactersInDecimaldigitsAndEIsTheMvOfExponentpart()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A11.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsExponentpartIsTheMvOfDecimaldigitsTimes10SupSmallESmallSupWhereEIsTheMvOfExponentpart()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A12.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigitsDecimaldigitsDecimaldigitIsTheMvOfDecimaldigitsTimes10PlusTheMvOfDecimaldigit()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A13.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfSignedintegerDecimaldigitsIsTheMvOfDecimaldigits()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A14.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfSignedintegerDecimaldigitsIsTheNegativeOfTheMvOfDecimaldigits()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A15.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit0OrOfHexdigit0Is0()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A16.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit1OrOfHexdigit1Is1()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A17.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit2OrOfHexdigit2Is2()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A18.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit3OrOfHexdigit3Is3()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A19.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStringnumericliteralStrwhitespaceIs0()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit4OrOfHexdigit4Is4()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A20.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit5OrOfHexdigit5Is5()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A21.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit6OrOfHexdigit6Is6()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A22.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit7OrOfHexdigit7Is7()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A23.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit8OrOfHexdigit8Is8()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A24.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfDecimaldigit9OrOfHexdigit9Is9()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A25.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitAOrOfHexdigitAIs10()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A26.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitBOrOfHexdigitBIs11()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A27.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitCOrOfHexdigitCIs12()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A28.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitDOrOfHexdigitDIs13()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A29.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitEOrOfHexdigitEIs14()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A30.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfHexdigitFOrOfHexdigitFIs15()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A31.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void OnceTheExactMvForAStringNumericLiteralHasBeenDeterminedItIsThenRoundedToAValueOfTheNumberTypeWith20SignificantDigitsByReplacingEachSignificantDigitAfterThe20ThWithA0DigitOrTheNumberValue()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A32.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStringnumericliteralStrwhitespaceoptStrnumericliteralStrwhitespaceoptIsTheMvOfStrnumericliteralNoMatterWhetherWhiteSpaceIsPresentOrNot()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStringnumericliteralStrwhitespaceoptStrnumericliteralStrwhitespaceoptIsTheMvOfStrnumericliteralNoMatterWhetherWhiteSpaceIsPresentOrNot2()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrdecimalliteralStrunsigneddecimalliteralIsTheMvOfStrunsigneddecimalliteral()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrdecimalliteralStrunsigneddecimalliteralIsTheMvOfStrunsigneddecimalliteral2()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrdecimalliteralStrunsigneddecimalliteralIsTheNegativeOfTheMvOfStrunsigneddecimalliteralTheNegativeOfThis0IsAlso0()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrdecimalliteralStrunsigneddecimalliteralIsTheNegativeOfTheMvOfStrunsigneddecimalliteralTheNegativeOfThis0IsAlso02()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrdecimalliteralStrunsigneddecimalliteralIsTheNegativeOfTheMvOfStrunsigneddecimalliteralTheNegativeOfThis0IsAlso03()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralInfinityIs10SupSmall10000SmallSupAValueSoLargeThatItWillRoundToBTtInfinTtB()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralInfinityIs10SupSmall10000SmallSupAValueSoLargeThatItWillRoundToBTtInfinTtB2()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsDecimaldigitsIsTheMvOfTheFirstDecimaldigitsPlusTheMvOfTheSecondDecimaldigitsTimes10SupSmallNSmallSupWhereNIsTheNumberOfCharactersInTheSecondDecimaldigits()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A7.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsExponentpartIsTheMvOfDecimaldigitsTimes10SupSmallESmallSupWhereEIsTheMvOfExponentpart2()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A8.js", false);
        }

        [Fact]
        [Trait("Category", "9.3.1")]
        public void TheMvOfStrunsigneddecimalliteralDecimaldigitsDecimaldigitsExponentpartIsTheMvOfTheFirstDecimaldigitsPlusTheMvOfTheSecondDecimaldigitsTimes10SupSmallNSmallSupTimes10SupSmallESmallSupWhereNIsTheNumberOfCharactersInTheSecondDecimaldigitsAndEIsTheMvOfExponentpart()
        {
			RunTest(@"TestCases/ch09/9.3/9.3.1/S9.3.1_A9.js", false);
        }


    }
}

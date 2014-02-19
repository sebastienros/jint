using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_8_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.8.5")]
        public void LiteralRegexpObjectsSyntaxerrorExceptionIsThrownIfTheRegularexpressionnonterminatorPositionOfARegularexpressionbackslashsequenceIsALineterminator()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/7.8.5-1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void EmptyLiteralRegexpShouldResultInASyntaxerror()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/7.8.5-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void EmptyDynamicRegexpShouldNotResultInASyntaxerror()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/7.8.5-2gs.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharNonterminatorButNotOrOrRegularexpressioncharsEmptyRegularexpressionflagsEmpty()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharNonterminatorButNotOrOrRegularexpressioncharsEmptyRegularexpressionflagsEmpty2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharOrOrOrEmptyIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharOrOrOrEmptyIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharOrOrOrEmptyIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharOrOrOrEmptyIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.2_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharLineterminatorIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceNonterminatorRegularexpressioncharsEmptyRegularexpressionflagsEmpty()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.4_T1.js", false);
        }

        [Fact(Skip = @"The pattern a\P is evaluatead as a syntax error in .NET")]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceNonterminatorRegularexpressioncharsEmptyRegularexpressionflagsEmpty2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionfirstcharBackslashsequenceLineterminatorIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A1.5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharNonterminatorButNotOrRegularexpressionflagsEmpty()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharNonterminatorButNotOrRegularexpressionflagsEmpty2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharOrIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharOrIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharLineterminatorIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceNonterminatorRegularexpressionflagsEmpty()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.4_T1.js", false);
        }

        [Fact(Skip = @"The pattern a\P is evaluatead as a syntax error in .NET")]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceNonterminatorRegularexpressionflagsEmpty2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressioncharBackslashsequenceLineterminatorIsIncorrect6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A2.5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart2()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart3()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart4()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart5()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart6()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart7()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart8()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void RegularexpressionflagsIdentifierpart9()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A3.1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void ARegularExpressionLiteralIsAnInputElementThatIsConvertedToARegexpObjectWhenItIsScanned()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "7.8.5")]
        public void TwoRegularExpressionLiteralsInAProgramEvaluateToRegularExpressionObjectsThatNeverCompareAsToEachOtherEvenIfTheTwoLiteralsContentsAreIdentical()
        {
			RunTest(@"TestCases/ch07/7.8/7.8.5/S7.8.5_A4.2.js", false);
        }


    }
}

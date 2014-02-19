using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ParesefloatTrimmedstringIsTheEmptyStringWhenInputstringDoesNotContainAnySuchCharacters()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/15.1.2.3-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorUseTostring7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar8()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar9()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void OperatorRemoveLeadingStrwhitespacechar10()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void IfNeitherResult2NorAnyPrefixOfResult2SatisfiesTheSyntaxOfAStrdecimalliteralSee931ReturnNan()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void IfNeitherResult2NorAnyPrefixOfResult2SatisfiesTheSyntaxOfAStrdecimalliteralSee931ReturnNan2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void IfNeitherResult2NorAnyPrefixOfResult2SatisfiesTheSyntaxOfAStrdecimalliteralSee931ReturnNan3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ComputeTheLongestPrefixOfResult2WhichMightBeResult2ItselfWhichSatisfiesTheSyntaxOfAStrdecimalliteral7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ReturnTheNumberValueForTheMvOfResult4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ReturnTheNumberValueForTheMvOfResult42()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ReturnTheNumberValueForTheMvOfResult43()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ReturnTheNumberValueForTheMvOfResult44()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void ParsefloatMayInterpretOnlyALeadingPortionOfTheStringAsANumberValueItIgnoresAnyCharactersThatCannotBeInterpretedAsPartOfTheNotationOfAnDecimalLiteralAndNoIndicationIsGivenThatAnySuchCharactersWereIgnored()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheLengthPropertyOfParsefloatHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheLengthPropertyOfParsefloatHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheLengthPropertyOfParsefloatHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheLengthPropertyOfParsefloatIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheParsefloatPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheParsefloatPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.3")]
        public void TheParsefloatPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.3/S15.1.2.3_A7.7.js", false);
        }


    }
}

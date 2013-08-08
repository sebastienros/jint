using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void PareseintSIsTheEmptyStringWhenInputstringDoesNotContainAnySuchCharacters()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/15.1.2.2-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTostring7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar8()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar9()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorRemoveLeadingStrwhitespacechar10()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseTonumber7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseToint32()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseToint322()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void OperatorUseToint323()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfR0OrRUndefinedThenR10()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfR0OrRUndefinedThenR102()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfR2OrR36ThenReturnNan()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfR2OrR36ThenReturnNan2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfR2OrR36ThenReturnNan3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A4.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ParseintIsNoLongerAllowedToTreatALeadingZeroAsIndicatingOctalIfRadixIsUndefinedOr0ItIsAssumedToBe10ExceptWhenTheNumberBeginsWithTheCharacterPairs0XOr0XInWhichCaseARadixOf16IsAssumed()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A5.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfTheLengthOfSIsAtLeast2AndTheFirstTwoCharactersOfSAreEither0XOr0XThenRemoveTheFirstTwoCharactersFromSAndLetR16()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A5.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfTheLengthOfSIsAtLeast2AndTheFirstTwoCharactersOfSAreEither0XOr0XThenRemoveTheFirstTwoCharactersFromSAndLetR162()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A5.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfSContainsAnyCharacterThatIsNotARadixRDigitThenLetZBeTheSubstringOfSConsistingOfAllCharactersBeforeTheFirstSuchCharacterOtherwiseLetZBeS6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A6.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfZIsEmptyReturnNan()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void IfZIsEmptyReturnNan2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ComputeTheMathematicalIntegerValueThatIsRepresentedByZInRadixRNotationUsingTheLettersAZAndAZForDigitsWithValues10Through35ComputeTheNumberValueForResult16()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ComputeTheMathematicalIntegerValueThatIsRepresentedByZInRadixRNotationUsingTheLettersAZAndAZForDigitsWithValues10Through35ComputeTheNumberValueForResult162()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ComputeTheMathematicalIntegerValueThatIsRepresentedByZInRadixRNotationUsingTheLettersAZAndAZForDigitsWithValues10Through35ComputeTheNumberValueForResult163()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ReturnSignResult17()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ReturnSignResult172()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ReturnSignResult173()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A7.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void ParseintMayInterpretOnlyALeadingPortionOfTheStringAsANumberValueItIgnoresAnyCharactersThatCannotBeInterpretedAsPartOfTheNotationOfAnDecimalLiteralAndNoIndicationIsGivenThatAnySuchCharactersWereIgnored()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheLengthPropertyOfParseintHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheLengthPropertyOfParseintHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheLengthPropertyOfParseintHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheLengthPropertyOfParseintIs2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheParseintPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheParseintPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.2")]
        public void TheParseintPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.2/S15.1.2.2_A9.7.js", false);
        }


    }
}

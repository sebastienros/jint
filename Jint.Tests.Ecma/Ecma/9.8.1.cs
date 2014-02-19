using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_8_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.8.1")]
        public void IfMIsNanReturnTheStringNan()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void ReturnTheStringConsistingOfTheMostSignificantDigitOfTheDecimalRepresentationOfSFollowedByADecimalPointFollowedByTheRemainingK1DigitsOfTheDecimalRepresentationOfSFollowedByTheLowercaseCharacterEFollowedByAPlusSignOrMinusSignAccordingToWhetherN1IsPositiveOrNegativeFollowedByTheDecimalRepresentationOfTheIntegerAbsN1WithNoLeadingZeros()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A10.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void IfMIs0Or0ReturnTheString0()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void IfMIsLessThanZeroReturnTheStringConcatenationOfTheStringAndTostringM()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void IfMIsInfinityReturnTheStringInfinity()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A4.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void If1S1E21Or1E21S1ReturnTheStringConsistingOfTheKDigitsOfTheDecimalRepresentationOfSInOrderWithNoLeadingZeroesFollowedByNKOccurrencesOfTheCharacter0()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A6.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void If1S1E21Or1E21S1AndSHasAFractionalComponentReturnTheStringConsistingOfTheMostSignificantNDigitsOfTheDecimalRepresentationOfSFollowedByADecimalPointFollowedByTheRemainingKNDigitsOfTheDecimalRepresentationOfS()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A7.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void If1S1And6N0ReturnTheStringConsistingOfTheCharacter0FollowedByADecimalPointFollowedByNOccurrencesOfTheCharacter0FollowedByTheKDigitsOfTheDecimalRepresentationOfS()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A8.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void ReturnTheStringConsistingOfTheSingleDigitOfSFollowedByLowercaseCharacterEFollowedByAPlusSignOrMinusSignAccordingToWhetherN1IsPositiveOrNegativeFollowedByTheDecimalRepresentationOfTheIntegerAbsN1WithNoLeadingZeros()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8.1")]
        public void ReturnTheStringConsistingOfTheSingleDigitOfSFollowedByLowercaseCharacterEFollowedByAPlusSignOrMinusSignAccordingToWhetherN1IsPositiveOrNegativeFollowedByTheDecimalRepresentationOfTheIntegerAbsN1WithNoLeadingZeros2()
        {
			RunTest(@"TestCases/ch09/9.8/9.8.1/S9.8.1_A9_T2.js", false);
        }


    }
}

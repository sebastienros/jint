using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.9")]
        public void AnEscapeSequenceOfTheFormFollowedByANonzeroDecimalNumberNMatchesTheResultOfTheNthSetOfCapturingParenthesesSee1510211()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.9/S15.10.2.9_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.9")]
        public void AnEscapeSequenceOfTheFormFollowedByANonzeroDecimalNumberNMatchesTheResultOfTheNthSetOfCapturingParenthesesSee15102112()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.9/S15.10.2.9_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.9")]
        public void AnEscapeSequenceOfTheFormFollowedByANonzeroDecimalNumberNMatchesTheResultOfTheNthSetOfCapturingParenthesesSee15102113()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.9/S15.10.2.9_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.9")]
        public void AnEscapeSequenceOfTheFormFollowedByANonzeroDecimalNumberNMatchesTheResultOfTheNthSetOfCapturingParenthesesSee15102114()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.9/S15.10.2.9_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.9")]
        public void AnEscapeSequenceOfTheFormFollowedByANonzeroDecimalNumberNMatchesTheResultOfTheNthSetOfCapturingParenthesesSee15102115()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.9/S15.10.2.9_A1_T5.js", false);
        }


    }
}

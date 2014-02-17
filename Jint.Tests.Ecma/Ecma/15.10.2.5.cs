using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.5")]
        public void TermSyntaxerrorWasThrownWhenMaxIsFiniteAndLessThanMin151025Step3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.5")]
        public void AnAtomFollowedByAQuantifierIsRepeatedTheNumberOfTimesSpecifiedByTheQuantifier()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5/S15.10.2.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.5")]
        public void AnAtomFollowedByAQuantifierIsRepeatedTheNumberOfTimesSpecifiedByTheQuantifier2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5/S15.10.2.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.5")]
        public void AnAtomFollowedByAQuantifierIsRepeatedTheNumberOfTimesSpecifiedByTheQuantifier3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5/S15.10.2.5_A1_T3.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.5")]
        public void AnAtomFollowedByAQuantifierIsRepeatedTheNumberOfTimesSpecifiedByTheQuantifier4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5/S15.10.2.5_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.5")]
        public void AnAtomFollowedByAQuantifierIsRepeatedTheNumberOfTimesSpecifiedByTheQuantifier5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.5/S15.10.2.5_A1_T5.js", false);
        }


    }
}

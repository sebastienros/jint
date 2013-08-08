using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsDecimaldigitsEvaluatesAs12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void ITheProductionQuantifierprefixDecimaldigitsEvaluatesIiTheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And1()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void ITheProductionQuantifierprefixDecimaldigitsEvaluatesIiTheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void ITheProductionQuantifierprefixDecimaldigitsEvaluatesIiTheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void ITheProductionQuantifierprefixDecimaldigitsEvaluatesIiTheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults1AndInfty14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty18()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty19()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty20()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0AndInfty21()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A4_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And1()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And18()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And19()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And110()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And111()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixEvaluatesByReturningTheTwoResults0And112()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A5_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.7")]
        public void TheProductionQuantifierprefixDecimaldigitsEvaluatesAsFollowsILetIBeTheMvOfDecimaldigitsIiReturnTheTwoResultsIAndInfty6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.7/S15.10.2.7_A6_T6.js", false);
        }


    }
}

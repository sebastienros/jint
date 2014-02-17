using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T15.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T16.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.3")]
        public void TheRegularExpressionOperatorSeparatesTwoAlternativesThePatternFirstTriesToMatchTheLeftAlternativeFollowedByTheSequelOfTheRegularExpressionIfItFailsItTriesToMatchTheRightDisjunctionFollowedByTheSequelOfTheRegularExpression17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.3/S15.10.2.3_A1_T9.js", false);
        }


    }
}

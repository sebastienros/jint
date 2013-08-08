using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.13")]
        public void SanityTestForThrowStatement()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A1.js", true);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression2()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression3()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression4()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression5()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression6()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void ThrowExpressionReturnsThrowGetvalueResult1EmptyWhere1EvaluatesExpression7()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression2()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression3()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression4()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression5()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.13")]
        public void EvaluateExpression6()
        {
			RunTest(@"TestCases/ch12/12.13/S12.13_A3_T6.js", false);
        }


    }
}

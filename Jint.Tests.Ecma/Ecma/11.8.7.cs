using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_8_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.8.7")]
        public void WhiteSpaceAndLineTerminatorBetweenRelationalexpressionAndInAndBetweenInAndShiftexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void OperatorInUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void OperatorInUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void OperatorInUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void IfShiftexpressionIsNotAnObjectThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.7")]
        public void OperatorInCallsTostringShiftexpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.7/S11.8.7_A4.js", false);
        }


    }
}

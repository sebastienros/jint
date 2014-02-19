using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_11_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.11.1")]
        public void WhiteSpaceAndLineTerminatorBetweenLogicalandexpressionAndOrBetweenAndBitwiseorexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void OperatorXYUsesGetvalue4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsFalseReturnX()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsFalseReturnX2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsFalseReturnX3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsFalseReturnX4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsTrueReturnY()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsTrueReturnY2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsTrueReturnY3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.1")]
        public void IfTobooleanXIsTrueReturnY4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.1/S11.11.1_A4_T4.js", false);
        }


    }
}

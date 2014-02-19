using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_11_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.11.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLogicalorexpressionAndOrBetweenAndLogicalandexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void OperatorXYUsesGetvalue4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsFalseReturnY()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsFalseReturnY2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsFalseReturnY3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsFalseReturnY4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsTrueReturnX()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsTrueReturnX2()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsTrueReturnX3()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.11.2")]
        public void IfTobooleanXIsTrueReturnX4()
        {
			RunTest(@"TestCases/ch11/11.11/11.11.2/S11.11.2_A4_T4.js", false);
        }


    }
}

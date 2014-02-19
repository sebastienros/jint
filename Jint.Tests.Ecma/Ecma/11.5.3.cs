using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_5_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.5.3")]
        public void WhiteSpaceAndLineTerminatorBetweenMultiplicativeexpressionAndOrBetweenAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TonumberFirstExpressionIsCalledFirstAndThenTonumberSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY2()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY3()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY4()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY5()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T1.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY6()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY7()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY8()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY9()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY10()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY11()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY12()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY13()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void OperatorXYReturnsTonumberXTonumberY14()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A3_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics2()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics3()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics4()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics5()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics6()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics7()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.5.3")]
        public void TheResultOfAEcmascriptFloatingPointRemainderOperationIsDeterminedByTheRulesOfIeeeArithmetics8()
        {
			RunTest(@"TestCases/ch11/11.5/11.5.3/S11.5.3_A4_T7.js", false);
        }


    }
}

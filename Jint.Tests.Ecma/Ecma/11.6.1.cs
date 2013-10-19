using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_11_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.6.1")]
        public void WhiteSpaceAndLineTerminatorBetweenAdditiveexpressionAndOrBetweenAndMultiplicativeexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesDefaultValue2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void OperatorXYUsesDefaultValue3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TonumberFirstExpressionIsCalledFirstAndThenTonumberSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY4()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY5()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY6()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY7()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsNotStringAndTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY8()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY4()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY5()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void IfTypePrimitiveXIsStringOrTypePrimitiveYIsStringThenOperatorXYReturnsTheResultOfConcatenatingTostringXFollowedByTostringY6()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A3.2_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics2()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics3()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics4()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics5()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics6()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics7()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics8()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "11.6.1")]
        public void TheResultOfAnAdditionIsDeterminedUsingTheRulesOfIeee754DoublePrecisionArithmetics9()
        {
			RunTest(@"TestCases/ch11/11.6/11.6.1/S11.6.1_A4_T9.js", false);
        }


    }
}

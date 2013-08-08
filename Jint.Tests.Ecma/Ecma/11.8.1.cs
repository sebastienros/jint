using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_8_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.8.1")]
        public void WhiteSpaceAndLineTerminatorBetweenRelationalexpressionAndOrBetweenAndShiftexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void TonumberFirstExpressionIsCalledFirstAndThenTonumberSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY4()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY5()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY6()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY7()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY8()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY9()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY10()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY11()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY12()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.1_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.2_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A3.2_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfYIsAPrefixOfXReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXIsAPrefixOfYAndXYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfYIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXAndYAreTheSameNumberValueReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXAndYAre0And0ReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXIsInfinityReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfYIsInfinityAndXYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXIsInfinityAndXYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfYIsInfinityReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.1")]
        public void IfXIsLessThanYAndTheseValuesAreBothFiniteNonZeroReturnTrueOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.1/S11.8.1_A4.9.js", false);
        }


    }
}

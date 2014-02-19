using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_8_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.8.4")]
        public void WhiteSpaceAndLineTerminatorBetweenRelationalexpressionAndOrAndShiftexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void TonumberFirstExpressionIsCalledFirstAndThenTonumberSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY4()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY5()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY6()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY7()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY8()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY9()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY10()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY11()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotStringThenOperatorXYReturnsTonumberXTonumberY12()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.1_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.2_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A3.2_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXIsAPrefixOfYAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfYIsAPrefixOfXReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfYIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXAndYAreTheSameNumberValueReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfEitherXOrYIs0AndTheOtherIs0ReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXIsInfinityReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfYIsInfinityAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXIsInfinityAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfYIsInfinityReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.4")]
        public void IfXIsGreaterOrEqualThanYAndTheseValuesAreBothFiniteNonZeroReturnTrueOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.4/S11.8.4_A4.9.js", false);
        }


    }
}

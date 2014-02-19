using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_8_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.8.3")]
        public void LessThanOrEqualOperatorPartialLeftToRightOrderEnforcedWhenUsingLessThanOrEqualOperatorValueofValueof()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/11.8.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void LessThanOrEqualOperatorPartialLeftToRightOrderEnforcedWhenUsingLessThanOrEqualOperatorValueofTostring()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/11.8.3-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void LessThanOrEqualOperatorPartialLeftToRightOrderEnforcedWhenUsingLessThanOrEqualOperatorTostringValueof()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/11.8.3-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void LessThanOrEqualOperatorPartialLeftToRightOrderEnforcedWhenUsingLessThanOrEqualOperatorTostringTostring()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/11.8.3-4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void LessThanOrEqualOperatorPartialLeftToRightOrderEnforcedWhenUsingLessThanOrEqualOperatorValueofValueof2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/11.8.3-5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void WhiteSpaceAndLineTerminatorBetweenRelationalexpressionAndOrBetweenAndShiftexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYUsesDefaultValue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void InEs5FirstExpressionShouldBeEvaluatedFirst()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString3()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString4()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString5()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString6()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString7()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString8()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString9()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString10()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString11()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTonumberXTonumberYIfTypePrimitiveXIsNotStringOrTypePrimitiveYIsNotString12()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.1_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.2_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void OperatorXYReturnsTostringXTostringYIfTypePrimitiveXIsStringAndTypePrimitiveYIsString2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A3.2_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfYIsAPrefixOfXAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXIsAPrefixOfYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfNeitherXNorYIsAPrefixOfEachOtherReturnedResultOfStringsComparisonAppliesASimpleLexicographicOrderingToTheSequencesOfCodePointValueValues2()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfYIsNanReturnFalseIfResultIn1185IsUndefinedReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXAndYAreTheSameNumberValueReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfEitherXOrYIs0AndTheOtherIs0ReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXIsInfinityAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfYIsInfinityAndXYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXIsInfinityReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfYIsInfinityAndXYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.8.3")]
        public void IfXIsLessOrEqualThanYAndTheseValuesAreBothFiniteNonZeroReturnTrueOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.8/11.8.3/S11.8.3_A4.9.js", false);
        }


    }
}

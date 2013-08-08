using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_9_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.9.1")]
        public void WhiteSpaceAndLineTerminatorBetweenEqualityexpressionAndOrBetweenAndRelationalexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void ReturnTrueIfXAndYAreBothTrueOrBothFalseOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsBooleanAndTypeYIsNumberReturnTheResultOfComparisonTonumberXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeYIsNumberAndTypeYIsBooleanReturnTheResultOfComparisonXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A3.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfXOrYIsNanReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfXOrYIsNanReturnFalse2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfXIs00AndYIs00ReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void TypeXAndTypeYAreNumberSMinusNan00ReturnTrueIfXIsTheSameNumberValueAsYOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void TypeXAndTypeYAreStringSReturnTrueIfXAndYAreExactlyTheSameSequenceOfCharactersOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsNumberAndTypeYIsStringReturnTheResultOfComparisonXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsStringAndTypeYIsNumberReturnTheResultOfComparisonTonumberXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXAsWellAsTypeYIsUndefinedOrNullReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfOneExpressionIsUndefinedOrNullAndAnotherIsNotReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A6.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfOneExpressionIsUndefinedOrNullAndAnotherIsNotReturnFalse2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A6.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void TypeXAndTypeYAreObjectSReturnTrueIfXAndYAreReferencesToTheSameObjectOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsObjectAndTypeYIsBooleanReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsBooleanAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsObjectAndTypeYIsNumberReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsNumberAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsObjectAndTypeYIsStringReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsStringAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsObjectAndTypeYIsPrimitiveTypeReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.1")]
        public void IfTypeXIsPrimitiveTypeAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.1/S11.9.1_A7.9.js", false);
        }


    }
}

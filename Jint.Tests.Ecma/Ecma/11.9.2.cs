using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_9_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.9.2")]
        public void WhiteSpaceAndLineTerminatorBetweenEqualityexpressionAndOrBetweenAndRelationalexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void ReturnFalseIfXAndYAreBothTrueOrBothFalseOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsBooleanAndTypeYIsNumberReturnTheResultOfComparisonTonumberXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeYIsNumberAndTypeYIsBooleanReturnTheResultOfComparisonXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A3.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfXOrYIsNanReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfXOrYIsNanReturnTrue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfXIs00AndYIs00ReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void TypeXAndTypeYAreNumberSMinusNan00ReturnFalseIfXIsTheSameNumberValueAsYOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void TypeXAndTypeYAreStringSReturnTrueIfXAndYAreExactlyTheSameSequenceOfCharactersOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsNumberAndTypeYIsStringReturnTheResultOfComparisonXTonumberY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsStringAndTypeYIsNumberReturnTheResultOfComparisonTonumberXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXAsWellAsTypeYIsUndefinedOrNullReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfOneExpressionIsUndefinedOrNullAndAnotherIsNotReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A6.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfOneExpressionIsUndefinedOrNullAndAnotherIsNotReturnFalse2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A6.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void TypeXAndTypeYAreObjectSReturnTrueIfXAndYAreReferencesToTheSameObjectOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsObjectAndTypeYIsBooleanReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsBooleanAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsObjectAndTypeYIsNumberReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsNumberAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsObjectAndTypeYIsStringReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsStringAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsObjectAndTypeYIsPrimitiveTypeReturnToprimitiveXY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.2")]
        public void IfTypeXIsPrimitiveTypeAndTypeYIsObjectReturnXToprimitiveY()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.2/S11.9.2_A7.9.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_9_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.9.4")]
        public void WhiteSpaceAndLineTerminatorBetweenEqualityexpressionAndOrBetweenAndRelationalexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void TypeXAndTypeYAreBooleanSReturnTrueIfXAndYAreBothTrueAndBothFalseOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfXOrYIsNanReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfXOrYIsNanReturnFalse2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfXIs00AndYIs00ReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void TypeXAndTypeYAreNumberSMinusNan00ReturnTrueIfXIsTheSameNumberValueAsYOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void TypeXAndTypeYAreStringSReturnTrueIfXAndYAreExactlyTheSameSequenceOfCharactersOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXAndTypeYAreUndefinedSReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXAndTypeYAreNullSReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A6.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void TypeXAndTypeYAreObjectSReturnTrueIfXAndYAreReferencesToTheSameObjectOtherwiseReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A7.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXIsDifferentFromTypeYReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXIsDifferentFromTypeYReturnFalse2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXIsDifferentFromTypeYReturnFalse3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXIsDifferentFromTypeYReturnFalse4()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.4")]
        public void IfTypeXIsDifferentFromTypeYReturnFalse5()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.4/S11.9.4_A8_T5.js", false);
        }


    }
}

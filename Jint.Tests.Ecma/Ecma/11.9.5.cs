using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_9_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.9.5")]
        public void WhiteSpaceAndLineTerminatorBetweenEqualityexpressionAndOrBetweenAndRelationalexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void OperatorXYUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void OperatorXYUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void OperatorXYUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void FirstExpressionIsEvaluatedFirstAndThenSecondExpression3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A2.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void TypeXAndTypeYAreBooleanSReturnFalseIfXAndYAreBothTrueOrBothFalseOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfXOrYIsNanReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfXOrYIsNanReturnTrue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfXIs00AndYIs00ReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void TypeXAndTypeYAreNumberSMinusNan00ReturnFalseIfXIsTheSameNumberValueAsYOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void TypeXAndTypeYAreStringSReturnFalseIfXAndYAreExactlyTheSameSequenceOfCharactersOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A5.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXAndTypeYAreUndefinedSReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXAndTypeYAreNullSReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A6.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void TypeXAndTypeYAreObjectSReturnFalseIfXAndYAreReferencesToTheSameObjectOtherwiseReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXIsDifferentFromTypeYReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXIsDifferentFromTypeYReturnTrue2()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXIsDifferentFromTypeYReturnTrue3()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXIsDifferentFromTypeYReturnTrue4()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.9.5")]
        public void IfTypeXIsDifferentFromTypeYReturnTrue5()
        {
			RunTest(@"TestCases/ch11/11.9/11.9.5/S11.9.5_A8_T5.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedTryCatch()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedTryCatchFinally()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-2.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedTryFinally()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-3.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedIfElse()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-4.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedIfElseIf()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-5.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedIfElseIfElse()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-6.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void BlockStatementlistoptIsNotAllowedDoWhile()
        {
			RunTest(@"TestCases/ch12/12.1/12.1-7.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void TheProductionStatementlistStatementIsEvaluatedAsFollows1EvaluateStatement2IfAnExceptionWasThrownReturnThrowVEmptyWhereVIsTheException()
        {
			RunTest(@"TestCases/ch12/12.1/S12.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void TheProductionBlockCanTBeInsideOfExpression()
        {
			RunTest(@"TestCases/ch12/12.1/S12.1_A4_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void TheProductionBlockCanTBeInsideOfExpression2()
        {
			RunTest(@"TestCases/ch12/12.1/S12.1_A4_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.1")]
        public void StatementlistStatementlistStatementInsideTheBlockIsEvaluatedFromLeftToRight()
        {
			RunTest(@"TestCases/ch12/12.1/S12.1_A5.js", false);
        }


    }
}

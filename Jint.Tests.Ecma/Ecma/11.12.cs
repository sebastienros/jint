using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_11_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.12")]
        public void WhiteSpaceAndLineTerminatorBetweenLogicalorexpressionAndOrBetweenAndAssignmentexpressionOrBetweenAssignmentexpressionAndOrBetweenAndAssignmentexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue4()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue5()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void OperatorXYZUsesGetvalue6()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A2.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsFalseReturnZ()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsFalseReturnZ2()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsFalseReturnZ3()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsFalseReturnZ4()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsTrueReturnY()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsTrueReturnY2()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsTrueReturnY3()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.12")]
        public void IfTobooleanXIsTrueReturnY4()
        {
			RunTest(@"TestCases/ch11/11.12/S11.12_A4_T4.js", false);
        }


    }
}

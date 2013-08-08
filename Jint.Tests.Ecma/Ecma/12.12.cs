using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.12")]
        public void LabelledStatementsAreOnlyUsedInConjunctionWithLabelledBreakAndContinueStatements()
        {
			RunTest(@"TestCases/ch12/12.12/S12.12_A1_T1.js", false);
        }


    }
}

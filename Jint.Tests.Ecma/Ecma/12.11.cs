using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.11")]
        public void IfResultTypeIsBreakAndResultTargetIsInTheCurrentLabelSetReturnNormalResultValueEmpty()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void IfResultTypeIsBreakAndResultTargetIsInTheCurrentLabelSetReturnNormalResultValueEmpty2()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void IfResultTypeIsBreakAndResultTargetIsInTheCurrentLabelSetReturnNormalResultValueEmpty3()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void IfResultTypeIsBreakAndResultTargetIsInTheCurrentLabelSetReturnNormalResultValueEmpty4()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void ThereCanBeOnlyOneDefaultclause()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void SyntaxConstructionsOfSwitchStatement()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void SyntaxConstructionsOfSwitchStatement2()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A3_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void SyntaxConstructionsOfSwitchStatement3()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A3_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void SyntaxConstructionsOfSwitchStatement4()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A3_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void SyntaxConstructionsOfSwitchStatement5()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A3_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.11")]
        public void EmbeddedSyntaxConstructionsOfSwitchStatement()
        {
			RunTest(@"TestCases/ch12/12.11/S12.11_A4_T1.js", false);
        }


    }
}

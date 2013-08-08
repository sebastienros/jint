using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_9_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart2()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart3()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart4()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart5()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart6()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T6.js", true);
        }

        [Fact]
        [Trait("Category", "7.9.2")]
        public void CheckExamplesForAutomaticSemicolonInsertionFromTheStandart7()
        {
			RunTest(@"TestCases/ch07/7.9/7.9.2/S7.9.2_A1_T7.js", false);
        }


    }
}

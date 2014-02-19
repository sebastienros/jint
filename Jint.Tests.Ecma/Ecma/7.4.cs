using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.4")]
        public void CorrectInterpretationOfSingleLineComments()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void CorrectInterpretationOfSingleLineComments2()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void CorrectInterpretationOfMultiLineComments()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void CorrectInterpretationOfMultiLineComments2()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void MultiLineCommentsCannotNest()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A3.js", true);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether2()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether3()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether4()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether5()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether6()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleAndMultiLineCommentsAreUsedTogether7()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void SingleLineCommentsCanContainAnyUnicodeCharacterWithoutLineTerminators()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "7.4")]
        public void IfMultiLineCommentsCsnNotNestTheyCanContainAnyUnicodeCharacter()
        {
			RunTest(@"TestCases/ch07/7.4/S7.4_A6.js", false);
        }


    }
}

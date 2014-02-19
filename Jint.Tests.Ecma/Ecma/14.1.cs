using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_14_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveCorrectUsage()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void OtherDirectivesMayFollowUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void CommentsMayPreceedUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void CommentsMayFollowUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void SemicolonInsertionWorksForUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void SemicolonInsertionMayComeBeforeUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void BlankLinesMayComeBeforeUseStrictDirective()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfItFollowAnEmptyStatement()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfItFollowSomeOtherStatmentEmptyStatement()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveCorrectUsageDoubleQuotes()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfItContainsExtraWhitespace()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfContainsLineContinuation()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void StrictmodeAUseStrictDirectiveFollowedByAStrictModeViolation()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-4gs.js", true);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfContainsAEscapesequence()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void StrictmodeAUseStrictDirectiveEmbeddedInADirectivePrologueFollowedByAStrictModeViolation()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-5gs.js", true);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfContainsATabInsteadOfASpace()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveNotRecognizedIfUpperCase()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveMayFollowOtherDirectives()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "14.1")]
        public void UseStrictDirectiveMayOccurMultipleTimes()
        {
			RunTest(@"TestCases/ch14/14.1/14.1-9-s.js", false);
        }


    }
}

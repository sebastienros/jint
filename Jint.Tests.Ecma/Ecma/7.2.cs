using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_7_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "7.2")]
        public void HorizontalTabU0009BetweenAnyTwoTokensIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void HorizontalTabU0009BetweenAnyTwoTokensIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void VerticalTabU000BBetweenAnyTwoTokensIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void VerticalTabU000BBetweenAnyTwoTokensIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void FormFeedU000CBetweenAnyTwoTokensIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void FormFeedU000CBetweenAnyTwoTokensIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SpaceU0020BetweenAnyTwoTokensIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SpaceU0020BetweenAnyTwoTokensIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void NoBreakSpaceU00A0BetweenAnyTwoTokensIsAllowed()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void NoBreakSpaceU00A0BetweenAnyTwoTokensIsAllowed2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A1.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void HorizontalTabU0009MayOccurWithinStrings()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void HorizontalTabU0009MayOccurWithinStrings2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void VerticalTabU000BMayOccurWithinStrings()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void VerticalTabU000BMayOccurWithinStrings2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void FormFeedU000CMayOccurWithinStrings()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void FormFeedU000CMayOccurWithinStrings2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SpaceU0020MayOccurWithinStrings()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SpaceU0020MayOccurWithinStrings2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void NoBreakSpaceU00A0MayOccurWithinStrings()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void NoBreakSpaceU00A0MayOccurWithinStrings2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A2.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainHorizontalTabU0009()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainHorizontalTabU00092()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainVerticalTabU000B()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainVerticalTabU000B2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainFormFeedU000C()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainFormFeedU000C2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainSpaceU0020()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainSpaceU00202()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainNoBreakSpaceU00A0()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void SingleLineCommentCanContainNoBreakSpaceU00A02()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A3.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainHorizontalTabU0009()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainHorizontalTabU00092()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainVerticalTabU000B()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainVerticalTabU000B2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainFormFeedU000C()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainFormFeedU000C2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainSpaceU0020()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainSpaceU00202()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainNoBreakSpaceU00A0()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void MultiLineCommentCanContainNoBreakSpaceU00A02()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A4.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void WhiteSpaceCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A5_T1.js", true);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void WhiteSpaceCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits2()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A5_T2.js", true);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void WhiteSpaceCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits3()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A5_T3.js", true);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void WhiteSpaceCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits4()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A5_T4.js", true);
        }

        [Fact]
        [Trait("Category", "7.2")]
        public void WhiteSpaceCannotBeExpressedAsAUnicodeEscapeSequenceConsistingOfSixCharactersNamelyUPlusFourHexadecimalDigits5()
        {
			RunTest(@"TestCases/ch07/7.2/S7.2_A5_T5.js", true);
        }


    }
}

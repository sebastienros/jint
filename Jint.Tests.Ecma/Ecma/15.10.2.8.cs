using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthPositiveLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustMatchAtTheCurrentPositionButTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequelIfDisjunctionCanMatchAtTheCurrentPositionInSeveralWaysOnlyTheFirstOneIsTried()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthPositiveLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustMatchAtTheCurrentPositionButTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequelIfDisjunctionCanMatchAtTheCurrentPositionInSeveralWaysOnlyTheFirstOneIsTried2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthPositiveLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustMatchAtTheCurrentPositionButTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequelIfDisjunctionCanMatchAtTheCurrentPositionInSeveralWaysOnlyTheFirstOneIsTried3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthPositiveLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustMatchAtTheCurrentPositionButTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequelIfDisjunctionCanMatchAtTheCurrentPositionInSeveralWaysOnlyTheFirstOneIsTried4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthPositiveLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustMatchAtTheCurrentPositionButTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequelIfDisjunctionCanMatchAtTheCurrentPositionInSeveralWaysOnlyTheFirstOneIsTried5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T5.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T6.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T7.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T8.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void TheFormDisjunctionSpecifiesAZeroWidthNegativeLookaheadInOrderForItToSucceedThePatternInsideDisjunctionMustFailToMatchAtTheCurrentPositionTheCurrentPositionIsNotAdvancedBeforeMatchingTheSequel11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T16.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T17.js", false);
        }

        [Fact(Skip = "Regular expression discrepancies between CLR and ECMAScript")]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T22.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T23.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T24.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction18()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T25.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction19()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T26.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction20()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T27.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction21()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T28.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction22()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T29.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction23()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction24()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T30.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction25()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T31.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction26()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T32.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction27()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T33.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction28()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction29()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction30()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction31()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction32()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void ParenthesesOfTheFormDisjunctionServeBothToGroupTheComponentsOfTheDisjunctionPatternTogetherAndToSaveTheResultOfTheMatchTheResultCanBeUsedEitherInABackreferenceFollowedByANonzeroDecimalNumberReferencedInAReplaceStringOrReturnedAsPartOfAnArrayFromTheRegularExpressionMatchingFunction33()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void TheProductionAtomEvaluatesAsFollowsILetABeTheSetOfAllCharactersExceptTheFourLineTerminatorCharactersLfCrLsOrPsIiCallCharactersetmatcherAFalseAndReturnItsMatcherResult9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A4_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void InCaseInsignificantMatchesAllCharactersAreImplicitlyConvertedToUpperCaseImmediatelyBeforeTheyAreCompared()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.2.8")]
        public void InCaseInsignificantMatchesAllCharactersAreImplicitlyConvertedToUpperCaseImmediatelyBeforeTheyAreCompared2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.8/S15.10.2.8_A5_T2.js", false);
        }


    }
}

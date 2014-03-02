using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void TheStringPrototypeSplitLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void TheLengthPropertyOfTheSplitMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms9()
        {
            RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T17.js", false);
        }
        
        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms17()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitICanBeTransferredToOtherKindsOfObjectsForUseAsAMethodSeparatorAndLimitCanBeAnyKindsOfObjectSinceIiIfSeparatorIsNotRegexpTostringSeparatorPerformsAndIiiTointegerLimitPerforms18()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T22.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T23.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject17()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T24.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject18()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T25.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject19()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T26.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject20()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T27.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject21()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T28.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject22()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T29.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject23()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject24()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T30.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject25()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T31.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject26()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T32.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject27()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T33.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject28()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T34.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject29()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T35.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject30()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T36.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject31()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T37.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject32()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T38.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject33()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T39.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject34()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject35()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T40.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject36()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T41.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject37()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T42.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject38()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T43.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject39()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject40()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject41()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject42()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredTheSubstringsAreDeterminedBySearchingFromLeftToRightForOccurrencesOfSeparatorTheseOccurrencesAreNotPartOfAnySubstringInTheReturnedArrayButServeToDivideUpTheStringValueTheValueOfSeparatorMayBeAStringOfAnyLengthOrItMayBeARegexpObject43()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitReturnsAnArrayObjectWithILengthEqualedTo1IiGet0EqualedToTheResultOfConvertingThisObjectToAString11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T22.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T23.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding17()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T24.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding18()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T25.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding19()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding20()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding21()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding22()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding23()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding24()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitSeparatorLimitReturnsAnArrayObjectIntoWhichSubstringsOfTheResultOfConvertingThisObjectToAStringHaveBeenStoredIfSeparatorIsARegularExpressionThenInsideOfSplitmatchHelperTheMatchMethodOfRIsCalledGivingItTheArgumentsCorresponding25()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A4_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void StringPrototypeSplitCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void TheStringPrototypeSplitLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.14")]
        public void TheStringPrototypeSplitLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.14/S15.5.4.14_A9.js", false);
        }


    }
}

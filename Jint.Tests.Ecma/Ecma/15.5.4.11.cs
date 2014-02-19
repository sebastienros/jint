using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ThisObjectUsedByTheReplacevalueFunctionOfAStringPrototypeReplaceInvocation()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/15.5.4.11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheStringPrototypeReplaceLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheLengthPropertyOfTheReplaceMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void CallReplacevaluePassingUndefinedAsTheThisValue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceSearchvalueReplacevalue16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheReplacementsAreDoneLeftToRightAndOnceSuchArePlacementIsPerformedTheNewReplacementTextIsNotSubjectToFurtherReplacements10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpUidDReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpUidDReturns2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpUidDReturns3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpAZ09AndReplaceFunctionReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpAZ09AndReplaceFunctionReturns2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpAZ09AndReplaceFunctionReturns3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void ReplaceWithRegexpAZ09AndReplaceFunctionReturns4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void UseReplaceWithRegexpAsSearchvalueAndUseInReplacevalue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void StringPrototypeReplaceCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheStringPrototypeReplaceLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.11")]
        public void TheStringPrototypeReplaceLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.11/S15.5.4.11_A9.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_6_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecTheRemovedStep9EWonTAffectedCurrentAlgorithm()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/15.10.6.2-9-e-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void TheRegexpPrototypeExecLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void TheLengthPropertyOfTheExecMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpExecActsLikeRegexpExecUndefinedStep2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch13()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T20.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch14()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T21.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch15()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch16()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch17()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T5.js", false);
        }

        [Fact(Skip = "Can't figure out why the results differ...")]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch18()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch19()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch20()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecStringPerformsARegularExpressionMatchOfTostringStringAgainstTheRegularExpressionAndReturnsAnArrayObjectContainingTheResultsOfTheMatchOrNullIfTheStringDidNotMatch21()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void ATypeerrorExceptionIsThrownIfTheThisValueIsNotAnObjectForWhichTheValueOfTheInternalClassPropertyIsRegexp10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueAndLastindexNotChangedManuallyNextExecCallingStartToMatchFromPositionWhereCurrentMatchFinished7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition6()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition7()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition8()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition9()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition10()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition11()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyIfGlobalIsTrueNextExecCallingStartToMatchFromLastindexPosition12()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A4_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyLetGlobalIsTrueAndLetIIfTointegerLastindexThenIfI0OriLengthThenSetLastindexTo0AndReturnNull()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyLetGlobalIsTrueAndLetIIfTointegerLastindexThenIfI0OriLengthThenSetLastindexTo0AndReturnNull2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecBehaviorDependsOnGlobalPropertyLetGlobalIsTrueAndLetIIfTointegerLastindexThenIfI0OriLengthThenSetLastindexTo0AndReturnNull3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void RegexpPrototypeExecCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void TheRegexpPrototypeExecLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.2")]
        public void TheRegexpPrototypeExecLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/15.10.6.2/S15.10.6.2_A9.js", false);
        }


    }
}

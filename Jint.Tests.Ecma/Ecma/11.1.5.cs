using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_1_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.1.5")]
        public void ItIsnTClearWhatSpecificRequirementsOfTheSpecificaitonAreBeingTestedHereThisTestShouldProbablyBeReplacedBySomeMoreTargetedTestsAllenwb()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void ItIsnTClearWhatSpecificRequirementsOfTheSpecificaitonAreBeingTestedHereThisTestShouldProbablyBeReplacedBySomeMoreTargetedTestsAllenwb2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenEvalOccursAsTheIdentifierInAPropertysetparameterlistOfAPropertyassignmentThatIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenEvalOccursAsTheIdentifierInAPropertysetparameterlistOfAPropertyassignmentThatIsContainedInStrictCode2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenArgumentsOccursAsTheIdentifierInAPropertysetparameterlistOfAPropertyassignmentThatIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenEvalCodeContainsAnObjectliteralWithMoreThanOneDefinitionOfAnyDataProperty()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenEvalsOccursAsTheIdentifierInAPropertysetparameterlistOfAPropertyassignmentIfItsFunctionbodyIsStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueAThisProductionIsContainedInStrictCodeAndIsdatadescriptorPreviousIsTrueAndIsdatadescriptorPropidDescriptorIsTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-4-4-a-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenArgumentsOccursAsTheIdentifierInAPropertysetparameterlistOfAPropertyassignmentIfItsFunctionbodyIsStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertyassignment3CallTheDefineownpropertyInternalMethodOfObjWithArgumentsPropidNamePropidDescriptorAndFalse()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_3-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueAThisProductionIsContainedInStrictCodeAndIsdatadescriptorPreviousIsTrueAndIsdatadescriptorPropidDescriptorIsTrue2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueAThisProductionIsContainedInStrictCodeAndIsdatadescriptorPreviousIsTrueAndIsdatadescriptorPropidDescriptorIsTrue3()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueBIsdatadescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueBIsdatadescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrue2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueCIsaccessordescriptorPreviousIsTrueAndIsdatadescriptorPropidDescriptorIsTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueCIsaccessordescriptorPreviousIsTrueAndIsdatadescriptorPropidDescriptorIsTrue2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-c-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueDIsaccessordescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrueAndEitherBothPreviousAndPropidDescriptorHaveGetFieldsOrBothPreviousAndPropidDescriptorHaveSetFields()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-d-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueDIsaccessordescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrueAndEitherBothPreviousAndPropidDescriptorHaveGetFieldsOrBothPreviousAndPropidDescriptorHaveSetFields2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-d-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueDIsaccessordescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrueAndEitherBothPreviousAndPropidDescriptorHaveGetFieldsOrBothPreviousAndPropidDescriptorHaveSetFields3()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-d-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment4IfPreviousIsNotUndefinedThenThrowASyntaxerrorExceptionIfAnyOfTheFollowingConditionsAreTrueDIsaccessordescriptorPreviousIsTrueAndIsaccessordescriptorPropidDescriptorIsTrueAndEitherBothPreviousAndPropidDescriptorHaveGetFieldsOrBothPreviousAndPropidDescriptorHaveSetFields4()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-4-d-4.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertynameandvaluelistPropertynameandvaluelistPropertyassignment5CallTheDefineownpropertyInternalMethodOfObjWithArgumentsPropidNamePropidDescriptorAndFalse()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_4-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertyassignmentPropertynameAssignmentexpression4LetDescBeThePropertyDescriptorValuePropvalueWritableTrueEnumerableTrueConfigurableTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_5-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenAnAssignmentToAReservedWordOrAFutureReservedWordIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_6-2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenAnAssignmentToAReservedWordOrAFutureReservedWordIsMadeInsideAStrictModeFunctionbodyOfAPropertyassignment()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_6-2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertyassignmentGetPropertynameFunctionbody3LetDescBeThePropertyDescriptorGetClosureEnumerableTrueConfigurableTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_6-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertyassignmentGetPropertynameFunctionbody3LetDescBeThePropertyDescriptorGetClosureEnumerableTrueConfigurableTrue2()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_6-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenAnAssignmentToAReservedWordIsContainedInStrictCode()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_7-2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void StrictModeSyntaxerrorIsThrownWhenAnAssignmentToAReservedWordIsMadeInAStrictFunctionbodyOfAPropertyassignment()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_7-2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertyassignmentSetPropertynamePropertysetparameterlistFunctionbody3LetDescBeThePropertyDescriptorSetClosureEnumerableTrueConfigurableTrue()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_7-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void Refer1115TheProductionPropertyassignmentGetPropertynameFunctionbody3LetDescBeThePropertyDescriptorGetClosureEnumerableTrueConfigurableTrue3()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/11.1.5_7-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteral()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteralNumericliteralAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteralStringliteralAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteralIdentifierAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteralPropertynameAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void EvaluateTheProductionObjectliteralPropertynameandvaluelist()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void ThePropertynameIsNotReallyABooleanliteral()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void ThePropertynameIsNotReallyANullliteral()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.5")]
        public void ThePropertynameIsUndefinedTostringBooleanliteralTostringNullliteral()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.5/S11.1.5_A4.3.js", false);
        }


    }
}

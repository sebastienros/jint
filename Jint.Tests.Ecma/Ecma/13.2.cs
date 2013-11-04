using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_13_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingOrReadingFromAPropertyNamedCallerOfFunctionObjectsIsAllowedUnderBothStrictAndNormalModes()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsOutsideOfTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsInsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void FunctionObjectHasLengthAsItsOwnPropertyAndDoesNotInvokeTheSetterDefinedOnFunctionPrototypeLengthStep15()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-15-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsOutsideOfTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsInsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void FunctionObjectHasConstructorAsItsOwnPropertyItIsNotEnumerableAndDoesNotInvokeTheSetterDefinedOnFunctionPrototypeConstructorStep17()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-17-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void FunctionObjectHasPrototypeAsItsOwnPropertyItIsNotEnumerableAndDoesNotInvokeTheSetterDefinedOnFunctionPrototypeStep18()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-18-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeErrorIsThrownWhenAssignAValueToTheCallerPropertyOfAFunctionObject()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-19-b-3gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsOutsideOfTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeATypeerrorIsThrownWhenAStrictModeCodeWritesToPropertiesNamedCallerOfFunctionInstances()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsInsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsOutsideOfTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsInsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedArgumentsOfFunctionObjectsIsNotAllowedOutsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsOutsideOfTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForArgumentsFailsInsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedCallerOfFunctionObjectsIsNotConfigurable()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingOrReadingFromAPropertyNamedArgumentsOfFunctionObjectsIsAllowedUnderBothStrictAndNormalModes()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedCallerOfFunctionObjectsIsNotConfigurable2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedCallerOfFunctionObjectsIsNotConfigurable3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedCallerOfFunctionObjectsIsNotConfigurable4()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedArgumentsOfFunctionObjectsIsNotConfigurable()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedArgumentsOfFunctionObjectsIsNotConfigurable2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-34-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedArgumentsOfFunctionObjectsIsNotConfigurable3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-35-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodePropertyNamedArgumentsOfFunctionObjectsIsNotConfigurable4()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-36-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeATypeerrorIsThrownWhenACodeInStrictModeTriesToWriteToArgumentsOfFunctionInstances()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeWritingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsOutsideOfTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeEnumeratingOverAFunctionObjectLookingForCallerFailsInsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void StrictmodeReadingAPropertyNamedCallerOfFunctionObjectsIsNotAllowedOutsideTheFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/13.2-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void APrototypePropertyIsAutomaticallyCreatedForEveryFunction()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void APrototypePropertyIsAutomaticallyCreatedForEveryFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void NestedFunctionAreAdmitted()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void NestedFunctionAreAdmitted2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void WhenFunctionObjectFIsConstructedTheLengthPropertyOfFIsSetToTheNumberOfFormalPropertiesSpecifiedInFormalparameterlist()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void WhenFunctionObjectFIsConstructedTheFollowingStepsFrom9To11TakePlace9CreateANewObjectAsWouldBeConstructedByTheExpressionNewObject10SetTheConstructorPropertyOfResult9ToFThisPropertyIsGivenAttributesDontenum11SetThePrototypePropertyOfFToResult9()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void WhenFunctionObjectFIsConstructedTheFollowingStepsFrom9To11TakePlace9CreateANewObjectAsWouldBeConstructedByTheExpressionNewObject10SetTheConstructorPropertyOfResult9ToFThisPropertyIsGivenAttributesDontenum11SetThePrototypePropertyOfFToResult92()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void WhenFunctionObjectFIsConstructedThePrototypePropertyOfFIsSetToTheOriginalFunctionPrototypeObjectAsSpecifiedIn15331()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A5.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfCallerPoisoningPoisonsGetownpropertydescriptorToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfArgumentsPoisoningPoisonsGetownpropertydescriptorToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfCallerPoisoningPoisonsHasownpropertyToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfArgumentsPoisoningPoisonsHasownpropertyToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfCallerPoisoningPoisonsInToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2")]
        public void CheckIfArgumentsPoisoningPoisonsInToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2_A8_T2.js", false);
        }


    }
}

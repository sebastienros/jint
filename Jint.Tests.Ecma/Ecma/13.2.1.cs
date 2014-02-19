using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.2.1")]
        public void TheDepthOfNestedFunctionCallsReaches32()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ObjectsAsArgumentsArePassedByReference()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ObjectsAsArgumentsArePassedByReference2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ObjectsAsArgumentsArePassedByReference3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ObjectsAsArgumentsArePassedByReference4()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ClosuresAreAdmitted()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void ClosuresAreAdmitted2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void PrimitiveTypesArePassedByValue()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void PrimitiveTypesArePassedByValue2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsReturnedThenResultValueIsReturnedToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsReturnedThenResultValueIsReturnedToo2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsReturnedThenResultValueIsReturnedToo3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsReturnedThenResultValueIsReturnedToo4()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsThrownThenResultValueIsThrownToo()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectFIsCalledTheFollowingStepsAreTaken2EvaluateFSFunctionbodyIfResultTypeIsThrownThenResultValueIsThrownToo2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectIsCalledTheBodyIsEvaluatedAndIfEvaluationResultHasTypeNormalThenUndefinedIsReturned()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A9.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectIsCalledTheBodyIsEvaluatedAndIfEvaluationResultHasTypeNormalThenUndefinedIsReturned2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A9.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectIsCalledTheBodyIsEvaluatedAndIfEvaluationResultHasTypeReturnItsValueIsNotDefinedThenUndefinedIsReturned()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.1")]
        public void WhenTheCallPropertyForAFunctionObjectIsCalledTheBodyIsEvaluatedAndIfEvaluationResultHasTypeReturnItsValueIsNotDefinedThenUndefinedIsReturned2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.1_A9_T2.js", false);
        }


    }
}

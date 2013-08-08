using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsPossibleAsLongAsThisAnyFunctionIsDeclared()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsPossibleAsLongAsThisAnyFunctionIsDeclaredAndCalled()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsPossibleAsLongAsThisAnyFunctionIsDeclaredAndCalled2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A12.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsInadmissibleAsLongAsThisAnyFunctionIsDeclaredByEvalAndCalled()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A13.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsInadmissibleAsLongAsThisAnyFunctionIsDeclaredByEvalAndCalled2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A14.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledAndTheObjectCreatedInTheFunctionIsReturnedTheObjectDeclaredWithThisWithinAFunctionWillBeStrongAndHealthy()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A15_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledAndTheObjectCreatedInTheFunctionIsReturnedTheObjectDeclaredWithThisWithinAFunctionWillBeStrongAndHealthy2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A15_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledAndTheObjectCreatedInTheFunctionIsReturnedTheObjectDeclaredWithThisWithinAFunctionWillBeStrongAndHealthy3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A15_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledAndTheObjectCreatedInTheFunctionIsReturnedTheObjectDeclaredWithThisWithinAFunctionWillBeStrongAndHealthy4()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A15_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionexpressionWithinANewStatementIsAdmitted()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A16_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionexpressionWithinANewStatementIsAdmitted2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A16_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionexpressionWithinANewStatementIsAdmitted3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A16_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionexpressionContainingWithStatementIsAdmitted()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A17_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionexpressionContainingWithStatementIsAdmitted2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A17_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void UsingArgumentsObjectWithinAWithExpressionThatIsNestedInAFunctionIsAdmitted()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A18_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void UsingArgumentsObjectWithinAWithExpressionThatIsNestedInAFunctionIsAdmitted2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A18_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared4()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T4.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared5()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T5.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared6()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T6.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared7()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T7.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void FunctionSScopeChainIsStartedWhenItIsDeclared8()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A19_T8.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void SinceAFunctionIsAnObjectItMightBeSetToPrototypePropertyOfANewCreatedObjectThroughConstructProperty()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void SinceAFunctionIsAnObjectItMightBeSetToPrototypePropertyOfANewCreatedObjectThroughConstructProperty2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void SinceAFunctionIsAnObjectItMightBeSetToPrototypePropertyOfANewCreatedObjectThroughConstructPropertyButCallPropertyMustFailWithTypeerrorError()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedItGetsTheValueOfThePrototypePropertyOfTheFDenoteItProtoValIfProtoValIsNotAnObjectSetsThePrototypePropertyOfNativeEcmascriptObjectJustCreatedToTheOriginalObjectPrototypeObjectAsDescribedIn15231()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedItGetsTheValueOfThePrototypePropertyOfTheFDenoteItProtoValIfProtoValIsNotAnObjectSetsThePrototypePropertyOfNativeEcmascriptObjectJustCreatedToTheOriginalObjectPrototypeObjectAsDescribedIn152312()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedGetsTheValueOfThePrototypePropertyOfTheFDenoteItProtoValIfProtoValIsAnObjectSetsThePrototypePropertyOfNativeEcmascriptObjectJustCreatedToTheProtoVal()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedGetsTheValueOfThePrototypePropertyOfTheFDenoteItProtoValIfProtoValIsAnObjectSetsThePrototypePropertyOfNativeEcmascriptObjectJustCreatedToTheProtoVal2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingNativeEcmascriptObjectJustCreatedAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValues()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingNativeEcmascriptObjectJustCreatedAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValues2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsNotObjectThenReturnPassedAsThisIntoCallObject()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsNotObjectThenReturnPassedAsThisIntoCallObject2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsAnObjectThenReturnThisJustAsObtainedObject()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsAnObjectThenReturnThisJustAsObtainedObject2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsAnFunctionThenReturnThisJustAsObtainedFunction()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsAnFunctionThenReturnThisJustAsObtainedFunction2()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void WhenTheConstructPropertyForAFunctionObjectFIsCalledANewNativeEcmascriptObjectIsCreatedInvokeTheCallPropertyOfFProvidingJustCreatedNativeEcmascriptObjectAsTheThisValueAndProvidingTheArgumentListPassedIntoConstructAsTheArgumentValuesIfTypeCallReturnedIsAnFunctionThenReturnThisJustAsObtainedFunction3()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "13.2.2")]
        public void CallingAFunctionAsAConstructorIsInadmissibleAsLongAsThisAnyFunctionIsCalledBeforeItIsDeclared()
        {
			RunTest(@"TestCases/ch13/13.2/S13.2.2_A9.js", false);
        }


    }
}

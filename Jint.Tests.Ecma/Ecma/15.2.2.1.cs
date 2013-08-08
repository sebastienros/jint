using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithNoArgumentsTheFollowingStepsAreTakenTheArgumentValueWasNotSuppliedOrItsTypeWasNullOrUndefinedICreateANewNativeEcmascriptObjectIiThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheObjectPrototypeObjectIiiTheClassPropertyOfTheNewlyConstructedObjectIsSetToObjectIvTheNewlyConstructedObjectHasNoValuePropertyVReturnTheNewlyCreatedNativeObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithNoArgumentsTheFollowingStepsAreTakenTheArgumentValueWasNotSuppliedOrItsTypeWasNullOrUndefinedICreateANewNativeEcmascriptObjectIiThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheObjectPrototypeObjectIiiTheClassPropertyOfTheNewlyConstructedObjectIsSetToObjectIvTheNewlyConstructedObjectHasNoValuePropertyVReturnTheNewlyCreatedNativeObject2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithNoArgumentsTheFollowingStepsAreTakenTheArgumentValueWasNotSuppliedOrItsTypeWasNullOrUndefinedICreateANewNativeEcmascriptObjectIiThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheObjectPrototypeObjectIiiTheClassPropertyOfTheNewlyConstructedObjectIsSetToObjectIvTheNewlyConstructedObjectHasNoValuePropertyVReturnTheNewlyCreatedNativeObject3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithNoArgumentsTheFollowingStepsAreTakenTheArgumentValueWasNotSuppliedOrItsTypeWasNullOrUndefinedICreateANewNativeEcmascriptObjectIiThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheObjectPrototypeObjectIiiTheClassPropertyOfTheNewlyConstructedObjectIsSetToObjectIvTheNewlyConstructedObjectHasNoValuePropertyVReturnTheNewlyCreatedNativeObject4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithNoArgumentsTheFollowingStepsAreTakenTheArgumentValueWasNotSuppliedOrItsTypeWasNullOrUndefinedICreateANewNativeEcmascriptObjectIiThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheObjectPrototypeObjectIiiTheClassPropertyOfTheNewlyConstructedObjectIsSetToObjectIvTheNewlyConstructedObjectHasNoValuePropertyVReturnTheNewlyCreatedNativeObject5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheValueIsANativeEcmascriptObjectDoNotCreateANewObjectButSimplyReturnValue7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsStringReturnToobjectString()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsStringReturnToobjectString2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsStringReturnToobjectString3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsBooleanReturnToobjectBoolean()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsBooleanReturnToobjectBoolean2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsBooleanReturnToobjectBoolean3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsNumberReturnToobjectNumber()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsNumberReturnToobjectNumber2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsNumberReturnToobjectNumber3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void WhenTheObjectConstructorIsCalledWithOneArgumentValueAndTheTypeOfValueIsNumberReturnToobjectNumber4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.2.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.2/S15.2.2.1_A6_T3.js", false);
        }


    }
}

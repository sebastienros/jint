using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_5_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVTheFollowingStepsAreTakenIIfVIsNotAnObjectReturnFalse8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVAndVIsAnObjectTheFollowingStepsAreTakenICallTheGetMethodOfFWithPropertyNamePrototypeIiLetOBeResultIIiiOIsNotAnObjectThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVAndVIsAnObjectTheFollowingStepsAreTakenICallTheGetMethodOfFWithPropertyNamePrototypeIiLetOBeResultIIiiOIsNotAnObjectThrowATypeerrorException2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVAndVIsAnObjectTheFollowingStepsAreTakenICallTheGetMethodOfFWithPropertyNamePrototypeIiLetOBeResultIIiiOIsNotAnObjectThrowATypeerrorException3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVAndVIsAnObjectTheFollowingStepsAreTakenICallTheGetMethodOfFWithPropertyNamePrototypeIiLetOBeResultIAndOIsAnObjectIiiLetVBeTheValueOfThePrototypePropertyOfVIvIfVIsNullReturnFalseVIfOAndVReferToTheSameObjectOrIfTheyReferToObjectsJoinedToEachOther1312ReturnTrueViGoToStepIii()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.3")]
        public void AssumeFIsAFunctionObjectWhenTheHasinstanceMethodOfFIsCalledWithValueVAndVIsAnObjectTheFollowingStepsAreTakenICallTheGetMethodOfFWithPropertyNamePrototypeIiLetOBeResultIAndOIsAnObjectIiiLetVBeTheValueOfThePrototypePropertyOfVIvIfVIsNullReturnFalseVIfOAndVReferToTheSameObjectOrIfTheyReferToObjectsJoinedToEachOther1312ReturnTrueViGoToStepIii2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.3_A3_T2.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void ThisDescriptionOfArrayConstructorAppliesIfAndOnlyIfTheArrayConstructorIsGivenNoArgumentsOrAtLeastTwoArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void TheLengthPropertyOfTheNewlyConstructedObjectIsSetToTheNumberOfArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.1")]
        public void The0PropertyOfTheNewlyConstructedObjectIsSetToItem0IfSuppliedThe1PropertyOfTheNewlyConstructedObjectIsSetToItem1IfSuppliedAndInGeneralForAsManyArgumentsAsThereAreTheKPropertyOfTheNewlyConstructedObjectIsSetToArgumentKWhereTheFirstArgumentIsConsideredToBeArgumentNumber0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.1/S15.4.2.1_A2.2_T1.js", false);
        }


    }
}

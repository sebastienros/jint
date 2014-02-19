using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.2.1")]
        public void WhenBooleanIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.2/S15.6.2.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalBooleanPrototypeObjectTheOneThatIsTheInitialValueOfBooleanPrototype()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.2/S15.6.2.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.2.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetToTobooleanValue()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.2/S15.6.2.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.2.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToBoolean()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.2/S15.6.2.1_A4.js", false);
        }


    }
}

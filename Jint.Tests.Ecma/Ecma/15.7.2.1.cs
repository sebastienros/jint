using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.2.1")]
        public void WhenNumberIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.2/S15.7.2.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalNumberPrototypeObjectTheOneThatIsTheInitialValueOfNumberPrototype()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.2/S15.7.2.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.2.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetToTonumberValueIfValueWasSuppliedElseTo0()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.2/S15.7.2.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.2.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToNumber()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.2/S15.7.2.1_A4.js", false);
        }


    }
}

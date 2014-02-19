using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.3.2")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObject()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.2")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.2")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.2_A3_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.2")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.2_A3_T1.2.js", false);
        }


    }
}

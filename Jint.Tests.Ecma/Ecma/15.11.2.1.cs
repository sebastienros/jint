using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.2.1")]
        public void IfTheArgumentMessageIsNotUndefinedTheMessagePropertyOfTheNewlyConstructedObjectIsSetToTostringMessage()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.2/S15.11.2.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalErrorPrototypeObjectTheOneThatIsTheInitialValueOfErrorPrototype151131()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.2/S15.11.2.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.2.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToError()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.2/S15.11.2.1_A3_T1.js", false);
        }


    }
}

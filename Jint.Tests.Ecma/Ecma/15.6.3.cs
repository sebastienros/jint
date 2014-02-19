using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.3")]
        public void TheBooleanConstructorHasThePropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/S15.6.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheBooleanConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/S15.6.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.3")]
        public void BooleanConstructorHasLengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.3/S15.6.3_A3.js", false);
        }


    }
}

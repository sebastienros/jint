using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3")]
        public void TheObjectConstructorHasThePropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/S15.2.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheObjectConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/S15.2.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3")]
        public void ObjectConstructorHasLengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/S15.2.3_A3.js", false);
        }


    }
}

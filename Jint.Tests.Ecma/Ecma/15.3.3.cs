using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.3")]
        public void TheFunctionConstructorHasThePropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/S15.3.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheFunctionConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/S15.3.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheFunctionConstructorIsTheFunctionPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/S15.3.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3")]
        public void FunctionConstructorHasLengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/S15.3.3_A3.js", false);
        }


    }
}

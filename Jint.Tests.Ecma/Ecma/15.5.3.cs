using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.3")]
        public void StringHasLengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheStringConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheStringConstructorIsTheFunctionPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3_A2_T2.js", false);
        }


    }
}

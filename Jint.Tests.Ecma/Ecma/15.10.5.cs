using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.5")]
        public void RegexpConstructorHasLengthPropertyWhoseValueIs2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.5")]
        public void TheValueOfTheInternalPrototypePropertyOfTheRegexpConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.5")]
        public void TheValueOfTheInternalPrototypePropertyOfTheRegexpConstructorIsTheFunctionPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.5/S15.10.5_A2_T2.js", false);
        }


    }
}

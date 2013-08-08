using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheErrorConstructorIsTheFunctionPrototypeObject1534()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.3")]
        public void TheLengthPropertyValueIs1()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.3/S15.11.3_A2_T1.js", false);
        }


    }
}

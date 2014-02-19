using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.4.1")]
        public void TheInitialValueOfErrorPrototypeConstructorIsTheBuiltInErrorConstructor()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.1")]
        public void TheInitialValueOfErrorPrototypeConstructorIsTheBuiltInErrorConstructor2()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.1_A1_T2.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4.1")]
        public void TheInitialValueOfObjectPrototypeConstructorIsTheBuiltInObjectConstructor()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4.1")]
        public void TheInitialValueOfObjectPrototypeConstructorIsTheBuiltInObjectConstructor2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4.1_A1_T2.js", false);
        }


    }
}

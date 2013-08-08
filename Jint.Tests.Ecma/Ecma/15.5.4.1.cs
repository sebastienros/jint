using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.1")]
        public void TheInitialValueOfStringPrototypeConstructorIsTheBuiltInStringConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.1")]
        public void TheInitialValueOfStringPrototypeConstructorIsTheBuiltInStringConstructor2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4.1_A1_T2.js", false);
        }


    }
}

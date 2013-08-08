using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.4.1")]
        public void TheInitialValueOfNumberPrototypeConstructorIsTheBuiltInNumberConstructor()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.1/S15.7.4.1_A1.js", false);
        }


    }
}

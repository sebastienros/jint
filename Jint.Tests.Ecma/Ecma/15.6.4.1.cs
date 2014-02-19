using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.4.1")]
        public void TheInitialValueOfBooleanPrototypeConstructorIsTheBuiltInBooleanConstructor()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4.1_A1.js", false);
        }


    }
}

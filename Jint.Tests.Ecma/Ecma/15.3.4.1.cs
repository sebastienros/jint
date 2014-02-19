using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.1")]
        public void TheInitialValueOfFunctionPrototypeConstructorIsTheBuiltInFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4.1_A1_T1.js", false);
        }


    }
}

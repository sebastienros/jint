using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.1")]
        public void TheInitialValueOfArrayPrototypeConstructorIsTheBuiltInArrayConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.1/S15.4.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.1")]
        public void TheConstructorPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.1/S15.4.4.1_A2.js", false);
        }


    }
}

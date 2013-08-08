using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_6_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.6.1")]
        public void TheInitialValueOfRegexpPrototypeConstructorIsTheBuiltInRegexpConstructor()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/S15.10.6.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.6.1")]
        public void TheInitialValueOfRegexpPrototypeConstructorIsTheBuiltInRegexpConstructor2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.6/S15.10.6.1_A1_T2.js", false);
        }


    }
}

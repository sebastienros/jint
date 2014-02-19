using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.14")]
        public void MathRandomReturnsANumberValueWithPositiveSignGreaterThanOrEqualTo0ButLessThan1()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.14/S15.8.2.14_A1.js", false);
        }


    }
}

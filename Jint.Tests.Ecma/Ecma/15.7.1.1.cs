using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.1.1")]
        public void NumberValueReturnsANumberValueNotANumberObjectComputedByTonumberValueIfValueWasSupplied()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.1/S15.7.1.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.1.1")]
        public void NumberReturns0()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.1/S15.7.1.1_A2.js", false);
        }


    }
}

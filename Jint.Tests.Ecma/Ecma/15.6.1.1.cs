using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void ReturnsABooleanValueNotABooleanObjectComputedByTobooleanValue()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void ReturnsABooleanValueNotABooleanObjectComputedByTobooleanValue2()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void ReturnsABooleanValueNotABooleanObjectComputedByTobooleanValue3()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void ReturnsABooleanValueNotABooleanObjectComputedByTobooleanValue4()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void ReturnsABooleanValueNotABooleanObjectComputedByTobooleanValue5()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.1.1")]
        public void BooleanReturnsFalse()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.1/S15.6.1.1_A2.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
        public class Test_9_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.4")]
        public void IfTonumberValueIsNanTointegerValueReturns0()
        {
			RunTest(@"TestCases/ch09/9.4/S9.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.4")]
        public void IfTonumberValueIs00InfinityOrInfinityReturnTonumberValue()
        {
			RunTest(@"TestCases/ch09/9.4/S9.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "9.4")]
        public void ResultOfTointegerValueConversionIsTheResultOfComputingSignTonumberValueFloorAbsTonumberValue()
        {
			RunTest(@"TestCases/ch09/9.4/S9.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.4")]
        public void ResultOfTointegerValueConversionIsTheResultOfComputingSignTonumberValueFloorAbsTonumberValue2()
        {
			RunTest(@"TestCases/ch09/9.4/S9.4_A3_T2.js", false);
        }


    }
}

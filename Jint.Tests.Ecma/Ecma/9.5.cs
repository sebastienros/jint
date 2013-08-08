using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.5")]
        public void IfValueIsNan00InfinityOrInfinityReturn0()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void Toint32ReturnsValuesBetween231And2311()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void Toint32ReturnsValuesBetween231And23112()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void ComputeResultModulo232()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void ComputeResultModulo2322()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void IfResultIsGreaterThanOrEqualTo231ReturnResult232()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void IfResultIsGreaterThanOrEqualTo231ReturnResult2322()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A2.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesTonumber()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesTonumber2()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesTonumber3()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesTonumber4()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesFloorAbs()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.5")]
        public void OperatorUsesFloorAbs2()
        {
			RunTest(@"TestCases/ch09/9.5/S9.5_A3.2_T2.js", false);
        }


    }
}

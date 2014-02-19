using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.6")]
        public void IfValueIsNan00InfinityOrInfinityReturn0()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void Touint32ReturnsValuesBetween0And2321()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void ComputeResultModulo232()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void OperatorUsesTonumber()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void OperatorUsesTonumber2()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void OperatorUsesTonumber3()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void OperatorUsesTonumber4()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "9.6")]
        public void OperatorUsesFloorAbs()
        {
			RunTest(@"TestCases/ch09/9.6/S9.6_A3.2_T1.js", false);
        }


    }
}

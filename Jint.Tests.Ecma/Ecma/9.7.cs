using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.7")]
        public void IfValueIsNan00InfinityOrInfinityReturn0()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void Touint16ReturnsValuesBetween0And2161()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void ComputeResultModulo216()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void OperatorUsesTonumber()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void OperatorUsesTonumber2()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void OperatorUsesTonumber3()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void OperatorUsesTonumber4()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "9.7")]
        public void OperatorUsesFloorAbs()
        {
			RunTest(@"TestCases/ch09/9.7/S9.7_A3.2_T1.js", false);
        }


    }
}

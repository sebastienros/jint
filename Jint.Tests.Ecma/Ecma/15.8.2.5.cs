using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfEitherXOrYIsNanMathXYIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIs0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualTo0AndX0MathAtan2YXIsAnImplementationDependentApproximationToPi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi22()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A13.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndYIsFiniteAndXIsEqualToInfinityMathAtan2YXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A14.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndYIsFiniteAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationToPi()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A15.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndYIsFiniteAndXIsEqualToInfinityMathAtan2YXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndYIsFiniteAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A17.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsInfinityAndXIsFiniteMathAtan2YXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A18.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsInfinityAndXIsFiniteMathAtan2YXIsAnImplementationDependentApproximationToPi22()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A19.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi23()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualToInfinityAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationToPi4()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A20.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualToInfinityAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationTo3Pi4()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A21.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualToInfinityAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationToPi42()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A22.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualToInfinityAndXIsEqualToInfinityMathAtan2YXIsAnImplementationDependentApproximationTo3Pi42()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A23.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void MathAtan2RecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A24.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfY0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi24()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIs0AndX0MathAtan2YXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIs0AndXIs0MathAtan2YXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIs0AndXIs0MathAtan2YXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualTo0AndX0MathAtan2YXIsAnImplementationDependentApproximationToPi2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIsEqualTo0AndX0MathAtan2YXIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.5")]
        public void IfYIs0AndXIs0MathAtan2YXIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.5/S15.8.2.5_A9.js", false);
        }


    }
}

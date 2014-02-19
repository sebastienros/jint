using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_2_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfYIsNanMathPowXYIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0MathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0MathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0AndYIsAnOddIntegerMathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A13.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0AndYIsNotAnOddIntegerMathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A14.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0AndYIsAnOddIntegerMathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A15.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsInfinityAndY0AndYIsNotAnOddIntegerMathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0MathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A17.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0MathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A18.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0AndYIsAnOddIntegerMathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A19.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfYIs0MathPowXYIs1EvenIfXIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0AndYIsNotAnOddIntegerMathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A20.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0AndYIsAnOddIntegerMathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A21.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIs0AndY0AndYIsNotAnOddIntegerMathPowXYIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A22.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfX0AndXIsFiniteAndYIsFiniteAndYIsNotAnIntegerMathPowXYIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A23.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void MathPowRecommendedThatImplementationsUseTheApproximationAlgorithmsForIeee754ArithmeticContainedInFdlibm()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A24.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfYIs0MathPowXYIs1EvenIfXIsNan2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfXIsNanAndYIsNonzeroMathPowXYIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIs0()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIsNan()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIsNan2()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.2.13")]
        public void IfAbsX1AndYIsInfinityMathPowXYIs02()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.2/15.8.2.13/S15.8.2.13_A9.js", false);
        }


    }
}

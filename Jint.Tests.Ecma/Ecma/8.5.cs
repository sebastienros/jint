using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.5")]
        public void NanNan()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityIsNotAKeyword()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A10.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void TheInteger0HasTwoRepresentations0And0()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void TheInteger0HasTwoRepresentations0And02()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityAndInfinityAreTheSameAsNumberPositiveInfinity()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A12.1.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityIsTheSameAsNumberNegativeInfinity()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A12.2.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void FiniteNonzeroValuesThatAreNormalisedHavingTheFormSM2EWhereSIs1Or1MIsAPositiveIntegerLessThan253ButNotLessThanS52AndEIsAnIntegerRangingFrom1074To971()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A13_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void WhenNumberAbsoluteValueIsBiggerOf21024ShouldConvertToInfinity()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A14_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void WhenNumberAbsoluteValueIsBiggerOf21024ShouldConvertToInfinity2()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A14_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void NumberTypeRepresentedAsTheDoublePrecision64BitFormatIeee754()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void NumberTypeRepresentedAsTheExtendedPrecision64BitFormatIeee754()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void NanExpressionHasATypeNumber()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void NanIsNotAKeyword()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A4.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void NanNotGreaterOrEqualZero()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A5.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityExpressionHasATypeNumber()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A6.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityExpressionHasATypeNumber2()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void InfinityIsTheSameAsInfinity()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A8.js", false);
        }

        [Fact]
        [Trait("Category", "8.5")]
        public void GloballyDefinedVariableNanHasNotBeenAlteredByProgramExecution()
        {
			RunTest(@"TestCases/ch08/8.5/S8.5_A9.js", false);
        }


    }
}

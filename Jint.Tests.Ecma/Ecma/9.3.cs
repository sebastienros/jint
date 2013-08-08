using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromUndefinedValueIsNan()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromUndefinedValueIsNan2()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNullValueIs0()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNullValueIs02()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromBooleanValueIs1IfTheArgumentIsTrueElseIs0()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromBooleanValueIs1IfTheArgumentIsTrueElseIs02()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNumberValueEqualsToTheInputArgumentNoConversion()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A4.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNumberValueEqualsToTheInputArgumentNoConversion2()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A4.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNumberValueEqualsToTheInputArgumentNoConversion3()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A4.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromNumberValueEqualsToTheInputArgumentNoConversion4()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A4.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromObjectValueIsTheResultOfConversionFromPrimitiveValue()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.3")]
        public void ResultOfNumberConversionFromObjectValueIsTheResultOfConversionFromPrimitiveValue2()
        {
			RunTest(@"TestCases/ch09/9.3/S9.3_A5_T2.js", false);
        }


    }
}

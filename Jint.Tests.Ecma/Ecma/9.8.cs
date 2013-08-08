using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_8 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromUndefinedValueIsUndefined()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromUndefinedValueIsUndefined2()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromNullValueIsNull()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromNullValueIsNull2()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromBooleanValueIsTrueIfTheArgumentIsTrueElseIsFalse()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfTostringConversionFromBooleanValueIsTrueIfTheArgumentIsTrueElseIsFalse2()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfStringConversionFromStringValueIsTheInputArgumentNoConversion()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfStringConversionFromStringValueIsTheInputArgumentNoConversion2()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfStringConversionFromObjectValueIsConversionFromPrimitiveValue()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.8")]
        public void ResultOfStringConversionFromObjectValueIsConversionFromPrimitiveValue2()
        {
			RunTest(@"TestCases/ch09/9.8/S9.8_A5_T2.js", false);
        }


    }
}

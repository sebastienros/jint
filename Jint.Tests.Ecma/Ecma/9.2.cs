using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromUndefinedValueIsFalse()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromUndefinedValueIsFalse2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNullValueIsFalse()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNullValueIsFalse2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromBooleanValueIsNoConversion()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromBooleanValueIsNoConversion2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNumberValueIsFalseIfTheArgumentIs00OrNanOtherwiseIsTrue()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNumberValueIsFalseIfTheArgumentIs00OrNanOtherwiseIsTrue2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNumberValueIsFalseIfTheArgumentIs00OrNanOtherwiseIsTrue3()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNumberValueIsFalseIfTheArgumentIs00OrNanOtherwiseIsTrue4()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNonemptyStringValueLengthIsNotZeroIsTrueFromEmptyStringLengthIsZeroIsFalse()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNonemptyStringValueLengthIsNotZeroIsTrueFromEmptyStringLengthIsZeroIsFalse2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNonemptyStringValueLengthIsNotZeroIsTrueFromEmptyStringLengthIsZeroIsFalse3()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromNonemptyStringValueLengthIsNotZeroIsTrueFromEmptyStringLengthIsZeroIsFalse4()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromObjectIsTrue()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "9.2")]
        public void ResultOfBooleanConversionFromObjectIsTrue2()
        {
			RunTest(@"TestCases/ch09/9.2/S9.2_A6_T2.js", false);
        }


    }
}

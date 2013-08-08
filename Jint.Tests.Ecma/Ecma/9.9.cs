using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_9_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromUndefinedValueMustThrowTypeerror()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A1.js", false);
        }

        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromNullValueMustThrowTypeerror()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A2.js", false);
        }

        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromBooleanCreateANewBooleanObjectWhoseValuePropertyIsSetToTheValueOfTheBoolean()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A3.js", false);
        }

        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromNumberCreateANewNumberObjectWhoseValuePropertyIsSetToTheValueOfTheNumber()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A4.js", false);
        }

        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromStringCreateANewStringObjectWhoseValuePropertyIsSetToTheValueOfTheString()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A5.js", false);
        }

        [Fact]
        [Trait("Category", "9.9")]
        public void ToobjectConversionFromObjectTheResultIsTheInputArgumentNoConversion()
        {
			RunTest(@"TestCases/ch09/9.9/S9.9_A6.js", false);
        }


    }
}

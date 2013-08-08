using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.3")]
        public void WhiteSpaceAndLineTerminatorBetweenTypeofAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void OperatorTypeofUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void OperatorTypeofUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfApplyingTypeofOperatorToUndefinedIsUndefined()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfApplyingTypeofOperatorToNullIsObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfApplyingTypeofOperatorToBooleanIsBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfAppyingTypeofOperatorToNumberIsNumber()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfAppyingTypeofOperatorToStringIsString()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfApplyingTypeofOperatorToTheObjectThatIsNativeAndDoesnTImplementCallIsObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.3")]
        public void ResultOfApplyingTypeofOperatorToTheObjectThatIsNativeAndImplementsCallIsFunction()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.3/S11.4.3_A3.7.js", false);
        }


    }
}

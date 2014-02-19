using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.2.1")]
        public void WhenDateIsCalledAsAFunctionRatherThanAsAConstructorItShouldBeStringRepresentingTheCurrentTimeUtc()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.2/S15.9.2.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.2.1")]
        public void AllOfTheArgumentsAreOptionalAnyArgumentsSuppliedAreAcceptedButAreCompletelyIgnoredAStringIsCreatedAndReturnedAsIfByTheExpressionNewDateTostring()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.2/S15.9.2.1_A2.js", false);
        }


    }
}

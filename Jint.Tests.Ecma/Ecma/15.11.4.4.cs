using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void TheErrorPrototypeHasTostringProperty()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void TheErrorPrototypeTostringReturnsAnImplementationDefinedString()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringReturnTheResultOfConcatenatingNameASingleSpaceCharacterAndMsgWhenNameAndMsgAreNonEmptyString()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringErrorIsReturnedWhenNameIsAbsentAndEmptyStringIsReturnedWhenMsgIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringErrorIsReturnedWhenNameIsAbsentAndValueOfMsgIsReturnedWhenMsgIsNonEmptyString()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringReturnTheValueOfMsgWhenNameIsEmptyStringAndMsgIsnTUndefined()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringReturnEmptyStringWhenNameIsEmptyStringAndMsgIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4.4")]
        public void ErrorPrototypeTostringReturnNameWhenNameIsNonEmptyStringAndMsgIsEmptyString()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/15.11.4.4/15.11.4.4-9-1.js", false);
        }


    }
}

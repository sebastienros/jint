using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.5.1")]
        public void LengthPropertyContainsTheNumberOfCharactersInTheStringValueRepresentedByThisStringObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5.1")]
        public void LengthPropertyHasTheAttributesDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5.1")]
        public void LengthPropertyHasTheAttributesDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5.1")]
        public void LengthPropertyHasTheAttributesReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5.1_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5.1")]
        public void OnceAStringObjectIsCreatedTheLengthPropertyIsUnchanging()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5.1_A5.js", false);
        }


    }
}

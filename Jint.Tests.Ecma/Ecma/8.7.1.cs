using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_7_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.7.1")]
        public void DeleteOperatorDeletesPurePropertySoItReturnsTrueToBeApplyedToThisProperty()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.1_A1.js", false);
        }

        [Fact(Skip = "Doesn't work in Chrome either")]
        [Trait("Category", "8.7.1")]
        public void DeleteOperatorCanTDeleteReferenceSoItReturnsFalseToBeApplyedToReference()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.1_A2.js", false);
        }


    }
}

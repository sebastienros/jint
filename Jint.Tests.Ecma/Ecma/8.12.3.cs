using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.3")]
        public void GetPMethodShouldReturnValueWhenPropertyPDoesNotExistInInstanceButPrototypeContainIt()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.3/S8.12.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.3")]
        public void GetPMethodShouldReturnUndefinedWhenPropertyPDoesNotExistBothInInstanceAndPrototype()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.3/S8.12.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.3")]
        public void WhenTheGetMethodOfOIsCalledWithPropertyNamePValueOfPReturns()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.3/S8.12.3_A3.js", false);
        }


    }
}

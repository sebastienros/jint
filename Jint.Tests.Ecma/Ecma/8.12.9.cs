using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.9")]
        public void RedefineAConfigurableDataPropertyToBeAnAccessorPropertyOnANewlyNonExtensibleObject()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.9/8.12.9-9-b-i_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.9")]
        public void RedefineAConfigurableDataPropertyToBeAnAccessorPropertyOnANewlyNonExtensibleObject2()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.9/8.12.9-9-b-i_2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.9")]
        public void RedefineAConfigurableAccessorPropertyToBeADataPropertyOnANonExtensibleObject()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.9/8.12.9-9-c-i_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.9")]
        public void RedefineAConfigurableAccessorPropertyToBeADataPropertyOnANonExtensibleObject2()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.9/8.12.9-9-c-i_2.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7.2")]
        public void RegexpPrototypeGlobalIsOfTypeBoolean()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.2/15.10.7.2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.2")]
        public void RegexpPrototypeGlobalIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.2/15.10.7.2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.2")]
        public void TheRegexpInstanceGlobalPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.2/S15.10.7.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.2")]
        public void TheRegexpInstanceGlobalPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.2/S15.10.7.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.2")]
        public void TheRegexpInstanceGlobalPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.2/S15.10.7.2_A9.js", false);
        }


    }
}

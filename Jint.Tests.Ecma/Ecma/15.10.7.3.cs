using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7.3")]
        public void RegexpPrototypeIgnorecaseIsOfTypeBoolean()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.3/15.10.7.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.3")]
        public void RegexpPrototypeIgnorecaseIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.3/15.10.7.3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.3")]
        public void TheRegexpInstanceIgnorecasePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.3/S15.10.7.3_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.3")]
        public void TheRegexpInstanceIgnorecasePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.3/S15.10.7.3_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7.3")]
        public void TheRegexpInstanceIgnorecasePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/15.10.7.3/S15.10.7.3_A9.js", false);
        }


    }
}

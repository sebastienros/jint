using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.3.1")]
        public void ObjectPrototypeIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.1/15.2.3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.1")]
        public void TheObjectPrototypePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.1/S15.2.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.1")]
        public void TheObjectPrototypePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.1/S15.2.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.3.1")]
        public void CheckingIfDeletingObjectPrototypePropertyFails()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.3/15.2.3.1/S15.2.3.1_A3.js", false);
        }


    }
}

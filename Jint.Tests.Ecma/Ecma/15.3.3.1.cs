using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.3.1")]
        public void TheFunctionPrototypePropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/15.3.3.1/S15.3.3.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3.1")]
        public void TheFunctionPrototypePropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/15.3.3.1/S15.3.3.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3.1")]
        public void TheFunctionPrototypePropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/15.3.3.1/S15.3.3.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.3.1")]
        public void DetectsWhetherTheValueOfAFunctionSPrototypePropertyAsSeenByNormalObjectOperationsMightDeviateFromTheValueAsSeemByObjectGetownpropertydescriptor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/15.3.3.1/S15.3.3.1_A4.js", false);
        }


    }
}

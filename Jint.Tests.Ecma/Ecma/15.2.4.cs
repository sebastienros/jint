using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.4")]
        public void ObjectPrototypeObjectHasNotPrototype()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4")]
        public void ObjectPrototypeObjectHasNotPrototype2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4")]
        public void TheValueOfTheInternalClassPropertyOfObjectPrototypeObjectIsObject()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4")]
        public void SinceTheObjectPrototypeObjectIsNotAFunctionItHasNotCallMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.4")]
        public void SinceTheObjectPrototypeObjectIsNotAFunctionItHasNotCreateMethod()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.4/S15.2.4_A4.js", false);
        }


    }
}

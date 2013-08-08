using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T03.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject4()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T04.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject5()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T05.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject6()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T06.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.5")]
        public void NumberInstancesHaveNoSpecialPropertiesBeyondThoseInheritedFromTheNumberPrototypeObject7()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.5/S15.7.5_A1_T07.js", false);
        }


    }
}

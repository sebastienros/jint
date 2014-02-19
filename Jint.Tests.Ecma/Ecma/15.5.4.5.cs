using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatCanAcceptManyArguments()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void TheStringPrototypeCharcodeatLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void TheLengthPropertyOfTheCharcodeatMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatPos9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void WhenStringPrototypeCharcodeatPosCallsIfTointegerPosLessThan0TheNanReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void WhenStringPrototypeCharcodeatPosCallsIfTointegerPosNotLessThanTostringThisValueTheNanReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void WhenStringPrototypeCharcodeatPosCallsFirstCallsTostringGivingItTheThisValueAsItsArgument()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void StringPrototypeCharcodeatCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void TheStringPrototypeCharcodeatLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.5")]
        public void TheStringPrototypeCharcodeatLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.5/S15.5.4.5_A9.js", false);
        }


    }
}

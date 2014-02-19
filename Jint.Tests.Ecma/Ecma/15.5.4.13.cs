using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void TheStringPrototypeSliceLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void TheLengthPropertyOfTheSliceMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEnd14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndReturnsAStringValueNotObject9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndCanBeAppliedToObjectInstances()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndCanBeAppliedToObjectInstances2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndCanBeAppliedToObjectInstances3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceStartEndCanBeAppliedToObjectInstances4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void StringPrototypeSliceCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void TheStringPrototypeSliceLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.13")]
        public void TheStringPrototypeSliceLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.13/S15.5.4.13_A9.js", false);
        }


    }
}

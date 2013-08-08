using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_15 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void TheStringPrototypeSubstringLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void TheLengthPropertyOfTheSubstringMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEnd14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndReturnsAStringValueNotObject10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringStartEndCanBeAppliedToNonStringObjectInstanceAndReturnsAStringValueNotObject11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void StringPrototypeSubstringCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void TheStringPrototypeSubstringLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.15")]
        public void TheStringPrototypeSubstringLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.15/S15.5.4.15_A9.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void ThrowRangeerrorIfAttemptToSetArrayLengthPropertyTo4294967296232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-3.d-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void ThrowRangeerrorIfAttemptToSetArrayLengthPropertyTo42949672971232()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-3.d-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void SetArrayLengthPropertyToMaxValue42949672952321()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-3.d-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void DefiningAPropertyNamed42949672952321NotAnArrayElement()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void DefiningAPropertyNamed42949672952321DoesnTChangeLengthOfTheArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/15.4.5.1-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void IfTouint32LengthTonumberLengthThrowRangeerror()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void IfTouint32LengthTonumberLengthThrowRangeerror2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void ForEveryIntegerKThatIsLessThanTheValueOfTheLengthPropertyOfAButNotLessThanTouint32LengthIfAItselfHasAPropertyNotAnInheritedPropertyNamedTostringKThenDeleteThatProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void ForEveryIntegerKThatIsLessThanTheValueOfTheLengthPropertyOfAButNotLessThanTouint32LengthIfAItselfHasAPropertyNotAnInheritedPropertyNamedTostringKThenDeleteThatProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void ForEveryIntegerKThatIsLessThanTheValueOfTheLengthPropertyOfAButNotLessThanTouint32LengthIfAItselfHasAPropertyNotAnInheritedPropertyNamedTostringKThenDeleteThatProperty3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void SetTheValueOfPropertyLengthOfAToUint32Length()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void SetTheValueOfPropertyLengthOfAToUint32Length2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void IfPIsNotAnArrayIndexReturnCreateAPropertyWithNamePSetItsValueToVAndGiveItEmptyAttributes()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void IfTouint32PIsLessThanTheValueOfTheLengthPropertyOfAThenReturn()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.1")]
        public void IfTouint32PIsLessThanTheValueOfTheLengthPropertyOfAChangeOrSetLengthToTouint32P1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.1/S15.4.5.1_A2.3_T1.js", false);
        }


    }
}

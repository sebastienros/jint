using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfXIsNotAStringValueReturnX()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfXIsNotAStringValueReturnX2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfTheEvalFunctionIsCalledWithSomeArgumentThenUseAFirstArgument()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfTheParseFailsThrowASyntaxerrorExceptionButSeeAlsoClause16()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfTheParseFailsThrowASyntaxerrorExceptionButSeeAlsoClause162()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsAValueVThenReturnTheValueV()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsAValueVThenReturnTheValueV2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined5()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined6()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined7()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNormalAndItsCompletionValueIsEmptyThenReturnTheValueUndefined8()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNotNormalThenResult3TypeMustBeThrowThrowResult3ValueAsAnException()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNotNormalThenResult3TypeMustBeThrowThrowResult3ValueAsAnException2()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNotNormalThenResult3TypeMustBeThrowThrowResult3ValueAsAnException3()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void IfResult3TypeIsNotNormalThenResult3TypeMustBeThrowThrowResult3ValueAsAnException4()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A3.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheLengthPropertyOfEvalHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheLengthPropertyOfEvalHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheLengthPropertyOfEvalHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheLengthPropertyOfEvalIs1()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheEvalPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheEvalPropertyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.2.1")]
        public void TheEvalPropertyCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.2/15.1.2.1/S15.1.2.1_A4.7.js", false);
        }


    }
}

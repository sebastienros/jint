using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void IfLengthIsZeroReturnTheEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void IfSeparatorIsUndefinedASingleCommaIsUsedAsTheSeparator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void IfSeparatorIsUndefinedASingleCommaIsUsedAsTheSeparator2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void IfArrayElementIsUndefinedOrNullUseTheEmptyString()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void OperatorUseTostringFromSeparator()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void OperatorUseTostringFromSeparator2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void OperatorUseTostringFromArrayArguments()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void OperatorUseTostringFromArrayArguments2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheLengthPropertyOfJoinHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheLengthPropertyOfJoinHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheLengthPropertyOfJoinHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheLengthPropertyOfJoinIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.5")]
        public void TheJoinPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.5/S15.4.4.5_A6.7.js", false);
        }


    }
}

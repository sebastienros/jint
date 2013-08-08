using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void IfLengthEqualZeroCallThePutMethodOfThisObjectWithArgumentsLengthAnd0AndReturnUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void TheLastElementOfTheArrayIsRemovedFromTheArrayAndReturned()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void GetDeleteFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void GetDeleteFromNotAnInheritedProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void TheLengthPropertyOfPopHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void TheLengthPropertyOfPopHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void TheLengthPropertyOfPopHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void TheLengthPropertyOfPopIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.6")]
        public void ThePopPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.6/S15.4.4.6_A5.7.js", false);
        }


    }
}

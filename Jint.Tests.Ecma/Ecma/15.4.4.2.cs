using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheResultOfCallingThisFunctionIsTheSameAsIfTheBuiltInJoinMethodWereInvokedForThisObjectWithNoArgument()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheResultOfCallingThisFunctionIsTheSameAsIfTheBuiltInJoinMethodWereInvokedForThisObjectWithNoArgument2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheResultOfCallingThisFunctionIsTheSameAsIfTheBuiltInJoinMethodWereInvokedForThisObjectWithNoArgument3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheResultOfCallingThisFunctionIsTheSameAsIfTheBuiltInJoinMethodWereInvokedForThisObjectWithNoArgument4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheLengthPropertyOfTostringHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheLengthPropertyOfTostringHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheLengthPropertyOfTostringHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheLengthPropertyOfTostringIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheTostringPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheTostringPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.2")]
        public void TheTostringPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.2/S15.4.4.2_A4.7.js", false);
        }


    }
}

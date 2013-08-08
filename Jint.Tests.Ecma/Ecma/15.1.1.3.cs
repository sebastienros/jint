using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1_1_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void GlobalUndefinedIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-0.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void UndefinedIsNotWritableShouldNotThrowInNonStrictMode()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void UndefinedIsNotWritableShouldThrowTypeerrorInStrictMode()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void UndefinedIsNotWritableSimpleAssignmentShouldReturnTheRvalValue111316()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/15.1.1.3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void TheInitialValueOfUndefinedIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/S15.1.1.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void TheUndefinedIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/S15.1.1.3_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.1.1.3")]
        public void TheUndefinedIsDontenum()
        {
			RunTest(@"TestCases/ch15/15.1/15.1.1/15.1.1.3/S15.1.1.3_A3.2.js", false);
        }


    }
}

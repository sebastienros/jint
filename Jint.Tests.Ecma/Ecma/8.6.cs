using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.6")]
        public void DoNotCrashWithPostincrementCustomProperty()
        {
			RunTest(@"TestCases/ch08/8.6/S8.6_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.6")]
        public void DoNotCrashWithPostincrementCustomProperty2()
        {
			RunTest(@"TestCases/ch08/8.6/S8.6_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.6")]
        public void DoNotCrashWithPefixincrementCustomProperty()
        {
			RunTest(@"TestCases/ch08/8.6/S8.6_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.6")]
        public void DoNotCrashWithPefixincrementCustomProperty2()
        {
			RunTest(@"TestCases/ch08/8.6/S8.6_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.6")]
        public void AnObjectIsAnUnorderedCollectionOfProperties()
        {
			RunTest(@"TestCases/ch08/8.6/S8.6_A4_T1.js", false);
        }


    }
}

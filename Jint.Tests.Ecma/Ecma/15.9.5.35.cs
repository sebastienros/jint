using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_35 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypePropertySetutchoursHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypePropertySetutchoursHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypePropertySetutchoursHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheLengthPropertyOfTheSetutchoursIs4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypeSetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypeSetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.35")]
        public void TheDatePrototypeSetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.35/S15.9.5.35_A3_T3.js", false);
        }


    }
}

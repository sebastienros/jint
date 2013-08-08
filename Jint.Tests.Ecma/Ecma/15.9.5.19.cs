using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_19 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypePropertyGetutchoursHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypePropertyGetutchoursHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypePropertyGetutchoursHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheLengthPropertyOfTheGetutchoursIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypeGetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypeGetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.19")]
        public void TheDatePrototypeGetutchoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.19/S15.9.5.19_A3_T3.js", false);
        }


    }
}

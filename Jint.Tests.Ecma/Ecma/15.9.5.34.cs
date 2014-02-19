using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_34 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypePropertySethoursHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypePropertySethoursHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypePropertySethoursHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheLengthPropertyOfTheSethoursIs4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypeSethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypeSethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.34")]
        public void TheDatePrototypeSethoursPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.34/S15.9.5.34_A3_T3.js", false);
        }


    }
}

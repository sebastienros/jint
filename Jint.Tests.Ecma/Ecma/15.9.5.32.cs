using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_32 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypePropertySetminutesHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypePropertySetminutesHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypePropertySetminutesHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheLengthPropertyOfTheSetminutesIs3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypeSetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypeSetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.32")]
        public void TheDatePrototypeSetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.32/S15.9.5.32_A3_T3.js", false);
        }


    }
}

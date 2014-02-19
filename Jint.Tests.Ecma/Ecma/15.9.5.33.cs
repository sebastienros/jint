using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_33 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypePropertySetutcminutesHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypePropertySetutcminutesHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypePropertySetutcminutesHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheLengthPropertyOfTheSetutcminutesIs3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypeSetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypeSetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.33")]
        public void TheDatePrototypeSetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.33/S15.9.5.33_A3_T3.js", false);
        }


    }
}

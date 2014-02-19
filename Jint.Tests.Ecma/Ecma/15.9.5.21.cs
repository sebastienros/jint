using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_21 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypePropertyGetutcminutesHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypePropertyGetutcminutesHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypePropertyGetutcminutesHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheLengthPropertyOfTheGetutcminutesIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypeGetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypeGetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.21")]
        public void TheDatePrototypeGetutcminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.21/S15.9.5.21_A3_T3.js", false);
        }


    }
}

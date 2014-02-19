using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_20 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypePropertyGetminutesHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypePropertyGetminutesHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypePropertyGetminutesHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheLengthPropertyOfTheGetminutesIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypeGetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypeGetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.20")]
        public void TheDatePrototypeGetminutesPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.20/S15.9.5.20_A3_T3.js", false);
        }


    }
}

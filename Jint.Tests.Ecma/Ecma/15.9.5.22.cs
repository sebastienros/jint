using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_22 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypePropertyGetsecondsHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypePropertyGetsecondsHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypePropertyGetsecondsHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheLengthPropertyOfTheGetsecondsIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypeGetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypeGetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.22")]
        public void TheDatePrototypeGetsecondsPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.22/S15.9.5.22_A3_T3.js", false);
        }


    }
}

using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypePropertyGettimeHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypePropertyGettimeHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypePropertyGettimeHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheLengthPropertyOfTheGettimeIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypeGettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypeGettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.9")]
        public void TheDatePrototypeGettimePropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.9/S15.9.5.9_A3_T3.js", false);
        }


    }
}

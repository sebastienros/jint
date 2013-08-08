using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_13 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypePropertyGetutcmonthHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypePropertyGetutcmonthHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypePropertyGetutcmonthHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheLengthPropertyOfTheGetutcmonthIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypeGetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypeGetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.13")]
        public void TheDatePrototypeGetutcmonthPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.13/S15.9.5.13_A3_T3.js", false);
        }


    }
}

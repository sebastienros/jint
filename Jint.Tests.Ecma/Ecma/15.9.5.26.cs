using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_26 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypePropertyGettimezoneoffsetHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypePropertyGettimezoneoffsetHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypePropertyGettimezoneoffsetHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheLengthPropertyOfTheGettimezoneoffsetIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypeGettimezoneoffsetPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypeGettimezoneoffsetPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.26")]
        public void TheDatePrototypeGettimezoneoffsetPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.26/S15.9.5.26_A3_T3.js", false);
        }


    }
}

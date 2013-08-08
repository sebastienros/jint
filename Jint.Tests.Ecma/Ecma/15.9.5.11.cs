using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_11 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypePropertyGetutcfullyearHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypePropertyGetutcfullyearHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypePropertyGetutcfullyearHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheLengthPropertyOfTheGetutcfullyearIs0()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypeGetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypeGetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.11")]
        public void TheDatePrototypeGetutcfullyearPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.11/S15.9.5.11_A3_T3.js", false);
        }


    }
}

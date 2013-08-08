using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypePropertyConstructorHasDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypePropertyConstructorHasDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypePropertyConstructorHasDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheLengthPropertyOfTheConstructorIs7()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypeConstructorPropertyLengthHasReadonlyDontdeleteDontenumAttributes()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypeConstructorPropertyLengthHasReadonlyDontdeleteDontenumAttributes2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.5.1")]
        public void TheDatePrototypeConstructorPropertyLengthHasReadonlyDontdeleteDontenumAttributes3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.5/15.9.5.1/S15.9.5.1_A3_T3.js", false);
        }


    }
}

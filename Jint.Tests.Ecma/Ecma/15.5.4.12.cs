using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpWithoutArgumentsBehavesLikeWithArgumentUndefined()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void TheStringPrototypeSearchLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void TheLengthPropertyOfTheSearchMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexp13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpReturns7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpIgnoresGlobalPropertiesOfRegexp()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchRegexpIgnoresGlobalPropertiesOfRegexp2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void StringPrototypeSearchCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void TheStringPrototypeSearchLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.12")]
        public void TheStringPrototypeSearchLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.12/S15.5.4.12_A9.js", false);
        }


    }
}

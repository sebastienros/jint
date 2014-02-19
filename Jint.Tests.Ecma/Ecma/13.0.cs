using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_13_0 : EcmaTest
    {
        [Fact]
        [Trait("Category", "13.0")]
        public void MultipleNamesInOneFunctionDeclarationIsNotAllowedTwoFunctionNames()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-1.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows2()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows3()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows4()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows5()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows6()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows7()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows8()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void MultipleNamesInOneFunctionDeclarationIsNotAllowedThreeFunctionNames()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-2.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void PropertyNamesInFunctionDefinitionIsNotAllowedAddANewPropertyIntoObject()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-3.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void MultipleNamesInOneFunctionDeclarationIsNotAllowedAddANewPropertyIntoAPropertyWhichIsAObject()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-4.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows9()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows10()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void Refer13TheProductionFunctionbodySourceelementsoptIsEvaluatedAsFollows11()
        {
			RunTest(@"TestCases/ch13/13.0/13.0-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void StrictModeSourceelementsIsNotEvaluatedAsStrictModeCodeWhenAFunctionConstructorIsContainedInStrictModeCode()
        {
			RunTest(@"TestCases/ch13/13.0/13.0_4-17gs.js", true);
        }

        [Fact]
        [Trait("Category", "13.0")]
        public void StrictModeSourceelementsIsEvaluatedAsStrictModeCodeWhenAFunctiondeclarationIsContainedInStrictModeCode()
        {
			RunTest(@"TestCases/ch13/13.0/13.0_4-5gs.js", true);
        }


    }
}

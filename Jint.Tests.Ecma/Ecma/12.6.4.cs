using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_6_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheForInStatementAPropertyNameMustNotBeVisitedMoreThanOnceInAnyEnumeration()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/12.6.4-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheForInStatementTheValuesOfEnumerableAttributesAreNotConsideredWhenDeterminingIfAPropertyOfAPrototypeObjectIsShadowedByAPreviousObjectOnThePrototypeChain()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/12.6.4-2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void ForKeyInUndefinedStatementIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void FunctionexpessionWithinAForInExpressionIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A14_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void BlockWithinAForInExpressionIsNotAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A15.js", true);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void ForKeyInNullExpressionIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement3()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement4()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A4.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement5()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement6()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement7()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void TheProductionIterationstatementForVarVariabledeclarationnoinInExpressionStatement8()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A6.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void PropertiesOfTheObjectBeingEnumeratedMayBeDeletedDuringEnumeration()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.6.4")]
        public void PropertiesOfTheObjectBeingEnumeratedMayBeDeletedDuringEnumeration2()
        {
			RunTest(@"TestCases/ch12/12.6/12.6.4/S12.6.4_A7_T2.js", false);
        }


    }
}

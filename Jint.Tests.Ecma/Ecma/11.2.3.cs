using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_2_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableFunctiondeclaration()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableFunctionexpression()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreNotEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableUndefinedMember()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableProperty()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableEvalEd()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_5.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableGetterCalled()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_6.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableGetterCalledAsIndexedProperty()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_7.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallArgumentsAreEvaluatedBeforeTheCheckIsMadeToSeeIfTheObjectIsActuallyCallableGlobalObject()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/11.2.3-3_8.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void WhiteSpaceAndLineTerminatorBetweenMemberexpressionAndArgumentsAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void CallexpressionMemberexpressionArgumentsUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionIsNotObjectThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionIsNotObjectThrowTypeerror2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionIsNotObjectThrowTypeerror3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionIsNotObjectThrowTypeerror4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionIsNotObjectThrowTypeerror5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionDoesNotImplementTheInternalCallMethodThrowTypeerror()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionDoesNotImplementTheInternalCallMethodThrowTypeerror2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionDoesNotImplementTheInternalCallMethodThrowTypeerror3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionDoesNotImplementTheInternalCallMethodThrowTypeerror4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.3")]
        public void IfMemberexpressionDoesNotImplementTheInternalCallMethodThrowTypeerror5()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.3/S11.2.3_A4_T5.js", false);
        }


    }
}

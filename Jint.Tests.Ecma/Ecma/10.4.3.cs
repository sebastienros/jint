using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4.3")]
        public void ThisIsNotCoercedToAnObjectInStrictModeNumber()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionPassedAsArgToStringPrototypeReplaceFromNonStrictContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-100-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionPassedAsArgToStringPrototypeReplaceFromNonStrictContext2()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-100gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionPassedAsArgToStringPrototypeReplaceFromStrictContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-101-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionPassedAsArgToStringPrototypeReplaceFromStrictContext2()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-101gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictAnonymousFunctionPassedAsArgToStringPrototypeReplaceFromNonStrictContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-102-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictAnonymousFunctionPassedAsArgToStringPrototypeReplaceFromNonStrictContext2()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-102gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void NonStrictModeShouldToobjectThisargIfNotAnObjectAbstractEqualityOperatorShouldSucceed()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-103.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeShouldNotToobjectThisargIfNotAnObjectStrictEqualityOperatorShouldSucceed()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-104.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void CreatedBasedOnFeedbackInHttpsBugsEcmascriptOrgShowBugCgiId333()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-105.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void CreatedBasedOnFeedbackInHttpsBugsEcmascriptOrgShowBugCgiId3332()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-106.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-10gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-11gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-12gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-13gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-14gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-15gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-16gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-17gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeEvalIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-18gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisIndirectEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeIndirectEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-19gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void ThisIsNotCoercedToAnObjectInStrictModeString()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisIndirectEvalIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeIndirectEvalIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-20gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-21gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-22gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-23gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-24gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-25gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNewEdObjectFromAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNewEdObjectFromAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-26gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-27gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-28gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-29gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void ThisIsNotCoercedToAnObjectInStrictModeUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-30gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-31gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-32gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-33gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-34-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-34gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-35-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-35gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-36-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-36gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-37-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-37gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-38-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-38gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-39-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-39gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void ThisIsNotCoercedToAnObjectInStrictModeBoolean()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-40-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-40gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-41-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-41gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-42-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-42gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-43-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-43gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-44-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-44gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-45-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-45gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-46-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-46gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-47-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-47gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-48-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-48gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-49-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-49gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void ThisIsNotCoercedToAnObjectInStrictModeFunction()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-50-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-50gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-51-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-51gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-52-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-52gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-53-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-53gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisLiteralGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-54-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeLiteralGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-54gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisLiteralGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-55-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeLiteralGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-55gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisLiteralSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-56-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeLiteralSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-56gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisLiteralSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-57-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeLiteralSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-57gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisInjectedGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-58-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeInjectedGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-58gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisInjectedGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-59-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeInjectedGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-59gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisInjectedSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-60-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeInjectedSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-60gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisInjectedSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-61-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeInjectedSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-61gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByNonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-62-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByNonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-62gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByNonStrictEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-63-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByNonStrictEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-63gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByNonStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-64-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByNonStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-64gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByNonStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-65-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByNonStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-65gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-66-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-66gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-67-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-67gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-68-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-68gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-69-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-69gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-70-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-70gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-71-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-71gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-72-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-72gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-73-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-73gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-74-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-74gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-75-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-75gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-76-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-76gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-77-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-77gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-78-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-78gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-79-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-79gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-7gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisStrictFunctionDeclarationCalledByFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-80-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeStrictFunctionDeclarationCalledByFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-80gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-81-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-81gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-82-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-82gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-83-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-83gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-84-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-84gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-85-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-85gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-86-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-86gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-87-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-87gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-88-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-88gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-89-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-89gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-8gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-90-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-90gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-91-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-91gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-92-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-92gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-93-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-93gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-94-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-94gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-95-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-95gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-96-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-96gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-97-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-97gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-98-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-98gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictModeCheckingThisNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-99-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-99gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void StrictCheckingThisFromAGlobalScopeFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/10.4.3-1-9gs.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.3")]
        public void WhenCallingAStrictAnonymousFunctionAsAFunctionThisShouldBeBoundToUndefined()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.3/S10.4.3_A1.js", false);
        }


    }
}

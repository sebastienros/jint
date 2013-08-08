using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_5_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNewEdFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-10gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-11gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToNonStrictFunctionCallerFromNonStrictFunctionEvalIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-12gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToNonStrictFunctionCallerFromStrictFunctionIndirectEvalUsedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-13gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToNonStrictFunctionCallerFromNonStrictFunctionIndirectEvalIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-14gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionNewEdObjectFromFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-15gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNewEdObjectFromFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-16gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionNewEdObjectFromFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-17gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNewEdObjectFromFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-18gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionNewEdObjectFromAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-19gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctiondeclarationDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNewEdObjectFromAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-20gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctiondeclarationDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-21gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-22gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-23gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctiondeclarationDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-24gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-25gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionAnonymousFunctionexpressionDefinedWithinAFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-26gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-27gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-28gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionInsideStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-29gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-30gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-31gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionDefinedWithinAFunctiondeclarationWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-32gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-33gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-34gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionDefinedWithinAFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-35gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-36gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-37gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionDefinedWithinAnAnonymousFunctionexpressionWithAStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-38gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-39gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-3gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-40gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctiondeclaration()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-41gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-42gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-43gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-44gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctiondeclarationWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-45gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-46gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionWithAStrictDirectivePrologueDefinedWithinAnAnonymousFunctionexpression()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-47gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionLiteralGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-48gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionLiteralGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-49gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-4gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionLiteralSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-50gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionLiteralSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-51gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionInjectedGetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-52gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionInjectedGetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-53gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionInjectedSetterDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-54gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionInjectedSetterIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-55gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByNonStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-56gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByNonStrictEval()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-57gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByNonStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-58gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByNonStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-59gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromStrictFunctionAnonymousFunctionexpressionDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-5gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-60gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-61gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-62gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-63gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-64gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-65gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-66gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-67gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-68gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-69gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionAnonymousFunctionexpressionIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-6gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-70gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-71gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-72gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-73gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionStrictFunctionDeclarationCalledByFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-74gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionDeclaration()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-75gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictEval()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-76gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-77gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictNewEdFunctionConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-78gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApply()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-79gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToNonStrictFunctionCallerFromStrictFunctionFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-7gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-80gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-81gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplySomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-82gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeApplyGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-83gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCall()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-84gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-85gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-86gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallSomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-87gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeCallGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-88gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBind()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-89gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionFunctionConstructorIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-8gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-90gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-91gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindSomeobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-92gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionNonStrictFunctionDeclarationCalledByStrictFunctionPrototypeBindGlobalobject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-93gs.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictFunctionExpressionFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-94gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictConstructorBasedFunctionFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-95gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromNonStrictPropertyFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-96gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToStrictFunctionCallerFromBoundNonStrictFunctionFunctiondeclarationIncludesStrictDirectivePrologue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-97gs.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.5.4")]
        public void StrictModeCheckingAccessToNonStrictFunctionCallerFromStrictFunctionNewEdFunctionConstructorDefinedWithinStrictMode()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-9gs.js", true);
        }


    }
}

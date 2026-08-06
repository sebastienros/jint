using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Which scope a declaration actually reaches. <see cref="HoistingScope"/> answers that once per script,
/// function body or module at prepare time, and two of its answers were wrong in the same direction: a
/// declaration was collected into a scope that does not own it.
/// </summary>
public class DeclarationScopeTests
{
    // https://tc39.es/ecma262/#sec-class-static-block-definitions
    // A ClassStaticBlockBody is its own var scope, hoisted as a function body of its own by
    // ClassStaticBlockDefinitionEvaluation, so nothing it declares reaches the enclosing scope.

    [Fact]
    public void AFunctionDeclaredInAStaticBlockDoesNotReachTheEnclosingScope()
    {
        var engine = new Engine();

        engine.Evaluate("class C { static { function f() {} } } typeof f;").AsString().Should().Be("undefined");
    }

    [Fact]
    public void AVarDeclaredInAStaticBlockDoesNotReachTheEnclosingScope()
    {
        var engine = new Engine();

        // The quieter half: the leaked binding was created in the enclosing scope but assigned inside the
        // static block's own, so it stayed `undefined` rather than throwing - a feature detect of the shape
        // `if (sv)` silently took the false branch instead of failing loudly.
        engine.Evaluate("class C { static { var sv = 1; } } Object.getOwnPropertyNames(globalThis).indexOf('sv') >= 0;")
            .AsBoolean().Should().BeFalse();
        engine.Evaluate("typeof sv;").AsString().Should().Be("undefined");
    }

    [Fact]
    public void AStaticBlockOfAClassExpressionDoesNotLeakEither()
    {
        var engine = new Engine();

        engine.Evaluate("var C = class { static { var q = 1; function qf() {} } }; typeof q + ' ' + typeof qf;")
            .AsString().Should().Be("undefined undefined");
    }

    [Fact]
    public void AStaticBlockInsideAFunctionDoesNotReachThatFunctionsScope()
    {
        var engine = new Engine();

        engine.Evaluate("(function () { class C { static { var x = 1; function xf() {} } } return typeof x + ' ' + typeof xf; })();")
            .AsString().Should().Be("undefined undefined");
    }

    [Fact]
    public void AStaticBlockStillHoistsWithinItself()
    {
        // The control: not reaching the enclosing scope must not mean not being hoisted at all. Both
        // declarations are used before their own position inside the block.
        var engine = new Engine();

        engine.Evaluate("class C { static { C.v = h(); C.w = iv; function h() { return 7; } var iv; iv = 3; } } C.v + ',' + typeof C.w;")
            .AsString().Should().Be("7,undefined");
        engine.Evaluate("class D { static { function h() { return 7; } var iv = 3; D.v = h() + iv; } } D.v;")
            .AsInteger().Should().Be(10);
    }

    // https://tc39.es/ecma262/#sec-web-compat-functiondeclarationinstantiation
    // B.3.2.1 skips the var-hoisting of a block-level function declaration when the name is already
    // lexically declared in the function body - and a class declaration is a lexical declaration.

    [Fact]
    public void AClassNameBlocksAnnexBHoistingOfASameNamedBlockFunction()
    {
        var engine = new Engine();

        engine.Evaluate("(function () { class f { static tag = 'class'; } { function f() {} } return String(f.tag); })();")
            .AsString().Should().Be("class");
    }

    [Fact]
    public void ALetNameBlocksAnnexBHoistingOfASameNamedBlockFunction()
    {
        // The `let` analogue was always correct: the variable-declaration branch of the walk collected the
        // bound name where the class branch did not.
        var engine = new Engine();

        engine.Evaluate("(function () { let f = 'lex'; { function f() {} } return String(f); })();")
            .AsString().Should().Be("lex");
    }

    [Fact]
    public void AClassNameAtGlobalScopeAlreadyBlockedAnnexBHoisting()
    {
        // The second control, and why this only ever showed up inside a function: the script path asks
        // GlobalEnvironment.HasLexicalDeclaration, which is built from the declarations rather than from the
        // collected names, so it saw classes all along.
        var engine = new Engine();

        engine.Evaluate("class f { static tag = 'class'; } { function f() {} } String(f.tag);")
            .AsString().Should().Be("class");
    }

    [Fact]
    public void AnnexBStillHoistsABlockFunctionWhoseNameIsFree()
    {
        // The third control: the conflict filter must not have become a blanket refusal.
        var engine = new Engine();

        engine.Evaluate("(function () { { function g() { return 'hoisted'; } } return typeof g === 'function' ? g() : 'not hoisted'; })();")
            .AsString().Should().Be("hoisted");
    }

    [Fact]
    public void AClassDeclarationDoesNotSuppressTheArgumentsObject()
    {
        // The collected lexical names have a second reader: FunctionDeclarationInstantiation skips creating the
        // arguments object when the body lexically declares that name. Class names now reach that set, so this
        // pins that they only matter when the name actually is `arguments` - which for a class is unreachable,
        // since a class body is always strict and `class arguments {}` is therefore a syntax error in any mode.
        var engine = new Engine();

        engine.Evaluate("(function () { class other {} return typeof arguments; })();")
            .AsString().Should().Be("object");
        Invoking(() => engine.Evaluate("(function () { class arguments {} })();"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Unexpected eval or arguments in strict mode*");
    }

    [Fact]
    public void APreparedModulesCachedScopeAgreesWithTheModuleWalk()
    {
        // An Acornima Module reports NodeType.Program, but preparing one no longer caches a scope on the root at
        // all: every reader of a cached scope is Script-typed and SourceTextModule.InitializeEnvironment re-walks,
        // so the walk prepare time used to do was thrown away. The two walks must still agree: the moment they do
        // not, the obvious cleanup - having the module consume a scope built at prepare time - silently drops every
        // exported lexical declaration.
        var program = Engine.PrepareModule("export const a = 1; export class B {} const c = 2;").Program;

        program.UserData.Should().BeNull();

        var moduleWalk = HoistingScope.GetModuleLevelDeclarations((Acornima.Ast.Module) program);
        var programWalk = HoistingScope.GetProgramLevelDeclarations(program, collectVarNames: true);

        moduleWalk._lexicalDeclarations.Should().NotBeNull().And.HaveCount(3);
        programWalk._lexicalDeclarations.Should().NotBeNull().And.HaveCount(3);
    }
}

namespace Jint.Tests.Runtime;

/// <summary>
/// Annex B's "Block-Level Function Declarations Web Legacy Compatibility Semantics" - the Normative
/// Optional step of https://tc39.es/ecma262/#sec-functiondeclarationinstantiation and its
/// EvalDeclarationInstantiation / GlobalDeclarationInstantiation siblings. A sloppy-mode function
/// declared in a Block, CaseClause or DefaultClause also gets a var binding in the enclosing variable
/// environment, and that binding is <em>assigned when the declaration is evaluated</em>, not when the
/// block is entered.
/// </summary>
public class AnnexBBlockFunctionTests
{
    [Fact]
    public void TheVarAliasIsUndefinedUntilTheDeclarationIsEvaluated()
    {
        var engine = new Engine();

        // Jint used to write the alias from BlockDeclarationInstantiation, i.e. at block entry, which
        // made the name visible one or more statements too early. Inside the block the name resolves to
        // the block's own binding, so the var alias is observed through a closure made before it.
        engine.Evaluate("(function(){ var probe = function(){ return typeof f; }; var seen; { seen = probe(); function f(){} } return seen + ',' + probe(); })()")
            .AsString().Should().Be("undefined,function");
    }

    [Fact]
    public void TheVarAliasTakesTheBlockBindingsCurrentValueNotTheDeclarationsOwn()
    {
        var engine = new Engine();

        // Both declarations are instantiated at block entry, last one winning, so BOTH evaluations
        // assign the second function - inside the block and after it.
        engine.Evaluate("(function(){ { function f(){ return 1; } function f(){ return 2; } } return f(); })()")
            .AsNumber().Should().Be(2);
        engine.Evaluate("(function(){ var seen = []; { seen.push(f()); function f(){ return 1; } seen.push(f()); function f(){ return 2; } seen.push(f()); } seen.push(f()); return seen.join(','); })()")
            .AsString().Should().Be("2,2,2,2");
    }

    [Fact]
    public void TheVarAliasIsWrittenToTheVariableEnvironmentNotThroughTheScopeChain()
    {
        var engine = new Engine();

        // Inside `with`, the LexicalEnvironment resolves `f` on the with object, but the alias goes to
        // the running VariableEnvironment, so the with object's property is untouched.
        engine.Evaluate("var o = { f: 'string-f' }; with (o) { function f(){ return 'fun-f'; } } o.f + ',' + f();")
            .AsString().Should().Be("string-f,fun-f");
    }

    [Fact]
    public void TheGlobalVarAliasExistsBeforeTheDeclarationIsEvaluated()
    {
        var engine = new Engine();

        // GlobalDeclarationInstantiation creates it with CreateGlobalVarBinding(F, false).
        engine.Evaluate("var o = {}; with (o) { var d = Object.getOwnPropertyDescriptor(this, 'f'); function f(){} } String(d.value) + ',' + d.writable + ',' + d.enumerable + ',' + d.configurable;")
            .AsString().Should().Be("undefined,true,true,false");
    }

    [Fact]
    public void ABlockFunctionNamedArgumentsAssignsOverTheArgumentsObject()
    {
        var engine = new Engine();

        // The eligibility condition is "paramNames does not contain funcName" - paramNames, not
        // paramBindings, so the implicit `arguments` binding does not disqualify the name. Only the
        // creation of a NEW var binding is skipped for it; the assignment still runs.
        engine.Evaluate("(function(){ var before = typeof arguments; { function arguments(){} } return before + ',' + typeof arguments; })()")
            .AsString().Should().Be("object,function");
        engine.Evaluate("(function(x){ { function arguments(){} } return typeof arguments; })()")
            .AsString().Should().Be("function");
        engine.Evaluate("(function(..._){ { function arguments(){} } return typeof arguments; })()")
            .AsString().Should().Be("function");
    }

    [Fact]
    public void AFormalParameterNamedArgumentsStillBlocksTheAlias()
    {
        var engine = new Engine();

        // The other half of the same condition: here paramNames really does contain the name.
        engine.Evaluate("(function(arguments){ { function arguments(){} } return typeof arguments; })()")
            .AsString().Should().Be("undefined");
        engine.Evaluate("(function(f){ { function f(){} } return f; })(1)").AsNumber().Should().Be(1);
    }

    [Fact]
    public void ALabelledBlockFunctionIsScopedToTheBlockAndStillGetsTheAlias()
    {
        var engine = new Engine();

        // Annex B.3.1 legalizes `l: function f(){}` in sloppy code and every web implementation gives
        // it the same treatment as the unlabelled form. Jint used to hoist it to the enclosing var
        // scope outright, so the block's own binding was missing.
        engine.Evaluate("(function(){ var seen; { seen = f(); l: function f(){ return 1; } } return seen + ',' + f(); })()")
            .AsString().Should().Be("1,1");
        engine.Evaluate("(function(){ { function f(){ return 1; } l0: l: function f(){ return 2; } } return f(); })()")
            .AsNumber().Should().Be(2);
    }

    [Fact]
    public void ALabelledFunctionAtTheTopLevelOfAFunctionBodyStillHoists()
    {
        var engine = new Engine();

        // TopLevelVarScopedDeclarations sees through the label, so this one is an ordinary hoisted
        // function declaration - callable before its own statement.
        engine.Evaluate("(function(){ var seen = f(); l: function f(){ return 1; } return seen; })()")
            .AsNumber().Should().Be(1);
    }

    [Fact]
    public void ACaseClauseFunctionGetsTheAliasWhenItsClauseIsReached()
    {
        var engine = new Engine();

        // As for a Block: the whole CaseBlock's declarations are instantiated on entry, so the alias is
        // what distinguishes "reached" from "not reached" and is observed through an outside closure.
        engine.Evaluate("(function(){ var probe = function(){ return typeof f; }; var seen; switch (1) { case 1: seen = probe(); case 2: function f(){} } return seen + ',' + probe(); })()")
            .AsString().Should().Be("undefined,function");
        engine.Evaluate("(function(){ var probe = function(){ return typeof f; }; switch (1) { case 1: break; case 2: function f(){} } return probe(); })()")
            .AsString().Should().Be("undefined");
    }

    [Fact]
    public void ALexicalDeclarationOfTheSameNameSuppressesTheAlias()
    {
        var engine = new Engine();

        // "replacing the FunctionDeclaration with a VariableStatement ... would not produce any Early
        // Errors" - a global lexical declaration of the name makes that replacement an early error, so
        // GlobalDeclarationInstantiation creates no global var binding and nothing is assigned.
        engine.Evaluate("let f; { function f(){} } typeof Object.getOwnPropertyDescriptor(this, 'f');")
            .AsString().Should().Be("undefined");

        // control: the same shape without the lexical declaration does create the property
        new Engine().Evaluate("{ function f(){} } typeof Object.getOwnPropertyDescriptor(this, 'f');")
            .AsString().Should().Be("object");
    }

    [Fact]
    public void AnEvalIntroducedAliasSurvivesADeleteOfTheBindingItCreated()
    {
        var engine = new Engine();

        // EvalDeclarationInstantiation settles eligibility once, up front. The var binding it creates
        // is deletable, so deleting it before the block runs must not turn the alias off - the
        // assignment recreates it (SetMutableBinding on a missing sloppy binding).
        engine.Evaluate("(function(){ eval('var seen = (delete q); { function q(){ return 1; } }'); return seen + ',' + q(); })()")
            .AsString().Should().Be("true,1");
    }

    [Fact]
    public void AnEvalAliasTargetsTheVariableEnvironmentThroughAWith()
    {
        var engine = new Engine();

        engine.Evaluate(
            """
            (function f() {
              function g() { return 'outer-g'; }
              var o = { g: function () { return 'with-g'; } };
              with (o) {
                eval('{ function g() { return "eval-g"; } }');
              }
              return g() + ',' + o.g();
            })()
            """).AsString().Should().Be("eval-g,with-g");
    }

    [Fact]
    public void ADestructuredCatchParameterSuppressesTheEvalAlias()
    {
        var engine = new Engine();

        // B.3.4 exempts only a simple BindingIdentifier catch parameter, so `catch ({ f })` makes the
        // VariableStatement replacement an early error and the extension is not observed at all.
        engine.Evaluate("(function(){ eval('try { throw {}; } catch ({ f }) {{ function f(){} }}'); return typeof f; })()")
            .AsString().Should().Be("undefined");

        // ...while a simple one does not.
        engine.Evaluate("(function(){ eval('try { throw 1; } catch (f) {{ function f(){} }}'); return typeof f; })()")
            .AsString().Should().Be("function");
    }

    [Fact]
    public void AStrictBlockFunctionGetsNoAlias()
    {
        var engine = new Engine();

        engine.Evaluate("(function(){ 'use strict'; { function f(){} } return typeof f; })()")
            .AsString().Should().Be("undefined");
    }

    [Fact]
    public void AGeneratorOrAsyncBlockDeclarationGetsNoAlias()
    {
        var engine = new Engine();

        // B.3.2 covers FunctionDeclaration only.
        engine.Evaluate("(function(){ { function* f(){} } return typeof f; })()").AsString().Should().Be("undefined");
        engine.Evaluate("(function(){ { async function f(){} } return typeof f; })()").AsString().Should().Be("undefined");
    }
}

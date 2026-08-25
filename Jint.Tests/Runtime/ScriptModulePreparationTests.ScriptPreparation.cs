using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

public partial class ScriptModulePreparationTests
{
    [Fact]
    public void ScriptPreparationAcceptsReturnOutsideOfFunctions()
    {
        var preparedScript = Engine.PrepareScript("return 1;");
        preparedScript.Program.Body[0].Should().BeOfType<ReturnStatement>();
    }

    [Fact]
    public void CanPreCompileRegex()
    {
        var script = Engine.PrepareScript("var x = /[cgt]/ig; var y = /[cgt]/ig; 'g'.match(x).length;");
        var declaration = script.Program.Body[0].Should().BeOfType<VariableDeclaration>().Which;
        var init = declaration.Declarations[0].Init.Should().BeOfType<RegExpLiteral>().Which;

        // Regex is pre-compiled during preparation with Compiled flag
        var conversionOptions = init.ParseResult.AdditionalData as Engine.RegexConversionOptions;
        conversionOptions.Should().NotBeNull();
        conversionOptions.Compilation.Should().Be(Jint.Native.RegExp.RegexCompilation.Compiled);

        // Prepared script executes correctly
        new Engine().Evaluate(script).AsNumber().Should().Be(1);
    }

    [Fact]
    public void ScriptPreparationFoldsConstants()
    {
        var preparedScript = Engine.PrepareScript("return 1 + 2;");
        var returnStatement = preparedScript.Program.Body[0].Should().BeOfType<ReturnStatement>().Which;
        var constant = (returnStatement.Argument?.UserData).Should().BeOfType<JintConstantExpression>().Which;

        constant.GetValue(null!).AsNumber().Should().Be(3);
        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(3);
    }

    [Fact]
    public void ScriptPreparationOptimizesNegatingUnaryExpression()
    {
        var preparedScript = Engine.PrepareScript("-1");
        var expression = preparedScript.Program.Body[0].Should().BeOfType<NonSpecialExpressionStatement>().Which;
        var unaryExpression = expression.Expression.Should().BeOfType<NonUpdateUnaryExpression>().Which;
        var constant = unaryExpression.UserData.Should().BeOfType<JintConstantExpression>().Which;

        constant.GetValue(null!).AsNumber().Should().Be(-1);
        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(-1);
    }

    [Fact]
    public void ScriptPreparationOptimizesConstantReturn()
    {
        var preparedScript = Engine.PrepareScript("return false;");
        var statement = preparedScript.Program.Body[0].Should().BeOfType<ReturnStatement>().Which;
        var returnStatement = statement.UserData.Should().BeOfType<ConstantStatement>().Which;

        var builtStatement = JintStatement.Build(statement);
        returnStatement.Should().BeSameAs(builtStatement);

        var result = builtStatement.Execute(new EvaluationContext( new Engine())).Value;
        result.Should().Be(JsBoolean.False);
    }

    [Fact]
    public void ScriptPreparationCachesTheHoistingScopeOfAScriptRootOnly()
    {
        // A script root's cached scope is what GlobalDeclarationInstantiation, direct eval and ShadowRealm read.
        Engine.PrepareScript("var a = 1; const b = 2;").Program.UserData.Should().BeOfType<CachedHoistingScope>();

        // A module root reports the same node type but has no reader: SourceTextModuleRecord walks its own
        // declarations, so caching a scope for it was a whole-AST walk nothing consumed.
        Engine.PrepareModule("export const a = 1; const b = 2;").Program.UserData.Should().BeNull();
    }

    [Fact]
    public void ScriptPreparationBuildsNoBlockStateForFunctionBodies()
    {
        // A function body is a BlockStatement by node type, but the only reader of a block's UserData takes a
        // NestedBlockStatement - the statement list wraps a function body without consulting UserData at all.
        // Retained function source text rides the function node, never its body, so the body stays clean.
        var preparedScript = Engine.PrepareScript("function f() { let a = 1; return a; } f();");
        var declaration = preparedScript.Program.Body[0].Should().BeOfType<FunctionDeclaration>().Which;
        var body = declaration.Body.Should().BeOfType<FunctionBody>().Which;

        body.UserData.Should().BeNull();

        // and executing the body does not lazily fill it in either - the cost is gone, not deferred
        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(1);
        body.UserData.Should().BeNull();
    }

    [Fact]
    public void ScriptPreparationBuildsBlockStateForNestedBlocks()
    {
        var preparedScript = Engine.PrepareScript("{ let outer = 1; } function f() { { let inner = 2; return inner; } } f();");

        var topLevelBlock = preparedScript.Program.Body[0].Should().BeOfType<NestedBlockStatement>().Which;
        topLevelBlock.UserData.Should().BeOfType<JintBlockStatement.BlockState>();

        var declaration = preparedScript.Program.Body[1].Should().BeOfType<FunctionDeclaration>().Which;
        var body = declaration.Body.Should().BeOfType<FunctionBody>().Which;
        var blockInFunction = body.Body[0].Should().BeOfType<NestedBlockStatement>().Which;
        blockInFunction.UserData.Should().BeOfType<JintBlockStatement.BlockState>();

        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(2);
    }

    [Fact]
    public void CompiledRegexShouldProduceSameResultAsNonCompiled()
    {
        const string Script = """JSON.stringify(/(.*?)a(?!(a+)b\2c)\2(.*)/.exec("baaabaac"))""";

        var engine = new Engine();
        var nonCompiledResult = engine.Evaluate(Script);
        var compiledResult = engine.Evaluate(Engine.PrepareScript(Script));

        nonCompiledResult.Should().Be(compiledResult);
    }

    [Fact]
    public void PrepareScriptShouldNotLeakAcornimaException()
    {
        var ex = Invoking(() => Engine.PrepareScript("class A { } A().#nonexistent = 1;")).Should().ThrowExactly<ScriptPreparationException>().Which;
        ex.Message.Should().Be("Could not prepare script: Private field '#nonexistent' must be declared in an enclosing class (<anonymous>:1:17)");
        ex.InnerException.Should().BeOfType<SyntaxErrorException>();
    }

    [Fact]
    public void PrepareModuleShouldNotLeakAcornimaException()
    {
        var ex = Invoking(() => Engine.PrepareModule("class A { } A().#nonexistent = 1;")).Should().ThrowExactly<ScriptPreparationException>().Which;
        ex.Message.Should().Be("Could not prepare script: Private field '#nonexistent' must be declared in an enclosing class (<anonymous>:1:17)");
        ex.InnerException.Should().BeOfType<SyntaxErrorException>();
    }
}

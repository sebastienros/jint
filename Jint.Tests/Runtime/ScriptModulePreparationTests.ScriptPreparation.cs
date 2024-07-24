using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

public class ScriptModulePreparationTests
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
        var declaration = Assert.IsType<VariableDeclaration>(script.Program.Body[0]);
        var init = Assert.IsType<RegExpLiteral>(declaration.Declarations[0].Init);

        init.Value.ToString().Should().Be("[cgt]");
        (init.Value.Options & RegexOptions.Compiled).Should().Be(RegexOptions.Compiled);
        new Engine().Evaluate(script).AsNumber().Should().Be(1);
    }

    [Fact]
    public void ScriptPreparationFoldsConstants()
    {
        var preparedScript = Engine.PrepareScript("return 1 + 2;");
        var returnStatement = Assert.IsType<ReturnStatement>(preparedScript.Program.Body[0]);
        var constant = Assert.IsType<JintConstantExpression>(returnStatement.Argument?.UserData);

        constant.GetValue(null!).AsNumber().Should().Be(3);
        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(3);
    }

    [Fact]
    public void ScriptPreparationOptimizesNegatingUnaryExpression()
    {
        var preparedScript = Engine.PrepareScript("-1");
        var expression = Assert.IsType<NonSpecialExpressionStatement>(preparedScript.Program.Body[0]);
        var unaryExpression = Assert.IsType<NonUpdateUnaryExpression>(expression.Expression);
        var constant = Assert.IsType<JintConstantExpression>(unaryExpression.UserData);

        constant.GetValue(null!).AsNumber().Should().Be(-1);
        new Engine().Evaluate(preparedScript).AsNumber().Should().Be(-1);
    }

    [Fact]
    public void ScriptPreparationOptimizesConstantReturn()
    {
        var preparedScript = Engine.PrepareScript("return false;");
        var statement = Assert.IsType<ReturnStatement>(preparedScript.Program.Body[0]);
        var returnStatement = Assert.IsType<ConstantStatement>(statement.UserData);

        var builtStatement = JintStatement.Build(statement);
        returnStatement.Should().BeSameAs(builtStatement);

        var result = builtStatement.Execute(new EvaluationContext( new Engine())).Value;
        result.Should().Be(JsBoolean.False);
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
        var ex = Assert.Throws<ScriptPreparationException>(() => Engine.PrepareScript("class A { } A().#nonexistent = 1;"));
        ex.Message.Should().Be("Could not prepare script: Private field '#nonexistent' must be declared in an enclosing class (1:17)");
        ex.InnerException.Should().BeOfType<SyntaxErrorException>();
    }

    [Fact]
    public void PrepareModuleShouldNotLeakAcornimaException()
    {
        var ex = Assert.Throws<ScriptPreparationException>(() => Engine.PrepareModule("class A { } A().#nonexistent = 1;"));
        ex.Message.Should().Be("Could not prepare script: Private field '#nonexistent' must be declared in an enclosing class (1:17)");
        ex.InnerException.Should().BeOfType<SyntaxErrorException>();
    }
}

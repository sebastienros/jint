using System.Text.RegularExpressions;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

public partial class EngineTests
{
    [Fact]
    public void ScriptPreparationAcceptsReturnOutsideOfFunctions()
    {
        var preparedScript = Engine.PrepareScript("return 1;");
        Assert.IsType<ReturnStatement>(preparedScript.Body[0]);
    }

    [Fact]
    public void CanPreCompileRegex()
    {
        var script = Engine.PrepareScript("var x = /[cgt]/ig; var y = /[cgt]/ig; 'g'.match(x).length;");
        var declaration = Assert.IsType<VariableDeclaration>(script.Body[0]);
        var init = Assert.IsType<RegExpLiteral>(declaration.Declarations[0].Init);
        var regex = Assert.IsType<Regex>(init.AssociatedData);
        Assert.Equal("[cgt]", regex.ToString());
        Assert.Equal(RegexOptions.Compiled, regex.Options & RegexOptions.Compiled);

        Assert.Equal(1, _engine.Evaluate(script));
    }

    [Fact]
    public void ScriptPreparationFoldsConstants()
    {
        var preparedScript = Engine.PrepareScript("return 1 + 2;");
        var returnStatement = Assert.IsType<ReturnStatement>(preparedScript.Body[0]);
        var constant = Assert.IsType<JintConstantExpression>(returnStatement.Argument?.AssociatedData);
        Assert.Equal(3, constant.GetValue(null!));

        Assert.Equal(3, _engine.Evaluate(preparedScript));
    }

    [Fact]
    public void ScriptPreparationOptimizesNegatingUnaryExpression()
    {
        var preparedScript = Engine.PrepareScript("-1");
        var expression = Assert.IsType<ExpressionStatement>(preparedScript.Body[0]);
        var unaryExpression = Assert.IsType<UnaryExpression>(expression.Expression);
        var constant = Assert.IsType<JintConstantExpression>(unaryExpression.AssociatedData);

        Assert.Equal(-1, constant.GetValue(null!));
        Assert.Equal(-1, _engine.Evaluate(preparedScript));
    }

    [Fact]
    public void ScriptPreparationOptimizesConstantReturn()
    {
        var preparedScript = Engine.PrepareScript("return false;");
        var statement = Assert.IsType<ReturnStatement>(preparedScript.Body[0]);
        var returnStatement = Assert.IsType<ConstantStatement>(statement.AssociatedData);

        var builtStatement = JintStatement.Build(statement);
        Assert.Same(returnStatement, builtStatement);

        var result = builtStatement.Execute(new EvaluationContext(_engine)).Value;
        Assert.Equal(JsBoolean.False, result);
    }

    [Fact]
    public void CompiledRegexShouldProduceSameResultAsNonCompiled()
    {
        const string Script = """JSON.stringify(/(.*?)a(?!(a+)b\2c)\2(.*)/.exec("baaabaac"))""";

        var nonCompiled = _engine.Evaluate(Script);
        var compiled = _engine.Evaluate(Engine.PrepareScript(Script));

        Assert.Equal(nonCompiled, compiled);
    }
}

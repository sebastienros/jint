using Esprima.Ast;
using Jint.Native.Function;

namespace Jint.Tests.PublicInterface;

public class FunctionTests
{
    [Fact]
    public void CanConstructorCustomScriptFunction()
    {
        var engine = new Engine();
        var functionExp = new FunctionExpression(
            new Identifier("f"),
            NodeList.Create(Array.Empty<Node>()),
            new BlockStatement(NodeList.Create<Statement>(Array.Empty<Statement>())),
            generator: false,
            strict: true,
            async: false);

        var functionObject = new ScriptFunctionInstance(
            engine,
            functionExp,
            engine.CreateNewDeclarativeEnvironment(),
            strict: true
        );

        Assert.NotNull(functionObject);
    }
}

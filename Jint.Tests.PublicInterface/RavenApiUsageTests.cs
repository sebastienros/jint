using Esprima.Ast;
using Jint.Constraints;
using Jint.Native.Function;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Tests related to functionality that RavenDB needs exposed.
/// </summary>
public class RavenApiUsageTests
{
    [Fact]
    public void CanBuildCustomScriptFunctionInstance()
    {
        var engine = new Engine();

        var properties = new List<Node>
        {
            new Property(PropertyKind.Data, new Identifier("field"), false,
                new StaticMemberExpression(new Identifier("self"), new Identifier("field"), optional: false), false, false)
        };

        var functionExp = new FunctionExpression(
            new Identifier("functionId"),
            NodeList.Create<Node>(new List<Expression> { new Identifier("self") }),
            new BlockStatement(NodeList.Create(new List<Statement> { new ReturnStatement(new ObjectExpression(NodeList.Create(properties))) })),
            generator: false,
            strict: false,
            async: false);

        var functionObject = new ScriptFunctionInstance(
            engine,
            functionExp,
            engine.CreateNewDeclarativeEnvironment(),
            strict: false
        );

        Assert.NotNull(functionObject);
    }

    [Fact]
    public void CanChangeMaxStatementValue()
    {
        var engine = new Engine(options => options.MaxStatements(123));

        var constraint = engine.FindConstraint<MaxStatementsConstraint>();
        Assert.NotNull(constraint);

        var oldMaxStatements = constraint.MaxStatements;
        constraint.MaxStatements = 321;

        Assert.Equal(123, oldMaxStatements);
        Assert.Equal(321, constraint.MaxStatements);
    }
}

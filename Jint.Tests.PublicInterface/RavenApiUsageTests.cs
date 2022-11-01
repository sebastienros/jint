using Esprima.Ast;
using Jint.Constraints;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

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

    [Fact]
    public void CanConstructArrayInstanceFromDescriptorArray()
    {
        var descriptors = new[]
        {
            new PropertyDescriptor(42, writable: false, enumerable: false, configurable: false),
        };

        var engine = new Engine();
        var array = new ArrayInstance(engine, descriptors);
        Assert.Equal(1L, array.Length);
        Assert.Equal(42, array[0]);
    }

    [Fact]
    public void CanGetPropertyDescriptor()
    {
        var engine = new Engine();
        var obj = new DirectoryInfo("the-path");
        var propertyDescriptor = ObjectWrapper.GetPropertyDescriptor(engine, obj, obj.GetType().GetProperty(nameof(DirectoryInfo.Name)));
        Assert.Equal("the-path", propertyDescriptor.Value);
    }
}

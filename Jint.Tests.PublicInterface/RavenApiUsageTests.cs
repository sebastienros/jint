using Esprima.Ast;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Object;
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

    [Fact]
    public void CanInjectConstructedObjects()
    {
        var engine = new Engine();
        var obj = new ObjectInstance(engine);
        obj.FastSetDataProperty("name", "test");

        var emptyArray = new ArrayInstance(engine);

        var array = new ArrayInstance(engine, new object[]
        {
            JsNumber.Create(1),
            JsNumber.Create(2),
            JsNumber.Create(3)
        });
        var date = new DateInstance(engine, new DateTime(2022, 10, 20));

        engine.SetValue("obj", obj);
        engine.SetValue("emptyArray", emptyArray);
        engine.SetValue("array", array);
        engine.SetValue("date", date);

        Assert.Equal("test", engine.Evaluate("obj.name"));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("array.findIndex(x => x === 2)"));
        Assert.Equal(2022, engine.Evaluate("date.getFullYear()"));

        array.Push(4);
        array.Push(new JsValue[] { 5, 6 });

        Assert.Equal(4, array[3]);
        Assert.Equal(5, array[4]);
        Assert.Equal(6, array[5]);

        var i = 0;
        foreach (var entry in array.GetEntries())
        {
            Assert.Equal(i.ToString(), entry.Key);
            Assert.Equal(i + 1, entry.Value);
            i++;
        }
        Assert.Equal(6, i);
    }
}

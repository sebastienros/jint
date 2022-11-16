using Esprima.Ast;
using Jint.Constraints;
using Jint.Native;
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
            new Property(PropertyKind.Init, new Identifier("field"), false,
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
        var array = new JsArray(engine, descriptors);
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
        var obj = new JsObject(engine);
        obj.FastSetDataProperty("name", "test");

        var array1 = new JsArray(engine, new JsValue[]
        {
            JsNumber.Create(1),
            JsNumber.Create(2),
            JsNumber.Create(3)
        });
        engine.SetValue("array1", array1);

        TestArrayAccess(engine, array1, "array1");

        var array3 = new JsArray(engine, new []
        {
            new PropertyDescriptor(JsNumber.Create(1), true, true, true),
            new PropertyDescriptor(JsNumber.Create(2), true, true, true),
            new PropertyDescriptor(JsNumber.Create(3), true, true, true),
        });
        engine.SetValue("array3", array3);
        TestArrayAccess(engine, array3, "array3");

        engine.SetValue("obj", obj);
        Assert.Equal("test", engine.Evaluate("obj.name"));

        engine.SetValue("emptyArray", new JsArray(engine));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("emptyArray.push(1); return emptyArray.length"));

        engine.SetValue("emptyArray", new JsArray(engine, Array.Empty<JsValue>()));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("emptyArray.push(1); return emptyArray.length"));

        engine.SetValue("emptyArray", new JsArray(engine, Array.Empty<PropertyDescriptor>()));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("emptyArray.push(1); return emptyArray.length"));

        engine.SetValue("date", new JsDate(engine, new DateTime(2022, 10, 20)));
        Assert.Equal(2022, engine.Evaluate("date.getFullYear()"));
    }

    private static void TestArrayAccess(Engine engine, JsArray array, string name)
    {
        Assert.Equal(1, engine.Evaluate($"{name}.findIndex(x => x === 2)"));
        Assert.Equal(2, array.GetOwnProperty("1").Value);

        array.Push(4);
        array.Push(new JsValue[] { 5, 6 });

        var i = 0;
        foreach (var entry in array.GetEntries())
        {
            Assert.Equal(i.ToString(), entry.Key);
            Assert.Equal(i + 1, entry.Value);
            i++;
        }

        Assert.Equal(6, i);

        array[0] = "";
        array[1] = false;
        array[2] = null;

        Assert.Equal("", array[0]);
        Assert.Equal(false, array[1]);
        Assert.Equal(JsValue.Undefined, array[2]);
        Assert.Equal(4, array[3]);
        Assert.Equal(5, array[4]);
        Assert.Equal(6, array[5]);

        for (i = 0; i < 100; ++i)
        {
            array.Push(JsValue.Undefined);
        }

        Assert.Equal(106L, array.Length);
        Assert.True(array.All(x => x is JsNumber or JsUndefined or JsNumber or JsString or JsBoolean));
    }
}

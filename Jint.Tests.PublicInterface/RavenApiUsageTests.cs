using Esprima.Ast;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
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

        var functionObject = new ScriptFunction(
            engine,
            functionExp,
            strict: false);

        Assert.NotNull(functionObject);
    }

    [Fact]
    public void CanChangeMaxStatementValue()
    {
        var engine = new Engine(options => options.MaxStatements(123));

        var constraint = engine.Constraints.Find<MaxStatementsConstraint>();
        Assert.NotNull(constraint);

        var oldMaxStatements = constraint.MaxStatements;
        constraint.MaxStatements = 321;

        Assert.Equal(123, oldMaxStatements);
        Assert.Equal(321, constraint.MaxStatements);
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

        engine.SetValue("obj", obj);
        Assert.Equal("test", engine.Evaluate("obj.name"));

        engine.SetValue("emptyArray", new JsArray(engine));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("emptyArray.push(1); return emptyArray.length"));

        engine.SetValue("emptyArray", new JsArray(engine, Array.Empty<JsValue>()));
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

    // Checks different ways how string can be checked for equality without the need to materialize lazy value
    [Fact]
    public void CanInheritCustomString()
    {
        var engine = new Engine();

        var str = new CustomString("the-value");
        engine.SetValue("str", str);

        var empty = new CustomString("");
        engine.SetValue("empty", empty);

        var obj = new JsObject(engine);
        obj.Set("name", new CustomString("the name"));
        engine.SetValue("obj", obj);

        var array = new JsArray(engine, Enumerable.Range(1, 100).Select(x => new CustomString(x.ToString())).ToArray<JsValue>());
        engine.SetValue("array", array);

        Assert.True(engine.Evaluate("str ? true : false").AsBoolean());
        Assert.False(engine.Evaluate("empty ? true : false").AsBoolean());

        Assert.True(engine.Evaluate("array.includes('2')").AsBoolean());
        Assert.True(engine.Evaluate("array.filter(x => x === '2').length > 0").AsBoolean());

        engine.SetValue("objArray", new JsArray(engine, new JsValue[] { obj, obj }));
        Assert.True(engine.Evaluate("objArray.filter(x => x.name === 'the name').length === 2").AsBoolean());

        Assert.Equal(9, engine.Evaluate("str.length"));

        Assert.True(engine.Evaluate("str == 'the-value'").AsBoolean());
        Assert.True(engine.Evaluate("str === 'the-value'").AsBoolean());

        Assert.True(engine.Evaluate("str.indexOf('value-too-long') === -1").AsBoolean());
        Assert.True(engine.Evaluate("str.lastIndexOf('value-too-long') === -1").AsBoolean());
        Assert.False(engine.Evaluate("str.startsWith('value-too-long')").AsBoolean());
        Assert.False(engine.Evaluate("str.endsWith('value-too-long')").AsBoolean());
        Assert.False(engine.Evaluate("str.includes('value-too-long')").AsBoolean());

        Assert.True(engine.Evaluate("empty.trim() === ''").AsBoolean());
        Assert.True(engine.Evaluate("empty.trimStart() === ''").AsBoolean());
        Assert.True(engine.Evaluate("empty.trimEnd() === ''").AsBoolean());
    }

    [Fact]
    public void CanDefineCustomNull()
    {
        var engine = new Engine();
        engine.SetValue("value", new CustomNull());
        Assert.Equal("foo", engine.Evaluate("value ? value + 'bar' : 'foo'"));
    }

    [Fact]
    public void CanDefineCustomUndefined()
    {
        var engine = new Engine();
        engine.SetValue("value", new CustomUndefined());
        Assert.Equal("foo", engine.Evaluate("value ? value + 'bar' : 'foo'"));
    }

    [Fact]
    public void CanResetCallStack()
    {
        var engine = new Engine();
        engine.Advanced.ResetCallStack();
    }
}

file sealed class CustomString : JsString
{
    private readonly string _value;

    public CustomString(string value) : base(null)
    {
        _value = value;
    }

    public override string ToString()
    {
        // when called we know that we couldn't use fast paths
        throw new InvalidOperationException("I don't want to be materialized!");
    }

    public override char this[int index] => _value[index];

    public override int Length => _value.Length;

    public override bool Equals(JsString obj)
    {
        return obj switch
        {
            CustomString customString => _value == customString._value,
            _ => _value == obj.ToString()
        };
    }

    protected override bool IsLooselyEqual(JsValue value)
    {
        return value switch
        {
            CustomString customString => _value == customString._value,
            JsString jsString => _value == jsString.ToString(),
            _ => base.IsLooselyEqual(value)
        };
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}

file sealed class CustomNull : JsValue
{
    public CustomNull() : base(Types.Null)
    {
    }

    public override object ToObject()
    {
        return null;
    }

    public override string ToString()
    {
        return "null";
    }
}

file sealed class CustomUndefined : JsValue
{
    public CustomUndefined() : base(Types.Null)
    {
    }

    public override object ToObject()
    {
        return null;
    }

    public override string ToString()
    {
        return "null";
    }
}

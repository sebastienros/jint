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

        var properties = new Node[]
        {
            new ObjectProperty(PropertyKind.Init, new Identifier("field"),
                new MemberExpression(new Identifier("self"), new Identifier("field"), computed: false, optional: false), false, false, false)
        };

        var functionExp = new FunctionExpression(
            new Identifier("functionId"),
            NodeList.From<Node>(new Identifier("self")),
            new FunctionBody(NodeList.From<Statement>(new ReturnStatement(new ObjectExpression(NodeList.From(properties)))), strict: false),
            generator: false,
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

        var array1 = new JsArray(engine, [
            JsNumber.Create(1),
            JsNumber.Create(2),
            JsNumber.Create(3)
        ]);
        engine.SetValue("array1", array1);

        TestArrayAccess(engine, array1, "array1");

        engine.SetValue("obj", obj);
        Assert.Equal("test", engine.Evaluate("obj.name"));

        engine.SetValue("emptyArray", new JsArray(engine));
        Assert.Equal(0, engine.Evaluate("emptyArray.length"));
        Assert.Equal(1, engine.Evaluate("emptyArray.push(1); return emptyArray.length"));

        engine.SetValue("emptyArray", new JsArray(engine, []));
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
        array.Push([5, 6]);

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

        engine.SetValue("x", new CustomString("x", allowMaterialize: true));

        var obj = new JsObject(engine);
        obj.Set("name", new CustomString("the name"));
        engine.SetValue("obj", obj);

        var array = new JsArray(engine, Enumerable.Range(1, 100).Select(x => new CustomString(x.ToString())).ToArray<JsValue>());
        engine.SetValue("array", array);

        Assert.True(engine.Evaluate("str ? true : false").AsBoolean());
        Assert.False(engine.Evaluate("empty ? true : false").AsBoolean());

        Assert.True(engine.Evaluate("array.includes('2')").AsBoolean());
        Assert.True(engine.Evaluate("array.filter(x => x === '2').length > 0").AsBoolean());

        engine.SetValue("objArray", new JsArray(engine, [obj, obj]));
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

        Assert.True(engine.Evaluate("str[1] === 'h'").AsBoolean());
        Assert.True(engine.Evaluate("str[x] === undefined").AsBoolean());
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

    [Fact]
    public void CanUseCustomReferenceResolver()
    {
        var engine = new Engine(options =>
        {
            options.ReferenceResolver = new MyReferenceResolver();
        });

        engine
            .Execute("""
                          function output(doc) {
                         var rows123 = [{}];
                     var test = null;
                         return { 
                             Rows : [{}].map(row=>({row:row, myRows:test.filter(x=>x)
                             })).map(__rvn4=>({
                                 Custom:__rvn4.myRows[0].Custom,
                                 Custom2:__rvn4.myRows
                                 }))
                                 };
                     }
                     """);

        var result = engine.Evaluate("output()");

        var rows = result.AsObject()["Rows"];
        var custom = rows.AsArray()[0].AsObject()["Custom"];
        Assert.Equal(JsValue.Null, custom);
    }
}

file sealed class CustomString : JsString
{
    private readonly string _value;
    private readonly bool _allowMaterialize;

    public CustomString(string value, bool allowMaterialize = false) : base(null)
    {
        _value = value;
        _allowMaterialize = allowMaterialize;
    }

    public override string ToString()
    {
        if (!_allowMaterialize)
        {
            // when called we know that we couldn't use fast paths
            throw new InvalidOperationException("I don't want to be materialized!");
        }

        return _value;
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

file sealed class MyReferenceResolver : IReferenceResolver
{
    public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
    {
        JsValue referencedName = reference.ReferencedName;

        if (referencedName.IsString())
        {
            value = reference.IsPropertyReference ? JsValue.Undefined : JsValue.Null;
            return true;
        }

        throw new InvalidOperationException();
    }

    public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
    {
        return value.IsNull() || value.IsUndefined();
    }

    public bool TryGetCallable(Engine engine, object callee, out JsValue value)
    {
        value = new ClrFunction(engine, "function", static (_, _) => JsValue.Undefined);
        return true;
    }

    public bool CheckCoercible(JsValue value)
    {
        return true;
    }
}

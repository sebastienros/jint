using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime;

public class NullPropagation
{
    public class NullPropagationReferenceResolver : IReferenceResolver
    {
        public bool TryUnresolvableReference(Engine engine, Reference reference, out JsValue value)
        {
            value = reference.Base;
            return true;
        }

        public bool TryPropertyReference(Engine engine, Reference reference, ref JsValue value)
        {
            return value.IsNull() || value.IsUndefined();
        }

        public bool TryGetCallable(Engine engine, object callee, out JsValue value)
        {
            if (callee is Reference reference)
            {
                var name = reference.ReferencedName.AsString();
                if (name == "filter")
                {
                    value = new ClrFunction(engine, "map", (thisObj, values) => engine.Realm.Intrinsics.Array.ArrayCreate(0));
                    return true;
                }
            }

            value = new ClrFunction(engine, "anonymous", (thisObj, values) => thisObj);
            return true;
        }

        public bool CheckCoercible(JsValue value)
        {
            return true;
        }
    }

    [Fact]
    public void CanCallFilterOnNull()
    {
        var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

        const string Script = @"
var input = {};

var output = { Tags : input.Tags.filter(x=>x!=null) };
";

        engine.Execute(Script);

        var output = engine.GetValue("output").AsObject();

        Assert.True(output.Get("Tags").IsArray());
    }

    [Fact]
    public void NullPropagationTest()
    {
        var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

        const string Script = @"
var input = {
	Address : null
};

var address = input.Address;
var city = input.Address.City;
var length = input.Address.City.length;

var output = {
	Count1 : input.Address.City.length,
	Count2 : this.XYZ.length
};
";

        engine.Execute(Script);

        var address = engine.GetValue("address");
        var city = engine.GetValue("city");
        var length = engine.GetValue("length");
        var output = engine.GetValue("output").AsObject();

        Assert.Equal(JsValue.Null, address);
        Assert.Equal(JsValue.Null, city);
        Assert.Equal(JsValue.Null, length);

        Assert.Equal(JsValue.Null, output.Get("Count1"));
        Assert.Equal(JsValue.Undefined, output.Get("Count2"));
    }

    [Fact]
    public void NullPropagationFromArg()
    {
        var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));


        const string Script = @"
function test(arg) {
    return arg.Name;
}

function test2(arg) {
    return arg.Name.toUpperCase();
}
";
        engine.Execute(Script);
        var result = engine.Invoke("test", JsValue.Null);

        Assert.Equal(JsValue.Null, result);

        result = engine.Invoke("test2", JsValue.Null);

        Assert.Equal(JsValue.Null, result);
    }

    [Fact]
    public void NullPropagationShouldNotAffectOperators()
    {
        var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

        var jsObject = engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        jsObject.Set("NullField", JsValue.Null);

        var script = @"
this.is_nullfield_not_null = this.NullField !== null;
this.is_notnullfield_not_null = this.NotNullField !== null;
this.has_emptyfield_not_null = this.EmptyField !== null;
";

        var wrapperScript = $@"function ExecutePatchScript(docInner){{ (function(doc){{ {script} }}).apply(docInner); }};";

        engine.Execute(wrapperScript, "main.js");

        engine.Invoke("ExecutePatchScript", jsObject);

        Assert.False(jsObject.Get("is_nullfield_not_null").AsBoolean());
        Assert.True(jsObject.Get("is_notnullfield_not_null").AsBoolean());
        Assert.True(jsObject.Get("has_emptyfield_not_null").AsBoolean());
    }
}
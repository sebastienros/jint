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

        output.Get("Tags").IsArray().Should().BeTrue();
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

        address.Should().Be(JsValue.Null);
        city.Should().Be(JsValue.Null);
        length.Should().Be(JsValue.Null);

        output.Get("Count1").Should().Be(JsValue.Null);
        output.Get("Count2").Should().BeUndefined();
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

        result.Should().Be(JsValue.Null);

        result = engine.Invoke("test2", JsValue.Null);

        result.Should().Be(JsValue.Null);
    }

    [Fact]
    public void CanCallUnresolvableReference()
    {
        // A call to an unresolvable identifier must be routed through the reference resolver,
        // like a read is, rather than throwing. Regression: the unresolvable reference base is a
        // sentinel (not undefined), so the call path stopped consulting the resolver and cast the
        // sentinel to an Environment, throwing InvalidCastException.
        var engine = new Engine(cfg => cfg.SetReferencesResolver(new NullPropagationReferenceResolver()));

        var act = () => engine.Execute("var read = missing; var called = missing();");

        act.Should().NotThrow();
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

        jsObject.Get("is_nullfield_not_null").AsBoolean().Should().BeFalse();
        jsObject.Get("is_notnullfield_not_null").AsBoolean().Should().BeTrue();
        jsObject.Get("has_emptyfield_not_null").AsBoolean().Should().BeTrue();
    }
}
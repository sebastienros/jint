#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins that <b>the enumeration hook is the one enumeration uses</b>. A host object whose properties live
/// outside the engine's property tables declares them by overriding <c>GetOwnPropertyKeys</c> — and
/// <c>ProbeOwnProperty</c> beside it, so existence and enumerability are answered without materializing a
/// descriptor per key — and that one pair now answers <em>every</em> consumer.
///
/// <para>
/// It used not to. <c>ObjectInstance.GetOwnProperties</c> was a second, independent <c>virtual</c>, and it
/// was the one whose name reads like the enumeration hook. A real integrator overrode only that and shipped
/// an object whose keys were invisible to <c>Object.keys</c>, <c>for..in</c>, spread, <c>Object.assign</c>
/// and <c>JSON.stringify</c>; the reverse mistake — overriding the key hooks alone, which is the correct
/// thing to do — left the same object invisible to <c>GetOwnProperties</c>' own consumers, of which the CLR
/// conversion path and the debugger are the two an embedder meets. <c>GetOwnProperties</c> is derived from
/// <c>GetOwnPropertyKeys</c> + <c>GetOwnProperty</c> now, and is no longer overridable, so neither half of
/// that trap exists.
/// </para>
///
/// <para>
/// These live in the public-interface suite on purpose: the project references Jint without any internals
/// access, so everything below is proven reachable by a third-party host.
/// </para>
/// </summary>
public class HostEnumerationHookTests
{
    [Test]
    public void TheKeyHooksAloneMakeAHostEnumerableFromScript()
    {
        // The control. This half already worked: script-visible enumeration has always gone through
        // GetOwnPropertyKeys and ProbeOwnProperty.
        var engine = new Engine();
        engine.SetValue("record", Sample(engine));

        engine.Evaluate("Object.keys(record).join(',')").Should().Be("zulu,alpha,7");
        engine.Evaluate("Object.values(record).join(',')").Should().Be("z,a,seven");
        engine.Evaluate("var seen = []; for (var k in record) seen.push(k); seen.join(',')").Should().Be("zulu,alpha,7");
        // spread and assign copy into an ordinary target, whose own-key order hoists the integer-like key
        engine.Evaluate("""JSON.stringify({...record})""").Should().Be("""{"7":"seven","zulu":"z","alpha":"a"}""");
        engine.Evaluate("""JSON.stringify(Object.assign({}, record))""").Should().Be("""{"7":"seven","zulu":"z","alpha":"a"}""");
        engine.Evaluate("""JSON.stringify(record)""").Should().Be("""{"zulu":"z","alpha":"a","7":"seven"}""");
    }

    [Test]
    public void TheKeyHooksAloneMakeAHostEnumerableThroughGetOwnProperties()
    {
        // The half that did not work. Before GetOwnProperties was derived, this host — which declares
        // nothing but the key hooks, exactly as the documentation tells it to — enumerated as empty here.
        var engine = new Engine();
        var record = Sample(engine);

        var enumerated = record.GetOwnProperties()
            .Select(pair => pair.Key.ToString() + "=" + pair.Value.Value.ToString())
            .ToList();

        enumerated.Should().Equal("zulu=z", "alpha=a", "7=seven");
    }

    [Test]
    public void TheKeyHooksAloneMakeAHostConvertibleToAClrObject()
    {
        // GetOwnProperties' most-reached consumer: JsValue.ToObject() under Options.Interop.CreateClrObject.
        var engine = new Engine();
        var record = Sample(engine);

        var converted = record.ToObject() as IDictionary<string, object?>;

        converted.Should().NotBeNull();
        converted!.Keys.Should().Equal("zulu", "alpha", "7");
        converted["zulu"].Should().Be("z");
        converted["alpha"].Should().Be("a");
        converted["7"].Should().Be("seven");
    }

    [Test]
    public void TheKeyHooksAloneMakeAHostVisibleToTheDebugger()
    {
        // The other consumer: the debugger's binding-name enumeration, reached through a `with` scope, whose
        // binding object is the host itself.
        var engine = new Engine(options =>
        {
            options.Debugger.Enabled = true;
            options.Debugger.StatementHandling = DebuggerStatementHandling.Script;
        });
        engine.SetValue("record", Sample(engine));

        IReadOnlyList<string>? withScopeBindings = null;
        engine.Debugger.Break += (_, info) =>
        {
            withScopeBindings = info.CurrentScopeChain
                .First(scope => scope.ScopeType == DebugScopeType.With)
                .BindingNames;
            return StepMode.None;
        };

        engine.Execute("with (record) { debugger; }");

        withScopeBindings.Should().NotBeNull();
        withScopeBindings.Should().Equal("zulu", "alpha", "7");
    }

    [Test]
    public void GetOwnPropertiesReportsTheOrderGetOwnPropertyKeysReports()
    {
        // The host's own key order is the enumeration order, whatever it is: this one is deliberately neither
        // sorted nor index-first, and both surfaces agree on it.
        var engine = new Engine();
        var record = Sample(engine);
        engine.SetValue("record", record);

        var fromScript = engine.Evaluate("Object.getOwnPropertyNames(record).join(',')").AsString();
        var fromHost = string.Join(",", record.GetOwnProperties().Select(pair => pair.Key.ToString()));

        fromHost.Should().Be(fromScript);
        fromHost.Should().Be("zulu,alpha,7");
    }

    [Test]
    public void AnExpandoWrittenByScriptJoinsTheSameEnumeration()
    {
        // The projected names and the property bag are one enumeration, in the one order both surfaces agree
        // on — the host chains to base.GetOwnPropertyKeys, and nothing else is needed to keep them together.
        var engine = new Engine();
        var record = Sample(engine);
        engine.SetValue("record", record);

        engine.Execute("record.written = 'w';");

        engine.Evaluate("Object.keys(record).join(',')").Should().Be("zulu,alpha,7,written");
        record.GetOwnProperties().Select(pair => pair.Key.ToString())
            .Should().Equal("zulu", "alpha", "7", "written");
    }

    [Test]
    public void AKeyWhoseDescriptorIsAbsentIsNotReported()
    {
        // GetOwnProperties never hands back PropertyDescriptor.Undefined: a key the list still names but
        // GetOwnProperty no longer answers for is skipped, which is what every script-visible enumeration
        // does with the same key.
        var engine = new Engine();
        var record = Sample(engine);
        record.Retract("alpha");
        engine.SetValue("record", record);

        record.GetOwnProperties().Select(pair => pair.Key.ToString()).Should().Equal("zulu", "7");
        engine.Evaluate("Object.keys(record).join(',')").Should().Be("zulu,7");
    }

    [Test]
    public void GetOwnPropertiesIsNotAnExtensionPoint()
    {
        // The whole point: there is no second hook to get wrong. A host that wants to be enumerated overrides
        // GetOwnPropertyKeys, and this one is derived from it.
        var method = typeof(ObjectInstance).GetMethod(nameof(ObjectInstance.GetOwnProperties), Type.EmptyTypes);

        method.Should().NotBeNull();
        method!.IsVirtual.Should().BeFalse();
    }

    [Test]
    public void AFunctionPrototypeReportsTheConstructorItOwns()
    {
        // An in-box object that had the very defect this change exists to make impossible: a lazily created
        // f.prototype keeps `constructor` in a field, and used to declare it to GetOwnProperties alone — so
        // hasOwnProperty and getOwnPropertyDescriptor both saw it while getOwnPropertyNames and
        // Reflect.ownKeys answered an empty list.
        var engine = new Engine();

        engine.Evaluate("function f() {} Object.getOwnPropertyNames(f.prototype).join(',')").Should().Be("constructor");
        engine.Evaluate("function g() {} Reflect.ownKeys(g.prototype).join(',')").Should().Be("constructor");
        engine.Evaluate("function h() {} h.prototype.hasOwnProperty('constructor')").Should().Be(true);
        // still non-enumerable, so the enumerations that filter on that keep skipping it
        engine.Evaluate("function i() {} Object.keys(i.prototype).length").Should().Be(0);
        engine.Evaluate("function j() {} delete j.prototype.constructor; Object.getOwnPropertyNames(j.prototype).length").Should().Be(0);
    }

    private static ProjectedRecordObject Sample(Engine engine)
    {
        var record = new ProjectedRecordObject(engine);
        record.Project("zulu", "z");
        record.Project("alpha", "a");
        record.Project("7", "seven");
        return record;
    }
}

/// <summary>
/// A host record projected from native state: not one of its fields lives in the engine's property tables, so
/// every question about them is answered by an override. It declares exactly the three the documentation asks
/// for — <c>GetOwnProperty</c> for the descriptor, <c>GetOwnPropertyKeys</c> for the keys and
/// <c>ProbeOwnProperty</c> so existence and enumerability cost no descriptor — and nothing else.
/// </summary>
internal sealed class ProjectedRecordObject : ObjectInstance
{
    private readonly List<KeyValuePair<string, JsValue>> _fields = new List<KeyValuePair<string, JsValue>>();

    public ProjectedRecordObject(Engine engine) : base(engine)
    {
    }

    public void Project(string name, JsValue value) => _fields.Add(new KeyValuePair<string, JsValue>(name, value));

    public void Retract(string name) => _fields.RemoveAll(field => string.Equals(field.Key, name, StringComparison.Ordinal));

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (TryProject(property, out var value))
        {
            return new PropertyDescriptor(value, writable: true, enumerable: true, configurable: true);
        }

        return base.GetOwnProperty(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(_fields.Count);
        if ((types & Types.String) != Types.Empty)
        {
            foreach (var field in _fields)
            {
                keys.Add(new JsString(field.Key));
            }
        }

        keys.AddRange(base.GetOwnPropertyKeys(types));
        return keys;
    }

    protected override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        return TryProject(property, out _) ? OwnPropertyProbe.Enumerable : base.ProbeOwnProperty(property);
    }

    private bool TryProject(JsValue property, out JsValue value)
    {
        if (property.IsString())
        {
            var name = property.ToString();
            foreach (var field in _fields)
            {
                if (string.Equals(field.Key, name, StringComparison.Ordinal))
                {
                    value = field.Value;
                    return true;
                }
            }
        }

        value = Undefined;
        return false;
    }
}

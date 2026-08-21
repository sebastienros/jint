using System.Dynamic;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

public class ClrWriteConfigurationTests
{
    internal sealed class Host
    {
        private readonly Dictionary<string, int> _indexed = new(StringComparer.Ordinal)
        {
            ["item"] = 3
        };

        public int Field = 1;

        public static int StaticField = 1;

        public int Property { get; set; } = 2;

        public int SetterCalls { get; private set; }

        public int SetterTracked
        {
            get => 4;
            set => SetterCalls++;
        }

        public int this[string key]
        {
            get => _indexed[key];
            set => _indexed[key] = value;
        }

        public void Mutate(int value)
        {
            Field = value;
        }
    }

    [Fact]
    public void DirectClrWritesAreDisabledByDefault()
    {
        new Options().Interop.AllowWrite.Should().BeFalse();

        var host = new Host();
        var dictionary = new Dictionary<string, int>(StringComparer.Ordinal) { ["item"] = 5 };
        var list = new List<int> { 6 };
        var array = new[] { 7 };
        var engine = CreateEngine(host, dictionary, list, array);

        engine.Execute(
            """
            host.Field = 11;
            host.Property = 12;
            host.SetterTracked = 13;
            host.item = 14;
            dictionary.item = 15;
            list[0] = 16;
            array[0] = 17;
            """);

        host.Field.Should().Be(1);
        host.Property.Should().Be(2);
        host.SetterCalls.Should().Be(0);
        host["item"].Should().Be(3);
        dictionary["item"].Should().Be(5);
        list.Should().Equal(6);
        array.Should().Equal(7);
    }

    [Fact]
    public void ExplicitOptInEnablesDirectClrWrites()
    {
        var host = new Host();
        var dictionary = new Dictionary<string, int>(StringComparer.Ordinal) { ["item"] = 5 };
        var list = new List<int> { 6 };
        var array = new[] { 7 };
        var engine = CreateEngine(host, dictionary, list, array, allowWrite: true);

        engine.Execute(
            """
            host.Field = 11;
            host.Property = 12;
            host.SetterTracked = 13;
            host.item = 14;
            dictionary.item = 15;
            list[0] = 16;
            array[0] = 17;
            """);

        host.Field.Should().Be(11);
        host.Property.Should().Be(12);
        host.SetterCalls.Should().Be(1);
        host["item"].Should().Be(14);
        dictionary["item"].Should().Be(15);
        list.Should().Equal(16);
        array.Should().Equal(17);
    }

    [Fact]
    public void StrictModeSurfacesBlockedWritesAsTypeErrors()
    {
        var host = new Host();
        var engine = new Engine().SetValue("host", host);

        var exception = Invoking(() => engine.Execute("'use strict'; host.Property = 12;"))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        host.Property.Should().Be(2);
    }

    [Fact]
    public void AllowClrDoesNotImplicitlyEnableDirectWrites()
    {
        var host = new Host();
        var options = new Options().AllowClr();
        var engine = new Engine(options).SetValue("host", host);

        options.Interop.AllowWrite.Should().BeFalse();
        engine.Execute("host.Property = 12;");

        host.Property.Should().Be(2);
    }

    [Fact]
    public void CachedAccessorsDoNotShareWriteAuthorityAcrossEngines()
    {
        var writableHost = new Host();
        var writableEngine = new Engine(options => options.AllowClrWrite()).SetValue("host", writableHost);
        writableEngine.Execute("host.Property = 12;");
        writableHost.Property.Should().Be(12);

        var readOnlyHost = new Host();
        var readOnlyEngine = new Engine().SetValue("host", readOnlyHost);
        var exception = Invoking(() => readOnlyEngine.Execute("'use strict'; host.Property = 13;"))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        readOnlyHost.Property.Should().Be(2);
    }

    [Fact]
    public void AlternateClrMutationRoutesAreBlockedByDefault()
    {
        Host.StaticField = 1;
        IDictionary<string, object> expando = new ExpandoObject();
        expando["Value"] = 3;
        var host = new Host();
        var engine = new Engine()
            .SetValue("host", host)
            .SetValue("expando", expando);
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(Host)));

        engine.Execute(
            """
            host.Field++;
            host.Property += 10;
            const proxy = new Proxy(host, {});
            proxy.Property = 20;
            Host.StaticField = 30;
            expando.Value = 40;
            """);

        host.Field.Should().Be(1);
        host.Property.Should().Be(2);
        Host.StaticField.Should().Be(1);
        expando["Value"].Should().Be(3);
        engine.Evaluate("Reflect.set(host, 'Property', 50)").Should().BeFalse();
        engine.Evaluate("Reflect.deleteProperty(host, 'Property')").Should().BeFalse();
        Invoking(() => engine.Execute("Object.defineProperty(host, 'Property', { value: 60 })"))
            .Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void OrdinaryJavaScriptObjectWritesRemainEnabled()
    {
        var engine = new Engine();

        engine.Evaluate(
            """
            const value = { count: 1 };
            value.count++;
            value.extra = 3;
            Object.defineProperty(value, 'defined', { value: 4 });
            value.count + value.extra + value.defined;
            """).Should().Be(9);
    }

    [Fact]
    public void ClrWriteOptInDoesNotBypassJavaScriptObjectExtensibility()
    {
        var engine = new Engine(options => options.AllowClrWrite());

        var exception = Invoking(() => engine.Execute("const value = [0, , 2]; Object.preventExtensions(value); value.fill(9);"))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        engine.Evaluate("1 in value").Should().BeFalse();

        exception = Invoking(() => engine.Execute(
                """
                const result = [0, , 2];
                Object.preventExtensions(result);
                const source = [3, 4];
                source.constructor = { [Symbol.species]: function () { return result; } };
                source.filter(() => true);
                """))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        engine.Evaluate("1 in result").Should().BeFalse();
    }

    [Theory]
    [InlineData("Array.prototype.push.call(list, 9)")]
    [InlineData("Array.prototype.unshift.call(list, 9)")]
    [InlineData("Array.prototype.splice.call(list, 0, 1)")]
    [InlineData("Array.prototype.fill.call(list, 9)")]
    [InlineData("const source = [3, 4]; source.constructor = { [Symbol.species]: function () { return list; } }; source.filter(() => true)")]
    public void ArrayGenericsCannotMutateClrListsByDefault(string script)
    {
        var list = new List<int> { 1, 2 };
        var engine = new Engine().SetValue("list", list);

        var exception = Invoking(() => engine.Execute(script))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        list.Should().Equal(1, 2);
    }

    [Theory]
    [InlineData("Array.prototype.fill.call(list, 9)")]
    [InlineData("Array.prototype.push.call(list, 9)")]
    [InlineData("Array.prototype.pop.call(list)")]
    [InlineData("Array.prototype.shift.call(list)")]
    [InlineData("Array.prototype.splice.call(list, 0, 1)")]
    [InlineData("list['0'] = 9")]
    [InlineData("Reflect.set(list, '0', 9)")]
    [InlineData("Object.assign(list, { 0: 9 })")]
    [InlineData("Object.getOwnPropertyDescriptor(list, '0').set.call(list, 9)")]
    public void ArrayGenericsCannotMutateFrozenClrListsWhenWritesAreEnabled(string script)
    {
        var list = new List<int> { 1, 2 };
        var engine = new Engine(options => options.AllowClrWrite()).SetValue("list", list);
        engine.Execute("Object.freeze(list);");

        var exception = Invoking(() => engine.Execute(script))
            .Should().Throw<JavaScriptException>().Which;

        exception.Error.Get("name").AsString().Should().Be("TypeError");
        list.Should().Equal(1, 2);
    }

    [Fact]
    public void AbsentPropertiesCanBeDeletedFromReadOnlyClrWrappers()
    {
        var engine = new Engine().SetValue("list", new List<int> { 1, 2 });

        engine.Evaluate("Reflect.deleteProperty(list, 'missing')").Should().BeTrue();
        engine.Evaluate("Reflect.deleteProperty(list, '99')").Should().BeTrue();
        engine.Evaluate("'use strict'; delete list.missing").Should().BeTrue();
    }

    [Fact]
    public void ReadOnlyDictionaryShapedWrappersUseKeyMembershipForDeletion()
    {
        var value = new JObject
        {
            ["5"] = 1,
            ["other"] = 2
        };
        var engine = new Engine().SetValue("value", value);

        engine.Evaluate("Reflect.deleteProperty(value, '5')").Should().BeFalse();
        engine.Evaluate("Reflect.deleteProperty(value, '0')").Should().BeTrue();
        value["5"]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void MethodsAndExtensionMethodsCanStillMutateHostState()
    {
        var host = new Host();
        var list = new List<int> { 1 };
        var engine = new Engine(options => options.AddExtensionMethods(typeof(ClrWriteHostExtensions)))
            .SetValue("host", host)
            .SetValue("list", list);

        engine.Execute(
            """
            host.Mutate(20);
            host.MutateThroughExtension(21);
            list.Add(2);
            """);

        host.Field.Should().Be(21);
        list.Should().Equal(1, 2);
    }

    private static Engine CreateEngine(
        Host host,
        Dictionary<string, int> dictionary,
        List<int> list,
        int[] array,
        bool allowWrite = false)
    {
        return new Engine(options =>
            {
                if (allowWrite)
                {
                    options.AllowClrWrite();
                }
            })
            .SetValue("host", host)
            .SetValue("dictionary", dictionary)
            .SetValue("list", list)
            .SetValue("array", array);
    }
}

internal static class ClrWriteHostExtensions
{
    public static void MutateThroughExtension(this ClrWriteConfigurationTests.Host value, int newValue)
    {
        value.Mutate(newValue);
    }
}

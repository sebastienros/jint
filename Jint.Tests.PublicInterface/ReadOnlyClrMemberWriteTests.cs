using Jint.Runtime;
using Jint.Runtime.Interop;


namespace Jint.Tests.PublicInterface;

/// <summary>
/// A CLR member that cannot be written must behave like a non-writable ordinary property:
/// <c>[[Set]]</c> returns <c>false</c>, which makes the assignment a silent no-op in sloppy mode and a
/// <c>TypeError</c> in strict mode (https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor,
/// https://tc39.es/ecma262/#sec-putvalue). It must never leave a JS-side own property shadowing the
/// CLR member, and the host object must be untouched. These have to hold whether or not the member was
/// read before the write, and a refused write must not disturb reads on other wrappers of the same type.
/// </summary>
public class ReadOnlyClrMemberWriteTests
{
    private const string Initial = "initial";

    public class Host
    {
        public string GetterOnly { get; } = Initial;

        public string Computed => Initial;

        public readonly string ReadOnlyField = Initial;

        public string PrivateSetter { get; private set; } = Initial;

        public string Writable { get; set; } = Initial;

        public string Method() => Initial;
    }

    public class IndexerWithoutSetter
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal) { ["a"] = Initial };

        public string this[string key] => _values.TryGetValue(key, out var value) ? value : null;
    }

    public class SetterOnlyHost
    {
        public string Written;

        public string Value
        {
            set => Written = value;
        }
    }

    public static class StaticHost
    {
        public static string GetterOnly { get; } = Initial;

        public static readonly string ReadOnlyField = Initial;

        public const string Constant = Initial;
    }

    private static Engine CreateEngine(object host) => new Engine().SetValue("host", host);

    private static void AssertTypeError(Engine engine, string script, string memberName)
    {
        var exception = Invoking(() => engine.Evaluate(script)).Should().Throw<JavaScriptException>().Which;
        exception.Error.Get("name").AsString().Should().Be("TypeError");
        exception.Message.Should().Contain(memberName);
    }

    // ---------------------------------------------------------------------------------------------
    // instance members, write before any read (the resolution lane that produced the original defect)
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("Computed")]
    [InlineData("ReadOnlyField")]
    public void SloppyWriteToReadOnlyMemberIsSilentlyIgnored(string member)
    {
        var host = new Host();
        var engine = CreateEngine(host);

        engine.Evaluate($"host.{member} = 'written';");

        engine.Evaluate($"host.{member}").AsString().Should().Be(Initial);
        host.GetterOnly.Should().Be(Initial);
        host.Computed.Should().Be(Initial);
        host.ReadOnlyField.Should().Be(Initial);
    }

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("Computed")]
    [InlineData("ReadOnlyField")]
    public void StrictWriteToReadOnlyMemberThrowsTypeErrorNamingTheMember(string member)
    {
        var host = new Host();
        var engine = CreateEngine(host);

        AssertTypeError(engine, $"'use strict'; host.{member} = 'written';", member);

        engine.Evaluate($"host.{member}").AsString().Should().Be(Initial);
        host.GetterOnly.Should().Be(Initial);
        host.Computed.Should().Be(Initial);
        host.ReadOnlyField.Should().Be(Initial);
    }

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("Computed")]
    [InlineData("ReadOnlyField")]
    public void RefusedWriteLeavesNoShadowingOwnProperty(string member)
    {
        var engine = CreateEngine(new Host());

        engine.Evaluate($"host.{member} = 'written';");

        // the member must still be described by the live CLR accessor, not by a data property
        // carrying the value the script tried to write
        engine.Evaluate($"var d = Object.getOwnPropertyDescriptor(host, '{member}');");
        engine.Evaluate("typeof d.get").AsString().Should().Be("function");
        engine.Evaluate("d.set === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("'value' in d").AsBoolean().Should().BeFalse();

        // and the reported key set must not have grown
        engine.Evaluate($"Object.getOwnPropertyNames(host).filter(function (n) {{ return n === '{member}'; }}).length")
            .AsNumber().Should().Be(1);
    }

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("Computed")]
    [InlineData("ReadOnlyField")]
    public void ReflectSetReportsFailureForReadOnlyMember(string member)
    {
        var engine = CreateEngine(new Host());

        engine.Evaluate($"Reflect.set(host, '{member}', 'written')").AsBoolean().Should().BeFalse();
        engine.Evaluate($"host.{member}").AsString().Should().Be(Initial);
    }

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("Computed")]
    [InlineData("ReadOnlyField")]
    public void WriteBeforeAnyReadBehavesLikeWriteAfterRead(string member)
    {
        var writeFirst = CreateEngine(new Host());
        var readFirst = CreateEngine(new Host());
        readFirst.Evaluate($"host.{member}");

        foreach (var engine in new[] { writeFirst, readFirst })
        {
            engine.Evaluate($"host.{member} = 'written';");
            engine.Evaluate($"host.{member}").AsString().Should().Be(Initial);
            AssertTypeError(engine, $"'use strict'; host.{member} = 'written';", member);
        }
    }

    [Fact]
    public void RefusedWriteDoesNotDisturbReadsOnOtherWrappersOfTheSameType()
    {
        var engine = new Engine()
            .SetValue("first", new Host())
            .SetValue("second", new Host());

        engine.Evaluate("first.GetterOnly = 'written';");

        // member resolution is cached per engine and per CLR type: a refused write must not leave a
        // "no such member" verdict behind that later reads of any wrapper of that type pick up
        engine.Evaluate("second.GetterOnly").AsString().Should().Be(Initial);
        engine.Evaluate("first.GetterOnly").AsString().Should().Be(Initial);
    }

    [Fact]
    public void ReadOfWriteOnlyMemberDoesNotDisturbLaterWrites()
    {
        var host = new SetterOnlyHost();
        var engine = CreateEngine(host);

        // the read resolves nothing (the member is not readable) and must not be remembered as the
        // answer for the write that follows
        engine.Evaluate("host.Value").IsUndefined().Should().BeTrue();

        engine.Evaluate("host.Value = 'written';");

        host.Written.Should().Be("written");
    }

    // ---------------------------------------------------------------------------------------------
    // indexers
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SloppyWriteThroughIndexerWithoutSetterIsSilentlyIgnored()
    {
        var host = new IndexerWithoutSetter();
        var engine = CreateEngine(host);

        engine.Evaluate("host.a = 'written';");

        engine.Evaluate("host.a").AsString().Should().Be(Initial);
        host["a"].Should().Be(Initial);
    }

    [Fact]
    public void StrictWriteThroughIndexerWithoutSetterThrowsTypeError()
    {
        var host = new IndexerWithoutSetter();
        var engine = CreateEngine(host);

        AssertTypeError(engine, "'use strict'; host.a = 'written';", "a");

        engine.Evaluate("host.a").AsString().Should().Be(Initial);
        host["a"].Should().Be(Initial);
    }

    // ---------------------------------------------------------------------------------------------
    // static members exposed through a type reference
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("ReadOnlyField")]
    [InlineData("Constant")]
    public void SloppyWriteToReadOnlyStaticMemberIsSilentlyIgnored(string member)
    {
        var engine = new Engine();
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(StaticHost)));

        engine.Evaluate($"Host.{member} = 'written';");

        engine.Evaluate($"Host.{member}").AsString().Should().Be(Initial);
        StaticHost.GetterOnly.Should().Be(Initial);
        StaticHost.ReadOnlyField.Should().Be(Initial);
    }

    [Theory]
    [InlineData("GetterOnly")]
    [InlineData("ReadOnlyField")]
    [InlineData("Constant")]
    public void StrictWriteToReadOnlyStaticMemberThrowsTypeError(string member)
    {
        var engine = new Engine();
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(StaticHost)));

        AssertTypeError(engine, $"'use strict'; Host.{member} = 'written';", member);

        engine.Evaluate($"Host.{member}").AsString().Should().Be(Initial);
        StaticHost.GetterOnly.Should().Be(Initial);
        StaticHost.ReadOnlyField.Should().Be(Initial);
    }

    // ---------------------------------------------------------------------------------------------
    // array-like and dictionary-backed wrappers use the same lane
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SloppyWriteToReadOnlyMemberOfArrayLikeWrapperIsSilentlyIgnored()
    {
        var list = new List<int> { 1, 2, 3 };
        var engine = CreateEngine(list);

        engine.Evaluate("host.Count = 99;");

        engine.Evaluate("host.Count").AsNumber().Should().Be(3);
        list.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void StrictWriteToReadOnlyMemberOfArrayLikeWrapperThrowsTypeError()
    {
        var list = new List<int> { 1, 2, 3 };
        var engine = CreateEngine(list);

        AssertTypeError(engine, "'use strict'; host.Count = 99;", "Count");

        engine.Evaluate("host.Count").AsNumber().Should().Be(3);
        list.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SloppyWriteToReadOnlyMemberOfCollectionWrapperIsSilentlyIgnored()
    {
        var set = new HashSet<int> { 1, 2, 3 };
        var engine = CreateEngine(set);

        engine.Evaluate("host.Count = 99;");

        engine.Evaluate("host.Count").AsNumber().Should().Be(3);
        set.Count.Should().Be(3);
    }

    [Fact]
    public void DictionaryBackedWrapperKeepsWritingEntries()
    {
        var dictionary = new Dictionary<string, int>(StringComparer.Ordinal) { ["a"] = 1 };
        var engine = CreateEngine(dictionary);

        engine.Evaluate("host.a = 42; host.b = 7;");

        dictionary["a"].Should().Be(42);
        dictionary["b"].Should().Be(7);
    }

    // ---------------------------------------------------------------------------------------------
    // controls: nothing about writable members or wrapper extension may change
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WritableMemberIsStillWritten()
    {
        var host = new Host();
        var engine = CreateEngine(host);

        engine.Evaluate("host.Writable = 'sloppy';");
        host.Writable.Should().Be("sloppy");

        engine.Evaluate("'use strict'; host.Writable = 'strict';");
        host.Writable.Should().Be("strict");

        engine.Evaluate("Reflect.set(host, 'Writable', 'reflected')").AsBoolean().Should().BeTrue();
        host.Writable.Should().Be("reflected");
    }

    [Fact]
    public void PropertyWithNonPublicSetterIsStillWritten()
    {
        // a property whose setter exists but is not public still reports CanWrite, so the write
        // reaches the CLR object; it is not a read-only member and is unaffected here
        var host = new Host();
        var engine = CreateEngine(host);

        engine.Evaluate("'use strict'; host.PrivateSetter = 'written';");

        host.PrivateSetter.Should().Be("written");
    }

    [Fact]
    public void UnknownMemberStillExtendsTheWrapper()
    {
        var engine = CreateEngine(new Host());

        engine.Evaluate("host.brandNew = 1;");
        engine.Evaluate("host.brandNew").AsNumber().Should().Be(1);

        engine.Evaluate("'use strict'; host.alsoNew = 2;");
        engine.Evaluate("host.alsoNew").AsNumber().Should().Be(2);
    }

    [Fact]
    public void DefinePropertyKeepsItsExistingAnswers()
    {
        var host = new Host();
        var engine = CreateEngine(host);

        // a reflected CLR member is reported as non-configurable, so redefining it is refused - the
        // refused assignment must not have turned it into a configurable JS-side data property
        Invoking(() => engine.Evaluate("host.GetterOnly = 'written'; Object.defineProperty(host, 'GetterOnly', { value: 'redefined' });"))
            .Should().Throw<JavaScriptException>();
        engine.Evaluate("host.GetterOnly").AsString().Should().Be(Initial);
        host.GetterOnly.Should().Be(Initial);

        // defining a name the CLR type does not carry stays allowed
        engine.Evaluate("Object.defineProperty(host, 'extra', { value: 'defined', configurable: true });");
        engine.Evaluate("host.extra").AsString().Should().Be("defined");
    }

    [Fact]
    public void DisallowingClrWritesStillRefusesWritableMembers()
    {
        var host = new Host();
        var engine = new Engine(options => options.AllowClrWrite(false)).SetValue("host", host);

        engine.Evaluate("host.Writable = 'written';");
        host.Writable.Should().Be(Initial);

        AssertTypeError(engine, "'use strict'; host.Writable = 'written';", "Writable");
        host.Writable.Should().Be(Initial);
    }

    [Fact]
    public void ThrowOnUnresolvedMemberOnlyFiresForGenuinelyUnknownNames()
    {
        var host = new Host();
        var engine = new Engine(options => options.Interop.ThrowOnUnresolvedMember = true).SetValue("host", host);

        // the member resolves, it merely cannot be written: an ordinary refusal, not a missing member
        engine.Evaluate("host.GetterOnly = 'written';");
        host.GetterOnly.Should().Be(Initial);

        Invoking(() => engine.Evaluate("host.noSuchMember = 1;")).Should().Throw<MissingMemberException>();
    }
}

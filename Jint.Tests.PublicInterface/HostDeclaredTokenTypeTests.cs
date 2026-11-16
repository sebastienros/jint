#nullable enable

using Jint.Runtime.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A <c>JObject</c> that reaches script through a member typed <c>JToken</c>, <c>JContainer</c> or
/// <c>IDictionary&lt;string, JToken&gt;</c> is the same keyed object it is when it crosses as <c>JObject</c>
/// (#4524).
/// </summary>
/// <remarks>
/// <para>
/// #4501 made a wrapped <c>JObject</c> an ordinary keyed object: no array-like view of its <c>JProperty</c> nodes and
/// <c>Object.prototype</c> as its prototype. That decision read the type the value crossed as. A member typed
/// <c>JToken</c> or <c>JContainer</c> names a type that is not a dictionary, so the wrapper took the list instead, and a
/// member typed <c>IDictionary&lt;string, JToken&gt;</c> names no list, so the target fell back to the non-generic
/// list view. Both put the object on <c>Array.prototype</c> with a <c>length</c> of 0, and a <c>JToken</c>-typed one
/// read nothing by key at all.
/// </para>
/// <para>
/// The expected answers are an ordinary object's, for <c>{ a: 1, b: 2 }</c>, and Node gives the same:
/// <c>x.a</c> is 1 (<see href="https://tc39.es/ecma262/#sec-ordinaryget">OrdinaryGet</see>), <c>0 in x</c> is false
/// (<see href="https://tc39.es/ecma262/#sec-ordinaryhasproperty">OrdinaryHasProperty</see>), <c>Object.keys</c> lists
/// <c>a</c> and <c>b</c> (<see href="https://tc39.es/ecma262/#sec-ordinaryownpropertykeys">OrdinaryOwnPropertyKeys</see>),
/// <c>JSON.stringify</c> gives <c>{"a":1,"b":2}</c> (<see href="https://tc39.es/ecma262/#sec-json.stringify">JSON.stringify</see>),
/// and <c>Array.prototype.slice.call(x)</c> is empty (<see href="https://tc39.es/ecma262/#sec-lengthofarraylike">LengthOfArrayLike</see>
/// is 0). The control is the same <c>JObject</c> crossing as itself, which must answer identically.
/// </para>
/// </remarks>
public class HostDeclaredTokenTypeTests
{
    /// <summary>
    /// The members a host hands the same <c>JObject</c> through: as itself (the control), typed <c>object</c>, typed
    /// <c>JToken</c> by a property, a method and a field, typed <c>JContainer</c>, and typed
    /// <c>IDictionary&lt;string, JToken&gt;</c>.
    /// </summary>
    public static IEnumerable<string> Members => ["AsObject", "AsJObject", "AsToken", "GetToken()", "TokenField", "AsContainer", "AsDictionary"];

    private const string Ordinary = "1,2,true,false,true,a|b,a|b,{\"a\":1,\"b\":2},true,false,true,0,true";

    /// <summary>
    /// Every lane a script reads the object through, joined so a failure names the lanes that disagreed.
    /// </summary>
    private const string Lanes = """
        [
            x.a,
            x['b'],
            'a' in x,
            0 in x,
            x[0] === undefined,
            Object.keys(x).join('|'),
            (() => { const keys = []; for (const k in x) keys.push(k); return keys.join('|'); })(),
            JSON.stringify(x),
            Object.getPrototypeOf(x) === Object.prototype,
            Array.isArray(x),
            x.length === undefined,
            Array.prototype.slice.call(x).length,
            Object.getOwnPropertyDescriptor(x, 0) === undefined
        ].join(',')
        """;

    [TestCaseSource(nameof(Members))]
    public void AJObjectIsAKeyedObjectWhateverItCrossesAs(string member)
    {
        var engine = CreateEngine(JObject.Parse("""{ "a": 1, "b": 2 }"""), member, allowWrite: false);

        engine.Evaluate(Lanes).AsString().Should().Be(Ordinary, "a JObject crossing as {0} is the object it is as JObject", member);
    }

    /// <summary>
    /// The issue's reproduction: a nested <c>JObject</c> handed over through a <c>JToken</c>-typed property.
    /// </summary>
    [Test]
    public void TheIssuesReproduction()
    {
        var root = JObject.Parse("""{ "a": { "b": 1 } }""");
        var engine = new Engine().SetValue("host", new { Data = root["a"] });

        engine.Evaluate("""
            [
                host.Data.b,
                Object.keys(host.Data).join('|'),
                JSON.stringify(host.Data),
                host.Data[0] === undefined
            ].join(',')
            """).AsString().Should().Be("1,b,{\"b\":1},true");
    }

    /// <summary>
    /// Writing and deleting through the member is writing and deleting a key, as it is for the object crossing as
    /// itself: <c>x[0] = 5</c> adds the key <c>"0"</c> (<see href="https://tc39.es/ecma262/#sec-topropertykey">ToPropertyKey</see>)
    /// and a <c>length</c> write adds the key <c>length</c> rather than truncating.
    /// </summary>
    [TestCaseSource(nameof(Members))]
    public void WritesAndDeletesAreKeyOperations(string member)
    {
        var target = JObject.Parse("""{ "a": 1, "b": 2 }""");
        var engine = CreateEngine(target, member, allowWrite: true);

        engine.Evaluate("x.c = 3; x[0] = 5; x.length = 0; [delete x.a, delete x[7]].join(',')").AsString().Should().Be("true,true");

        // a number written from script reaches a JToken as a double, as it does for the JObject crossing as itself
        target.ToString(Formatting.None).Should().Be("""{"b":2,"c":3.0,"0":5.0,"length":0.0}""");
    }

    /// <summary>
    /// Reading the same member twice hands script the same wrapper, as it does for an object crossing as itself,
    /// under the identity map and under the recent-wrapper ring. Asked of the host by reference: script's `===` on two
    /// wrappers compares their CLR targets, so it cannot tell a fresh wrapper from the cached one.
    /// </summary>
    [TestCaseSource(nameof(Members))]
    public void ReadingTheMemberTwiceIsTheSameObject(string member)
    {
        foreach (var (track, ring) in new[] { (true, false), (false, true) })
        {
            var host = new TokenHost(JObject.Parse("""{ "a": 1 }"""));
            var engine = new Engine(options =>
            {
                options.Interop.TrackObjectWrapperIdentity = track;
                options.Interop.CacheRecentObjectWrappers = ring;
            }).SetValue("host", host);

            var first = engine.Evaluate($"host.{member}");
            var second = engine.Evaluate($"host.{member}");
            ReferenceEquals(first, second).Should().BeTrue("{0}, identity map {1}, ring {2}", member, track, ring);
        }
    }

    /// <summary>
    /// A <c>JArray</c> reached through a <c>JToken</c>- or <c>JContainer</c>-typed member stays a list, with its
    /// positions and <c>Array.prototype</c> as the <c>JArray</c> crossing as itself has
    /// (<see href="https://tc39.es/ecma262/#sec-array.prototype.slice">Array.prototype.slice</see> reads them). A name
    /// it does not have is undefined: as a <c>JToken</c>, every name reached <c>JToken</c>'s object indexer, and
    /// <c>JArray</c>'s threw <c>ArgumentException</c> for <c>toJSON</c>, so <c>JSON.stringify</c> threw too.
    /// </summary>
    [TestCase("AsJArray")]
    [TestCase("AsToken")]
    [TestCase("AsContainer")]
    [TestCase("GetToken()")]
    public void AJArrayStaysAList(string member)
    {
        var array = JArray.Parse("[1, 2]");
        var engine = CreateEngine(array, member, allowWrite: true);

        engine.Evaluate("""
            [
                x[0],
                x['1'],
                0 in x,
                5 in x,
                x.length,
                Array.prototype.slice.call(x).join('|'),
                Object.getPrototypeOf(x) === Array.prototype,
                JSON.stringify(x)
            ].join(',')
            """).AsString().Should().Be("1,2,true,false,2,1|2,true,[1,2]");

        engine.Evaluate("[x.foo === undefined, (x[0] = 7, x[0]), x.push(3), x.length].join(',')").AsString().Should().Be("true,7,3,3");
        array.ToString(Formatting.None).Should().Be("[7.0,2,3.0]");
    }

    /// <summary>
    /// A <c>JValue</c> reached through a <c>JToken</c>-typed member crosses as its primitive value.
    /// </summary>
    [TestCase("AsToken")]
    [TestCase("GetToken()")]
    [TestCase("TokenField")]
    public void AJValueCrossesAsItsValue(string member)
    {
        var values = new JArray(1, 2.5, "s", true, JValue.CreateNull());
        var answers = new List<string>();
        foreach (var value in values)
        {
            var engine = CreateEngine(value, member, allowWrite: false);
            answers.Add(engine.Evaluate("typeof x + ':' + String(x)").AsString());
        }

        string.Join(",", answers).Should().Be("number:1,number:2.5,string:s,boolean:true,object:null");
    }

    /// <summary>
    /// The runtime type a declared type deferred to last is remembered, and another one crossing in between must not
    /// make the first read a different object the next time.
    /// </summary>
    [TestCase("AsToken", true)]
    [TestCase("AsToken", false)]
    [TestCase("GetToken()", true)]
    [TestCase("GetToken()", false)]
    public void IdentitySurvivesAnotherRuntimeTypeCrossingInBetween(string member, bool trackIdentity)
    {
        var engine = new Engine(options => options.Interop.TrackObjectWrapperIdentity = trackIdentity)
            .SetValue("objectHost", new TokenHost(JObject.Parse("""{ "a": 1 }""")))
            .SetValue("arrayHost", new TokenHost(JArray.Parse("[1]")));

        var obj = engine.Evaluate($"objectHost.{member}");
        var array = engine.Evaluate($"arrayHost.{member}");

        ReferenceEquals(engine.Evaluate($"objectHost.{member}"), obj).Should().BeTrue();
        ReferenceEquals(engine.Evaluate($"arrayHost.{member}"), array).Should().BeTrue();
        engine.Evaluate($"[objectHost.{member}.a, arrayHost.{member}[0]].join(',')").AsString().Should().Be("1,1");
    }

    /// <summary>
    /// What must not move: an object that is no list, declared as a class it derives from, is the ordinary object it
    /// was, and a list declared as a list interface keeps that interface's view.
    /// </summary>
    [Test]
    public void DeclaredViewsOfOrdinaryObjectsAreUnchanged()
    {
        var engine = new Engine().SetValue("host", new ViewHost());

        engine.Evaluate("""
            [
                host.Pet.Name,
                host.Pet.Bark(),
                Object.getPrototypeOf(host.Pet) === Object.prototype,
                host.Numbers.length,
                host.Numbers[1],
                Object.getPrototypeOf(host.Numbers) === Array.prototype
            ].join(',')
            """).AsString().Should().Be("rex,woof,true,2,2,true");
    }

    public class Pet
    {
        public string Name => "rex";
    }

    public sealed class Dog : Pet
    {
        public string Bark() => "woof";
    }

    public sealed class ViewHost
    {
        public Pet Pet { get; } = new Dog();

        public IList<int> Numbers { get; } = new List<int> { 1, 2 };
    }

    private static Engine CreateEngine(JToken token, string member, bool allowWrite)
    {
        var engine = new Engine(options => options.Interop.AllowWrite = allowWrite);
        engine.SetValue("host", new TokenHost(token));
        engine.Execute($"var x = host.{member};");
        return engine;
    }

    public sealed class TokenHost(JToken token)
    {
        public JToken TokenField = token;

        public object AsObject => TokenField;

        public JObject AsJObject => (JObject) TokenField;

        public JArray AsJArray => (JArray) TokenField;

        public JToken AsToken => TokenField;

        public JContainer AsContainer => (JContainer) TokenField;

        public IDictionary<string, JToken?> AsDictionary => (JObject) TokenField;

        public JToken GetToken() => TokenField;
    }
}

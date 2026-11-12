#nullable enable

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host object that answers reads itself, by overriding <c>Get</c>, can sit on an array's prototype chain and is asked
/// for an index the array does not hold, with the array as the receiver, the way OrdinaryGet asks a parent
/// (https://tc39.es/ecma262/#sec-ordinary-object-internal-methods-and-internal-slots-get-p-receiver). Jint used to probe
/// such a prototype for an own property instead, find none, and read past it to whatever its own prototype held.
/// </summary>
public class HostArrayPrototypeReadTests
{
    [Test]
    public void AnArraysHoleIsAnsweredByAHostPrototypeWithTheArrayAsReceiver()
    {
        var engine = new Engine();
        var host = new IndexAnsweringPrototype(engine);
        engine.SetValue("host", host);
        engine.Execute("""
            Object.setPrototypeOf(host, Array.prototype);
            Object.prototype[1] = 'object prototype';
            var arr = [1, , 3];
            Object.setPrototypeOf(arr, host);
            """);
        var array = engine.GetValue("arr").AsArray();

        engine.Evaluate("arr[1]").Should().Be("host 1");
        engine.Evaluate("var out = []; for (var v of arr) out.push(v); out.join()").Should().Be("1,host 1,3");
        engine.Evaluate("[...arr].join()").Should().Be("1,host 1,3");
        engine.Evaluate("(function ([a, b, c]) { return b; })(arr)").Should().Be("host 1");

        // the indexer is a plain read; TryGetValue asks HasProperty first, which this host answers too
        array[1].Should().Be("host 1");
        array.TryGetValue(1, out var inherited).Should().BeTrue();
        inherited.Should().Be("host 1");

        host.Receivers.Should().NotBeEmpty().And.OnlyContain(receiver => ReferenceEquals(receiver, array));
    }

    /// <summary>Answers, and reports, every array index below <see cref="Size"/>; anything else reads as ordinary.</summary>
    private sealed class IndexAnsweringPrototype(Engine engine) : ObjectInstance(engine)
    {
        private const uint Size = 8;

        public List<JsValue> Receivers { get; } = [];

        public override JsValue Get(JsValue property, JsValue receiver)
        {
            if (IsAnsweredIndex(property))
            {
                Receivers.Add(receiver);
                return "host " + property.AsString();
            }

            return base.Get(property, receiver);
        }

        public override bool HasProperty(JsValue property) => IsAnsweredIndex(property) || base.HasProperty(property);

        private static bool IsAnsweredIndex(JsValue property)
            => property.IsString() && uint.TryParse(property.AsString(), out var index) && index < Size;
    }
}

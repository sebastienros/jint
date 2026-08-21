#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The two event interfaces a <c>WebSocket</c> dispatches, as constructible interfaces in their own right:
/// <c>CloseEvent</c> (https://websockets.spec.whatwg.org/#the-closeevent-interface) and <c>MessageEvent</c>
/// (https://html.spec.whatwg.org/multipage/comms.html#messageevent).
/// </summary>
public class WebSocketEventTests
{
    private static Engine WebEngine() => new(options => options.UseWebSocket());

    [Fact]
    public void CloseEventDefaultsEveryMemberOfItsDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("var e = new CloseEvent('close');");

        engine.Evaluate("e.type").AsString().Should().Be("close");
        engine.Evaluate("e.wasClean").AsBoolean().Should().BeFalse();
        engine.Evaluate("e.code").AsNumber().Should().Be(0);
        engine.Evaluate("e.reason").AsString().Should().BeEmpty();
        engine.Evaluate("e.isTrusted").AsBoolean().Should().BeFalse("a script constructed it");
        engine.Evaluate("e instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(CloseEvent) === Event").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void CloseEventReadsItsDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("var e = new CloseEvent('close', { code: 3001, reason: 'bye', wasClean: true, bubbles: true });");

        engine.Evaluate("[e.code, e.reason, e.wasClean, e.bubbles].join(',')").AsString().Should().Be("3001,bye,true,true");
    }

    /// <summary>
    /// <c>code</c> is a plain <c>unsigned short</c>, so https://webidl.spec.whatwg.org/#js-unsigned-short
    /// wraps rather than clamping — unlike <c>close()</c>'s own <c>[Clamp]</c> argument.
    /// </summary>
    [Theory]
    [InlineData("65536", 0)]
    [InlineData("-1", 65535)]
    [InlineData("NaN", 0)]
    [InlineData("1000.9", 1000)]
    public void CloseEventWrapsItsCode(string code, double expected)
    {
        var engine = WebEngine();

        engine.Evaluate($"new CloseEvent('close', {{ code: {code} }}).code").AsNumber().Should().Be(expected);
    }

    [Fact]
    public void CloseEventRequiresAType()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CloseEvent()"))
            .Error.Get("name").AsString().Should().Be("TypeError");
    }

    [Fact]
    public void MessageEventDefaultsEveryMemberOfItsDictionary()
    {
        var engine = WebEngine();

        engine.Evaluate("var e = new MessageEvent('message');");

        engine.Evaluate("e.data").IsNull().Should().BeTrue("the IDL default is null, not undefined");
        engine.Evaluate("e.origin").AsString().Should().BeEmpty();
        engine.Evaluate("e.lastEventId").AsString().Should().BeEmpty();
        engine.Evaluate("e instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(MessageEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(e)").AsString().Should().Be("[object MessageEvent]");
    }

    [Fact]
    public void MessageEventCarriesAnyValueAsItsData()
    {
        var engine = WebEngine();

        engine.Evaluate("var payload = { a: 1 }; var e = new MessageEvent('message', { data: payload, origin: 'wss://x', lastEventId: '7' });");

        engine.Evaluate("e.data === payload").AsBoolean().Should().BeTrue();
        engine.Evaluate("e.origin").AsString().Should().Be("wss://x");
        engine.Evaluate("e.lastEventId").AsString().Should().Be("7");
    }

    /// <summary>
    /// Both interfaces' attributes are accessors that brand-check, as WebIDL specifies.
    /// </summary>
    [Fact]
    public void ThePrototypesAreNotInstances()
    {
        var engine = WebEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("CloseEvent.prototype.code"))
            .Error.Get("name").AsString().Should().Be("TypeError");
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("MessageEvent.prototype.data"))
            .Error.Get("name").AsString().Should().Be("TypeError");

        engine.Evaluate("Object.getOwnPropertyNames(new CloseEvent('x')).length").AsNumber().Should().Be(0);
    }

    /// <summary>
    /// Both are dispatchable like any other event, which is what makes them usable from script for something
    /// other than a socket.
    /// </summary>
    [Fact]
    public void BothCanBeDispatchedAtAnyEventTarget()
    {
        var engine = WebEngine();

        var result = engine.Evaluate("""
            (() => {
                const seen = [];
                const target = new EventTarget();
                target.addEventListener('close', e => seen.push(e.code + ':' + e.wasClean));
                target.addEventListener('message', e => seen.push(e.data));
                target.dispatchEvent(new CloseEvent('close', { code: 1000, wasClean: true }));
                target.dispatchEvent(new MessageEvent('message', { data: 'hi' }));
                return seen.join('|');
            })()
            """);

        result.AsString().Should().Be("1000:true|hi");
    }
}
#endif

#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins what <c>DefineOwnPropertyUnchecked</c> / <c>DefineOwnDataPropertyUnchecked</c> promise, which is what
/// their old names — <c>FastSetProperty</c> / <c>FastSetDataProperty</c> — did not: the write always creates
/// an <em>own</em> property, it shadows the prototype chain, it invokes no inherited setter, and it runs no
/// <c>[[DefineOwnProperty]]</c> validation, so it can never raise a <c>TypeError</c>.
///
/// <para>
/// Every assertion below is the difference from <c>Set</c> or <c>DefineOwnProperty</c>, which is the choice a
/// host is actually making when it reaches for one of these.
/// </para>
/// </summary>
public class HostUncheckedDefineTests
{
    [Test]
    public void AnUncheckedDefineShadowsThePrototypeAndInvokesNoInheritedSetter()
    {
        var engine = new Engine();
        engine.Execute("""
            var proto = {};
            var setterCalls = 0;
            Object.defineProperty(proto, 'member', {
                get: function () { return 'from-prototype'; },
                set: function (v) { setterCalls++; },
                configurable: true
            });
            var target = Object.create(proto);
            """);

        var target = engine.Evaluate("target").AsObject();
        target.Get("member").Should().Be("from-prototype");

        target.DefineOwnDataPropertyUnchecked("member", "own");

        engine.Evaluate("setterCalls").Should().Be(0);
        engine.Evaluate("target.member").Should().Be("own");
        engine.Evaluate("target.hasOwnProperty('member')").Should().Be(true);
        // the accessor is untouched and reappears once the shadow is deleted
        engine.Evaluate("delete target.member; target.member").Should().Be("from-prototype");
    }

    [Test]
    public void AnUncheckedDefineRedefinesANonConfigurableNonWritablePropertyWithoutThrowing()
    {
        var engine = new Engine();
        engine.Execute("var frozen = Object.freeze({ member: 'original' });");
        var frozen = engine.Evaluate("frozen").AsObject();

        // the checked route refuses, exactly as the specification says
        engine.Evaluate("""
            (function () {
                try { Object.defineProperty(frozen, 'member', { value: 'checked' }); return 'no throw'; }
                catch (e) { return e.constructor.name; }
            })()
            """).Should().Be("TypeError");

        frozen.DefineOwnPropertyUnchecked("member", new PropertyDescriptor("unchecked", writable: false, enumerable: true, configurable: false));

        engine.Evaluate("frozen.member").Should().Be("unchecked");
        engine.Evaluate("Object.isFrozen(frozen)").Should().Be(true);
    }

    [Test]
    public void AnUncheckedDefineAddsAPropertyToANonExtensibleObject()
    {
        var engine = new Engine();
        engine.Execute("var sealed_ = Object.preventExtensions({ existing: 1 });");
        var target = engine.Evaluate("sealed_").AsObject();

        engine.Evaluate("""
            (function () {
                'use strict';
                try { sealed_.added = 2; return 'no throw'; }
                catch (e) { return e.constructor.name; }
            })()
            """).Should().Be("TypeError");

        target.DefineOwnDataPropertyUnchecked("added", 2);

        engine.Evaluate("sealed_.added").Should().Be(2);
        engine.Evaluate("Object.isExtensible(sealed_)").Should().Be(false);
    }

    [Test]
    public void AnUncheckedDataDefineIsConfigurableEnumerableAndWritable()
    {
        var engine = new Engine();
        var target = new JsObject(engine);
        engine.SetValue("target", target);

        target.DefineOwnDataPropertyUnchecked("member", "value");

        engine.Evaluate("""JSON.stringify(Object.getOwnPropertyDescriptor(target, 'member'))""")
            .Should().Be("""{"value":"value","writable":true,"enumerable":true,"configurable":true}""");
    }

    [Test]
    public void TheKeyedOverloadTakesASymbol()
    {
        var engine = new Engine();
        var target = new JsObject(engine);
        engine.SetValue("target", target);

        var tag = engine.Evaluate("Symbol.toStringTag");
        target.DefineOwnPropertyUnchecked(tag, new PropertyDescriptor("Widget", writable: false, enumerable: false, configurable: true));

        engine.Evaluate("Object.prototype.toString.call(target)").Should().Be("[object Widget]");
        engine.Evaluate("Object.keys(target).length").Should().Be(0);
    }
}

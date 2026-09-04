using Jint.Browser.Dom;

namespace Jint.Tests.Browser;

/// <summary>
/// The object model the bindings put in front of a script: the prototype chain, the interface objects, the
/// brand checks, and what a DOM object looks like to reflection.
/// </summary>
public sealed class DomPrototypeTests
{
    private const string Page = "<div id='a'>x</div>";

    [Test]
    public void AnElementIsAnInstanceOfEveryInterfaceInItsChain()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("document.querySelector('#a') instanceof HTMLDivElement").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') instanceof HTMLElement").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') instanceof Element").Should().BeTrue();
        fixture.Bool("document.querySelector('#a') instanceof Node").Should().BeTrue();

        // The one that makes the whole chain worth building: the root is Jint's own EventTarget, so a DOM
        // node is an EventTarget the engine recognizes rather than one this package invented.
        fixture.Bool("document.querySelector('#a') instanceof EventTarget").Should().BeTrue();

        fixture.Bool("document.querySelector('#a') instanceof HTMLSpanElement").Should().BeFalse();
    }

    [Test]
    public void ThePrototypeChainIsTheInterfaceHierarchy()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("Object.getPrototypeOf(document.querySelector('#a')) === HTMLDivElement.prototype").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(HTMLDivElement.prototype) === HTMLElement.prototype").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(HTMLElement.prototype) === Element.prototype").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(Element.prototype) === Node.prototype").Should().BeTrue();
        fixture.Bool("Object.getPrototypeOf(Node.prototype) === EventTarget.prototype").Should().BeTrue();

        // A non-node interface roots at Object.prototype instead.
        fixture.Bool("Object.getPrototypeOf(NodeList.prototype) === Object.prototype").Should().BeTrue();
    }

    [Test]
    public void AnInterfaceObjectInheritsFromItsParentInterfaceObject()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://webidl.spec.whatwg.org/#interface-object — an inheriting interface's interface object has
        // its parent's as [[Prototype]], which is what lets a constant be read off a derived interface.
        fixture.Bool("Object.getPrototypeOf(HTMLDivElement) === HTMLElement").Should().BeTrue();
        fixture.Number("HTMLDivElement.ELEMENT_NODE").Should().Be(1);
    }

    [Test]
    public void APrototypeNamesItsInterfaceObjectAsItsConstructor()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Bool("HTMLDivElement.prototype.constructor === HTMLDivElement").Should().BeTrue();
        fixture.Bool("document.querySelector('#a').constructor === HTMLDivElement").Should().BeTrue();

        // Non-enumerable, per the interface-prototype-object algorithm.
        fixture.Bool("Object.keys(HTMLDivElement.prototype).includes('constructor')").Should().BeFalse();
        fixture.Bool("Object.getOwnPropertyNames(HTMLDivElement.prototype).includes('constructor')").Should().BeTrue();
    }

    [Test]
    public void ToStringTagIsTheInterfaceName()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("Object.prototype.toString.call(document.querySelector('#a'))").Should().Be("[object HTMLDivElement]");
        fixture.Text("Object.prototype.toString.call(document)").Should().Be("[object HTMLDocument]");
        fixture.Text("Object.prototype.toString.call(document.querySelector('#a').childNodes)").Should().Be("[object NodeList]");

        // AngleSharp types querySelectorAll as IHtmlCollection<IElement> where DOM §4.2.6 specifies a static
        // NodeList. The binding contains that return-type divergence rather than exposing HTMLCollection's
        // prototype and named-property semantics.
        fixture.Text("Object.prototype.toString.call(document.querySelectorAll('div'))").Should().Be("[object NodeList]");
    }

    [Test]
    public void AnInterfaceObjectIsNotConstructible()
    {
        using var fixture = DomTestFixture.Create(Page);

        // No [DomName] interface in the pinned assemblies carries [DomConstructor], and a browser answers the
        // same thing for `new HTMLDivElement()`.
        fixture.Text("try { new HTMLDivElement(); 'no throw'; } catch (e) { e.constructor.name + ': ' + e.message; }")
            .Should().Be("TypeError: Illegal constructor");

        fixture.Text("try { HTMLDivElement(); 'no throw'; } catch (e) { e.constructor.name + ': ' + e.message; }")
            .Should().Be("TypeError: Illegal constructor");
    }

    [Test]
    public void AMemberCalledOnAWrongReceiverIsAnIllegalInvocation()
    {
        using var fixture = DomTestFixture.Create(Page);

        fixture.Text("try { Element.prototype.getAttribute.call({}, 'id'); 'no throw'; } catch (e) { e.constructor.name + ': ' + e.message; }")
            .Should().Be("TypeError: Failed to execute 'Element.getAttribute': Illegal invocation");

        // An accessor is branded the same way.
        fixture.Text("try { Object.getOwnPropertyDescriptor(Element.prototype, 'id').get.call({}); 'no throw'; } catch (e) { e.constructor.name; }")
            .Should().Be("TypeError");

        // And a DOM object of the wrong interface is just as wrong a receiver as a plain object.
        fixture.Text("try { HTMLInputElement.prototype.__lookupGetter__('value').call(document.querySelector('#a')); 'no throw'; } catch (e) { e.constructor.name; }")
            .Should().Be("TypeError");
    }

    [Test]
    public void AnElementCarriesNoOwnPropertiesOfItsOwn()
    {
        using var fixture = DomTestFixture.Create(Page);

        // Everything lives on the shaped prototype, which is what keeps the wrapper on the engine's ordinary
        // access lane and its prototype on the inline caches.
        fixture.Number("Object.getOwnPropertyNames(document.querySelector('#a')).length").Should().Be(0);
        fixture.Number("Object.keys(document.querySelector('#a')).length").Should().Be(0);
    }

    [Test]
    public void EveryPrototypeIsBuiltInTheEnginesSharedShapeRepresentation()
    {
        using var fixture = DomTestFixture.Create(Page);
        var realm = DomRealm.Of(fixture.Engine);

        // The whole reason the members are declared through JsObjectShape rather than defined one by one: a
        // shaped prototype is a valid holder for the prototype-method inline cache, and a dictionary-mode one
        // is not. Engine.Advanced.HasSharedShape is the sanctioned assertion that the shaping happened.
        foreach (var definition in DomInterfaces.All)
        {
            var prototype = realm.PrototypeOf(definition);
            fixture.Engine.Advanced.HasSharedShape(prototype)
                .Should().BeTrue("{0}.prototype must stay in the shared-layout representation", definition.Name);
        }
    }

    [Test]
    public void EveryInterfaceObjectIsInstalledAsANonEnumerableGlobal()
    {
        using var fixture = DomTestFixture.Create(Page);

        // https://webidl.spec.whatwg.org/#es-interfaces — { writable: true, enumerable: false,
        // configurable: true }.
        fixture.Text("""
            var d = Object.getOwnPropertyDescriptor(globalThis, 'HTMLDivElement');
            [d.writable, d.enumerable, d.configurable].join(',');
            """).Should().Be("true,false,true");

        // An interface AngleSharp marked [DomNoInterfaceObject] gets a prototype but no global.
        fixture.Bool("typeof ParentNode === 'undefined'").Should().BeTrue();
        fixture.Bool("typeof ChildNode === 'undefined'").Should().BeTrue();
    }

    [Test]
    public void InstallingIsNonClobbering()
    {
        using var engine = new Engine(options => options.UseWebApis());
        engine.SetValue("Node", "the host's own");

        DomBindings.Install(engine);

        engine.Evaluate("Node").AsString().Should().Be("the host's own");
        engine.Evaluate("typeof Element").AsString().Should().Be("function");
    }
}

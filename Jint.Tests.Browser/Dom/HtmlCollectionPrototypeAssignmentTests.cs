namespace Jint.Tests.Browser.Dom;

public sealed class HtmlCollectionPrototypeAssignmentTests
{
    [TestCase(false, "root.children")]
    [TestCase(true, "root.children")]
    [TestCase(false, "root.getElementsByTagName('*')")]
    [TestCase(true, "root.getElementsByTagName('*')")]
    public void AnInheritingReceiverCanAssignItsOwnProperty(bool strict, string source)
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool($$"""
            (() => {
              {{(strict ? "'use strict';" : "")}}
              const root = document.createElement('div'), element = document.createElement('p');
              element.id = 'named'; root.appendChild(element);
              const collection = {{source}}, receiver = Object.create(collection);
              if (receiver.named !== element) return false;
              receiver.named = 'own';
              const descriptor = Object.getOwnPropertyDescriptor(receiver, 'named');
              if (descriptor.value !== 'own' || !descriptor.writable || !descriptor.enumerable || !descriptor.configurable) return false;
              if (collection.named !== element || collection.namedItem('named') !== element) return false;
              if (Reflect.set(collection, 'named', 'replace') || Reflect.set(collection, '0', 'replace', receiver)) return false;
              delete receiver.named;
              if (receiver.named !== element) return false;
              element.remove();
              return receiver.named === undefined && collection.namedItem('named') === null;
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void ReflectSetHonorsTheReceiversExistingProperty()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div id='root'><p id='named'></p></div>");
        fixture.Bool("""
            (() => {
              const collection = document.getElementById('root').children;
              const receiver = {};
              if (!Reflect.set(collection, 'named', 'own', receiver) || receiver.named !== 'own') return false;
              Object.defineProperty(receiver, 'named', { writable: false });
              if (Reflect.set(collection, 'named', 'replace', receiver)) return false;
              let calls = 0;
              const accessor = { set named(value) { calls++; } };
              return !Reflect.set(collection, 'named', 'replace', accessor) && calls === 0 &&
                !Reflect.set(collection, 'named', 'replace', Object.preventExtensions({}));
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void OrdinaryOwnDescriptorsAndPrototypeSettersStillApply()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div id='root'><p id='named'></p></div>");
        fixture.Bool("""
            (() => {
              const collection = document.getElementById('root').children, receiver = Object.create(collection);
              Object.defineProperty(collection, 'locked', { value: 'own' });
              if (Reflect.set(collection, 'locked', 'replace', receiver)) return false;
              let seenThis, seenValue;
              const prototype = Object.create(Object.getPrototypeOf(collection));
              Object.defineProperty(prototype, 'named', { set(value) { seenThis = this; seenValue = value; } });
              Object.setPrototypeOf(collection, prototype);
              receiver.named = 'assigned';
              return seenThis === receiver && seenValue === 'assigned' && !Object.hasOwn(receiver, 'named');
            })()
            """).Should().BeTrue();
    }
}

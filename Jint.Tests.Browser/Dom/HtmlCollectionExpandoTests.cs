namespace Jint.Tests.Browser.Dom;

/// <summary>An ordinary own property hides a later supported name, but never changes namedItem's lookup.</summary>
public sealed class HtmlCollectionExpandoTests
{
    [TestCase(false, "id", "root.children")]
    [TestCase(true, "id", "root.children")]
    [TestCase(false, "name", "root.children")]
    [TestCase(true, "name", "root.children")]
    [TestCase(false, "id", "root.getElementsByTagName('*')")]
    [TestCase(true, "id", "root.getElementsByTagName('*')")]
    [TestCase(false, "name", "root.getElementsByTagName('*')")]
    [TestCase(true, "name", "root.getElementsByTagName('*')")]
    public void AssignmentBeforeAnElementAppearsKeepsItsOwnValue(bool strict, string attribute, string source)
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool($$"""
            (() => {
              {{(strict ? "'use strict';" : "")}}
              const root = document.createElement('div'), collection = {{source}};
              collection.later = 'own';
              const element = document.createElement('span'); element.setAttribute('{{attribute}}', 'later');
              root.appendChild(element);
              if (collection.later !== 'own' || collection.namedItem('later') !== element || collection[0] !== element) return false;
              const descriptor = Object.getOwnPropertyDescriptor(collection, 'later');
              if (descriptor.value !== 'own' || !descriptor.writable || !descriptor.enumerable || !descriptor.configurable) return false;
              if (JSON.stringify(Object.getOwnPropertyNames(collection)) !== '["0","later"]' ||
                  JSON.stringify(Object.keys(collection)) !== '["0","later"]') return false;
              delete collection.later;
              return collection.later === element && collection.namedItem('later') === element &&
                JSON.stringify(Object.keys(collection)) === '["0"]';
            })()
            """).Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ANonConfigurableOwnPropertySurvivesTheLaterName(bool strict)
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool($$"""
            (() => {
              {{(strict ? "'use strict';" : "")}}
              const root = document.createElement('div'), collection = root.children;
              Object.defineProperty(collection, 'later', { value: 'own' });
              const element = document.createElement('span'); element.id = 'later'; root.appendChild(element);
              if (collection.later !== 'own' || collection.namedItem('later') !== element) return false;
              if (Reflect.deleteProperty(collection, 'later')) return false;
              const descriptor = Object.getOwnPropertyDescriptor(collection, 'later');
              return collection.later === 'own' && descriptor.value === 'own' &&
                !descriptor.configurable && !descriptor.writable && !descriptor.enumerable &&
                JSON.stringify(Object.getOwnPropertyNames(collection)) === '["0","later"]';
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void AnOwnAccessorIsNotInvokedByNameEnumerationOrNamedItem()
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool("""
            (() => {
              const root = document.createElement('div'), collection = root.children;
              let reads = 0;
              Object.defineProperty(collection, 'later', { configurable: true, get() { reads++; return 'own'; } });
              const element = document.createElement('span'); element.id = 'later'; root.appendChild(element);
              if (!Object.hasOwn(collection, 'later') || Object.getOwnPropertyNames(collection).filter(x => x === 'later').length !== 1) return false;
              if (collection.namedItem('later') !== element || reads !== 0) return false;
              if (collection.later !== 'own' || reads !== 1) return false;
              element.remove();
              return collection.namedItem('later') === null && collection.later === 'own' && reads === 2;
            })()
            """).Should().BeTrue();
    }
}

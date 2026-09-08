namespace Jint.Tests.Browser.Dom;

/// <summary>NamedNodeMap has LegacyUnenumerableNamedProperties, but not LegacyOverrideBuiltIns.</summary>
public sealed class NamedNodeMapVisibilityTests
{
    [TestCase("item")]
    [TestCase("setNamedItem")]
    [TestCase("toString")]
    public void PrototypeMembersWinWhileIndexedAndExplicitNamedAccessStillReturnTheAttribute(string name)
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool($$"""
            (() => {
              const element = document.createElementNS('urn:test', 'node');
              element.setAttributeNS(null, '{{name}}', 'value');
              const map = element.attributes, attribute = map[0];
              return typeof map['{{name}}'] === 'function' && map.item(0) === attribute &&
                map.getNamedItem('{{name}}') === attribute && !Object.hasOwn(map, '{{name}}') &&
                Object.getOwnPropertyDescriptor(map, '{{name}}') === undefined &&
                JSON.stringify(Object.getOwnPropertyNames(map)) === '["0"]' &&
                JSON.stringify(Object.keys(map)) === '["0"]';
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void PrototypeChangesAffectLookupAndNamesWithoutInvokingGettersDuringProbes()
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool("""
            (() => {
              const element = document.createElement('div'); element.setAttribute('value', 'native');
              const map = element.attributes, attribute = map[0];
              const proto = Object.create(NamedNodeMap.prototype); Object.setPrototypeOf(map, proto);
              if (map.value !== attribute || !Object.getOwnPropertyNames(map).includes('value')) return false;
              let reads = 0;
              Object.defineProperty(proto, 'value', { configurable: true, get() { reads++; return 'prototype'; } });
              if (Object.hasOwn(map, 'value') || Object.getOwnPropertyNames(map).includes('value') || reads !== 0) return false;
              if (map.value !== 'prototype' || reads !== 1) return false;
              delete proto.value;
              return map.value === attribute && Object.hasOwn(map, 'value') &&
                Object.getOwnPropertyNames(map).includes('value') &&
                !Object.getOwnPropertyDescriptor(map, 'value').enumerable;
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void RemovingAndRestoringThePrototypeKeepsTheAttributeAndIndicesLive()
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool("""
            (() => {
              const element = document.createElement('div'); element.setAttribute('item', 'native');
              const map = element.attributes, attribute = map[0], proto = Object.getPrototypeOf(map);
              Object.setPrototypeOf(map, null);
              if (map.item !== attribute || !Object.getOwnPropertyNames(map).includes('item')) return false;
              Object.setPrototypeOf(map, proto);
              if (typeof map.item !== 'function' || Object.getOwnPropertyNames(map).includes('item')) return false;
              element.removeAttribute('item');
              if (map.length !== 0 || map[0] !== undefined) return false;
              element.setAttribute('plain', 'new');
              return map.length === 1 && map[0] === map.plain &&
                JSON.stringify(Object.getOwnPropertyNames(map)) === '["0","plain"]';
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void AnExistingOwnPropertyWinsUntilItIsDeleted()
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool("""
            (() => {
              const element = document.createElement('div'), map = element.attributes;
              Object.defineProperty(map, 'value', { value: 'own', configurable: true });
              element.setAttribute('value', 'native');
              if (map.value !== 'own' || Object.getOwnPropertyNames(map).filter(x => x === 'value').length !== 1) return false;
              delete map.value;
              return map.value === map[0] && map.getNamedItem('value') === map[0];
            })()
            """).Should().BeTrue();
    }

    [Test]
    public void UnsupportedNamesAndNumericIndicesDoNotProbeThePrototype()
    {
        using var fixture = DomTestFixture.Create("<!doctype html>");
        fixture.Bool("""
            (() => {
              const element = document.createElement('div'); element.setAttribute('plain', 'native');
              const map = element.attributes, attribute = map[0];
              let probes = 0;
              Object.setPrototypeOf(map, new Proxy(Object.create(null), {
                getOwnPropertyDescriptor() { probes++; return undefined; }
              }));
              return !Object.hasOwn(map, 'missing') && Object.hasOwn(map, '0') && map[0] === attribute && probes === 0;
            })()
            """).Should().BeTrue();
    }

    [Test]
    public async Task TheWindowsNamedPropertiesObjectEndsTheVisibilitySearch()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<div id='probe'></div>");
        (await page.EvaluateAsync<bool>("""
            (() => {
              const element = document.createElementNS('urn:test', 'node');
              element.setAttributeNS(null, 'probe', 'named property');
              element.setAttributeNS(null, 'toString', 'farther prototype property');
              const map = element.attributes;
              const probe = map.getNamedItem('probe'), toString = map.getNamedItem('toString');
              const windowNames = Object.getPrototypeOf(Window.prototype);
              Object.setPrototypeOf(map, windowNames);
              return Object.hasOwn(windowNames, 'probe') &&
                map.probe === probe && map.toString === toString &&
                Object.hasOwn(map, 'probe') && Object.hasOwn(map, 'toString') &&
                Object.getOwnPropertyNames(map).includes('probe') &&
                Object.getOwnPropertyNames(map).includes('toString');
            })()
            """)).Should().BeTrue();
    }

}

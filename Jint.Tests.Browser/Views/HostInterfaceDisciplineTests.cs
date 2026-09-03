using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;

namespace Jint.Tests.Browser.Views;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// The interfaces the runtime owns rather than the generator, held to the same two rules every generated one
/// is: the prototype keeps a shared shape, and every member carries WebIDL's attributes for its kind.
/// </summary>
/// <remarks>
/// <c>DomPrototypeTests</c> and <c>WebIdlPropertyAttributeTests</c> walk <c>DomInterfaces.All</c>, which is
/// the generated registry; none of these five is in it, so they would otherwise be the one part of the
/// surface nothing checks — and a hand-written shape is exactly where the mistake is easiest to make.
/// </remarks>
public sealed class HostInterfaceDisciplineTests
{
    private static readonly string[] _interfaces =
    [
        "MutationObserver",
        "IntersectionObserver",
        "IntersectionObserverEntry",
        "ResizeObserver",
        "ResizeObserverEntry",
        "DOMParser",
        "XMLSerializer",
        "Selection",
        "MediaQueryListEvent",
        "MediaQueryList",
        "Geolocation",
        "Window",
    ];

    [Test]
    public async Task EveryRuntimeOwnedPrototypeKeepsItsSharedShape()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var unshaped = await page.RunOnLoopAsync(engine =>
        {
            var names = new List<string>();

            foreach (var name in _interfaces)
            {
                var prototype = ((ObjectInstance) engine.GetValue(name)).Get("prototype");
                if (!engine.Advanced.HasSharedShape((ObjectInstance) prototype))
                {
                    names.Add(name);
                }
            }

            return string.Join(", ", names);
        });

        unshaped.Should().BeEmpty("a shaped prototype is what makes the prototype-method inline cache valid");
    }

    [Test]
    public async Task EveryRuntimeOwnedMemberCarriesItsKindsAttributes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var violations = await page.RunOnLoopAsync(engine =>
        {
            var found = new List<string>();

            foreach (var name in _interfaces)
            {
                var prototype = (ObjectInstance) ((ObjectInstance) engine.GetValue(name)).Get("prototype");

                foreach (var key in prototype.GetOwnPropertyKeys())
                {
                    var descriptor = prototype.GetOwnProperty(key);
                    var member = name + ".prototype[" + key + "]";

                    // Symbol.toStringTag: { writable: false, enumerable: false, configurable: true }.
                    if (key.IsSymbol())
                    {
                        Check(found, member, descriptor.Writable, descriptor.Enumerable, descriptor.Configurable, false, false, true);
                        continue;
                    }

                    // https://webidl.spec.whatwg.org/#interface-prototype-object
                    if (key.AsString() == "constructor")
                    {
                        Check(found, member, descriptor.Writable, descriptor.Enumerable, descriptor.Configurable, true, false, true);
                        continue;
                    }

                    // https://webidl.spec.whatwg.org/#es-attributes — an accessor, enumerable and configurable.
                    if (descriptor.Get is not null || descriptor.Set is not null)
                    {
                        Check(found, member, null, descriptor.Enumerable, descriptor.Configurable, null, true, true);
                        continue;
                    }

                    // https://webidl.spec.whatwg.org/#es-operations — enumerable, which is the opposite of
                    // ECMAScript's rule for a built-in and the mistake a hand-written shape makes by default.
                    Check(found, member, descriptor.Writable, descriptor.Enumerable, descriptor.Configurable, true, true, true);
                }
            }

            return string.Join("\n", found);
        });

        violations.Should().BeEmpty();
    }

    [Test]
    public async Task TheInterfaceObjectsCarryTheirWebIdlAttributesToo()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        foreach (var name in _interfaces)
        {
            // https://webidl.spec.whatwg.org/#es-interfaces — an interface object is a non-enumerable,
            // writable, configurable property of the global, and its `prototype` is none of the three.
            var descriptor = await page.EvaluateAsync<string>(
                "(() => { const d = Object.getOwnPropertyDescriptor(globalThis, '" + name + "'); "
                + "return [d.writable, d.enumerable, d.configurable].join('|') })()");

            descriptor.Should().Be("true|false|true", "{0} is an interface object", name);

            var prototypeDescriptor = await page.EvaluateAsync<string>(
                "(() => { const d = Object.getOwnPropertyDescriptor(" + name + ", 'prototype'); "
                + "return [d.writable, d.enumerable, d.configurable].join('|') })()");

            prototypeDescriptor.Should().Be("false|false|false", "{0}.prototype is unforgeable", name);
        }
    }

    [Test]
    public async Task NoneOfTheGlobalsIsForcedIntoExistenceByTheInstaller()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        // Lazy: the property is there, and reading it is what builds the prototype. Enumerating the global's
        // own keys must not force any of them, which is what makes an engine that never mentions
        // ResizeObserver pay nothing for it.
        foreach (var name in _interfaces)
        {
            (await page.EvaluateAsync<bool>("Object.getOwnPropertyNames(globalThis).includes('" + name + "')"))
                .Should().BeTrue("{0} is installed as a global", name);
        }

        (await page.EvaluateAsync<bool>("Object.keys(globalThis).includes('MutationObserver')")).Should().BeFalse();
    }

    private static void Check(
        List<string> found,
        string member,
        bool? writable,
        bool enumerable,
        bool configurable,
        bool? expectedWritable,
        bool expectedEnumerable,
        bool expectedConfigurable)
    {
        if (expectedWritable is not null && writable != expectedWritable)
        {
            found.Add(member + ": writable is " + writable + ", expected " + expectedWritable);
        }

        if (enumerable != expectedEnumerable)
        {
            found.Add(member + ": enumerable is " + enumerable + ", expected " + expectedEnumerable);
        }

        if (configurable != expectedConfigurable)
        {
            found.Add(member + ": configurable is " + configurable + ", expected " + expectedConfigurable);
        }
    }
}

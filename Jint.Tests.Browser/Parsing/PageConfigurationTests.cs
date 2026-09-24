using System.Collections;
using AngleSharp;
using AngleSharp.Dom;
using Jint.Browser.Runtime.Parsing;

namespace Jint.Tests.Browser.Parsing;

public class PageConfigurationTests
{
    [Test]
    public void ConstructionAndRepeatedConsumptionDoNotReplayEarlierReplacements()
    {
        var input = new CountedServices(Configuration.Default.Services.ToArray());
        IConfiguration configuration = new Configuration(input);
        var marker = new Marker();
        for (var i = 0; i < 6; i++)
        {
            configuration = configuration.WithOnly<IMarker>(marker).MaterializeServices();
        }

        var constructionPasses = input.Passes;
        constructionPasses.Should().BeLessThanOrEqualTo(3);
        for (var i = 0; i < 10; i++)
        {
            configuration.Services.OfType<IMarker>().Single().Should().BeSameAs(marker);
        }
        input.Passes.Should().Be(constructionPasses);
    }

    [Test]
    public void SnapshotPreservesOrderIdentityAndUnresolvedContextCreators()
    {
        var first = new Marker();
        var second = new Marker();
        var calls = new List<IBrowsingContext>();
        Func<IBrowsingContext, IMarker> creator = context =>
        {
            calls.Add(context);
            return new Marker();
        };
        var source = new Configuration([first, creator, second]);
        var snapshot = source.MaterializeServices();
        var services = snapshot.Services.ToArray();
        services.Length.Should().Be(3);
        services[0].Should().BeSameAs(first);
        services[1].Should().BeSameAs(creator);
        services[2].Should().BeSameAs(second);
        calls.Should().BeEmpty();

        var creators = new Configuration([creator]).MaterializeServices();
        using var a = BrowsingContext.New(creators);
        using var b = BrowsingContext.New(creators);
        var serviceA = a.GetService<IMarker>();
        var serviceB = b.GetService<IMarker>();
        serviceA.Should().NotBeNull().And.NotBeSameAs(serviceB);
        calls.Should().Equal(a, b);
    }

    [Test]
    public void XmlRegistrationMutatesOnlyTheDocumentFactoryForThisConfiguration()
    {
        var first = new PageDocumentFactory();
        var second = new PageDocumentFactory();
        var a = Configuration.Default.WithOnly<IDocumentFactory>(first).MaterializeServices();
        var b = Configuration.Default.WithOnly<IDocumentFactory>(second).MaterializeServices();
        a = a.WithCss().MaterializeServices().WithXml().MaterializeServices();
        b = b.WithCss().MaterializeServices().WithXml().MaterializeServices();
        first.ReadXmlWithTheXmlParser();
        second.ReadXmlWithTheXmlParser();
        a.Services.OfType<IDocumentFactory>().Single().Should().BeSameAs(first);
        b.Services.OfType<IDocumentFactory>().Single().Should().BeSameAs(second);
        var creator = first.Unregister("text/xml");
        creator.Should().NotBeNull();
        second.Unregister("text/xml").Should().NotBeNull();
        first.Unregister("text/xml").Should().BeNull();
    }

    private interface IMarker;
    private sealed class Marker : IMarker;

    private sealed class CountedServices(object[] services) : IEnumerable<object>
    {
        internal int Passes { get; private set; }

        public IEnumerator<object> GetEnumerator()
        {
            Passes++;
            foreach (var service in services)
            {
                yield return service;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

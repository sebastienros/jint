#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// GetTemplateObject canonicalizes one template object per tagged-template Parse Node per realm
/// (https://tc39.es/ecma262/#sec-gettemplateobject). The realm's map is weak on both ends so that an engine
/// fed a fresh source per operation stops accumulating template objects forever (#4117), and these are the
/// per-site identity claims that weakness must not disturb. test262 pins them too - <c>cache-same-site</c>,
/// <c>cache-identical-source</c>, <c>template-object-template-map</c> - but the map is the part of them that
/// could plausibly regress, so they are worth a fast local signal as well.
/// </summary>
public class TaggedTemplateCacheTests
{
    private const string OneSite = "function t(s) { return s; } function f() { return t`head${1}tail`; } f();";

    [Test]
    public void TheSameSiteGivesTheSameObjectOnEveryEvaluation()
    {
        var engine = new Engine();
        var prepared = Engine.PrepareScript(OneSite);

        var first = engine.Evaluate(prepared);
        var second = engine.Evaluate(prepared);

        ReferenceEquals(first, second).Should().BeTrue("one Parse Node has one template object, however often it is reached");
    }

    [Test]
    public void ASiteReachedTwiceWithinOneEvaluationGivesTheSameObject()
    {
        var engine = new Engine();

        engine.Evaluate("function t(s) { return s; } function f() { return t`x`; } f() === f();")
            .AsBoolean().Should().BeTrue("a loop or a second call reaches the same site, not a new one");
    }

    [Test]
    public void TwoSitesWithIdenticalSourceGiveDifferentObjects()
    {
        var engine = new Engine();

        engine.Evaluate("function t(s) { return s; } t`x` === t`x`;")
            .AsBoolean().Should().BeFalse("the cache is by site, not by string contents");
    }
}

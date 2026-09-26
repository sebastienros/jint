#nullable enable

using Jint.Browser.Dom;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeDormantMediaTests
{
    [Test]
    public void DormantMediaPrototypesRejectDirectAndForgedReceiversBeforeCoercion()
    {
        using var engine = new Engine(options => options.UseWebApis());
        DomBindings.Install(engine);
        engine.Execute("""
            var conversions = 0;
            var unexpected = [];
            var argument = { valueOf() { conversions++; return 1; }, toString() { conversions++; return 'x'; } };
            function requiresTypeError(operation) {
                try { operation(); unexpected.push('success'); }
                catch (error) { if (!(error instanceof TypeError)) unexpected.push(error.name); }
            }
            for (var receiver of [{}, Object.create(CanvasRenderingContext2D.prototype)]) {
                requiresTypeError(() => Object.getOwnPropertyDescriptor(CanvasRenderingContext2D.prototype, 'canvas').get.call(receiver));
                requiresTypeError(() => Object.getOwnPropertyDescriptor(CanvasRenderingContext2D.prototype, 'width').set.call(receiver, argument));
                requiresTypeError(() => CanvasRenderingContext2D.prototype.save.call(receiver));
            }
            for (var receiver of [{}, Object.create(TextTrackCue.prototype)]) {
                requiresTypeError(() => TextTrackCue.prototype.getCueAsHTML.call(receiver));
                requiresTypeError(() => Object.getOwnPropertyDescriptor(TextTrackCue.prototype, 'text').set.call(receiver, argument));
                requiresTypeError(() => Object.getOwnPropertyDescriptor(TextTrackCue.prototype, 'endTime').set.call(receiver, argument));
            }
            requiresTypeError(() => new CanvasRenderingContext2D());
            requiresTypeError(() => new TextTrackCue());
            """);
        engine.Evaluate("conversions").Should().Be(0);
        engine.Evaluate("JSON.stringify(unexpected)").AsString().Should().Be("[]");
        engine.Evaluate("CanvasRenderingContext2D.prototype.save.length").Should().Be(0);
        engine.Evaluate("TextTrackCue.prototype.getCueAsHTML.length").Should().Be(0);
        engine.Evaluate("Object.getOwnPropertyDescriptor(CanvasRenderingContext2D.prototype, 'canvas').enumerable").Should().BeTrue();
        engine.Evaluate("Object.getOwnPropertyDescriptor(TextTrackCue.prototype, 'text').configurable").Should().BeTrue();
    }
}

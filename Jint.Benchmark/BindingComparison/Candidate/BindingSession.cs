// Explicitly use the public, non-contract representation diagnostic to assert this spike stays shaped.
#pragma warning disable JINT0001

using AngleSharp;
using AngleSharp.Dom;
using Jint;
using Jint.Native;
using Jint.Native.Object;

namespace BindingComparison;

/// <summary>A deliberately narrow public-API spike, not the generated Jint.Browser binding surface.</summary>
internal sealed class BindingSession : IDisposable
{
    internal const string Arm = "main hand-shaped Node/Element/Document";
    private readonly IBrowsingContext _context;
    private readonly Dictionary<INode, ObjectInstance> _wrappers = new(ReferenceEqualityComparer.Instance);
    private readonly ObjectInstance _nodePrototype;
    private readonly ObjectInstance _elementPrototype;
    private readonly ObjectInstance _documentPrototype;
    internal Engine Engine { get; } = new();

    private static readonly JsObjectShape NodeShape = new JsObjectShape.Builder()
        .Accessor("nodeType", static (t, _) => (int) State(t).Node.NodeType)
        .Accessor("nodeName", static (t, _) => State(t).Node.NodeName)
        .Build();
    private static readonly JsObjectShape ElementShape = new JsObjectShape.Builder()
        .Accessor("id", static (t, _) => ((IElement) State(t).Node).Id ?? "")
        .Method("getAttribute", static (t, a) => ((IElement) State(t).Node).GetAttribute(a[0].AsString()) is { } value ? value : JsValue.Null, length: 1)
        .Method("setAttribute", static (t, a) =>
        {
            ((IElement) State(t).Node).SetAttribute(a[0].AsString(), a[1].AsString());
            return JsValue.Undefined;
        }, length: 2)
        .Build();
    private static readonly JsObjectShape DocumentShape = new JsObjectShape.Builder()
        .Accessor("documentElement", static (t, _) =>
        {
            var state = State(t);
            return state.Session.Wrap(((IDocument) state.Node).DocumentElement);
        })
        .Method("getElementById", static (t, a) =>
        {
            var state = State(t);
            return state.Session.Wrap(((IDocument) state.Node).GetElementById(a[0].AsString()));
        }, length: 1)
        .Build();
    private static readonly JsObjectShape InstanceShape = new JsObjectShape.Builder().Build();

    private BindingSession()
    {
        _context = BrowsingContext.New(Configuration.Default);
        var document = _context.OpenAsync(request => request.Content(Workloads.Html)).GetAwaiter().GetResult();
        _nodePrototype = NodeShape.Instantiate(Engine);
        _elementPrototype = ElementShape.Instantiate(Engine, _nodePrototype);
        _documentPrototype = DocumentShape.Instantiate(Engine, _nodePrototype);
        Engine.SetValue("document", Wrap(document));
    }
    internal void Validate()
    {
        // Materialize a descriptor without invoking a getter on the prototype. Validation happens in
        // GlobalSetup, so neither forcing this layout nor the diagnostic enters a measured operation.
        _nodePrototype.GetOwnProperty("nodeType");
        _elementPrototype.GetOwnProperty("id");
        _documentPrototype.GetOwnProperty("documentElement");
        foreach (var prototype in new[] { _nodePrototype, _elementPrototype, _documentPrototype })
        {
            if (Engine.Diagnostics.GetObjectRepresentation(prototype) != ObjectRepresentation.SharedBuiltinLayout)
                throw new InvalidOperationException("The candidate prototype did not engage the shared shape lane.");
        }
    }
    private static HostState State(JsValue receiver) => (HostState) JsObjectShape.GetHostState(receiver.AsObject())!;
    private JsValue Wrap(INode? node)
    {
        if (node is null) return JsValue.Null;
        if (_wrappers.TryGetValue(node, out var existing)) return existing;
        var prototype = node is IDocument ? _documentPrototype : node is IElement ? _elementPrototype : _nodePrototype;
        var wrapper = InstanceShape.Instantiate(Engine, prototype);
        JsObjectShape.SetHostState(wrapper, new HostState(this, node));
        _wrappers.Add(node, wrapper);
        return wrapper;
    }
    private sealed record HostState(BindingSession Session, INode Node);
    internal static BindingSession Create() => new();
    public void Dispose()
    {
        Engine.Dispose();
        _context.Dispose();
    }
}

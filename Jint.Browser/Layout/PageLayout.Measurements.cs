using Jint.Browser.Styling;
using Jint.HtmlParser;

namespace Jint.Browser.Layout;

internal sealed partial class PageLayout
{
    private SizeMeasurements? _measurements;
    private object? _measurementPlan;
    private Document? _observedDocument;
    private ulong _observedStamp;
    private readonly HashSet<DirtyMeasurement> _dirtyMeasurements = new();
    private bool _unknownMeasurementWrite;
    private Action<Node?, NativeMutationKind, string?, Node?>? _mutationSignal;

    private readonly record struct DirtyMeasurement(Node Target, NativeMutationKind Kind, string? Attribute, Node? ChangedChild);

    private void ObserveMeasurements(Document document)
    {
        if (ReferenceEquals(_observedDocument, document)) return;
        if (_observedDocument is not null) _observedDocument.MutationSignal -= _mutationSignal;
        _observedDocument = document;
        _observedStamp = document.MutationStamp;
        if (_mutationSignal is null)
        {
            var weak = new WeakReference<PageLayout>(this);
            _mutationSignal = (target, kind, attribute, child) =>
            {
                if (weak.TryGetTarget(out var layout)) layout.RecordMeasurementMutation(target, kind, attribute, child);
            };
        }
        document.MutationSignal += _mutationSignal;
    }

    private void RecordMeasurementMutation(Node? target, NativeMutationKind kind, string? attribute, Node? changedChild)
    {
        // Synchronous and bounded: native writers never walk styles, call script, or run a
        // checkpoint here. Dirty subtrees are processed at the next budgeted geometry read.
        _observedStamp = _observedDocument!.MutationStamp;
        RetireQueries();
        if (_measurements is null) return;
        if (target is null && kind == NativeMutationKind.Attributes) return;
        if (target is null || kind == NativeMutationKind.Unknown || target is Document || _dirtyMeasurements.Count >= 64)
        {
            _unknownMeasurementWrite = true;
            _dirtyMeasurements.Clear();
        }
        else if (!_unknownMeasurementWrite)
        {
            _dirtyMeasurements.Add(new(target, kind, attribute, changedChild));
        }
    }

    private void ResetMeasurements(string reason = "reset")
    {
        if (_measurements is not null) Diagnostics?.MeasurementsDiscarded(reason);
        _measurements = null;
        _measurementPlan = null;
        _dirtyMeasurements.Clear();
        _unknownMeasurementWrite = false;
    }

    private FlatLayout.SizeQuery RetainedSizes(NativeCssReadContext context)
    {
        try
        {
            context.Verify();
            var plan = context.Query.MeasurementPlan;
            if (_unknownMeasurementWrite || !ReferenceEquals(_measurementPlan, plan))
            {
                if (_measurements is not null)
                    Diagnostics?.MeasurementsDiscarded(_unknownMeasurementWrite ? "unknown-write" : "rule-plan");
                _measurements = null;
            }
            if (_measurements is not null)
            {
                foreach (var dirty in _dirtyMeasurements)
                {
                    var root = dirty.Target as Element ?? dirty.Target.ParentNode as Element;
                    var globalDependency = context.Query.RequiresGlobalMeasurements(dirty.Kind, dirty.Attribute);
                    if (globalDependency
                        || root is null && dirty.Kind != NativeMutationKind.CharacterData
                            && (dirty.Target is not DocumentFragment || dirty.Target is ShadowRoot))
                    {
                        Diagnostics?.MeasurementsDiscarded(globalDependency ? $"selector-{dirty.Kind}" : $"target-{dirty.Target.GetType().Name}");
                        _measurements = null;
                        break;
                    }
                    var measurements = _measurements;
                    if (root is not null)
                    {
                        if (dirty.Kind == NativeMutationKind.ChildList)
                            measurements.InvalidateChildren(root, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
                        else
                            measurements.Invalidate(root, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
                    }
                    context.Verify();
                    if (dirty.ChangedChild is Element changed)
                        measurements.Invalidate(changed, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
                    context.Verify();
                }
            }
            context.Verify();
            var retained = _measurements is not null;
            var sizes = context.MeasureSizes(_measurements ?? new(), Diagnostics);
            _measurements = sizes.Measurements;
            _measurementPlan = plan;
            _unknownMeasurementWrite = false;
            _dirtyMeasurements.Clear();
            ObserveMeasurements(_runtime.Document!);
            if (retained) Diagnostics?.MeasurementsRetained();
            return sizes;
        }
        catch
        {
            ResetMeasurements();
            throw;
        }
    }
}

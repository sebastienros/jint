namespace Jint.HtmlParser.Css.Selectors;

// Invocation-owned. Passing this value by ref preserves native work's polling remainder.
internal struct SelectorMatchWork
{
    private readonly Node? _root;
    private readonly Document? _document;
    private readonly ulong _stamp;
    private readonly CancellationToken _token;
    private HtmlDisabledWork _native;
    private Cell? _cell;
    private bool _active;
    internal const string Invalidated = "The native selector view was invalidated by mutation.";
    internal const string AlreadyActive = "The native selector invocation is already active.";

    internal SelectorMatchWork(Node observationRoot, CancellationToken cancellationToken, Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(observationRoot);
        cancellationToken.ThrowIfCancellationRequested();
        _root = observationRoot;
        _document = observationRoot as Document ?? observationRoot.OwnerDocument;
        _stamp = _document?.MutationStamp ?? 0;
        _token = cancellationToken;
        _native = new HtmlDisabledWork(cancellationToken);
        if (checkpoint is not null)
        {
            _cell = new Cell(observationRoot, _document, _stamp, checkpoint, cancellationToken);
            _cell.Native = new HtmlDisabledWork(cancellationToken, _cell.Poll);
        }
        Verify();
    }

    internal CancellationToken Token => _token;
    internal Action? Checkpoint => _cell?.CheckpointAdapter;
    private void Verify()
    {
        if (_root is null) throw new InvalidOperationException("Uninitialized selector work.");
        _token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_root as Document ?? _root.OwnerDocument, _document) ||
            _document is not null && (_stamp == ulong.MaxValue || _document.MutationStamp != _stamp))
            throw new InvalidOperationException(Invalidated);
        _cell?.Verify();
    }
    internal void Step()
    {
        if (_root is null) throw new InvalidOperationException("Uninitialized selector work.");
        if (_cell is { InCheckpoint: true }) throw new InvalidOperationException(AlreadyActive);
        if (_cell is { } cell) cell.Step();
        else _native.Step();
    }
    internal void VerifyRead() => Verify();

    internal void Check()
    {
        Verify();
        _cell?.Check();
    }
    internal void Enter(Node node, Node? scope, in SelectorEnvironment environment)
    {
        Verify();
        if (_active || _cell is { Active: true } or { InCheckpoint: true })
            throw new InvalidOperationException(AlreadyActive);
        if (environment.Document is null)
        {
            if (environment.FocusedElement is not null || environment.PointerPressTarget is not null ||
                environment.TargetElement is not null) throw new ArgumentException("Environment seeds require a document.");
        }
        else if (environment.FocusedElement is { } focus && focus.OwnerDocument != environment.Document ||
                 environment.PointerPressTarget is { } press && press.OwnerDocument != environment.Document ||
                 environment.TargetElement is { } target && target.OwnerDocument != environment.Document)
            throw new ArgumentException("Environment seeds must belong to its document.");
        Observe(node);
        if (scope is not null) Observe(scope);
        _active = true;
        if (_cell is { } cell) cell.Active = true;
    }
    internal void Exit()
    {
        _active = false;
        if (_cell is { } cell) cell.Active = false;
    }
    internal void Observe(Node node)
    {
        Verify();
        if (ReferenceEquals(node, _root)) return;
        // Additional identities are retained only when necessary. A document stamp
        // covers all ordinary reads; retain explicit roots to detect adoption too.
        EnsureCell().Observe(node);
    }
    internal Cell EnsureCell()
    {
        Verify();
        if (_cell is null)
        {
            _cell = new Cell(_root!, _document, _stamp, null, _token) { Native = _native, Active = _active };
            _native = default;
        }
        return _cell;
    }
    internal HtmlDisabledState DisabledState(Element element)
    {
        if (_cell is { } cell) return HtmlDisabledness.GetState(element, ref cell.Native);
        return HtmlDisabledness.GetState(element, ref _native);
    }
    internal HtmlRequiredState RequiredState(Element element)
    {
        if (_cell is { } cell) return HtmlRequiredness.GetState(element, ref cell.Native);
        return HtmlRequiredness.GetState(element, ref _native);
    }

    // Begin once per fresh helper invocation/context, not once per element's multi-helper read.
    // The cached adapter translates that helper's cumulative counts into shared selector work.
    internal Action<int> BeginControlProducerRead()
    {
        var cell = EnsureCell();
        cell.BeginProducerRead();
        return cell.ProducerCheckpoint;
    }

    internal bool MatchCheckable(Element element, bool indeterminate)
    {
        var cell = EnsureCell();
        cell.BeginProducerRead();
        var matched = indeterminate
            ? HtmlCheckableState.MatchesIndeterminate(element, cell.ProducerCheckpoint, _token)
            : HtmlCheckableState.MatchesChecked(element, cell.ProducerCheckpoint, _token);
        Verify();
        return matched;
    }
    internal HtmlSelectMetadata SelectMetadata(Element element)
    {
        var cell = EnsureCell();
        cell.BeginProducerRead();
        var context = new HtmlSelectWorkContext(cell.ProducerCheckpoint, _token);
        var work = new HtmlSelectWork(element.OwnerDocument?.SelectWorkProbe, context, _token);
        var metadata = HtmlSelectMetadata.Read(element.Attributes, ref work);
        Verify();
        return metadata;
    }

    internal sealed class Cell : ISlotQueryWork
    {
        internal HtmlDisabledWork Native;
        internal bool Active;
        internal bool InCheckpoint;
        private readonly Node _root;
        private readonly Document? _document;
        private readonly ulong _stamp;
        private readonly CancellationToken _token;
        private readonly Action? _checkpoint;
        private List<(Node Node, Document? Document, ulong Stamp)>? _observations;
        internal HashSet<Element>? Focus;
        internal HashSet<Element>? FocusWithin;
        internal HashSet<Element>? ActiveElements;
        internal bool TargetResolved;
        internal Element? Target;
        internal Cell(Node root, Document? document, ulong stamp, Action? checkpoint, CancellationToken token)
        {
            _root = root;
            _document = document;
            _stamp = stamp;
            _token = token;
            _checkpoint = checkpoint;
        }
        internal Action? CheckpointAdapter => _checkpoint is null ? null : Check;
        private int _producerUnits;
        private Action<int>? _producerCheckpoint;
        internal Action<int> ProducerCheckpoint => _producerCheckpoint ??= ProducerPoll;
        internal void BeginProducerRead()
        {
            Verify();
            _producerUnits = 0;
        }
        // Helpers report cumulative counts. Every delta joins the selector invocation's counter;
        // resetting the cursor at a new helper call preserves short reads' accumulated work.
        private void ProducerPoll(int units)
        {
            var delta = units > _producerUnits ? units - _producerUnits : 0;
            _producerUnits = units;
            while (delta-- > 0) Step();
            Check();
        }

        internal void Observe(Node node)
        {
            if (ReferenceEquals(node, _root)) return;
            _observations ??= [];
            foreach (var observation in _observations)
                if (ReferenceEquals(observation.Node, node)) return;
            var document = node as Document ?? node.OwnerDocument;
            _observations.Add((node, document, document?.MutationStamp ?? 0));
            Verify();
        }
        internal void Verify()
        {
            _token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_root as Document ?? _root.OwnerDocument, _document) ||
                _document is not null && (_stamp == ulong.MaxValue || _document.MutationStamp != _stamp))
                throw new InvalidOperationException(Invalidated);
            if (_observations is null) return;
            foreach (var (node, document, stamp) in _observations)
                if (!ReferenceEquals(node as Document ?? node.OwnerDocument, document) ||
                    document is not null && (stamp == ulong.MaxValue || document.MutationStamp != stamp))
                    throw new InvalidOperationException(Invalidated);
        }
        internal void Poll(int _) => Check();
        public void Step() => Native.Step();
        // Existing table-native test checkpoints run on each grid step. Preserve
        // that seam while charging the same native selector counter first.
        internal void TableStep()
        {
            Native.Step();
            Check();
        }
        public void Check()
        {
            Verify();
            if (_checkpoint is null) return;
            if (InCheckpoint) throw new InvalidOperationException(AlreadyActive);
            InCheckpoint = true;
            try { _checkpoint(); }
            finally { InCheckpoint = false; }
            Verify();
        }
    }
}

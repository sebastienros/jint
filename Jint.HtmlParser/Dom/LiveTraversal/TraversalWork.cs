namespace Jint.HtmlParser;

// Invocation-local work, shared by ref across nested native reads. A checkpoint is
// a trusted host budget check: it must not run author code or mutate/reenter DOM.
// Ordinary traversal filters have their own mutation semantics and do not use
// these read observations. No callback or captured state survives the invocation.
internal struct TraversalWork
{
    private readonly CancellationToken _token;
    private readonly Action<int>? _checkpoint;
    private readonly ReadObservation _first;
    private readonly ReadObservation _second;
    private readonly ReadObservation _third;
    private readonly ReadObservation _fourth;
    private readonly DomRange? _range;
    private readonly DomRange? _source;
    private readonly BoundaryPoint _start;
    private readonly BoundaryPoint _end;
    private readonly BoundaryPoint _sourceStart;
    private readonly BoundaryPoint _sourceEnd;
    private int _count;

    internal TraversalWork(CancellationToken token) : this(null, token) { }

    internal TraversalWork(Action<int>? checkpoint, CancellationToken token)
        : this(default, default, checkpoint, token) { }

    internal TraversalWork(DomNodeIdentity first, DomNodeIdentity second,
        Action<int>? checkpoint, CancellationToken token)
    {
        this = default;
        _token = token;
        _checkpoint = checkpoint;
        if (checkpoint is not null)
        {
            _first = new(first);
            _second = new(second);
        }
        Check();
    }

    internal TraversalWork(DomRange range, DomRange? source, DomNodeIdentity node,
        Action<int>? checkpoint, CancellationToken token)
    {
        this = default;
        _token = token;
        _checkpoint = checkpoint;
        if (checkpoint is not null)
        {
            _range = range;
            _source = source;
            _start = range.Start;
            _end = range.End;
            _sourceStart = source?.Start ?? default;
            _sourceEnd = source?.End ?? default;
            _first = new(_start.Container);
            _second = new(_end.Container);
            _third = new(source is null ? node : _sourceStart.Container);
            _fourth = new(_sourceEnd.Container);
        }
        Check();
    }

    internal void Step() { if ((++_count & 255) == 0) Check(); }

    internal readonly void Check()
    {
        _token.ThrowIfCancellationRequested();
        if (_checkpoint is not null)
        {
            Verify();
            _checkpoint(_count);
            // A callback exception passes through untouched; never replace a host
            // budget sentinel (or its cancellation token) with invalidation.
            _token.ThrowIfCancellationRequested();
            Verify();
        }
    }

    private readonly void Verify()
    {
        _first.Verify();
        _second.Verify();
        _third.Verify();
        _fourth.Verify();
        if (_range is not null && (_range.Start != _start || _range.End != _end) ||
            _source is not null && (_source.Start != _sourceStart || _source.End != _sourceEnd))
            throw new InvalidOperationException("The native range read was invalidated by mutation.");
    }

    private readonly struct ReadObservation
    {
        private readonly DomNodeIdentity _identity;
        private readonly Document? _document;
        private readonly ulong _stamp;

        internal ReadObservation(DomNodeIdentity identity)
        {
            _identity = identity;
            _document = identity.IsValid ? LiveTraversalTracking.DocumentOf(identity) : null;
            _stamp = _document?.MutationStamp ?? 0;
        }

        internal void Verify()
        {
            // Owner stamps also cover detached ordinary roots, template contents
            // and shadow trees. Capture ownership as well: adoption must not let a
            // read continue under a different document's unchanged stamp.
            if (_identity.IsValid && !ReferenceEquals(LiveTraversalTracking.DocumentOf(_identity), _document) ||
                _document is not null && (_stamp == ulong.MaxValue || _document.MutationStamp != _stamp))
                throw new InvalidOperationException("The native range read was invalidated by mutation.");
        }
    }
}

namespace Jint.HtmlParser.Css.Values.References;

internal enum CssSubstitutionBindingKind { Uninitialized, Specified, Computed, Invalid, Pending }

internal readonly struct CssSubstitutionBinding
{
    private readonly CssReferenceInput? _input;
    private readonly CssSubstitutedValue? _value;
    private readonly string? _feature;
    private readonly bool _animationTainted;

    private CssSubstitutionBinding(CssSubstitutionBindingKind kind, string name,
        CssReferenceInput? input, CssSubstitutedValue? value, bool animationTainted, string? feature, CssSubstitutionSnapshot? scope = null)
    {
        Kind = kind;
        Name = name;
        _input = input;
        _value = value;
        _animationTainted = animationTainted;
        Scope = scope;
        _feature = feature;
    }

    internal CssSubstitutionSnapshot? Scope { get; }
    internal CssSubstitutionBinding WithScope(CssSubstitutionSnapshot scope) =>
        new(Kind, Name, _input, _value, _animationTainted, _feature, scope);

    internal CssSubstitutionBindingKind Kind { get; }
    internal string Name { get; }
    internal CssReferenceInput Input => Kind == CssSubstitutionBindingKind.Specified
        ? _input! : throw new InvalidOperationException();
    internal CssSubstitutedValue Value => Kind == CssSubstitutionBindingKind.Computed
        ? _value! : throw new InvalidOperationException();
    internal bool AnimationTainted => Kind is CssSubstitutionBindingKind.Specified or
        CssSubstitutionBindingKind.Computed or CssSubstitutionBindingKind.Invalid
        ? _animationTainted : throw new InvalidOperationException();
    internal string PendingFeature => Kind == CssSubstitutionBindingKind.Pending
        ? _feature! : throw new InvalidOperationException();

    internal static CssSubstitutionBinding Specified(string name, CssReferenceInput input,
        bool animationTainted) => new(CssSubstitutionBindingKind.Specified,
            CssSubstitutionArguments.RequireCustomName(name),
            input ?? throw new ArgumentNullException(nameof(input)), null, animationTainted, null);

    internal static CssSubstitutionBinding Computed(string name, CssSubstitutedValue value,
        bool animationTainted) => new(CssSubstitutionBindingKind.Computed,
            CssSubstitutionArguments.RequireCustomName(name), null,
            value ?? throw new ArgumentNullException(nameof(value)), animationTainted, null);

    internal static CssSubstitutionBinding Invalid(string name, bool animationTainted) =>
        new(CssSubstitutionBindingKind.Invalid, CssSubstitutionArguments.RequireCustomName(name),
            null, null, animationTainted, null);

    internal static CssSubstitutionBinding Pending(string name, string feature) =>
        new(CssSubstitutionBindingKind.Pending, CssSubstitutionArguments.RequireCustomName(name),
            null, null, false,
            !string.IsNullOrEmpty(feature) ? feature : throw new ArgumentException("A feature is required.", nameof(feature)));
}

internal sealed class CssSubstitutionSnapshot
{
    private readonly CssSubstitutionBinding[] _bindings;
    private readonly CssSubstitutionSnapshot? _parent;
    private readonly Dictionary<uint, List<int>> _buckets;
    private readonly ICssQueryBindingResolver? _queryResolver;
    internal bool IsQueryBound => _queryResolver is not null;

    private CssSubstitutionSnapshot(CssSubstitutionBinding[] bindings, Dictionary<uint, List<int>> buckets,
        CssSubstitutionSnapshot? parent, ICssQueryBindingResolver? queryResolver = null)
    {
        _bindings = bindings;
        _buckets = buckets;
        _parent = parent;
        _queryResolver = queryResolver;
    }

    internal static CssSubstitutionSnapshot Create(ReadOnlySpan<CssSubstitutionBinding> bindings,
        CssValueWork work) => CreateLayer(bindings, null, work);

    // Immutable ancestry preserves the scope where an inherited variable was specified.
    internal static CssSubstitutionSnapshot CreateLayer(ReadOnlySpan<CssSubstitutionBinding> bindings,
        CssSubstitutionSnapshot? parent, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        if (parent is { IsQueryBound: true })
            throw new ArgumentException("Frozen variable snapshots cannot retain a query-bound parent.", nameof(parent));
        var copy = new CssSubstitutionBinding[bindings.Length];
        var buckets = new Dictionary<uint, List<int>>();
        work.CheckCancellation();
        for (var i = 0; i < bindings.Length; i++)
        {
            work.Charge(1);
            var binding = bindings[i];
            if (binding.Scope is { IsQueryBound: true })
                throw new ArgumentException("Frozen variable snapshots cannot retain a query-bound defining scope.", nameof(bindings));
            if (binding.Kind == CssSubstitutionBindingKind.Uninitialized ||
                !CssSubstitutionArguments.IsCustomName(binding.Name))
                throw new ArgumentException("A valid binding is required.", nameof(bindings));
            var hash = CssSubstitutionArguments.Hash(binding.Name, work);
            if (!buckets.TryGetValue(hash, out var bucket))
            {
                work.CheckCancellation();
                bucket = new List<int>();
                buckets.Add(hash, bucket);
            }
            foreach (var index in bucket)
            {
                work.Charge(1);
                if (CssSubstitutionArguments.Equals(binding.Name, copy[index].Name, work))
                    throw new ArgumentException("Duplicate custom-property name.", nameof(bindings));
            }
            copy[i] = binding;
            work.CheckCancellation();
            bucket.Add(i);
        }
        work.CheckCancellation();
        return new CssSubstitutionSnapshot(copy, buckets, parent);
    }

    // Invocation-affine adapter only. Existing factories remain immutable/shareable and callback-free.
    internal static CssSubstitutionSnapshot CreateQueryLayer(CssSubstitutionSnapshot? parent,
        ICssQueryBindingResolver localResolver)
        => new([], new Dictionary<uint, List<int>>(), parent,
            localResolver ?? throw new ArgumentNullException(nameof(localResolver)));

    internal bool TryGet(string name, CssValueWork work, out CssSubstitutionBinding binding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(work);
        CssSubstitutionArguments.RequireCustomName(name);
        work.CheckCancellation();
        var hash = CssSubstitutionArguments.Hash(name, work);
        for (CssSubstitutionSnapshot? scope = this; scope is not null; scope = scope._parent)
        {
            work.Charge(1);
            if (scope._queryResolver is { } resolver)
            {
                if (!resolver.TryResolve(name, work, out var local)) continue;
                work.CheckCancellation();
                binding = local.WithScope(scope);
                return true;
            }
            if (!scope._buckets.TryGetValue(hash, out var bucket)) continue;
            foreach (var index in bucket)
            {
                work.Charge(1);
                var candidate = scope._bindings[index];
                if (!CssSubstitutionArguments.Equals(name, candidate.Name, work)) continue;
                work.CheckCancellation();
                binding = candidate.WithScope(scope);
                return true;
            }
        }
        work.CheckCancellation();
        binding = default;
        return false;
    }
}

// Only an invocation-owned query may implement this. False means no local override, never failure.
internal interface ICssQueryBindingResolver
{
    bool TryResolve(string name, CssValueWork work, out CssSubstitutionBinding binding);
}

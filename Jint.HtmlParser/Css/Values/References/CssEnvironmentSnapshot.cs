namespace Jint.HtmlParser.Css.Values.References;

internal sealed class CssEnvironmentBinding
{
    private readonly string[] _indices;

    private CssEnvironmentBinding(string name, string[] indices, CssReferenceInput value)
    {
        Name = name;
        _indices = indices;
        LiteralValue = value;
    }

    internal string Name { get; }
    internal CssReferenceInput LiteralValue { get; }
    internal int IndexCount => _indices.Length;
    internal string Index(int index) => _indices[index];

    internal static CssEnvironmentBinding Create(string name, ReadOnlySpan<string> indexSpellings,
        CssReferenceInput literalValue, CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(literalValue);
        CssSubstitutionArguments.RequireEnvironmentName(name);
        work.CheckCancellation();
        var analysis = CssReferenceParser.Analyze(literalValue, CssReferenceUse.PropertyValue, work);
        if (analysis.Kind != CssReferenceAnalysisKind.Literal)
            throw new ArgumentException("An environment value must be a valid literal.", nameof(literalValue));
        var indices = new string[indexSpellings.Length];
        work.CheckCancellation();
        for (var i = 0; i < indices.Length; i++)
        {
            work.Charge(1);
            indices[i] = CssSubstitutionArguments.CanonicalIndex(indexSpellings[i], work)
                ?? throw new ArgumentException("A nonnegative integer index is required.", nameof(indexSpellings));
        }
        work.CheckCancellation();
        return new CssEnvironmentBinding(name, indices, literalValue);
    }
}

internal sealed class CssEnvironmentSnapshot
{
    private readonly CssEnvironmentBinding[] _bindings;
    private readonly Dictionary<uint, List<int>> _buckets;

    private CssEnvironmentSnapshot(CssEnvironmentBinding[] bindings, Dictionary<uint, List<int>> buckets)
    {
        _bindings = bindings;
        _buckets = buckets;
    }

    internal static CssEnvironmentSnapshot Create(ReadOnlySpan<CssEnvironmentBinding> bindings,
        CssValueWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        work.CheckCancellation();
        var copy = new CssEnvironmentBinding[bindings.Length];
        var buckets = new Dictionary<uint, List<int>>();
        work.CheckCancellation();
        for (var i = 0; i < bindings.Length; i++)
        {
            work.Charge(1);
            var binding = bindings[i] ?? throw new ArgumentException("A binding cannot be null.", nameof(bindings));
            var hash = HashKey(binding, work);
            if (!buckets.TryGetValue(hash, out var bucket))
            {
                work.CheckCancellation();
                bucket = new List<int>();
                buckets.Add(hash, bucket);
            }
            foreach (var index in bucket)
            {
                work.Charge(1);
                if (SameKey(binding, copy[index], work))
                    throw new ArgumentException("Duplicate environment key.", nameof(bindings));
            }
            copy[i] = binding;
            work.CheckCancellation();
            bucket.Add(i);
        }
        work.CheckCancellation();
        return new CssEnvironmentSnapshot(copy, buckets);
    }

    internal bool TryGet(string name, ReadOnlySpan<string> canonicalIndices,
        CssValueWork work, out CssReferenceInput? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(work);
        CssSubstitutionArguments.RequireEnvironmentName(name);
        work.CheckCancellation();
        var hash = CssSubstitutionArguments.Hash(name, work);
        hash = Mix(hash, canonicalIndices.Length);
        foreach (var index in canonicalIndices)
        {
            ArgumentNullException.ThrowIfNull(index);
            var normalized = CssSubstitutionArguments.CanonicalIndex(index, work);
            if (normalized is null || !CssSubstitutionArguments.Equals(index, normalized, work))
                throw new ArgumentException("Canonical nonnegative indices are required.", nameof(canonicalIndices));
            hash = Mix(hash, index.Length);
            hash = Mix(hash, CssSubstitutionArguments.Hash(index, work));
        }
        if (_buckets.TryGetValue(hash, out var bucket))
        {
            foreach (var index in bucket)
            {
                work.Charge(1);
                var binding = _bindings[index];
                if (!CssSubstitutionArguments.Equals(name, binding.Name, work) ||
                    binding.IndexCount != canonicalIndices.Length) continue;
                var same = true;
                for (var i = 0; i < canonicalIndices.Length; i++)
                {
                    work.Charge(1);
                    if (CssSubstitutionArguments.Equals(canonicalIndices[i], binding.Index(i), work)) continue;
                    same = false;
                    break;
                }
                if (!same) continue;
                work.CheckCancellation();
                value = binding.LiteralValue;
                return true;
            }
        }
        work.CheckCancellation();
        value = null;
        return false;
    }

    private static bool SameKey(CssEnvironmentBinding left, CssEnvironmentBinding right, CssValueWork work)
    {
        if (!CssSubstitutionArguments.Equals(left.Name, right.Name, work) ||
            left.IndexCount != right.IndexCount) return false;
        for (var i = 0; i < left.IndexCount; i++)
        {
            work.Charge(1);
            if (!CssSubstitutionArguments.Equals(left.Index(i), right.Index(i), work)) return false;
        }
        return true;
    }

    private static uint HashKey(CssEnvironmentBinding binding, CssValueWork work)
    {
        var hash = CssSubstitutionArguments.Hash(binding.Name, work);
        hash = Mix(hash, binding.IndexCount);
        for (var i = 0; i < binding.IndexCount; i++)
        {
            work.Charge(1);
            var index = binding.Index(i);
            hash = Mix(hash, index.Length);
            hash = Mix(hash, CssSubstitutionArguments.Hash(index, work));
        }
        return hash;
    }

    private static uint Mix(uint hash, int value) => (hash ^ (uint) value) * 16777619;
    private static uint Mix(uint hash, uint value) => (hash ^ value) * 16777619;
}

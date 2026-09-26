namespace Jint.HtmlParser;

/// <summary>Ordinary-root-owned membership. Building records flags; it never selects a winner.</summary>
internal sealed class HtmlRadioGroupIndex
{
    private readonly Node _root;
    internal Node Root => _root;
    internal int RegisteredCount => _members.Count;
    // Hashes are computed with bounded work. Collisions compare code units using the same counter.
    private readonly Dictionary<(Element? Owner, uint Hash), List<Bucket>> _buckets = [];
    private readonly List<HtmlInputCheckedState> _members = [];
    private HtmlRadioGroupIndex(Node root) => _root = root;

    internal sealed class Bucket
    {
        internal Bucket(Element? owner, string name, uint hash) { Owner = owner; Name = name; Hash = hash; }
        internal Element? Owner { get; }
        internal string Name { get; }
        internal uint Hash { get; }
        internal int MemberCount;
        internal List<HtmlInputCheckedState> Checked { get; } = [];
        internal int RequiredCount;
    }

    internal static void Ensure(HtmlInputCheckedState state, ref HtmlCheckedWork work)
    {
        work.Check();
        if (state.Index is not null) return;
        Node root = state.Element;
        while (root.ParentNode is { } parent) { work.Step(); root = parent; }
        if (ReferenceEquals(root, state.Element) && root.FirstChild is null) return;
        if (root.RadioIndex is { } existing)
        {
            existing.Add(state, ref work);
            return;
        }
        var staged = new HtmlRadioGroupIndex(root);
        var entries = new List<(HtmlInputCheckedState State, Bucket? Group, bool Required, int CheckedPosition)>();
        for (Node? current = root; current is not null; current = Next(current, root, ref work))
        {
            work.Step();
            if (current is not Element element) continue;
            if (HtmlCheckableState.Type(element, ref work) != HtmlInputType.Radio) continue;
            var member = HtmlCheckableState.Get(element, ref work)!;
            var bucket = staged.Find(member, ref work);
            var required = member.RegisteredRequired;
            staged._members.Add(member);
            if (bucket is not null)
            {
                bucket.MemberCount++;
                if (member.Checked) bucket.Checked.Add(member);
                if (required) bucket.RequiredCount++;
            }
            entries.Add((member, bucket, required, member.Checked && bucket is not null ? bucket.Checked.Count - 1 : -1));
        }
        work.Finish();
        root.RadioIndex = staged;
        root.OwnerDocument?.CheckedWorkProbe?.Built();
        if (root is Document document) document.CheckedWorkProbe?.Built();
        for (var i = 0; i < entries.Count; i++)
        {
            var (member, bucket, required, checkedPosition) = entries[i];
            member.Index = staged;
            member.Group = bucket;
            member.RegisteredRequired = required;
            member.MemberPosition = i;
            member.CheckedPosition = checkedPosition;
        }
    }

    internal static Node? Next(Node current, Node root, ref HtmlCheckedWork work)
    {
        if (current.FirstChild is { } child) return child;
        while (!ReferenceEquals(current, root) && current.NextSibling is null)
        { work.Step(); current = current.ParentNode!; }
        return ReferenceEquals(current, root) ? null : current.NextSibling;
    }

    private Bucket? Find(HtmlInputCheckedState state, ref HtmlCheckedWork work)
    {
        var name = state.Name;
        if (string.IsNullOrEmpty(name)) return null;
        uint hash = 2166136261;
        foreach (var unit in name) { work.Step(); hash = unchecked((hash ^ unit) * 16777619); }
        var key = (HtmlFormState.GetOwner(state.Element), hash);
        if (!_buckets.TryGetValue(key, out var candidates))
        { candidates = []; }
        foreach (var candidate in candidates)
        {
            work.Step();
            if (candidate.Name.Length != name.Length) continue;
            var equal = true;
            for (var i = 0; i < name.Length; i++)
            { work.Step(); if (name[i] != candidate.Name[i]) { equal = false; break; } }
            if (equal) { work.Check(); return candidate; }
        }
        work.Check();
        var bucket = new Bucket(key.Item1, name, hash);
        if (candidates.Count == 0) _buckets.TryAdd(key, candidates);
        candidates.Add(bucket);
        return bucket;
    }

    internal void Add(HtmlInputCheckedState state, ref HtmlCheckedWork work)
    {
        var group = Find(state, ref work);
        _members.EnsureCapacity(_members.Count + 1);
        if (state.Checked && group is not null) group.Checked.EnsureCapacity(group.Checked.Count + 1);
        work.Finish();
        state.MemberPosition = _members.Count;
        _members.Add(state);
        state.Index = this;
        state.Group = group;

        if (group is null) return;
        group.MemberCount++;
        if (state.Checked) AddChecked(group, state);
        if (state.RegisteredRequired) group.RequiredCount++;
    }

    internal void Remove(HtmlInputCheckedState state)
    {
        var lastMember = _members[^1];
        _members[state.MemberPosition] = lastMember;
        lastMember.MemberPosition = state.MemberPosition;
        _members.RemoveAt(_members.Count - 1);
        if (state.Group is { } group)
        {
            group.MemberCount--;
            RemoveChecked(group, state);
            if (state.RegisteredRequired) group.RequiredCount--;
            if (group.MemberCount == 0)
            {
                var key = (group.Owner, group.Hash);
                var candidates = _buckets[key];
                candidates.Remove(group);
                if (candidates.Count == 0) _buckets.Remove(key);
            }
        }
        state.Index = null;
        state.Group = null;
        state.MemberPosition = -1;
        state.CheckedPosition = -1;
    }

    internal void Retire()
    {
        foreach (var member in _members)
        {
            member.Index = null;
            member.Group = null;
            member.MemberPosition = -1;
            member.CheckedPosition = -1;
        }
        _members.Clear();
        _buckets.Clear();
        _root.RadioIndex = null;
    }

    internal static void CheckedChanged(HtmlInputCheckedState state)
    {
        if (state.Group is not { } group) return;
        if (state.Checked) AddChecked(group, state);
        else RemoveChecked(group, state);
    }

    private static void AddChecked(Bucket group, HtmlInputCheckedState state)
    {
        if (state.CheckedPosition >= 0) return;
        state.CheckedPosition = group.Checked.Count;
        group.Checked.Add(state);
    }

    private static void RemoveChecked(Bucket group, HtmlInputCheckedState state)
    {
        if (state.CheckedPosition < 0) return;
        var last = group.Checked[^1];
        group.Checked[state.CheckedPosition] = last;
        last.CheckedPosition = state.CheckedPosition;
        group.Checked.RemoveAt(group.Checked.Count - 1);
        state.CheckedPosition = -1;
    }

    internal static void RequiredChanged(HtmlInputCheckedState state, bool required)
    {
        if (required == state.RegisteredRequired) return;
        if (state.Group is { } group) group.RequiredCount += required ? 1 : -1;
        state.RegisteredRequired = required;
    }
}

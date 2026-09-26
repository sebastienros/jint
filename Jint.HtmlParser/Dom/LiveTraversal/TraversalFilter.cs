namespace Jint.HtmlParser;

internal delegate ushort TraversalFilter(DomNodeIdentity node);

internal static class NativeFiltering
{
    // DOM §6 filter. Only the invocation setting active may clear it.
    internal static ushort Filter(DomNodeIdentity node, uint mask, TraversalFilter? filter, ref bool active, ref TraversalWork work)
    {
        work.Check();
        if (active) throw DomRange.Error("InvalidStateError");
        var type = node.Attribute is not null ? 2 : (int) node.Node!.NodeType;
        if ((mask & (1u << (type - 1))) == 0) return 3;
        if (filter is null) return 1;
        active = true;
        try { var result = filter(node); work.Check(); return result; }
        finally { active = false; }
    }
}

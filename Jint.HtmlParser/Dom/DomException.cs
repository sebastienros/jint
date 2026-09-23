namespace Jint.HtmlParser;

/// <summary>A native DOM operation failure with a stable web-facing error name.</summary>
public sealed class DomException : Exception
{
    public DomException(string name, string message) : base(message) => Name = name;

    public string Name { get; }

    internal static DomException Hierarchy() => new("HierarchyRequestError", "The requested tree structure is invalid.");
    internal static DomException NotFound() => new("NotFoundError", "The node is not a child of this parent.");
    internal static DomException Namespace() => new("NamespaceError", "The qualified name and namespace are incompatible.");
    internal static DomException InvalidCharacter() => new("InvalidCharacterError", "The name contains invalid characters.");
    internal static DomException InUseAttribute() => new("InUseAttributeError", "The attribute belongs to another element.");
    internal static DomException NotSupported() => new("NotSupportedError", "The operation is not supported for this document.");
}

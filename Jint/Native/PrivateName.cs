using Esprima.Ast;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Private names are a bit like symbols, they follow AST reference equality so that each one is globally unique,
/// only exception to the rule is get/set pair which should share same private name.
/// </summary>
internal sealed class PrivateName : JsValue, IEquatable<PrivateName>
{
    internal readonly PrivateIdentifier _identifier;

    public PrivateName(PrivateIdentifier identifier) : base(InternalTypes.PrivateName)
    {
        _identifier = identifier;
        Description = identifier.Name;
    }

    public string Description { get; }

    public override string ToString() => _identifier.Name;

    public override object ToObject()
    {
        throw new NotImplementedException();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PrivateName);
    }

    public override bool Equals(JsValue? other)
    {
        return Equals(other as PrivateName);
    }

    public bool Equals(PrivateName? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ReferenceEquals(this, other);
    }

    public override int GetHashCode()
    {
        return _identifier.Name.GetHashCode();
    }
}

/// <summary>
/// Names are compared by description when they are inserted to environment, so first one wins (get/set pair).
/// </summary>
internal sealed class PrivateNameDescriptionComparer : IEqualityComparer<PrivateName>
{
    internal static readonly PrivateNameDescriptionComparer _instance = new();

    public bool Equals(PrivateName? x, PrivateName? y)
    {
        return x?.Description == y?.Description;
    }

    public int GetHashCode(PrivateName obj)
    {
        return obj.Description.GetHashCode();
    }
}

internal sealed class PrivateIdentifierNameComparer : IEqualityComparer<PrivateIdentifier>
{
    internal static readonly PrivateIdentifierNameComparer _instance = new();

    public bool Equals(PrivateIdentifier? x, PrivateIdentifier? y)
    {
        return x?.Name == y?.Name;
    }

    public int GetHashCode(PrivateIdentifier obj)
    {
        return obj.Name.GetHashCode();
    }
}


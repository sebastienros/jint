using Esprima.Ast;
using Jint.Runtime;

namespace Jint.Native;

internal sealed class PrivateName : JsValue, IEquatable<PrivateName>
{
    private readonly PrivateIdentifier _identifier;

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

        return _identifier.Name == other._identifier.Name;
    }

    public override int GetHashCode()
    {
        return _identifier.Name.GetHashCode();
    }
}

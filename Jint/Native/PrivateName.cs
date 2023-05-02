using Jint.Runtime;

namespace Jint.Native;

internal sealed class PrivateName : JsValue, IEquatable<PrivateName>
{
    private readonly string _description;

    public PrivateName(string description) : base(InternalTypes.PrivateName)
    {
        _description = description;
    }

    public override string ToString() => _description;

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

        return _description == other._description;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}

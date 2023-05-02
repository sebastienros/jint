using Jint.Runtime;

namespace Jint.Native;

internal sealed class PrivateName : JsValue, IEquatable<PrivateName>
{
    private readonly string _name;

    public PrivateName(string name) : base(InternalTypes.PrivateName)
    {
        _name = name;
        Description = "#" + name;
    }

    public string Description { get; }

    public override string ToString() => _name;

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

        return Description == other.Description;
    }

    public override int GetHashCode()
    {
        return _name.GetHashCode();
    }
}

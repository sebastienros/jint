using Jint.Runtime;

namespace Jint.Native;

internal sealed class PrivateName : JsValue
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
}

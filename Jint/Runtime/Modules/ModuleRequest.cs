using Jint.Native;

namespace Jint.Runtime.Modules;

internal readonly record struct ModuleRequest(string Specifier, List<KeyValuePair<string, JsValue>> Attributes)
{
    /// <summary>
    /// https://tc39.es/proposal-import-attributes/#sec-ModuleRequestsEqual
    /// </summary>
    public bool Equals(ModuleRequest other)
    {
        if (!string.Equals(Specifier, other.Specifier, StringComparison.Ordinal))
        {
            return false;
        }

        if (this.Attributes.Count != other.Attributes.Count)
        {
            return false;
        }

        foreach (var pair in Attributes)
        {
            if (!other.Attributes.Contains(pair))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Specifier);
    }
}

namespace Jint.Runtime.Modules;

public readonly record struct ModuleImportAttribute(string Key, string Value);

public readonly record struct ModuleRequest(string Specifier, ModuleImportAttribute[] Attributes)
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

        if (this.Attributes.Length != other.Attributes.Length)
        {
            return false;
        }

        if (Attributes.Length == 0
            || (Attributes.Length == 1 && Attributes[0].Equals(other.Attributes[0])))
        {
            return true;
        }

        foreach (var pair in Attributes)
        {
            if (Array.IndexOf(other.Attributes, pair) == -1)
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

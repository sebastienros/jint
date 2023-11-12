using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Environments;

/// <summary>
/// https://tc39.es/ecma262/#sec-privateenvironment-records
/// </summary>
internal sealed class PrivateEnvironmentRecord
{
    public PrivateEnvironmentRecord(PrivateEnvironmentRecord? outerPrivEnv)
    {
        OuterPrivateEnvironment = outerPrivEnv;
    }

    public PrivateEnvironmentRecord? OuterPrivateEnvironment { get; }
    public Dictionary<PrivateIdentifier, PrivateName> Names { get; } = new();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolve-private-identifier
    /// </summary>
    public PrivateName? ResolvePrivateIdentifier(string identifier)
    {
        foreach (var pn in Names)
        {
            if (string.Equals(pn.Value.Description, identifier, StringComparison.Ordinal))
            {
                return pn.Value;
            }
        }

        return OuterPrivateEnvironment?.ResolvePrivateIdentifier(identifier);
    }
}

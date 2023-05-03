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
    public HashSet<PrivateName> Names { get; } = new(PrivateNameDescriptionComparer._instance);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-resolve-private-identifier
    /// </summary>
    public PrivateName? ResolvePrivateIdentifier(string identifier)
    {
        foreach (var pn in Names)
        {
            if (pn.Description == identifier)
            {
                return pn;
            }
        }

        return OuterPrivateEnvironment?.ResolvePrivateIdentifier(identifier);
    }

    /// <summary>
    /// Names are compared by description when they are inserted to environment, so first one winds (get/set pair).
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
}

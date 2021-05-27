using System.Collections.Generic;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-privateenvironment-records
    /// </summary>
    internal sealed class PrivateEnvironmentRecord
    {
        private readonly PrivateEnvironmentRecord _outerPrivateEnvironment;
        private List<PrivateName> _names = new List<PrivateName>();

        private readonly struct PrivateName
        {
            public PrivateName(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public readonly string Name;
            public readonly string Description;
        }

        public PrivateEnvironmentRecord(PrivateEnvironmentRecord outerPrivEnv)
        {
            _outerPrivateEnvironment = outerPrivEnv;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-resolve-private-identifier
        /// </summary>
        public string ResolvePrivateIdentifier(string identifier)
        {
            var names = _names;
            foreach (var privateName in names)
            {
                if (privateName.Description == identifier)
                {
                    return privateName.Name;
                }
            }

            return _outerPrivateEnvironment.ResolvePrivateIdentifier(identifier);
        }
    }
}
using System;

namespace Jint.Runtime.Interop
{
    public enum DiscoveryModes
    {
        OptOut,
        OptIn
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class JintClassAttribute : Attribute
    {
        public JintClassAttribute(DiscoveryModes discoveryMode = DiscoveryModes.OptOut)
        {
            DiscoveryMode = discoveryMode;
        }

        public DiscoveryModes DiscoveryMode { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class JintMethodAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class JintPropertyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class JintFieldAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class JintConstructorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor)]
    public sealed class JintIgnoreAttribute : Attribute
    {
    }
}

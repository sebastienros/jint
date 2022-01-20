using System;

namespace Jint.Runtime.Modules;

public class ModuleResolutionException : Exception
{
    public ModuleResolutionException(string message, string specifier, string parent)
        : base($"{message} in module '{parent ?? "(null)"}': '{specifier}'")
    {
    }
}
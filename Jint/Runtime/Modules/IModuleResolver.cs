namespace Jint.Runtime.Modules;

public interface IModuleResolver
{
    ResolvedSpecifier Resolve(string referencingModuleLocation, string specifier);
}
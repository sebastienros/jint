namespace Jint.Runtime.Modules
{
    public interface IModuleLoader
    {
        public bool TryLoadModule(string location, string referencingLocation, out string moduleSource, out string moduleLocation);
        public void AddModuleSource(params IModuleSource[] moduleSources);
    }
}

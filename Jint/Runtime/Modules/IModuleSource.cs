using System;

namespace Jint.Runtime.Modules
{
    public interface IModuleSource
    {
        public bool TryLoadModuleSource(Uri location, out string moduleSourceCode);
    }
}
